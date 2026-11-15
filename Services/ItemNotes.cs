using System.Text.Json;

namespace SurviveWiki.Services;

/// <summary>
/// Knows which items have a hand-written beginner note in Data/item-notes.json.
/// Those are the items a new player meets first, so lists show them before the rest.
/// (The note text itself is copied into the database when the app starts.)
/// </summary>
public class ItemNotes
{
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    public ItemNotes(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "Data", "item-notes.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Array)
            {
                foreach (var note in notes.EnumerateArray())
                {
                    if (note.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } text)
                    {
                        _names.Add(text);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Without the file nothing is featured; lists fall back to their normal order.
        }
    }

    public bool Has(string itemName) => _names.Contains(itemName);
}
