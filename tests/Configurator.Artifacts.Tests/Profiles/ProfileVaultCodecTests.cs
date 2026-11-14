namespace Configurator.Artifacts.Tests.Profiles;

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Artifacts.Profiles;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Core.Profiles;

[NotInParallel("profile-container")]
public sealed class ProfileVaultCodecTests
{
    private static readonly byte[] First = Encoding.UTF8.GetBytes("synthetic-native-first-material");
    private static readonly byte[] Recovery = Encoding.UTF8.GetBytes("synthetic-portable-second-material");

    [Test]
    public async Task PortableComparisonKeyRemainsInsideEncryptionWhileBlindProjectionStaysSafe()
    {
        using var codec = new NativeProfileContainerCodec();
        Guid keyId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        byte[] payload;
        using (var key = new ProfileSecretCommitment(keyId, Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()))
            payload = Payload(key, ProfileProtectionMode.ImportUpgradeOnly);
        byte[] encoded = await codec.SealAsync(payload, [First, Recovery]);
        CryptographicOperations.ZeroMemory(payload);
        try
        {
            using UnlockedProfile profile = await codec.OpenAsync(encoded, 1, Recovery);
            CompareBaselineSnapshot baseline = CompareBaselineCodec.Create(profile);
            await Assert.That(baseline.KeyId).IsEqualTo(keyId);
            await Assert.That(baseline.Commitments.ContainsKey("/token")).IsTrue();
            await Assert.That(baseline.PublicValues.EnumerateObject().Any()).IsFalse();
            await Assert.That(() => ProfilePayloadCodec.InspectAnswers(profile)).Throws<ProfileException>();
            string safe = Encoding.UTF8.GetString(CompareBaselineCodec.Serialize(baseline));
            await Assert.That(safe.Contains("synthetic-private-answer", StringComparison.Ordinal)).IsFalse();
            await Assert.That(safe.Contains("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=", StringComparison.Ordinal)).IsFalse();
        }
        finally { CryptographicOperations.ZeroMemory(encoded); }
    }

    [Test]
    [Arguments("general-one", 0)]
    [Arguments("general-two", 0)]
    [Arguments("general-two", 1)]
    [Arguments("flattened-one", 0)]
    public async Task IndependentBrowserFixturesAuthenticateBeforeApplicationAdoption(string fixture, int recipient)
    {
        string json = fixture switch
        {
            "general-one" => GeneralOne,
            "general-two" => GeneralTwo,
            "flattened-one" => FlattenedOne,
            _ => throw new InvalidOperationException("Unknown synthetic fixture.")
        };
        using var codec = new NativeProfileContainerCodec();
        using UnlockedProfile profile = await codec.OpenAsync(
            Encoding.UTF8.GetBytes(json), recipient, recipient == 0 ? First : Recovery);
        await Assert.That(profile.ProfileId).IsEqualTo(Guid.Parse("ff6c108f-f794-4e32-b8a4-bc7807c6a7a4"));
        await Assert.That(profile.SnapshotId).IsEqualTo(Guid.Parse("d923397e-b5bb-475c-bb03-4d6517ae1605"));
        await Assert.That(ProfilePayloadCodec.InspectAnswers(profile).Values["token"].GetString())
            .IsEqualTo("synthetic-private-answer");
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(2, 0)]
    [Arguments(2, 1)]
    public async Task SelectedRecipientAuthenticatesAndAdmitsTheOwnedApplicationPayload(int count, int selected)
    {
        using var codec = new NativeProfileContainerCodec();
        byte[] payload = Payload();
        byte[] encoded = await codec.SealAsync(payload, count == 1 ? [First] : [First, Recovery]);
        CryptographicOperations.ZeroMemory(payload);
        try
        {
            using UnlockedProfile profile = await codec.OpenAsync(encoded, selected, selected == 0 ? First : Recovery);
            AnswerSnapshot answers = ProfilePayloadCodec.InspectAnswers(profile);
            await Assert.That(answers.Values["token"].GetString()).IsEqualTo("synthetic-private-answer");
            await Assert.That(profile.Mode).IsEqualTo(ProfileProtectionMode.InspectableEncrypted);
        }
        finally { CryptographicOperations.ZeroMemory(encoded); }
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task AWorkingRecoveryMaterialIsNotTriedAgainstOtherRecipientsAutomatically(int count)
    {
        using var codec = new NativeProfileContainerCodec();
        byte[] payload = Payload();
        byte[] encoded = await codec.SealAsync(payload, count == 1 ? [First] : [First, Recovery]);
        CryptographicOperations.ZeroMemory(payload);
        try
        {
            ProfileException? failure = null;
            try { using var profile = await codec.OpenAsync(encoded, 0, Recovery); }
            catch (ProfileException exception) { failure = exception; }
            await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.AuthenticationFailed);
            await Assert.That(failure?.ToString().Contains("synthetic", StringComparison.Ordinal)).IsFalse();
        }
        finally { CryptographicOperations.ZeroMemory(encoded); }
    }

