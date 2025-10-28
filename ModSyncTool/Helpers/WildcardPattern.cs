using System.Text.RegularExpressions;

namespace ModSyncTool.Helpers;

public static class WildcardPattern
{
    public static bool IsMatch(string pattern, string input)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var normalizedPattern = Normalize(pattern);
        var normalizedInput = Normalize(input);
        var regexPattern = "^" + Regex.Escape(normalizedPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(normalizedInput, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string Normalize(string value)
    {
        return value.Replace('\\', '/');
    }
}
