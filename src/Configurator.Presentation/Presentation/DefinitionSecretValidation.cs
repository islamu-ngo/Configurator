namespace Configurator.Presentation;

using System.Globalization;
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
            bool arabic = Locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
            var requirements = new List<DefinitionSecretRule>();
            void Add(string code, string english, string translated) => requirements.Add(new(code, arabic ? translated : english));
            string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
            if (specification.MinLength > 0)
                Add("secret-too-short", $"At least {Number(specification.MinLength)} Unicode characters.", $"ما لا يقل عن {Number(specification.MinLength)} من محارف يونيكود.");
            if (specification.MaxLength is { } maximum)
                Add("secret-too-long", $"At most {Number(maximum)} Unicode characters.", $"ما لا يزيد عن {Number(maximum)} من محارف يونيكود.");
            DefinitionSecretConstraints constraints = specification.Constraints;
            if (!constraints.AllowWhitespace) Add("secret-whitespace-forbidden", "No whitespace.", "لا يسمح بالمسافات البيضاء.");
            if (constraints.MinLowercase > 0) Add("secret-lowercase-required", $"At least {Number(constraints.MinLowercase)} lowercase letters.", $"ما لا يقل عن {Number(constraints.MinLowercase)} من الأحرف الصغيرة.");
            if (constraints.MinUppercase > 0) Add("secret-uppercase-required", $"At least {Number(constraints.MinUppercase)} uppercase letters.", $"ما لا يقل عن {Number(constraints.MinUppercase)} من الأحرف الكبيرة.");
            if (constraints.MinDigits > 0) Add("secret-digit-required", $"At least {Number(constraints.MinDigits)} digits.", $"ما لا يقل عن {Number(constraints.MinDigits)} من الأرقام.");
            if (constraints.MinSymbols > 0) Add("secret-symbol-required", $"At least {Number(constraints.MinSymbols)} symbols from {constraints.AllowedSymbols}.", $"ما لا يقل عن {Number(constraints.MinSymbols)} من الرموز التالية: {constraints.AllowedSymbols}.");
            if (constraints.MaxUtf8Bytes is { } bytes) Add("secret-utf8-limit", $"At most {Number(bytes)} UTF-8 bytes.", $"ما لا يزيد عن {Number(bytes)} بايت بترميز UTF-8.");
            if (constraints.CharacterSet == "ascii-printable") Add("secret-character-set-invalid", "Printable ASCII characters only.", "يسمح بمحارف ASCII القابلة للطباعة فقط.");
            if (constraints.ForbiddenCharacters.Length > 0) Add("secret-character-forbidden", "Exclude the characters forbidden by this definition.", "استبعد المحارف المحظورة في هذا التعريف.");
            if (specification.Encoding is not null) Add("secret-encoding-invalid", $"Required encoding: {specification.Encoding}.", $"الترميز المطلوب: {specification.Encoding}.");
            if (specification.Algorithm is not null) Add("secret-format-required", $"Required algorithm: {specification.Algorithm}.", $"الخوارزمية المطلوبة: {specification.Algorithm}.");
            if (specification.Kind is "opaque-token" or "symmetric-key")
                Add("secret-byte-length-invalid", $"Decoded size: {Number(constraints.MinBytes)}–{Number(constraints.MaxBytes)} bytes.", $"الحجم بعد فك الترميز: {Number(constraints.MinBytes)}–{Number(constraints.MaxBytes)} بايت.");
            if (specification.Kind == "private-key" && specification.Algorithm is "RS256" or "PS256")
                Add("secret-key-size-invalid", $"RSA key size: {Number(constraints.KeySize)} bits.", $"حجم مفتاح RSA: {Number(constraints.KeySize)} بت.");
            if (specification.Kind == "private-key" && constraints.Curve is { } curve)
                Add("secret-key-curve-invalid", $"Required curve: {curve}.", $"المنحنى المطلوب: {curve}.");
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

    public static string SecretRuleMessage(string code, string locale) =>
        (code, locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase)) switch
        {
            ("value-required", false) => "Enter a required value.",
            ("value-required", true) => "أدخل القيمة المطلوبة.",
            ("secret-hash-format-invalid", false) => "Use the required encoded hash format.",
            ("secret-hash-format-invalid", true) => "استخدم صيغة التجزئة المشفرة المطلوبة.",
            ("secret-cost-out-of-range", false) => "The encoded work parameters are outside the allowed bounds.",
            ("secret-cost-out-of-range", true) => "معاملات العمل المشفرة خارج الحدود المسموح بها.",
            ("secret-profile-unavailable", false) => "This secret profile is unavailable.",
            ("secret-profile-unavailable", true) => "ملف السر هذا غير متاح.",
            ("secret-input-invalid", false) => "Check the input encoding and algorithm limits. Bcrypt accepts at most 72 UTF-8 bytes and no NUL characters.",
            ("secret-input-invalid", true) => "تحقق من ترميز الإدخال وحدود الخوارزمية. يقبل Bcrypt حتى 72 بايت بترميز UTF-8 دون محارف NUL.",
            ("secret-input-limit", false) => "The transient input exceeds the supported size limit.",
            ("secret-input-limit", true) => "يتجاوز الإدخال المؤقت الحد المدعوم للحجم.",
            ("secret-encoding-invalid", false) => "Use the encoding required by the definition.",
            ("secret-encoding-invalid", true) => "استخدم الترميز المطلوب في التعريف.",
            ("secret-cancelled", false) => "Generation was cancelled; no generated value was applied.",
            ("secret-cancelled", true) => "أُلغي التوليد ولم تُطبق قيمة مولّدة.",
            ("secret-stale", false) => "The workspace changed; the generated result was discarded.",
            ("secret-stale", true) => "تغيرت مساحة العمل فتُم تجاهل النتيجة المولّدة.",
            ("secret-busy", false) => "Wait for the current secret operation to finish.",
            ("secret-busy", true) => "انتظر اكتمال عملية السر الحالية.",
            ("secret-operation-unavailable" or "secret-generation-unavailable", false) => "This field is not currently eligible for local generation. Check consent, activation and the declared recipe.",
            ("secret-operation-unavailable" or "secret-generation-unavailable", true) => "هذا الحقل غير مؤهل حالياً للتوليد المحلي. تحقق من الموافقة والتفعيل ووصفة التوليد المحددة.",
            ("secret-generated-value-invalid", false) => "Generated output did not meet all field requirements; the previous value was preserved.",
            ("secret-generated-value-invalid", true) => "لم يستوف الناتج المولّد جميع متطلبات الحقل؛ احتُفظ بالقيمة السابقة.",
            (_, false) => "The value does not meet this field's requirements.",
            (_, true) => "القيمة لا تستوفي متطلبات هذا الحقل."
        };
}
