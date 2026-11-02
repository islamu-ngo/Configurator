namespace Configurator.Cli;

public enum CliExitCode { Success = 0, Validation = 2, Incomplete = 3, Blocked = 4, Usage = 64, Data = 65, Internal = 70, Io = 74, Cancelled = 130 }

