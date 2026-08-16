using System.Globalization;

namespace SharpCampus.Client.Common;

// The UI culture is pinned at startup instead of being read from the OS, so a session always shows
// the language the launcher asked for and a verification run is reproducible on any machine.
public static class Localization
{
    private const string DefaultLanguage = "en";
    private const string Option = "--lang";
    private const string OptionPrefix = "--lang=";

    private static readonly string[] _supported = ["en", "ko", "ja"];

    public static string[] Apply(string[] args)
    {
        var remaining = new List<string>(args.Length);
        string? language = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == Option && i + 1 < args.Length)
            {
                language = args[++i];
            }
            else if (args[i].StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                language = args[i][OptionPrefix.Length..];
            }
            else
            {
                remaining.Add(args[i]);
            }
        }

        var culture = CultureInfo.GetCultureInfo(IsSupported(language) ? language! : DefaultLanguage);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;

        return [.. remaining];
    }

    public static string Format(string format, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static bool IsSupported(string? language) =>
        language is not null && Array.Exists(_supported, s => string.Equals(s, language, StringComparison.OrdinalIgnoreCase));
}
