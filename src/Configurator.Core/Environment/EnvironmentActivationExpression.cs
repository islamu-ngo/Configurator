namespace Configurator.Core.Environment;

public abstract record EnvironmentActivationExpression
{
    private protected EnvironmentActivationExpression(
        EnvironmentActivationKind kind,
        string? identifier,
        IEnumerable<EnvironmentActivationExpression> operands)
    {
        Kind = kind;
        Identifier = identifier;
        Operands = Array.AsReadOnly(operands.ToArray());
    }

    public EnvironmentActivationKind Kind { get; }
    public string? Identifier { get; }
    public IReadOnlyList<EnvironmentActivationExpression> Operands { get; }

    public static EnvironmentActivationExpression Topology(string identifier) =>
        new IdentifierExpression(EnvironmentActivationKind.Topology, identifier);

    public static EnvironmentActivationExpression Capability(string identifier) =>
        new IdentifierExpression(EnvironmentActivationKind.Capability, identifier);

    public static EnvironmentActivationExpression Provider(string identifier) =>
        new IdentifierExpression(EnvironmentActivationKind.Provider, identifier);

    public static EnvironmentActivationExpression Feature(string identifier) =>
        new IdentifierExpression(EnvironmentActivationKind.Feature, identifier);

    public static EnvironmentActivationExpression All(params EnvironmentActivationExpression[] operands) =>
        new CompoundExpression(EnvironmentActivationKind.All, operands);

    public static EnvironmentActivationExpression Any(params EnvironmentActivationExpression[] operands) =>
        new CompoundExpression(EnvironmentActivationKind.Any, operands);

    public static EnvironmentActivationExpression Not(EnvironmentActivationExpression operand) =>
        new CompoundExpression(EnvironmentActivationKind.Not, [operand]);

    public override string ToString() => Kind.ToString();

    private sealed record IdentifierExpression : EnvironmentActivationExpression
    {
        internal IdentifierExpression(EnvironmentActivationKind kind, string identifier)
            : base(kind, identifier, [])
        {
        }
    }

    private sealed record CompoundExpression : EnvironmentActivationExpression
    {
        internal CompoundExpression(
            EnvironmentActivationKind kind,
            IEnumerable<EnvironmentActivationExpression> operands)
            : base(kind, null, operands)
        {
        }
    }
}