    [Test]
    public async Task FlattenedContainerHasNoSecondRecipientToSelect()
    {
        using var codec = new NativeProfileContainerCodec();
        byte[] payload = Payload();
        byte[] encoded = await codec.SealAsync(payload, [First]);
        CryptographicOperations.ZeroMemory(payload);
        try
        {
            ProfileException? failure = null;
            try { using var profile = await codec.OpenAsync(encoded, 1, First); }
            catch (ProfileException exception) { failure = exception; }
            await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.InvalidContainer);
        }
        finally { CryptographicOperations.ZeroMemory(encoded); }
    }

    [Test]
    public async Task NormalizationDistinctPassphrasesRemainDistinctAtTheActualCryptoBoundary()
    {
        using var codec = new NativeProfileContainerCodec();
        byte[] first = Encoding.UTF8.GetBytes("synthetic-portable-\u00e9-input");
        byte[] other = Encoding.UTF8.GetBytes("synthetic-portable-e\u0301-input");
        byte[] payload = Payload();
        byte[] encoded = await codec.SealAsync(payload, [first, other]);
        CryptographicOperations.ZeroMemory(payload);
        try
        {
            ProfileException? failure = null;
            try { using var profile = await codec.OpenAsync(encoded, 0, other); }
            catch (ProfileException exception) { failure = exception; }
            await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.AuthenticationFailed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(other);
        }
    }

    [Test]
    [Arguments("tag")]
    [Arguments("ciphertext")]
    [Arguments("protected")]
    public async Task ChangedAuthenticatedBytesNeverPublishPlaintext(string field)
    {
        using var codec = new NativeProfileContainerCodec();
        byte[] payload = Payload();
        byte[] encoded = await codec.SealAsync(payload, [First]);
        CryptographicOperations.ZeroMemory(payload);
        var node = JsonNode.Parse(encoded)!;
        CryptographicOperations.ZeroMemory(encoded);
        byte[] bytes = Base64Url.DecodeFromChars(node[field]!.GetValue<string>());
        if (field == "ciphertext") node[field] = Base64Url.EncodeToString(bytes.AsSpan(0, bytes.Length - 1));
        else if (field == "protected") node[field] = Base64Url.EncodeToString(new byte[] { 32 }.Concat(bytes).ToArray());
        else
        {
            bytes[0] ^= 1;
            node[field] = Base64Url.EncodeToString(bytes);
        }
        CryptographicOperations.ZeroMemory(bytes);
        byte[] modified = Encoding.UTF8.GetBytes(node.ToJsonString());
        try
        {
            ProfileException? failure = null;
            try { using var profile = await codec.OpenAsync(modified, 0, First); }
            catch (ProfileException exception) { failure = exception; }
            await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.AuthenticationFailed);
        }
        finally { CryptographicOperations.ZeroMemory(modified); }
    }

    [Test]
    public async Task CallerMutationAfterSubmissionCannotChangeSealedAnswersOrUnlockMaterials()
    {
        using var codec = new NativeProfileContainerCodec();
        byte[] payload = Payload();
        byte[] material = (byte[])First.Clone();
        Task<byte[]> pending = codec.SealAsync(payload, [material]).AsTask();
        CryptographicOperations.ZeroMemory(payload);
        CryptographicOperations.ZeroMemory(material);
        byte[] encoded = await pending;
        try
        {
            using UnlockedProfile profile = await codec.OpenAsync(encoded, 0, First);
            await Assert.That(ProfilePayloadCodec.InspectAnswers(profile).Values["token"].GetString())
                .IsEqualTo("synthetic-private-answer");
        }
        finally { CryptographicOperations.ZeroMemory(encoded); }
    }

    [Test]
    public async Task CallerMutationAfterUnlockSubmissionCannotChangeTheAdoptedPayload()
    {
        using var codec = new NativeProfileContainerCodec();
        byte[] payload = Payload();
        byte[] encoded = await codec.SealAsync(payload, [First]);
        CryptographicOperations.ZeroMemory(payload);
        byte[] material = (byte[])First.Clone();
        Task<UnlockedProfile> pending = codec.OpenAsync(encoded, 0, material).AsTask();
        CryptographicOperations.ZeroMemory(encoded);
        CryptographicOperations.ZeroMemory(material);
        using UnlockedProfile profile = await pending;
        await Assert.That(ProfilePayloadCodec.InspectAnswers(profile).Values["token"].GetString())
            .IsEqualTo("synthetic-private-answer");
    }

    [Test]
    public async Task PreCancelledUnlockRejectsWithoutAdoptingAProfile()
    {
        using var codec = new NativeProfileContainerCodec();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.That(async () =>
        {
            using var profile = await codec.OpenAsync(new byte[] { 1 }, 0, First, cancellation.Token);
        }).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task OversizedRecipientMaterialIsRejectedWithoutAccessingItsBackingMemory()
    {
        using var codec = new NativeProfileContainerCodec();
        using var unmapped = new UnmappedMaterial();
        byte[] payload = Payload();
        try
        {
            ProfileException? failure = null;
            try { _ = await codec.SealAsync(payload, [unmapped.Memory]); }
            catch (ProfileException exception) { failure = exception; }
            await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.InvalidPassphrase);
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    [Test]
    public async Task CancellationDoesNotReleaseGlobalAdmissionWhileOwnedInputWorkIsStillRunning()
    {
        byte[] payload = Payload();
        using var material = new GatedMaterial(payload);
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> pending = Task.Run(async () =>
        {
            using var codec = new NativeProfileContainerCodec();
            return await codec.SealAsync(material.Memory, [First], cancellation.Token);
        });
        try
        {
            await material.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            ProfileException? failure = null;
            using var blockedCodec = new NativeProfileContainerCodec();
            try { _ = await blockedCodec.SealAsync(payload, [First]); }
            catch (ProfileException exception) { failure = exception; }
            await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.Busy);
        }
        finally { material.Release.Set(); }
        await Assert.That(async () => { _ = await pending.WaitAsync(TimeSpan.FromSeconds(10)); })
            .Throws<OperationCanceledException>();
        using var afterCodec = new NativeProfileContainerCodec();
        byte[] after = await afterCodec.SealAsync(payload, [First]);
        CryptographicOperations.ZeroMemory(after);
        CryptographicOperations.ZeroMemory(payload);
    }

    [Test]
    public async Task NullProtectedHeaderFailsWithAValueFreeContainerCode()
    {
        using var codec = new NativeProfileContainerCodec();
        byte[] encoded = """{"protected":null,"recipients":[],"iv":"","ciphertext":"","tag":""}"""u8.ToArray();
        ProfileException? failure = null;
        try { using var profile = await codec.OpenAsync(encoded, 0, First); }
        catch (ProfileException exception) { failure = exception; }
        await Assert.That(failure?.Code).IsEqualTo(ProfileFailureCode.InvalidContainer);
    }

    private sealed class UnmappedMaterial : MemoryManager<byte>
    {
        public override Memory<byte> Memory => CreateMemory(ProfileContainerPolicy.MaximumPassphraseUtf8Bytes + 1);
        public override Span<byte> GetSpan() => throw new InvalidOperationException("Backing memory must not be read.");
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }

    private sealed class GatedMaterial(byte[] bytes) : MemoryManager<byte>
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new();
        public override Memory<byte> Memory => CreateMemory(bytes.Length);
        public override Span<byte> GetSpan()
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return bytes;
        }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) => Release.Dispose();
    }

    // Independently created and opened in Chromium with panva/jose 6.2.17; comparisonKey is null.
    // Ten-field payload identity, 600000 iterations and 16-byte salts were checked after generation.
    internal const string GeneralOne = """
        {"ciphertext":"DySYCn5JtnzfkkYn-X-sQkbqEF6QzqzJLWChhk-S0HrdBFCSMeM-2OpOAqJBL1_8ZWHDxMUakyKY_qS_0wJXiiIP6Yo-D3iWQIzQV5yQe9pbAA45elE1I0Bl6xOswEsjBeJWH9Bj99IgKFNaKWexgeNXUW4rmpTDjHeSzJXCC5FZsF37HP0bJ3eA9_dM_QfRuoI_O6QwBW_K1NqNAS5_j0iyycXLs8vpEYDpwGRZXNFrOhfAr5N07C3-H4pGjQFv03rE_Xt41o7LHZ9EmdS-v8gSlYxxnEyI-1vQ14tV3wvbUGH9P1m-0W1uME9HvogWBuHncb-SacSzKPKVbJ_1mPSCsgH5wBNXQ43GZ2QxOnuDJVs8k1zW5WiAkyXXwo45LNgwEJ4d0qRXKFoUhO3WlaPVvsj5C1kUx5Q7WmJcRksCbyYGX6sU1YCKhdV8hFAP0PAuRZTGg5NnlcDbu62XlBoxm2sSkRpoicotT4wOopWr9DooayJTq8xoQlyjch2Gt2VPnpmfUFPyy0s-3FZztXSx9hrMeQeQqKDYNc-T8E5vlhXjgHrvdJMHN0JyKl48b5FAG4iCKCqSjpBtTaZPSzNujOU5nk2N54eQp6MKJ-u0wj0B8JSYvkEz44CKTk8ohGbGqL_VFbZmNloWwFb0w_aZy380hnRsQgFtMdZ2MDPq75UOZz9hkUL00XPCfkyIx-thhFcuA7zQ1BsSKCOmoPjh2_8EYobWDXDeQ5DL371yYW6mLZGpZTH00vt_zhuoKeqlGvUy1prdrlzaQXf-NdjgMCQqd8CW-Lv60P-jOpe_y2BTMciaTfdRmYM_qPu3vprPTkr0WnPhWxilqeO2xgcY9UWP2I_jj5b0o1eBuTMhwP9ZVXTXPgKOlsXRylJKXw5cbr5S4mQ_lRZBBpo4pifmfvI8bXW1JA00tiugY32dQtDhSJYxAgBdSpbzepsYe4JLeQUWyVtKIv_Sb-RIR9hLsH9T3O3QA3hmdOY5ZydCr1ja9__gxKLEBf7jJyQsjBXpR2BCzUdMnh_hWvunHQqElHhoBHBDNIW0FoFy2qYli7-hvvP5o3AVXd48UuzqJLxAB_TAAMWGCeVOe4AS4znHeOBCw3x1vDz15fQVwwrGhYjHfdle1CI7t9rUoZcNHZ-1vZe2vhgNBE83VSTxUK2S6kDGWqAYeEYHR7dnNJnP2zDili7hlkm354SALdWzebKYNBJIzHU4xPI-j4HnC9JSwKhPCwtpK8wtCIjm_mNiR6plByHBw_Ll5gJpfoNHA-9gMlzvbI7JO5Q52pLMwbukSCQzabfmZ6mnreryQTLnaMAn3tm0kcgTCFuGhYtJ_TQB5le-cU5U9SaCu20IACzhNwxLYXz2_FaJ0dEkYjWauFASfQg38OBWtcZ--gCeXaPALOl4WSvf6YK4Q7fl-HSQwzJtYrMU7-1nW94wRblf9nXton-Hy_74bqC_qq3UWr4thqgzliAbpJewZgsKqxdFCwZ9pbZDk2m0i9bSI2S8DloUw5_pGYnMJHAjz7TmuCScwKFjkK7xzX6jn0PpO_tsQOhJRG7QlJdExJdXNloTpVydp4Up-hTSpK-gyNXELuCcpihgq1aq8qI1HchXFiKuJzKN57PJA9JUOXSNM7ZISHUznc91G9YX9yzlXJmJQUgoSdSuNUGSL_Rh-QXptGSrgsaP_fN-qSFL1GpGmeDPy5AahQ","iv":"IbplbTNIZa3sgave","tag":"qYqBY2YVqhz8ZOmyJ-rkBg","protected":"eyJlbmMiOiJBMjU2R0NNIiwidHlwIjoiY2ZndmF1bHQiLCJwMmMiOjYwMDAwMCwicDJzIjoiWFZERjUteXluQWRlN0ZCYzhhVHlBdyJ9","recipients":[{"encrypted_key":"lC0u2I7N2mps4QNtN9TJMdKEd743XlANJMK-KdcwZZWpCUcSgZo4fA","header":{"alg":"PBES2-HS512+A256KW"}}]}
        """;
    private const string GeneralTwo = """
        {"ciphertext":"5av2k_x-nCexP__rSpyS0KfanUgLvL1jbYXx79qwRtzgdGtFZURQOrq-ZFcEZvZKBxxisATdZ0YD3aw62l44D6HthyXhn8rxT5Fc_lNcy7IbxpzdnRRlM3FsJ6o2RGg2Ikr9bFueO0ef1qoTwCUHwL2fq-v67jhv-TlI-jSzS5e6_QrIoJgDKaSF8SdH5GkVTZn_TBgRdmWBU4yukySzeWuIGdKQ5yiqTNO5GHwVUAQsD8NtX9kw0KT4laJvtknMI5SaGdHGPxGcufjA-SQePuCgZcvt078UAY-y4Hyy7OOL3cE4Mmc5thkaATazGcbVOdthu5oDBhhrw5_T-lrUCRJ1LypDSBgUC78nUZAClr4kD-SrJJj7TAJQGYMs2qsBK3RdTNKnWSH3FQjluwTFkDtxHY_YJLUCRyWV8iRWzqRwyXL8xMpk3tckmBeqV-81zjI-JI7Ej4H8ZG_aB43FgOmB8qiHxRuRyFjNGegq9aLxBKO0Wkd1yORcogEojAE2ItAyMqKpWHqS0vwAM2D-fveyW_UqcHOzYqJykioReElOKNWQra2dVzXMKXMOOc9WW87prcs1QlQ18A3YPA_3sm_NoWFeAq-l65yjBctL3DVFDB6qtOkg3Roc8KPlZwK3BHRJ9UzZpsO7tJwFC4SXV49WUY1ExshcINQMC7Mj6xLl8s7sD3lpR7YGP_QPqjy6cbnlGKg-h_Fmv1kgE83DslPvpBS7-EApkJCAIgVTfcXF3dAX2P308ze-v4XDeq27xXLGz--ldEErwgYiflon3cey7Tj7cUZI6hH4i6NLE6oBmDnq3-A71B644CknLY_Aepf3qj6hHp1OjZdavLBsJfU9c8XGULX7cNsErpLZ5GneXyJmWnWkRWpt9DjZRJOCtMkC6DMwA2pMYsqKtOCq5tFdOqFUkQ38wpLgQfwAvP35NVFrBcA1dMRi6JHD1Djr3FLi1gNZgX0sXZithRdJosOwKCiQM7o2iSc_rPXNVOZKWFDp1JTjqOULHjSR7GyDp2mGrcq-3kfMw25kZBNRywXkNkPuubUK3xz4uHHloy4ReibxyTEQZZi5mVaiT23ywHIjFRniEpX-zihmgeWMQlgyp-yJMZYl8EAxUpK1UxKn4BQdpzE-2KWE8fAgnat17L5zrm6cdxJkPrCXdqUkvyjr3P363K2fYUYzRQwXu_aMuiKv0WZpZCT86Zp3n9RloIjK4zeY61Zm1hFWlWL0D0nDpQRvtlkKMetrTysWJ7pNRyYqAD-ZEUguqwAAsS5fKCBBVfhfXv1Y7KVUFAwHogsZ8wf4tE_myEcT0VWdl2BjJY0EmRIgPzOd6fxHgZ3sHIRlxeuzBp4F_abOCXPmghvkIIh3FH4Bzw4pvlK7vx0wnZJEht49XzsHA26K6zAaZIBYVA-i13QI8X7RRdA9unIU3h8ly-Jjt6VeH3JMTwFZjq0dBvmxgxGp53gV7hDsOJszF5QVXgh2NnMmKgV13KTjt6bF79_UH4MijI2I-obWTOnFD6MzmC053lS9SN-aTEzE6YzPfgYilohIWUCyrqRQ6x5oSl44XYW1VxBb-Io9SmzjMU56Plz4kYdw2F6c1etN-ulLPnIaGoaD1UwfVP3LH9J6QW_emRLvM4I18luOdzATHT4vGQLKSzOI8Gx6_pCEtTo61gRYucLEepOUNQfvCf2sgwQ4V-5CHj_POeHBOABP7A","iv":"aMiZ8jcmAbKN4jGf","tag":"t5dD6b2u5pfgQCR2vrxsmQ","protected":"eyJlbmMiOiJBMjU2R0NNIiwidHlwIjoiY2ZndmF1bHQifQ","recipients":[{"encrypted_key":"Ucvkzhi62wGwko_VQ8pPbxOBphliwEqaP8Xda55CaeGFWt1K3f-acg","header":{"alg":"PBES2-HS512+A256KW","p2c":600000,"p2s":"_je6DsXX_ctyE3dKy6A9_g"}},{"encrypted_key":"W21m_OQaudllMKz2Jcl-nc8CmWKGswT2nc2c8-3q5yYjGhMf1qXHVg","header":{"alg":"PBES2-HS512+A256KW","p2c":600000,"p2s":"MKFRIpi78fSlsbC2eu4T5A"}}]}
        """;
    private const string FlattenedOne = """
        {"ciphertext":"K8vBrNcf4-WXm9G9SVpFTUasWD1Exx5hVsZlTqKGwbkOJV4KLbOllGf_SdSuOkhdfi6hOfCltSnlq-jxO-1VSzadidz7WguOzXtNzClHh3csFRMBwCu_YCy-2esjNFUkntL4ohwMA7-LAK8E6Jmrrw3qM-yKJWXzE705xs3BaKeL9ARhbvsA1h_w75ey9xKhOCwxT69WJnYTxJSTshYe9gxi-LZyd4mkZvw7YgJelqdZbx7UcLe2hpNYAI4axGWgg1UJPU4AMmFOvxDQhi6xDN-sK9T9Pqj3NE8gaXSLMsd1MqHmChzVSDcw3H_qs7H2uiHZj0qXU8SSz2vCDudRBNIDIGB-k8JpycsuYJQhUpYyANegglNYq_zn3h1ilfufuR6p9-CZKs2SfH68TAZfaeCq05g6aM1VGPdsqrT-Pw8WtOaBuWLnrZfb5p-4GJEXc6i_Hj2C7BWkeY5rEburhAFgMuB8ULsc48eW_TzrN60v4M_lwOOE6B3tS8v5vITqpvoEzh4AFfFz42GxPs3j6lAUspUHV_fep8-FesZzBcn9wUgNFFLYn5BeQ2a1BvugRf-joxfb_0D3LerSi77Dq-Hb55MkD4B5l4B-fHOrutrgB40KW7cTZGY1VbCWqmEwBcj0KvfCKLXx4ZWOHO86vze3uM_cowkVEgLrO1RuN4_blkdLRxAMvIgGNqZTvRdz5b7A2hbuEjhzcQuSQ8I3G4Nhlk9I-iR6HN7KcqDIjEaGrZdXolpmCyr-yI-LS91imOXelLiJJqrupm5SCzOlNAAuyzOf896lSk5WjWFKPSnQ4JsWmQfPH9UUdaPP1o5dNkbsbNC7hunBvQ4OtvMmKvI8EU-cVJtdfcwATPLoDy0TTrpDX0cd1JSO1Vpl53sBhxNKZIkKDnZOK40hQ37ZiSTbcGrFRn12ngBqMDuEh1cRT0VbsRoeSi86_uiDNxOBQaA7KgtZvT2I2yMN5IJC5XwM3B0OvI0S8v4eDafVq70Up69150ivJlksS-CRlIve1up90TFAEpmka9vRTVJsw_ntmsOQB4Hd_38rYFGiaCGb78n7yHLXAXB6o292F9IywAFAI-qEjYZBYcuvqtFSdoNtuiSJ8mJdcd7Iw3CJP2NGk_n6KFNrNIolWePJOVBRxkE8EvTdLbdHiIX5KqxbIwkq87uNbpN9azqI675-NbF-iVDHJ4kGiwjgXCojzLmwI-jU65lcjQAIfeJziiUdlQ06ruRrL0UIf2aA2wPmnIHYtOLSkiNS5flDA3-VhCyTd4K-CtYyLDp8cmUSxPZrodPQRtiMyCOjXx-mvzjrxSHgd5j5FSPntu07dCMZ29TLk_36YhsXEmNKpqVCdMSfwSeqAIppzGkQp5q_IrMJbc_UNhH2F98amYguFieoXQCA66dtBGgYdpZaH89DYu0bkHZ8uODXSRJSB9AKM9FjkTXXn3Y1IOZCvdXeh3V2QDOdpZjQ-z2ZEZOCfqpGfu0EMPwzxEunflK7ketzTEKBvjuvzhiY3UqSFlWFbN3IHN8wBPaEUoQwUg07Wec0GMIPFnls1oZkng8k2kePZiPEZH8Tsqk6huJalaWchxv7p3Ed_FGT4cMtO3MZDpE1FvNoDSksJ6hRbtacyXL7Te4vVAiA1-fHzKVkH-FiOOLubN9DtHjG3MIuewqmsd2n6JX-8bh1Z1OS1ZgbMNQ2p7YsYX-JV3rZAw","iv":"6e1oqc_XflcicdBC","tag":"l-y9mKU4htXIXDzECKQrVQ","encrypted_key":"xnbwGjzx3u421BCz-Pupcefb-eHhCAqNp8B9aOqXfHW7vjzbMRDDUQ","protected":"eyJlbmMiOiJBMjU2R0NNIiwidHlwIjoiY2ZndmF1bHQiLCJwMmMiOjYwMDAwMCwicDJzIjoiUmp0dHdKWm53TklvMGpMc2s5UjlrQSJ9","header":{"alg":"PBES2-HS512+A256KW"}}
        """;

    internal static byte[] Payload(ProfileSecretCommitment? comparisonKey = null,
        ProfileProtectionMode mode = ProfileProtectionMode.InspectableEncrypted)
    {
        Definition definition = DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"d678ac1c-a240-4429-b932-7ef4638471ea","revision":1},
             "project":{"id":"synthetic.profile","name":"Synthetic profile"},"applicationVersions":["1.0"],
             "configuration":{"type":"object","additionalProperties":false,"required":["token"],
               "x-configurator":{"sensitivity":"public"},"properties":{
                 "token":{"type":"string","x-configurator":{"sensitivity":"secret","notBlank":true}}}},
             "outputs":{".env":{"format":"dotenv","bindings":{"TOKEN":"/token"}}}}
            """u8.ToArray()));
        using JsonDocument values = JsonDocument.Parse("""{"token":"synthetic-private-answer"}""");
        var answers = new AnswerSnapshot(0, values.RootElement.EnumerateObject().ToDictionary(
            property => property.Name, property => property.Value, StringComparer.Ordinal));
        return ProfilePayloadCodec.Serialize(definition, answers, mode,
            Guid.Parse("ff6c108f-f794-4e32-b8a4-bc7807c6a7a4"),
            Guid.Parse("d923397e-b5bb-475c-bb03-4d6517ae1605"), "synthetic-local", "synthetic-environment", comparisonKey);
    }
}
