using System.Text.Json;

namespace SurviveWiki.Services;

/// <summary>
/// Looks up the icon for an item by name.
/// The icons and their manifest live in wwwroot/images/items and are produced by
/// Tools/fetch-item-images.py. Keeping them out of the database means the item
/// table and its seed data stay unchanged. Items without an icon return null and
/// the page shows a placeholder instead.
/// </summary>
public class ItemImages
{
    private const string FolderUrl = "/images/items/";

    private readonly Dictionary<string, string> _fileByItemName = new(StringComparer.OrdinalIgnoreCase);

    public ItemImages(IWebHostEnvironment environment, ILogger<ItemImages> logger)
    {
        var manifestPath = Path.Combine(environment.WebRootPath, "images", "items", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            logger.LogWarning("No item icon manifest at {Path}; items will show placeholders.", manifestPath);
            return;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<Manifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            foreach (var (itemName, fileName) in manifest?.Images ?? new())
            {
                _fileByItemName[itemName] = fileName;
            }
        }
        catch (JsonException error)
        {
            // Icons are decoration: a broken manifest must not stop the wiki from starting.
            logger.LogWarning(error, "The item icon manifest could not be read; items will show placeholders.");
        }
    }

    /// <summary>The URL of the item's icon, or null when there is none.</summary>
    public string? UrlFor(string? itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return null;
        }

        return _fileByItemName.TryGetValue(itemName.Trim(), out var fileName)
            ? FolderUrl + Uri.EscapeDataString(fileName)
            : null;
    }

    private sealed class Manifest
    {
        public Dictionary<string, string> Images { get; set; } = new();
    }
}
