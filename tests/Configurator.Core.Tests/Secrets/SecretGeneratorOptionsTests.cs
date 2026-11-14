namespace Configurator.Core.Tests.Secrets;

using System.Buffers.Binary;
using System.Security.Cryptography;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

public sealed class SecretGeneratorOptionsTests
{
    [Test]
    public async Task BoundsEnforceSchemaLimitsAnd128BitEntropyFloor()
    {
        var constrained = new DefinitionSecretSpecification
        {
            Kind = DefinitionSecretKind.Password,
            Acquisition = DefinitionSecretAcquisition.Local,
            MinLength = 10,
            MaxLength = 40,
            Constraints = new()
            {
                MinLowercase = 0,
                MinUppercase = 2,
                MinDigits = 2,
                MinSymbols = 1,
                AllowedSymbols = "!@#$",
                ForbiddenCharacters = "0O"
            },
            Generation = new() { Length = 32 }
        };

        SecretGenerationBounds bounds = SecretGenerationBounds.From(constrained);
        await Assert.That(bounds.SupportsOptions).IsTrue();
        await Assert.That(bounds.MaxLength).IsEqualTo(40);
        // 5 required positions force fixed categories; 128 bits across 66 chars takes ceil(128 / log2(66)) = 22 free positions => >= 27 total.
        await Assert.That(bounds.MinLength).IsGreaterThanOrEqualTo(27);
        await Assert.That(bounds.DefaultLength).IsEqualTo(32);
        await Assert.That(bounds.AllowsUppercaseToggle).IsFalse();
        await Assert.That(bounds.AllowsDigitsToggle).IsFalse();
        await Assert.That(bounds.AllowsSymbolsToggle).IsFalse();
        await Assert.That(bounds.AllowsLowercaseToggle).IsTrue();

        using SecretGenerationResult belowEntropy = SecretGenerator.Generate(
            constrained,
            new SecretGenerationOptions { Length = 16 });
        await Assert.That(belowEntropy.Succeeded).IsFalse();
        await Assert.That(belowEntropy.Output).IsNull();
        await Assert.That(belowEntropy.ErrorCode).IsNotNull();

        using SecretGenerationResult aboveMax = SecretGenerator.Generate(
            constrained,
            new SecretGenerationOptions { Length = 41 });
        await Assert.That(aboveMax.Succeeded).IsFalse();
        await Assert.That(aboveMax.Output).IsNull();
    }

    [Test]
    public async Task OptionsCannotDisableRequiredCategoriesOrIncludeForbiddenCharacters()
    {
        var specification = new DefinitionSecretSpecification
        {
            Kind = DefinitionSecretKind.Password,
            Acquisition = DefinitionSecretAcquisition.Local,
            MinLength = 28,
            MaxLength = 64,
            Constraints = new()
            {
                MinLowercase = 1,
                MinUppercase = 2,
                MinDigits = 1,
                MinSymbols = 1,
                AllowedSymbols = "!@#",
                ForbiddenCharacters = "O0"
            },
            Generation = new() { Length = 36 }
        };

        using SecretGenerationResult disabledRequiredUppercase = SecretGenerator.Generate(
            specification,
            new SecretGenerationOptions { Length = 36, IncludeUppercase = false });
        await Assert.That(disabledRequiredUppercase.Succeeded).IsFalse();
        await Assert.That(disabledRequiredUppercase.Output).IsNull();

        using SecretGenerationResult tightened = SecretGenerator.Generate(
            specification,
            new SecretGenerationOptions
            {
                Length = 36,
                AdditionalForbiddenCharacters = "1lI"
            });
        await Assert.That(tightened.Succeeded).IsTrue();
        string generated = tightened.Output!.CopyValue();
        await Assert.That(generated.Length).IsEqualTo(36);
        await Assert.That(generated.Any(c => c is 'O' or '0' or '1' or 'l' or 'I')).IsFalse();
    }

