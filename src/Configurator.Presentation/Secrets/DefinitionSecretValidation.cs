namespace Configurator.Presentation;

using Configurator.Core.Definitions;

public sealed record DefinitionSecretRule(string Code, string Message);
public sealed record DefinitionSecretValidation(long InputRevision, bool IsActive, bool IsDeferred,
    bool ShowErrors, IReadOnlyList<DefinitionSecretRule> Requirements, IReadOnlyList<DefinitionSecretRule> Failures);

public sealed partial class DefinitionFormModel
{
    /// <summary>Projects public requirements and safe current-revision diagnostics without retaining answer values.</summary>
    public DefinitionSecretValidation GetSecretValidation(IReadOnlyList<string> path, bool showErrors = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DefinitionNodeState? state = Facts.EditorState(path);
            bool active = state?.IsActive == true;
            bool deferred = SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets && !CanEdit(path);
            bool visible = showErrors || AnswerOrigin(path) is DefinitionAnswerOrigin.Explicit or DefinitionAnswerOrigin.Generated
                || ReadDraftText(path) is not null;
            DefinitionSecretSpecification? specification = GetEditorSchema(path)?.Secret;
            if (specification is null) return new(InputRevision, active, deferred, visible, [], []);
            var requirements = new List<DefinitionSecretRule>();
            void Add(string code, string key, params object[] arguments) =>
                requirements.Add(new(code, PresentationStrings.Format(key, Locale, arguments)));
            if (specification.MinLength > 0) Add("secret-too-short", "SecretTooShort", specification.MinLength);
            if (specification.MaxLength is { } maximum) Add("secret-too-long", "SecretTooLong", maximum);
            DefinitionSecretConstraints constraints = specification.Constraints;
            if (!constraints.AllowWhitespace) Add("secret-whitespace-forbidden", "SecretWhitespace");
            if (constraints.MinLowercase > 0) Add("secret-lowercase-required", "SecretLowercase", constraints.MinLowercase);
            if (constraints.MinUppercase > 0) Add("secret-uppercase-required", "SecretUppercase", constraints.MinUppercase);
            if (constraints.MinDigits > 0) Add("secret-digit-required", "SecretDigits", constraints.MinDigits);
            if (constraints.MinSymbols > 0) Add("secret-symbol-required", "SecretSymbols", constraints.MinSymbols, constraints.AllowedSymbols);
            if (constraints.MaxUtf8Bytes is { } bytes) Add("secret-utf8-limit", "SecretUtf8Limit", bytes);
            if (constraints.CharacterSet == DefinitionSecretCharacterSet.AsciiPrintable) Add("secret-character-set-invalid", "SecretCharacterSet");
            if (constraints.ForbiddenCharacters.Length > 0) Add("secret-character-forbidden", "SecretForbiddenCharacters");
            if (specification.Encoding is { } encoding) Add("secret-encoding-invalid", "SecretEncoding", DefinitionSecretVocabulary.Token(encoding));
            if (specification.Algorithm is { } algorithm) Add("secret-format-required", "SecretAlgorithm", DefinitionSecretVocabulary.Token(algorithm));
            if (specification.Kind is DefinitionSecretKind.OpaqueToken or DefinitionSecretKind.SymmetricKey)
                Add("secret-byte-length-invalid", "SecretByteLength", constraints.MinBytes, constraints.MaxBytes);
            if (specification.Kind == DefinitionSecretKind.PrivateKey && specification.Algorithm is DefinitionSecretAlgorithm.Rs256 or DefinitionSecretAlgorithm.Ps256)
                Add("secret-key-size-invalid", "SecretKeySize", constraints.KeySize);
            if (specification.Kind == DefinitionSecretKind.PrivateKey && constraints.Curve is { } curve)
                Add("secret-key-curve-invalid", "SecretCurve", DefinitionSecretVocabulary.Token(curve));
            var failures = new List<DefinitionSecretRule>();
            if (visible && active && !deferred)
                foreach (DefinitionDiagnostic diagnostic in Facts.Validation.Diagnostics.Concat(Facts.InputDiagnostics))
                {
                    if (diagnostic.Location is not { } location ||
                        !TryCorrectionPath(diagnostic.FieldId, location, Facts.Validation, out IReadOnlyList<string> resolved) ||
                        !resolved.SequenceEqual(path)) continue;
                    DefinitionSecretRule? requirement = requirements.Find(item => item.Code == diagnostic.Code);
                    failures.Add(requirement ?? new(diagnostic.Code, SecretRuleMessage(diagnostic.Code, Locale)));
                }
            return new(InputRevision, active, deferred, visible, requirements.AsReadOnly(), failures.AsReadOnly());
        }
    }

    public static string SecretRuleMessage(string code, string locale) => PresentationStrings.Diagnostic(code, locale);
}
