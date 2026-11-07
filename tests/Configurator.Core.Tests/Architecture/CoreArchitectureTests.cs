namespace Configurator.Core.Tests;

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Configurator.Core;
using Configurator.Core.Dotenv;

[Category("Architecture")]
public sealed class CoreArchitectureTests
{
    private static readonly Assembly ProductAssembly = typeof(ArtifactDigest).Assembly;

    [Test]
    public async Task CompiledVerifierRejectsNativeImportsInsideComposition()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Configurator.SyntheticNativeBoundary"), AssemblyBuilderAccess.Run);
        TypeBuilder type = assembly.DefineDynamicModule("main").DefineType(
            "Configurator.Core.Composition.NativeCanary", TypeAttributes.NotPublic);
        type.DefinePInvokeMethod("ReadNative", "libc", "getpid",
            MethodAttributes.Static | MethodAttributes.PinvokeImpl,
            CallingConventions.Standard, typeof(int), Type.EmptyTypes,
            System.Runtime.InteropServices.CallingConvention.Cdecl,
            System.Runtime.InteropServices.CharSet.Ansi);
        Type compiled = type.CreateType()!;
        await Assert.That(SetupCoreAssemblyVerifier.VerifyTypes([compiled], assembly))
            .IsNotEmpty();
    }

    [Test]
    public async Task ProductAssemblyKeepsApprovedDependenciesAndBoundedCryptoAuthority()
    {
        string[] violations = SetupCoreAssemblyVerifier.VerifyAssembly(ProductAssembly);

        await Assert.That(violations).IsEmpty()
            .Because(string.Join("; ", violations));
    }

    [Test]
    public async Task DefinitionClosureHasNoHostIoOrLiveTargetCapability()
    {
        string[] violations = SetupCoreAssemblyVerifier.VerifyOfflinePortabilityClosure(ProductAssembly);

        await Assert.That(violations).IsEmpty()
            .Because(string.Join("; ", violations));
    }

    [Test]
    public async Task OfflineClosureAllowsOnlyTheDeclaredDialectDataContract()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Configurator.SyntheticDialectBoundary"), AssemblyBuilderAccess.Run);
        TypeBuilder contract = assembly.DefineDynamicModule("main").DefineType(
            "Configurator.Core.Definitions.SyntheticDialectBoundary", TypeAttributes.Public | TypeAttributes.Abstract);
        const MethodAttributes methods = MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual;
        contract.DefineMethod("Dialect", methods, typeof(EnvironmentDestinationDialect), Type.EmptyTypes);
        contract.DefineMethod("OptionalDialect", methods, typeof(EnvironmentDestinationDialect?), Type.EmptyTypes);
        contract.DefineMethod("UseDocument", methods, typeof(void), [typeof(DotenvDocument)]);
        Type compiled = contract.CreateType()!;

        string[] violations = SetupCoreAssemblyVerifier.VerifyOfflinePortabilityClosure([compiled]);
        await Assert.That(violations).HasSingleItem();
        await Assert.That(violations[0]).Contains(".UseDocument");
    }

    [Test]
    public async Task CoreHasNeitherEventAssemblyDependencyNorLiveAuthority()
    {
        string[] eventReferences = ProductAssembly.GetReferencedAssemblies()
            .Where(reference => reference.Name!.StartsWith("Event.", StringComparison.Ordinal)
                || reference.Name.StartsWith("Explore.", StringComparison.Ordinal))
            .Select(reference => reference.Name!).ToArray();
        string[] leakedCoreAuthority = ProductAssembly.GetExportedTypes()
            .Where(type => type.Namespace?.Contains(
                "SetupLive", StringComparison.Ordinal) == true)
            .Select(type => type.FullName!)
            .ToArray();

        await Assert.That(eventReferences).IsEmpty();
        await Assert.That(leakedCoreAuthority).IsEmpty()
            .Because("Setup Core remains offline and cannot own live authority: "
                + string.Join(", ", leakedCoreAuthority));
    }

    [Test]
    public async Task CompiledVerifierRejectsAmbientCallsMutableCollectionsAndLeakingDiagnostics()
    {
        string[] ambient = SetupCoreAssemblyVerifier.VerifyTypes([typeof(SyntheticAmbientFixture)]);
        string[] mutable = SetupCoreAssemblyVerifier.VerifyTypes([typeof(SyntheticMutableFixture)]);
        string[] leaking = SetupCoreAssemblyVerifier.VerifyTypes([typeof(SyntheticLeakingDiagnostic)]);

        await Assert.That(ambient.Any(item => item.Contains("UtcNow", StringComparison.Ordinal))).IsTrue();
        await Assert.That(mutable.Any(item => item.Contains("mutable collection", StringComparison.Ordinal))).IsTrue();
        await Assert.That(leaking.Any(item => item.Contains("diagnostic field", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task CompiledVerifierAllowsOnlyExactAssemblyNamespaceAndInternalIoRoles()
    {
        string[] assemblyViolations = SetupCoreAssemblyVerifier.VerifyAssemblyReferences(
        [
            new AssemblyName("System.Runtime"),
            new AssemblyName("netstandard"),
            new AssemblyName("YamlDotNet"),
            new AssemblyName("BouncyCastle.Cryptography"),
            new AssemblyName("Unapproved.Dependency")
        ]);
        SyntheticNamespaceCanaries canaries = SyntheticVerifierFactory.CreateNamespaceCanaries();
        string[] exact = SetupCoreAssemblyVerifier.VerifyTypes(
            [canaries.ApprovedIo], canaries.Assembly);
        string[] denied = SetupCoreAssemblyVerifier.VerifyTypes(
            [canaries.ChildNamespaceIo, canaries.RootNamespaceIo,
                canaries.PublicIo, canaries.Network], canaries.Assembly);

        await Assert.That(assemblyViolations).HasSingleItem();
        await Assert.That(assemblyViolations[0]).Contains("Unapproved.Dependency");
        await Assert.That(exact.Any(item => item.Contains("ambient call", StringComparison.Ordinal))).IsTrue();
        await Assert.That(denied.Any(item =>
            item.Contains(canaries.ChildNamespaceIo.FullName!, StringComparison.Ordinal)
            && item.Contains("namespace", StringComparison.Ordinal))).IsTrue();
        await Assert.That(denied.Any(item =>
            item.Contains(canaries.ChildNamespaceIo.FullName!, StringComparison.Ordinal)
            && item.Contains("ambient call", StringComparison.Ordinal))).IsTrue();
        await Assert.That(denied.Any(item =>
            item.Contains(canaries.RootNamespaceIo.FullName!, StringComparison.Ordinal)
            && item.Contains("ambient call", StringComparison.Ordinal))).IsTrue();
        await Assert.That(denied.Any(item =>
            item.Contains(canaries.PublicIo.FullName!, StringComparison.Ordinal)
            && item.Contains("public method dependency", StringComparison.Ordinal))).IsTrue();
        await Assert.That(denied.Any(item =>
            item.Contains(canaries.Network.FullName!, StringComparison.Ordinal)
            && item.Contains("ambient call", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task CompiledVerifierAllowsParserEventsAndRejectsEveryForbiddenYamlRole()
    {
        SyntheticYamlCanaries canaries = SyntheticVerifierFactory.CreateYamlCanaries();
        string[] approved = SetupCoreAssemblyVerifier.VerifyTypes(
            [canaries.Approved], canaries.Assembly);
        string[] denied = SetupCoreAssemblyVerifier.VerifyTypes(
            canaries.Forbidden, canaries.Assembly);

        await Assert.That(approved).IsEmpty().Because(string.Join("; ", approved));
        foreach (Type forbidden in canaries.Forbidden)
            await Assert.That(denied.Any(item =>
                item.Contains(forbidden.FullName!, StringComparison.Ordinal)
                && item.Contains("forbidden yaml role", StringComparison.Ordinal))).IsTrue();
        await Assert.That(denied.Any(item =>
            item.Contains(canaries.ConstructorAndLocal.FullName!, StringComparison.Ordinal)
            && item.Contains("forbidden yaml role", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task DigestEqualityAndFormattingAreCultureIndependentAndRepeatable()
    {
        const string expected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (string cultureName in new[] { "tr-TR", "ar-SA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
                ArtifactDigest computed = ArtifactDigest.Compute("abc"u8);
                ArtifactDigest parsed = ArtifactDigest.Parse(expected.ToUpperInvariant());
                await Assert.That(computed.ToString()).IsEqualTo(expected);
                await Assert.That(parsed).IsEqualTo(computed);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

}

internal static class SetupCoreAssemblyVerifier
{
    private const string CompositionNamespace = "Configurator.Core.Composition";

    private static readonly string[] ForbiddenPublicFragments =
    [
        "Password", "Secret", "Pii", "Credential", "ConnectionString",
        "Email", "UserId", "TenantId", "Provider", "DeploymentCoordinate",
        "EnvironmentValue", "LiveAuthority", "Message", "SuppliedValue"
    ];

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opCode => opCode.Value);

    private static readonly HashSet<string> ApprovedYamlTypes = new(StringComparer.Ordinal)
    {
        "YamlDotNet.Core.AnchorName",
        "YamlDotNet.Core.IParser",
        "YamlDotNet.Core.Mark",
        "YamlDotNet.Core.Parser",
        "YamlDotNet.Core.ScalarStyle",
        "YamlDotNet.Core.TagDirectiveCollection",
        "YamlDotNet.Core.TagName",
        "YamlDotNet.Core.YamlException",
        "YamlDotNet.Core.Tokens.TagDirective",
        "YamlDotNet.Core.Tokens.VersionDirective"
    };

    internal static string[] VerifyAssembly(Assembly assembly)
    {
        var failures = new List<string>();
        failures.AddRange(VerifyAssemblyReferences(assembly.GetReferencedAssemblies()));
        failures.AddRange(VerifyTypes(assembly.GetTypes(), assembly));
        return failures.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    internal static string[] VerifyAssemblyReferences(IEnumerable<AssemblyName> references) =>
        references.Where(reference => reference.Name is not null
                && !reference.Name.StartsWith("System", StringComparison.Ordinal)
                && reference.Name is not "netstandard" and not "YamlDotNet" and not "BouncyCastle.Cryptography")
            .Select(reference => $"forbidden assembly reference {reference.Name}")
            .ToArray();

    internal static string[] VerifyOfflinePortabilityClosure(Assembly assembly) =>
        VerifyOfflinePortabilityClosure(assembly.GetExportedTypes());

    internal static string[] VerifyOfflinePortabilityClosure(IEnumerable<Type> exportedTypes)
    {
        Type[] types = exportedTypes.Where(type =>
            type.Namespace is "Configurator.Core.Definitions" or "Configurator.Core.Definitions.Authoring"
                or "Configurator.Core.Artifacts").ToArray();
        string[] forbiddenCapabilities =
        [
            "EnvironmentValue", "Credential", "DeploymentCoordinate", "LiveAuthority",
            "ApplyAsync", "PublishAsync", "ScheduleAsync", "Consent", "UserId", "TenantId"
        ];
        var failures = new List<string>();
        if (types.Length == 0) failures.Add("definition-contracts-missing");
        foreach (Type type in types)
        {
            foreach (MethodInfo method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.Name != "get_RequiresFreshAcceptance"
                    && forbiddenCapabilities.Any(fragment =>
                        method.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                    failures.Add($"offline authority method {type.FullName}.{method.Name}");
                foreach (Type dependency in method.GetParameters().Select(parameter => parameter.ParameterType)
                             .Append(method.ReturnType))
                    if (dependency != typeof(EnvironmentDestinationDialect) && dependency != typeof(EnvironmentDestinationDialect?)
                        && dependency.FullName?.Contains("Dotenv", StringComparison.OrdinalIgnoreCase) == true)
                        failures.Add($"offline dotenv dependency {type.FullName}.{method.Name}");
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType != typeof(EnvironmentDestinationDialect) && property.PropertyType != typeof(EnvironmentDestinationDialect?)
                    && property.PropertyType.FullName?.Contains("Dotenv", StringComparison.OrdinalIgnoreCase) == true)
                    failures.Add($"offline dotenv property {type.FullName}.{property.Name}");
                if (property.Name != "RequiresFreshAcceptance"
                    && forbiddenCapabilities.Any(fragment =>
                        property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                    failures.Add($"offline authority property {type.FullName}.{property.Name}");
            }
        }

        return failures.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    internal static string[] VerifyTypes(IEnumerable<Type> types, Assembly? reviewedAssembly = null)
    {
        Type[] closure = types.ToArray();
        reviewedAssembly ??= typeof(ArtifactDigest).Assembly;
        var failures = new List<string>();
        foreach (Type type in closure)
        {
            bool isPublicContract = type.IsPublic || type.IsNestedPublic;
            bool isEnvironmentContract = type.Namespace is "Configurator.Core.Environment" or "Configurator.Core.Dotenv";
            bool isCompositionContract = string.Equals(
                type.Namespace, CompositionNamespace, StringComparison.Ordinal);
            if (isPublicContract
                && IsReviewedAssembly(type.Assembly, reviewedAssembly)
                && type.Namespace != "Configurator.Core"
                && !isEnvironmentContract
                && !isCompositionContract
                && type.Namespace is not "Configurator.Core.Definitions" and not "Configurator.Core.Definitions.Authoring"
                    and not "Configurator.Core.Artifacts" and not "Configurator.Core.Secrets")
                failures.Add($"forbidden public namespace {type.FullName}");

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                    && !field.IsLiteral
                    && !field.IsInitOnly)
                    failures.Add($"writable static field {type.FullName}.{field.Name}");
            }

            if (isPublicContract && HasForbiddenPublicName(type.Name)
                && !(isEnvironmentContract && IsApprovedEnvironmentName(type.Name))
                && !IsApprovedSecretName(type, type.Name))
                failures.Add($"forbidden public type claim {type.FullName}");

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if ((type == typeof(Configurator.Core.Definitions.DefinitionException)
                        || type == typeof(Configurator.Core.Definitions.Authoring.DefinitionSchemaImportException))
                    && property.DeclaringType == typeof(Exception)) continue;
                if (isPublicContract && IsMutableCollection(property.PropertyType))
                    failures.Add($"mutable collection {type.FullName}.{property.Name}");
                if (isPublicContract && HasForbiddenPublicName(property.Name)
                    && !(isEnvironmentContract && IsApprovedEnvironmentName(property.Name))
                    && !IsApprovedSecretName(type, property.Name))
                    failures.Add($"forbidden public member claim {type.FullName}.{property.Name}");
                if (isPublicContract && IsForbiddenDependency(property.PropertyType))
                    failures.Add($"forbidden public dependency {type.FullName}.{property.Name}");
                if (isPublicContract
                    && type.Name.EndsWith("Diagnostic", StringComparison.Ordinal)
                    && (isEnvironmentContract
                        ? property.Name is not "Code" and not "Path" and not "Key" and not "Category"
                        : type == typeof(Configurator.Core.Definitions.DefinitionDiagnostic)
                            ? property.Name is not "Code" and not "FieldId" and not "RelativePath"
                                && !(property.Name == "Location"
                                    && property.PropertyType == typeof(Configurator.Core.Definitions.DefinitionCorrectionLocation))
                            : property.Name is not "Code" and not "Path" and not "Severity"))
                    failures.Add($"value-bearing diagnostic field {type.FullName}.{property.Name}");
                if (type == typeof(Configurator.Core.Definitions.DefinitionCorrectionLocation)
                    && !(property.Name == "InputRevision" && property.PropertyType == typeof(long)
                        || property.Name == "Segments" && property.PropertyType ==
                            typeof(IReadOnlyList<Configurator.Core.Definitions.DefinitionLocationSegment>)))
                    failures.Add($"value-bearing correction field {type.FullName}.{property.Name}");
                if (type == typeof(Configurator.Core.Definitions.DefinitionLocationSegment)
                    && !(property.Name == "Kind" && property.PropertyType ==
                            typeof(Configurator.Core.Definitions.DefinitionLocationSegmentKind)
                        || property.Name == "PropertyName" && property.PropertyType == typeof(string)
                        || property.Name == "Index" && property.PropertyType == typeof(int?)))
                    failures.Add($"value-bearing correction segment {type.FullName}.{property.Name}");
            }

            foreach (MethodInfo method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (isPublicContract
                    && ((HasForbiddenPublicName(method.Name)
                        && !(isEnvironmentContract && IsApprovedEnvironmentName(method.Name))
                        && !IsApprovedSecretName(type, method.Name))
                    || method.GetParameters().Any(parameter =>
                        HasForbiddenPublicName(parameter.Name ?? string.Empty)
                        && !(isEnvironmentContract && IsApprovedEnvironmentName(parameter.Name ?? string.Empty)))))
                    failures.Add($"forbidden public method claim {type.FullName}.{method.Name}");
                if (isPublicContract
                    && (IsForbiddenDependency(method.ReturnType)
                        || method.GetParameters().Any(parameter => IsForbiddenDependency(parameter.ParameterType))))
                    failures.Add($"forbidden public method dependency {type.FullName}.{method.Name}");
            }

            foreach (MethodBase method in ImplementationBodies(type))
            {
                foreach (MethodBase called in CalledMethods(method).Where(IsAmbientCall))
                {
                    if (!IsApprovedAmbientCall(type, called, reviewedAssembly))
                        failures.Add($"ambient call {type.FullName}.{method.Name} -> {called.DeclaringType?.FullName}.{called.Name}");
                }
            }
            foreach (MethodInfo method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
                    failures.Add($"native import {type.FullName}.{method.Name}");
            }

            foreach (Type dependency in ReferencedTypes(type)
                         .SelectMany(TypeClosure)
                         .Where(IsForbiddenYamlRole))
                failures.Add($"forbidden yaml role {type.FullName} -> {dependency.FullName}");
        }

        return failures.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static bool HasForbiddenPublicName(string name) =>
        ForbiddenPublicFragments.Any(fragment =>
            name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool IsApprovedSecretName(Type owner, string name) => owner.FullName switch
    {
        "Configurator.Core.Definitions.DefinitionNode" => name is "Secret" or "get_Secret",
        "Configurator.Core.Definitions.DefinitionSensitivityPolicy" => name is "ContainsSecret" or "DeclaresSecret",
        "Configurator.Core.Definitions.DefinitionSecretConstraints" => name == owner.Name,
        "Configurator.Core.Definitions.DefinitionSecretSpecification" => name == owner.Name,
        "Configurator.Core.Definitions.DefinitionSecretAcquisition"
            or "Configurator.Core.Definitions.DefinitionSecretAlgorithm"
            or "Configurator.Core.Definitions.DefinitionSecretCharacterSet"
            or "Configurator.Core.Definitions.DefinitionSecretCurve"
            or "Configurator.Core.Definitions.DefinitionSecretEncoding"
            or "Configurator.Core.Definitions.DefinitionSecretInput"
            or "Configurator.Core.Definitions.DefinitionSecretKind"
            or "Configurator.Core.Definitions.DefinitionSecretOperations"
            or "Configurator.Core.Definitions.DefinitionSecretVocabulary" => name == owner.Name,
        "Configurator.Core.Definitions.DefinitionSecretGeneration" => name == owner.Name || name is "Password" or "get_Password" or "set_Password",
        "Configurator.Core.Secrets.SecretGenerationResult" => name == owner.Name || name is "GeneratedPassword" or "get_GeneratedPassword",
        "Configurator.Core.Secrets.SecretGenerator" or "Configurator.Core.Secrets.SecretDerivation"
            or "Configurator.Core.Secrets.SecretKeyMaterial" or "Configurator.Core.Secrets.SecretValue"
            or "Configurator.Core.Secrets.SecretValueValidator" => name == owner.Name,
        _ => false,
    };

    private static bool IsApprovedEnvironmentName(string name) => name is
        "Provider" or "Providers" or "get_Providers"
        or "SecretBindingEnvironmentKeys" or "get_SecretBindingEnvironmentKeys"
        or "secretBindingEnvironmentKeys" or "IsSecret" or "get_IsSecret" or "isSecret"
        or "Value" or "get_Value" or "value" or "CopyValue" or "CopyUtf8Bytes"
        or "ComposeNoSecrets" or "ComposeWithSecrets";

    private static bool IsForbiddenDependency(Type type)
    {
        return TypeClosure(type).Any(static dependency =>
        {
            string typeName = dependency.FullName ?? string.Empty;
            string typeNamespace = dependency.Namespace ?? string.Empty;
            return typeNamespace.StartsWith("System.IO", StringComparison.Ordinal)
                || typeNamespace.StartsWith("System.Net", StringComparison.Ordinal)
                || typeName == "System.Diagnostics.Process"
                || typeName.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)
                || typeName.StartsWith("System.Reflection", StringComparison.Ordinal);
        });
    }

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        const BindingFlags fields = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        if (type.BaseType is not null)
            yield return type.BaseType;
        foreach (Type dependency in type.GetInterfaces())
            yield return dependency;
        foreach (FieldInfo field in type.GetFields(fields))
            yield return field.FieldType;
        foreach (PropertyInfo property in type.GetProperties(fields))
            yield return property.PropertyType;

        foreach (MethodBase method in type.GetMethods(fields).Cast<MethodBase>()
                     .Concat(type.GetConstructors(fields)))
        {
            if (method is MethodInfo methodInfo)
                yield return methodInfo.ReturnType;
            foreach (ParameterInfo parameter in method.GetParameters())
                yield return parameter.ParameterType;
            foreach (LocalVariableInfo local in method.GetMethodBody()?.LocalVariables ?? [])
                yield return local.LocalType;
            foreach (MethodBase called in CalledMethods(method))
            {
                if (called.DeclaringType is not null)
                    yield return called.DeclaringType;
                if (called is MethodInfo calledMethod)
                    yield return calledMethod.ReturnType;
                foreach (ParameterInfo parameter in called.GetParameters())
                    yield return parameter.ParameterType;
            }
        }
    }

    private static IEnumerable<Type> TypeClosure(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (Type dependency in TypeClosure(element))
                yield return dependency;
        }
        foreach (Type argument in type.IsGenericType ? type.GetGenericArguments() : [])
        {
            foreach (Type dependency in TypeClosure(argument))
                yield return dependency;
        }
    }

    private static bool IsForbiddenYamlRole(Type type)
    {
        string typeNamespace = type.Namespace ?? string.Empty;
        if (!typeNamespace.Equals("YamlDotNet", StringComparison.Ordinal)
            && !typeNamespace.StartsWith("YamlDotNet.", StringComparison.Ordinal))
            return false;
        return typeNamespace != "YamlDotNet.Core.Events"
            && !ApprovedYamlTypes.Contains(type.FullName ?? string.Empty);
    }

    private static bool IsMutableCollection(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
            return false;
        if (type.IsArray || typeof(IList).IsAssignableFrom(type) || typeof(IDictionary).IsAssignableFrom(type))
            return true;
        if (!type.IsGenericType)
            return false;
        Type definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>)
            || definition == typeof(Dictionary<,>)
            || definition == typeof(HashSet<>)
            || definition == typeof(ICollection<>)
            || definition == typeof(IList<>)
            || definition == typeof(IDictionary<,>)
            || definition == typeof(ISet<>);
    }

    private static IEnumerable<MethodBase> ImplementationBodies(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        return type.GetMethods(flags).Cast<MethodBase>()
            .Concat(type.GetConstructors(flags))
            .Where(method => method.GetMethodBody() is not null);
    }

    private static IEnumerable<MethodBase> CalledMethods(MethodBase method)
    {
        byte[] il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        for (int index = 0; index < il.Length;)
        {
            OpCode opCode;
            byte first = il[index++];
            short value = first == 0xfe
                ? (short)(0xfe00 | il[index++])
                : first;
            if (!OpCodesByValue.TryGetValue(value, out opCode))
                yield break;

            int operandStart = index;
            int operandSize = OperandSize(opCode.OperandType, il, operandStart);
            if (opCode.OperandType == OperandType.InlineMethod && operandStart + 4 <= il.Length)
            {
                int token = BitConverter.ToInt32(il, operandStart);
                MethodBase? called = null;
                try
                {
                    called = method.Module.ResolveMethod(
                        token,
                        method.DeclaringType?.GetGenericArguments(),
                        method.IsGenericMethod ? method.GetGenericArguments() : null);
                }
                catch (ArgumentException)
                {
                }

                if (called is not null)
                    yield return called;
            }

            index = operandStart + operandSize;
        }
    }

    private static int OperandSize(OperandType operandType, byte[] il, int index) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI
            or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
            or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => index + 4 <= il.Length
            ? 4 + BitConverter.ToInt32(il, index) * 4
            : il.Length - index,
        _ => 0
    };

    private static bool IsAmbientCall(MethodBase method)
    {
        Type? owner = method.DeclaringType;
        string ownerName = owner?.FullName ?? string.Empty;
        string ownerNamespace = owner?.Namespace ?? string.Empty;
        return owner == typeof(System.Environment)
            || owner == typeof(Random)
            || owner == typeof(Guid) && method.Name == nameof(Guid.NewGuid)
            || owner == typeof(DateTime) && method.Name is "get_Now" or "get_UtcNow" or "get_Today"
            || owner == typeof(DateTimeOffset) && method.Name is "get_Now" or "get_UtcNow"
            || ownerName == "System.Threading.Thread" && method.Name == "Sleep"
            || ownerName == "System.Threading.Tasks.Task" && method.Name == "Delay"
            || ownerNamespace.StartsWith("System.IO", StringComparison.Ordinal)
            || ownerNamespace.StartsWith("System.Net", StringComparison.Ordinal)
            || ownerNamespace.StartsWith("System.Reflection", StringComparison.Ordinal)
            || owner == typeof(System.Security.Cryptography.RandomNumberGenerator)
            || owner?.IsSubclassOf(typeof(System.Security.Cryptography.RandomNumberGenerator)) == true
            || ownerName == "System.Diagnostics.Process";
    }

    private static bool IsApprovedAmbientCall(Type caller, MethodBase called, Assembly reviewedAssembly)
    {
        if (IsReviewedAssembly(caller.Assembly, reviewedAssembly)
            && caller == typeof(Configurator.Core.Definitions.Authoring.DefinitionAuthoringIdentityEditor)
            && called.DeclaringType == typeof(StringReader))
            return true;
        if (IsReviewedAssembly(caller.Assembly, reviewedAssembly)
            && caller == typeof(Configurator.Core.Composition.CompositionCompiler).Assembly.GetType(
                "Configurator.Core.Composition.CompositionYamlParser")
            && called.DeclaringType == typeof(StringReader))
            return true;
        return caller.FullName == "Configurator.Core.Secrets.SecretGenerator"
            && (called.DeclaringType == typeof(System.Security.Cryptography.RandomNumberGenerator)
                || called.DeclaringType?.IsSubclassOf(
                    typeof(System.Security.Cryptography.RandomNumberGenerator)) == true);
    }

    private static bool IsReviewedAssembly(Assembly candidate, Assembly reviewedAssembly) =>
        string.Equals(
            candidate.GetName().Name, reviewedAssembly.GetName().Name, StringComparison.Ordinal);
}

public sealed class SyntheticAmbientFixture
{
    public DateTime Read() => DateTime.UtcNow;
}

public sealed class SyntheticMutableFixture
{
    public List<string> Items { get; } = [];
}

public sealed record SyntheticLeakingDiagnostic(string Code, string Path, string Severity, string SuppliedValue);

internal sealed record SyntheticNamespaceCanaries(
    Assembly Assembly, Type ApprovedIo, Type ChildNamespaceIo, Type RootNamespaceIo,
    Type PublicIo, Type Network);

internal sealed record SyntheticYamlCanaries(
    Assembly Assembly, Type Approved, Type ConstructorAndLocal, Type[] Forbidden);

internal static class SyntheticVerifierFactory
{
    private const string CompositionNamespace = "Configurator.Core.Composition";

    internal static SyntheticNamespaceCanaries CreateNamespaceCanaries()
    {
        ModuleBuilder module = CreateModule();
        Type approved = DefineCall(module, $"{CompositionNamespace}.ApprovedIo",
            typeof(Path).GetMethod(nameof(Path.GetTempPath), Type.EmptyTypes)!);
        Type child = DefineCall(module, $"{CompositionNamespace}.Child.DeniedIo",
            typeof(Path).GetMethod(nameof(Path.GetTempPath), Type.EmptyTypes)!);
        Type root = DefineCall(module, "Configurator.Core.DeniedIo",
            typeof(Path).GetMethod(nameof(Path.GetTempPath), Type.EmptyTypes)!);
        Type publicIo = DefinePublicParameter(
            module, $"{CompositionNamespace}.PublicIo", typeof(Stream));
        Type network = DefineCall(module, $"{CompositionNamespace}.Network",
            typeof(System.Net.Dns).GetMethod(nameof(System.Net.Dns.GetHostName), Type.EmptyTypes)!);
        return new SyntheticNamespaceCanaries(
            module.Assembly, approved, child, root, publicIo, network);
    }

    internal static SyntheticYamlCanaries CreateYamlCanaries()
    {
        ModuleBuilder module = CreateModule();
        Type parser = DefineDependency(module, "YamlDotNet.Core.Parser");
        Type scalar = DefineDependency(module, "YamlDotNet.Core.Events.Scalar");
        Type approved = DefineFields(
            module, $"{CompositionNamespace}.ApprovedYaml", [parser, scalar]);

        string[] roles =
        [
            "YamlDotNet.Serialization.DeserializerBuilder",
            "YamlDotNet.Serialization.SerializerBuilder",
            "YamlDotNet.Core.Emitter",
            "YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention",
            "YamlDotNet.Serialization.ObjectFactories.DefaultObjectFactory",
            "YamlDotNet.Serialization.TypeInspectors.ReadablePropertiesTypeInspector",
            "YamlDotNet.Serialization.TypeDiscriminators.TypeDiscriminator",
            "YamlDotNet.Remote.IncludeResolver",
            "YamlDotNet.Dynamic.PolymorphicType"
        ];
        var forbidden = new List<Type>(roles.Length + 1);
        for (int index = 0; index < roles.Length; index++)
        {
            Type dependency = DefineDependency(module, roles[index]);
            forbidden.Add(DefineFields(
                module, $"{CompositionNamespace}.ForbiddenYamlRole{index}", [dependency]));
        }
        Type constructorDependency = DefineDependency(
            module, "YamlDotNet.Serialization.ConstructorOnlyRole");
        Type constructorAndLocal = DefineConstructorAndLocal(
            module, $"{CompositionNamespace}.ConstructorAndLocalYamlRole", constructorDependency);
        forbidden.Add(constructorAndLocal);
        return new SyntheticYamlCanaries(
            module.Assembly, approved, constructorAndLocal, forbidden.ToArray());
    }

    private static ModuleBuilder CreateModule()
    {
        var name = new AssemblyName($"SetupCoreVerifierCanaries.{Guid.NewGuid():N}");
        return AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run)
            .DefineDynamicModule(name.Name!);
    }

    private static Type DefineDependency(ModuleBuilder module, string fullName)
    {
        TypeBuilder builder = module.DefineType(
            fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        return builder.CreateType()!;
    }

    private static Type DefineFields(ModuleBuilder module, string fullName, Type[] dependencies)
    {
        TypeBuilder builder = module.DefineType(
            fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        for (int index = 0; index < dependencies.Length; index++)
            builder.DefineField($"_dependency{index}", dependencies[index], FieldAttributes.Private);
        return builder.CreateType()!;
    }

    private static Type DefineCall(ModuleBuilder module, string fullName, MethodInfo called)
    {
        TypeBuilder builder = module.DefineType(
            fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        MethodBuilder method = builder.DefineMethod(
            "Call", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Call, called);
        if (called.ReturnType != typeof(void))
            il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ret);
        return builder.CreateType()!;
    }

    private static Type DefinePublicParameter(ModuleBuilder module, string fullName, Type dependency)
    {
        TypeBuilder builder = module.DefineType(
            fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        MethodBuilder method = builder.DefineMethod(
            "Use", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [dependency]);
        method.GetILGenerator().Emit(OpCodes.Ret);
        return builder.CreateType()!;
    }

    private static Type DefineConstructorAndLocal(
        ModuleBuilder module, string fullName, Type dependency)
    {
        TypeBuilder builder = module.DefineType(
            fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        MethodBuilder method = builder.DefineMethod(
            "Construct", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        ILGenerator il = method.GetILGenerator();
        LocalBuilder local = il.DeclareLocal(dependency);
        il.Emit(OpCodes.Newobj, dependency.GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Stloc, local);
        il.Emit(OpCodes.Ret);
        return builder.CreateType()!;
    }
}
