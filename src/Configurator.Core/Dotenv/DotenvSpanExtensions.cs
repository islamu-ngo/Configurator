namespace Configurator.Core.Dotenv;

using System.Text;
using Configurator.Core.Environment;

internal static class DotenvSpanExtensions
{
    internal static bool All(this ReadOnlySpan<char> value, Func<char, bool> predicate)
    {
        foreach (char character in value)
            if (!predicate(character)) return false;
        return true;
    }
}
