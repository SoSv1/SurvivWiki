using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;

namespace SurviveWiki.Pages;

public class ReverseLookupModel : PageModel
{
    private readonly SurviveWikiContext _context;

    public ReverseLookupModel(SurviveWikiContext context)
    {
        _context = context;
    }

    // Holds all materials to display as checkboxes
    public List<Item> AvailableMaterials { get; set; } = new();

    // Captures the IDs of the checkboxes the user selects
    [BindProperty]
    public List<int> SelectedItemIds { get; set; } = new();

    // Holds the items the user can successfully craft
    public List<Item> CraftableItems { get; set; } = new();

    public bool HasSearched { get; set; }

    public async Task OnGetAsync()
    {
        // Load all items used as ingredients so the user can select them
        AvailableMaterials = await _context.Items
            .Where(i => i.UsedIn.Any() && i.Status == PageStatus.Approved)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        HasSearched = true;

        // Re-load materials so the checkboxes render again
        AvailableMaterials = await _context.Items
            .Where(i => i.UsedIn.Any() && i.Status == PageStatus.Approved)
            .OrderBy(i => i.Name)
            .ToListAsync();

        if (SelectedItemIds.Any())
        {
            // The Magic Query: Find items that HAVE a recipe, where ALL ingredients 
            // required by that recipe are present in the user's selected IDs.
            CraftableItems = await _context.Items
                .Include(i => i.CraftedFrom)
                    .ThenInclude(ri => ri.IngredientItem)
                .Where(item => item.CraftedFrom.Any() && 
                               item.CraftedFrom.All(recipeReq => SelectedItemIds.Contains(recipeReq.IngredientItemId)) &&
                               item.Status == PageStatus.Approved)
                .ToListAsync();
        }

        return Page();
    }
}