    [Test]
    public async Task PasswordGenerationUsesInjectedEntropyAndRejectionSamplingWithoutModuloBias()
    {
        var specification = new DefinitionSecretSpecification
        {
            Kind = DefinitionSecretKind.Password,
            Acquisition = DefinitionSecretAcquisition.Local,
            MinLength = 28,
            MaxLength = 64,
            Constraints = new()
            {
                MinLowercase = 1,
                MinUppercase = 1,
                MinDigits = 1,
                MinSymbols = 0,
                AllowedSymbols = ""
            },
            Generation = new() { Length = 32 }
        };

        // Alphabet has 62 characters; 2^32 % 62 = 4, so uint.MaxValue (0xFFFFFFFF) is in the bias tail >= limit (4294967292).
        using var biasedFirstDraw = new BiasedHeaderEntropy();
        using SecretGenerationResult first = SecretGenerator.Generate(
            specification,
            SecretGenerationOptions.FromBounds(SecretGenerationBounds.From(specification)),
            entropy: biasedFirstDraw);

        await Assert.That(first.Succeeded).IsTrue();
        // Required 3 draws + 29 free draws + 31 Fisher-Yates draws = 63 accepted draws + at least 1 rejected biased draw (>= 64 uint32 draws).
        await Assert.That(biasedFirstDraw.DrawCount).IsGreaterThanOrEqualTo(64);
        await Assert.That(biasedFirstDraw.RejectedTopValueCount).IsEqualTo(1);

        // Using two independent instances of the same deterministic RNG verifies Password truly consumes injected entropy.
        using var replayA = new CounterEntropy();
        using var replayB = new CounterEntropy();
        using SecretGenerationResult a = SecretGenerator.Generate(
            specification,
            SecretGenerationOptions.FromBounds(SecretGenerationBounds.From(specification)),
            entropy: replayA);
        using SecretGenerationResult b = SecretGenerator.Generate(
            specification,
            SecretGenerationOptions.FromBounds(SecretGenerationBounds.From(specification)),
            entropy: replayB);
        await Assert.That(a.Succeeded && b.Succeeded).IsTrue();
        await Assert.That(a.Output!.CopyValue()).IsEqualTo(b.Output!.CopyValue());
    }

    [Test]
    public async Task OpaqueTokenOptionsEnforce128BitFloorAndSchemaByteBounds()
    {
        var tokenSpec = new DefinitionSecretSpecification
        {
            Kind = DefinitionSecretKind.OpaqueToken,
            Acquisition = DefinitionSecretAcquisition.Local,
            Encoding = DefinitionSecretEncoding.Hex,
            Constraints = new() { MinBytes = 16, MaxBytes = 64 },
            Generation = new() { Bytes = 32 }
        };

        SecretGenerationBounds bounds = SecretGenerationBounds.From(tokenSpec);
        await Assert.That(bounds.SupportsOptions).IsTrue();
        await Assert.That(bounds.MinBytes).IsEqualTo(16);
        await Assert.That(bounds.MaxBytes).IsEqualTo(64);

        using SecretGenerationResult resized = SecretGenerator.Generate(
            tokenSpec,
            new SecretGenerationOptions { Bytes = 48 });
        await Assert.That(resized.Succeeded).IsTrue();
        await Assert.That(resized.Output!.CopyValue().Length).IsEqualTo(96);

        using SecretGenerationResult tooShort = SecretGenerator.Generate(
            tokenSpec,
            new SecretGenerationOptions { Bytes = 12 });
        await Assert.That(tooShort.Succeeded).IsFalse();

        using SecretGenerationResult tooLong = SecretGenerator.Generate(
            tokenSpec,
            new SecretGenerationOptions { Bytes = 65 });
        await Assert.That(tooLong.Succeeded).IsFalse();
    }

    private sealed class BiasedHeaderEntropy : RandomNumberGenerator
    {
        private uint _counter;
        public int DrawCount { get; private set; }
        public int RejectedTopValueCount { get; private set; }

        public override void GetBytes(byte[] data) => GetBytes(data.AsSpan());

        public override void GetBytes(Span<byte> data)
        {
            if (data.Length == 4 && DrawCount == 0)
            {
                DrawCount++;
                RejectedTopValueCount++;
                BinaryPrimitives.WriteUInt32LittleEndian(data, uint.MaxValue);
                return;
            }
            if (data.Length == 4)
            {
                DrawCount++;
                BinaryPrimitives.WriteUInt32LittleEndian(data, _counter++);
                return;
            }
            for (int index = 0; index < data.Length; index++)
                data[index] = (byte)(_counter++ & 0xFF);
        }
    }

    private sealed class CounterEntropy : RandomNumberGenerator
    {
        private uint _next = 1;
        public override void GetBytes(byte[] data) => GetBytes(data.AsSpan());
        public override void GetBytes(Span<byte> data)
        {
            if (data.Length == 4)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data, _next++);
                return;
            }
            for (int index = 0; index < data.Length; index++)
                data[index] = (byte)(_next++ & 0xFF);
        }
    }
}
