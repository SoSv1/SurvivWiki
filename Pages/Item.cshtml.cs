using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;

namespace SurviveWiki.Pages;

/// <summary>
/// Task N1: one item at a glance. The page answers, in this order:
/// what it does, when to use it, how to get it, and what it is used to make.
/// </summary>
public class ItemModel : PageModel
{
    private const string TogetherSuffix = "/DST";
    public const int MaxUsedInShown = 12;

    private readonly SurviveWikiContext _context;

    public ItemModel(SurviveWikiContext context)
    {
        _context = context;
    }

    public Item? CurrentItem { get; set; }

    /// <summary>Items that need this one as an ingredient, each listed once.</summary>
    public List<Item> UsedToMake { get; private set; } = new();

    /// <summary>
    /// The item whose recipe the page shows. Usually the item itself; when it has no
    /// recipe but its Don't Starve Together variant does, that variant.
    /// </summary>
    public Item? RecipeSource { get; private set; }

    public bool RecipeIsFromTogether => RecipeSource is not null && RecipeSource != CurrentItem;

    public string StationText => RecipeSource?.CraftingStation switch
    {
        "None" => "Crafted by hand – no station needed.",
        null or "" or "Not specified" => string.Empty,
        var station => $"Stand next to {(("AEIOU".Contains(station[0])) ? "an" : "a")} {station} to craft it.",
    };

    public async Task<IActionResult> OnGetAsync(string name)
    {
        // If no name is provided, send them back to the home page
        if (string.IsNullOrWhiteSpace(name))
        {
            return RedirectToPage("/Index");
        }

        // Fetch the item and its recipe
        CurrentItem = await _context.Items
            .AsNoTracking()
            .Include(i => i.CraftedFrom)
                .ThenInclude(ri => ri.IngredientItem)
            .FirstOrDefaultAsync(i => i.Name.ToLower() == name.ToLower()
                                   && i.Status == PageStatus.Approved);

        // If the item doesn't exist, return a 404 Not Found page
        if (CurrentItem == null)
        {
            return NotFound();
        }

        RecipeSource = CurrentItem;
        if (!CurrentItem.CraftedFrom.Any() && !CurrentItem.Name.EndsWith(TogetherSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var togetherName = (CurrentItem.Name + TogetherSuffix).ToLower();
            var together = await _context.Items
                .AsNoTracking()
                .Include(i => i.CraftedFrom)
                    .ThenInclude(ri => ri.IngredientItem)
                .FirstOrDefaultAsync(i => i.Name.ToLower() == togetherName && i.Status == PageStatus.Approved);

            if (together is not null && together.CraftedFrom.Any())
            {
                RecipeSource = together;
            }
        }

        var results = await _context.RecipeIngredients
            .AsNoTracking()
            .Where(recipe => recipe.IngredientItemId == CurrentItem.Id
                             && recipe.ResultItem.Status == PageStatus.Approved)
            .Select(recipe => recipe.ResultItem)
            .ToListAsync();

        // The catalog has "Spear" and "Spear/DST"; list such a pair once.
        UsedToMake = results
            .GroupBy(item => BaseName(item.Name), StringComparer.OrdinalIgnoreCase)
            .Select(variants => variants.OrderBy(item => item.Name.Length).First())
            .OrderBy(item => item.Name)
            .ToList();

        return Page();
    }

    private static string BaseName(string name) =>
        name.EndsWith(TogetherSuffix, StringComparison.OrdinalIgnoreCase) ? name[..^TogetherSuffix.Length] : name;
}
