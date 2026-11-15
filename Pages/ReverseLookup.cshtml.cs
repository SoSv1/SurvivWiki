using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;
using SurviveWiki.Services;

namespace SurviveWiki.Pages;

/// <summary>
/// Task N4: the player says what they are carrying and sees what they can make.
/// The page sends every recipe to the browser once; picking items then updates
/// the results instantly (wwwroot/js/reverse-lookup.js) with no page reload.
/// </summary>
public class ReverseLookupModel : PageModel
{
    private const string TogetherSuffix = "/DST";

    private readonly SurviveWikiContext _context;
    private readonly ItemImages _images;
    private readonly ItemNotes _notes;

    public ReverseLookupModel(SurviveWikiContext context, ItemImages images, ItemNotes notes)
    {
        _context = context;
        _images = images;
        _notes = notes;
    }

    /// <summary>Materials a new player has in the first days. Shown first and always open.</summary>
    private static readonly string[] StarterMaterials =
    {
        "Twigs", "Cut Grass", "Flint", "Rocks", "Log", "Gold Nugget", "Rope", "Boards",
        "Cut Stone", "Charcoal", "Petals", "Silk", "Pig Skin", "Spider Gland", "Pine Cone",
    };

    public record MaterialView(string Name, string? IconUrl);

    public record MaterialGroup(string Title, IReadOnlyList<MaterialView> Materials, bool Open);

    public List<MaterialGroup> Groups { get; private set; } = new();

    /// <summary>All recipes, as JSON for the script on the page.</summary>
    public string RecipesJson { get; private set; } = "[]";

    public async Task OnGetAsync()
    {
        var craftable = await _context.Items
            .AsNoTracking()
            .Include(item => item.CraftedFrom)
                .ThenInclude(recipe => recipe.IngredientItem)
            .Where(item => item.Status == PageStatus.Approved && item.CraftedFrom.Any())
            .ToListAsync();

        // The catalog lists many items twice: "Axe" and "Axe/DST". Show each once,
        // using the Don't Starve Together recipe when there is one, since that is the game our players play.
        var recipes = craftable
            .GroupBy(item => BaseName(item.Name), StringComparer.OrdinalIgnoreCase)
            .Select(variants => variants.FirstOrDefault(item => item.Name.EndsWith(TogetherSuffix))
                                ?? variants.First())
            .Select(item => new
            {
                Name = BaseName(item.Name),
                Url = Url.Page("/Item", new { name = item.Name }),
                Icon = _images.UrlFor(item.Name),
                Tip = ShortText(string.IsNullOrWhiteSpace(item.BeginnerTip) ? item.Description : item.BeginnerTip),
                Does = string.IsNullOrWhiteSpace(item.BeginnerTip) ? string.Empty : ShortText(item.Description),
                Station = item.CraftingStation,
                Featured = _notes.Has(BaseName(item.Name)),
                Ingredients = item.CraftedFrom
                    .Where(recipe => recipe.IngredientItem.Status == PageStatus.Approved)
                    .GroupBy(recipe => BaseName(recipe.IngredientItem.Name), StringComparer.OrdinalIgnoreCase)
                    .Select(group => new
                    {
                        Name = group.Key,
                        Icon = _images.UrlFor(group.First().IngredientItem.Name),
                        Quantity = group.First().QuantityRequired,
                    })
                    .ToList(),
            })
            .Where(recipe => recipe.Ingredients.Count > 0)
            .OrderBy(recipe => recipe.Name)
            .ToList();

        RecipesJson = JsonSerializer.Serialize(recipes, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        // Everything that appears as an ingredient can be picked on the left.
        var categoryByMaterial = craftable
            .SelectMany(item => item.CraftedFrom)
            .Select(recipe => recipe.IngredientItem)
            .GroupBy(ingredient => BaseName(ingredient.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(ingredient => ingredient.Name.Length).First(),
                StringComparer.OrdinalIgnoreCase);

        MaterialView View(string name) => new(name, _images.UrlFor(categoryByMaterial[name].Name));

        var starters = StarterMaterials.Where(categoryByMaterial.ContainsKey).ToList();
        Groups.Add(new MaterialGroup("Starter materials", starters.Select(View).ToList(), Open: true));

        var others = categoryByMaterial
            .Where(pair => !starters.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            .GroupBy(pair => pair.Value.Category)
            .ToList();

        Groups.AddRange(others
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .Select(group => new MaterialGroup(
                group.Key,
                group.Select(pair => pair.Key).OrderBy(name => name).Select(View).ToList(),
                Open: false)));

        var singles = others.Where(group => group.Count() == 1)
            .SelectMany(group => group)
            .Select(pair => pair.Key)
            .OrderBy(name => name)
            .ToList();
        if (singles.Count > 0)
        {
            Groups.Add(new MaterialGroup("Other", singles.Select(View).ToList(), Open: false));
        }
    }

    private static string BaseName(string name) =>
        name.EndsWith(TogetherSuffix, StringComparison.OrdinalIgnoreCase) ? name[..^TogetherSuffix.Length] : name;

    /// <summary>Keeps a recipe card to a glance: the first sentence, capped in length.</summary>
    private static string ShortText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        const int limit = 150;
        var trimmed = text.Trim();
        if (trimmed.Length <= limit)
        {
            return trimmed;
        }

        var sentenceEnd = trimmed.IndexOf(". ", StringComparison.Ordinal);
        return sentenceEnd is > 20 and < limit
            ? trimmed[..(sentenceEnd + 1)]
            : trimmed[..limit].TrimEnd() + "…";
    }
}
