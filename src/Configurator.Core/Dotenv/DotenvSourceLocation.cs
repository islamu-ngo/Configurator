namespace Configurator.Core.Dotenv;

using Configurator.Core.Environment;

/// <summary>One-based coordinates in the acquired source, before line-ending normalization.</summary>
public sealed record DotenvSourceLocation(int Line, int Column);
