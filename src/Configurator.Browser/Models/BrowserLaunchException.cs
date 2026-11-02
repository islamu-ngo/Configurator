namespace Configurator.Browser.Models;

public sealed class BrowserLaunchException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
