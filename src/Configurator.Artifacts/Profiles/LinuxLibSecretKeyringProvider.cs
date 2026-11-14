namespace Configurator.Artifacts.Profiles;

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Async libsecret ABI boundary. Each admitted operation owns its thread-default GLib context.
/// Thirty seconds requests GCancellable cancellation; it is not a physical completion deadline.
/// No unlock, replacement, deletion, subprocess, or fallback persistence is performed.
/// </summary>
internal sealed partial class LinuxLibSecretKeyringProvider : IOsKeyringNative
{
    public ValueTask<OsKeyringNativeResult> LookupAsync(Guid id, OsKeyringPurpose purpose, CancellationToken cancellationToken) =>
        RunAsync(id, purpose, default, creating: false, cancellationToken);

    public ValueTask<OsKeyringNativeResult> CreateAsync(Guid id, OsKeyringPurpose purpose,
        ReadOnlyMemory<byte> canonicalUtf8, CancellationToken cancellationToken) =>
        RunAsync(id, purpose, canonicalUtf8, creating: true, cancellationToken);

    private static async ValueTask<OsKeyringNativeResult> RunAsync(Guid id, OsKeyringPurpose purpose,
        ReadOnlyMemory<byte> value, bool creating, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
            return new OsKeyringNativeResult(OsKeyringStatus.UnsupportedPlatform);
        return await Task.Run(() =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            using var operation = new NativeOperation();
            try { return operation.Run(id, purpose, value, creating, deadline.Token); }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or
                BadImageFormatException or ExternalException)
            {
                return operation.Failure(OsKeyringStatus.Unavailable);
            }
        }).ConfigureAwait(false);
    }

    private sealed unsafe class NativeOperation : IDisposable
    {
        private enum Step { Service, Search, Collection, Create }
        private nint _context, _cancellable, _service, _attributes, _items, _collection, _value, _createdItem, _error, _result;
        private GCHandle _self;
        private CancellationTokenRegistration _registration;
        private bool _pushed, _completed, _callbackFailed, _createStarted;
        private Step _step;

        internal OsKeyringNativeResult Run(Guid id, OsKeyringPurpose purpose, ReadOnlyMemory<byte> value,
            bool creating, CancellationToken token)
        {
            if (token.IsCancellationRequested) return Failure(OsKeyringStatus.Cancelled);
            _context = Native.ContextNew();
            Native.ContextPush(_context);
            _pushed = true;
            _cancellable = Native.CancellableNew();
            _self = GCHandle.Alloc(this);
            _registration = token.Register(() => Native.CancellableCancel(_cancellable));
            Wait(Step.Service);
            _service = _result;
            if (Error() is { } serviceError) return Failure(serviceError);
            if (_service == 0) return Failure(OsKeyringStatus.ServiceMissing);
            if (token.IsCancellationRequested) return Failure(OsKeyringStatus.Cancelled);

            _attributes = Native.HashNew(&Hash, &Equal, &Free, &Free);
            Attribute("xdg:schema", "org.islamu.configurator.ProfileKey");
            Attribute("application", "org.islamu.configurator");
            Attribute("key-id", id.ToString("N"));
            Attribute("purpose", purpose == OsKeyringPurpose.NativeRecipient ? "native-recipient" : "comparison-key");
            if (!creating)
            {
                Wait(Step.Search);
                _items = _result;
                if (Error() is { } searchError) return Failure(searchError);
                if (token.IsCancellationRequested) return Failure(OsKeyringStatus.Cancelled);
                if (_items == 0) return Failure(OsKeyringStatus.Missing);
                nint item = Marshal.ReadIntPtr(_items);
                if (Native.ItemLocked(item) != 0) return Failure(OsKeyringStatus.Locked);
                if (Marshal.ReadIntPtr(_items, nint.Size) != 0) return Failure(OsKeyringStatus.InvalidValue);
                _value = Native.ItemSecret(item);
                if (_value == 0) return Failure(OsKeyringStatus.Unavailable);
                nint data = Native.ValueGet(_value, out nuint length);
                if (data == 0 || length != 44) return Failure(OsKeyringStatus.InvalidValue);
                byte[] owned = new byte[44];
                Marshal.Copy(data, owned, 0, owned.Length);
                return new(OsKeyringStatus.Success, owned);
            }

            Wait(Step.Collection);
            _collection = _result;
            if (Error() is { } collectionError) return Failure(collectionError);
            if (_collection == 0) return Failure(OsKeyringStatus.Unavailable);
            if (Native.CollectionLocked(_collection) != 0) return Failure(OsKeyringStatus.Locked);
            if (token.IsCancellationRequested) return Failure(OsKeyringStatus.Cancelled);
            using (MemoryHandle pinned = value.Pin())
                _value = Native.ValueNew((nint)pinned.Pointer, value.Length, "text/plain; charset=utf-8");
            if (_value == 0) return Failure(OsKeyringStatus.Unavailable);
            Wait(Step.Create);
            _createdItem = _result;
            if (Error() is { } createError) return Failure(createError);
            if (_createdItem == 0) return Failure(OsKeyringStatus.Unavailable);
            return new(token.IsCancellationRequested ? OsKeyringStatus.Cancelled : OsKeyringStatus.Success,
                creation: OsKeyringCreation.Created);
        }

        internal OsKeyringNativeResult Failure(OsKeyringStatus status) =>
            new(status, creation: _createStarted ? OsKeyringCreation.Uncertain : OsKeyringCreation.NotAttempted);

        private void Attribute(string name, string value) =>
            _ = Native.HashInsert(_attributes, Native.Duplicate(name), Native.Duplicate(value));

        private void Wait(Step step)
        {
            _step = step;
            _completed = false;
            _result = 0;
            nint owner = GCHandle.ToIntPtr(_self);
            switch (step)
            {
                case Step.Service:
                    // An independent proxy cannot retain a previous operation's thread-default context.
                    Native.ServiceOpen(Native.ServiceType(), 0, 2, _cancellable, &Ready, owner); // OPEN_SESSION
                    break;
                case Step.Search:
                    Native.Search(_service, 0, _attributes, 2 | 8, _cancellable, &Ready, owner); // ALL | LOAD_SECRETS
                    break;
                case Step.Collection:
                    Native.CollectionForAlias(_service, "default", 0, _cancellable, &Ready, owner);
                    break;
                case Step.Create:
                    _createStarted = true;
                    Native.ItemCreate(_collection, 0, _attributes, "Configurator profile key", _value,
                        0, _cancellable, &Ready, owner); // NONE, never REPLACE
                    break;
            }
            while (!_completed) _ = Native.ContextIteration(_context, 1);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static void Ready(nint source, nint result, nint data)
        {
            var owner = (NativeOperation)GCHandle.FromIntPtr(data).Target!;
            try
            {
                owner._result = owner._step switch
                {
                    Step.Service => Native.ServiceFinish(result, out owner._error),
                    Step.Search => Native.SearchFinish(source, result, out owner._error),
                    Step.Collection => Native.CollectionFinish(result, out owner._error),
                    Step.Create => Native.ItemCreateFinish(result, out owner._error),
                    _ => 0
                };
            }
            catch (Exception)
            {
                // A managed exception must never cross the unmanaged callback boundary.
                owner._callbackFailed = true;
            }
            finally { owner._completed = true; }
        }

        private OsKeyringStatus? Error()
        {
            if (_callbackFailed) return OsKeyringStatus.Unavailable;
            if (_error == 0) return null;
            uint secret = Native.SecretErrorQuark();
            uint io = Native.IoErrorQuark();
            uint dbus = Native.DbusErrorQuark();
            if (Native.ErrorMatches(_error, secret, 2) != 0) return OsKeyringStatus.Locked;
            if (Native.ErrorMatches(_error, io, 19) != 0) return OsKeyringStatus.Cancelled;
            if (Native.ErrorMatches(_error, io, 14) != 0 || Native.ErrorMatches(_error, dbus, 9) != 0 ||
                Native.ErrorMatches(_error, dbus, 10) != 0) return OsKeyringStatus.Denied;
            if (Native.ErrorMatches(_error, dbus, 2) != 0 || Native.ErrorMatches(_error, dbus, 3) != 0 ||
                Native.ErrorMatches(_error, dbus, 11) != 0) return OsKeyringStatus.ServiceMissing;
            return OsKeyringStatus.Unavailable;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static uint Hash(nint value) => Native.StringHash(value);
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static int Equal(nint left, nint right) => Native.StringEqual(left, right);
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static void Free(nint value) => Native.Free(value);

        public void Dispose()
        {
            // Dispose waits for a concurrently executing cancellation callback before unref.
            _registration.Dispose();
            if (_error != 0) Native.ErrorFree(_error);
            if (_value != 0) Native.ValueUnref(_value);
            if (_createdItem != 0) Native.ObjectUnref(_createdItem);
            if (_collection != 0) Native.ObjectUnref(_collection);
            if (_items != 0)
            {
                for (nint node = _items; node != 0; node = Marshal.ReadIntPtr(node, nint.Size))
                    Native.ObjectUnref(Marshal.ReadIntPtr(node));
                Native.ListFree(_items);
            }
            if (_attributes != 0) Native.HashUnref(_attributes);
            if (_service != 0) Native.ObjectUnref(_service);
            if (_cancellable != 0) Native.ObjectUnref(_cancellable);
            if (_self.IsAllocated) _self.Free();
            if (_pushed) Native.ContextPop(_context);
            if (_context != 0) Native.ContextUnref(_context);
        }
    }

    private static unsafe partial class Native
    {
        private const string Secret = "libsecret-1.so.0";
        private const string Glib = "libglib-2.0.so.0";
        private const string Gio = "libgio-2.0.so.0";
        private const string Gobject = "libgobject-2.0.so.0";

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_service_get_type")]
        internal static partial nuint ServiceType();
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_service_open")]
        internal static partial void ServiceOpen(nuint type, nint busName, int flags, nint cancellable,
            delegate* unmanaged[Cdecl]<nint, nint, nint, void> callback, nint data);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_service_open_finish")]
        internal static partial nint ServiceFinish(nint result, out nint error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_service_search")]
        internal static partial void Search(nint service, nint schema, nint attributes, int flags, nint cancellable,
            delegate* unmanaged[Cdecl]<nint, nint, nint, void> callback, nint data);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_service_search_finish")]
        internal static partial nint SearchFinish(nint service, nint result, out nint error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_collection_for_alias", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial void CollectionForAlias(nint service, string alias, int flags, nint cancellable,
            delegate* unmanaged[Cdecl]<nint, nint, nint, void> callback, nint data);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_collection_for_alias_finish")]
        internal static partial nint CollectionFinish(nint result, out nint error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_collection_get_locked")]
        internal static partial int CollectionLocked(nint collection);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_item_create", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial void ItemCreate(nint collection, nint schema, nint attributes, string label,
            nint value, int flags, nint cancellable, delegate* unmanaged[Cdecl]<nint, nint, nint, void> callback, nint data);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_item_create_finish")]
        internal static partial nint ItemCreateFinish(nint result, out nint error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_item_get_locked")]
        internal static partial int ItemLocked(nint item);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_item_get_secret")]
        internal static partial nint ItemSecret(nint item);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_value_new", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial nint ValueNew(nint value, nint length, string contentType);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_value_get")]
        internal static partial nint ValueGet(nint value, out nuint length);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_value_unref")]
        internal static partial void ValueUnref(nint value);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Secret, EntryPoint = "secret_error_get_quark")]
        internal static partial uint SecretErrorQuark();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Gio, EntryPoint = "g_cancellable_new")]
        internal static partial nint CancellableNew();
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Gio, EntryPoint = "g_cancellable_cancel")]
        internal static partial void CancellableCancel(nint cancellable);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Gio, EntryPoint = "g_io_error_quark")]
        internal static partial uint IoErrorQuark();
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Gio, EntryPoint = "g_dbus_error_quark")]
        internal static partial uint DbusErrorQuark();
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Gobject, EntryPoint = "g_object_unref")]
        internal static partial void ObjectUnref(nint value);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_main_context_new")]
        internal static partial nint ContextNew();
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_main_context_push_thread_default")]
        internal static partial void ContextPush(nint context);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_main_context_iteration")]
        internal static partial int ContextIteration(nint context, int mayBlock);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_main_context_pop_thread_default")]
        internal static partial void ContextPop(nint context);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_main_context_unref")]
        internal static partial void ContextUnref(nint context);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_hash_table_new_full")]
        internal static partial nint HashNew(delegate* unmanaged[Cdecl]<nint, uint> hash,
            delegate* unmanaged[Cdecl]<nint, nint, int> equal, delegate* unmanaged[Cdecl]<nint, void> keyFree,
            delegate* unmanaged[Cdecl]<nint, void> valueFree);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_hash_table_insert")]
        internal static partial int HashInsert(nint table, nint key, nint value);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_hash_table_unref")]
        internal static partial void HashUnref(nint table);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_strdup", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial nint Duplicate(string value);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_str_hash")]
        internal static partial uint StringHash(nint value);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_str_equal")]
        internal static partial int StringEqual(nint left, nint right);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_free")]
        internal static partial void Free(nint value);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_list_free")]
        internal static partial void ListFree(nint list);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_error_matches")]
        internal static partial int ErrorMatches(nint error, uint domain, int code);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(Glib, EntryPoint = "g_error_free")]
        internal static partial void ErrorFree(nint error);
    }
}
