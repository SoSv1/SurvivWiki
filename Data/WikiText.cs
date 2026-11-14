using System.Text.RegularExpressions;

namespace SurviveWiki.Data;

/// <summary>
/// Removes wiki markup that the import left inside item descriptions, such as
/// image captions ("thumb|300px|caption") and template parameters ("|link=...").
/// The catalog file is left as imported; text is cleaned when it is seeded.
/// A description that was only markup becomes empty, and pages show a fallback.
/// </summary>
public static partial class WikiText
{
    // "thumb", its layout options, and an optional caption up to the end of that sentence.
    [GeneratedRegex(@"\bthumb(?=\||\s*$)(\|(right|left|center|none|upright|\d+px))*(\|[^.|]*(\.\s*|$))?", RegexOptions.IgnoreCase)]
    private static partial Regex ImageCaption();

    // Everything from the first template parameter, e.g. "|dischargeRate=32px|link=Fuel".
    [GeneratedRegex(@"\|\s*[\w ]*=.*$", RegexOptions.Singleline)]
    private static partial Regex TemplateParameters();

    // A leftover parameter name with no text, e.g. "Generic=" or "Brightsmithy =".
    [GeneratedRegex(@"^[\w '\-]*=\s*$")]
    private static partial Regex BareParameter();

    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = ImageCaption().Replace(text, string.Empty);
        cleaned = TemplateParameters().Replace(cleaned, string.Empty);
        cleaned = cleaned.Trim(' ', '-', '|', '\t', '\r', '\n');

        return BareParameter().IsMatch(cleaned) ? string.Empty : cleaned;
    }
}
