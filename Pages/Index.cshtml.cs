using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;

namespace SurviveWiki.Pages;

public class IndexModel : PageModel
{
    private readonly SurviveWikiContext _context;

    public IndexModel(SurviveWikiContext context)
    {
        _context = context;
    }
    public async Task<Item?> GetItemWithRecipeAsync(string itemName)
    {
        return await _context.Items
            .Include(i => i.CraftedFrom)
                .ThenInclude(ri => ri.IngredientItem) // Gets the names of the ingredients
            .FirstOrDefaultAsync(i => i.Name.ToLower() == itemName.ToLower()
                                   && i.Status == PageStatus.Approved);
    }
    public async Task<List<Item>> GetCraftableItemsAsync(List<int> inventoryItemIds)
    {
        // Find all recipes where ALL required ingredients are in the user's inventory
        return await _context.Items
            .Where(item => item.CraftedFrom.Any() &&
                           item.CraftedFrom.All(recipeReq => inventoryItemIds.Contains(recipeReq.IngredientItemId)))
            .ToListAsync();
    }
    public async Task SubmitNewItemAsync(string name, string desc, string tip, List<RecipeIngredient> recipe)
    {
        var newItem = new Item
        {
            Name = name,
            Description = desc,
            BeginnerTip = tip,
            Status = PageStatus.Pending, // Awaits admin approval
            CraftedFrom = recipe
        };

        _context.Items.Add(newItem);
        await _context.SaveChangesAsync();
    }
    public async Task<List<GuideStep>> GetFirstDaysPlanAsync()
    {
        return await _context.GuideSteps
            .OrderBy(g => g.DayNumber)
            .Take(5)
            .ToListAsync();
    }
}

