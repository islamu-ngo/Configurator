namespace Configurator.Core.Composition;

using Configurator.Core;

public sealed class CompositionScaleAdmission
{
    private CompositionScaleAdmission(
        CompositionScaleAdmissionCode code,
        CompositionScaleProfile? profile)
    {
        Code = code;
        Profile = profile;
    }

    public bool Succeeded =>
        Code == CompositionScaleAdmissionCode.Accepted && Profile is not null;
    public CompositionScaleAdmissionCode Code { get; }
    public CompositionScaleProfile? Profile { get; }

    internal static CompositionScaleAdmission Accepted(
        CompositionScaleProfile profile) =>
        new(CompositionScaleAdmissionCode.Accepted, profile);

    internal static CompositionScaleAdmission Rejected(
        CompositionScaleAdmissionCode code) =>
        new(code, null);

    public override string ToString() =>
        $"{nameof(CompositionScaleAdmission)}:{Code}:Succeeded={Succeeded}";
}
