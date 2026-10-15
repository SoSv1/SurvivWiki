using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;

namespace SurviveWiki.Pages;

public class ItemModel : PageModel
{
    private readonly SurviveWikiContext _context;

    public ItemModel(SurviveWikiContext context)
    {
        _context = context;
    }

    public Item? CurrentItem { get; set; }

    public async Task<IActionResult> OnGetAsync(string name)
    {
        // If no name is provided, send them back to the home page
        if (string.IsNullOrWhiteSpace(name))
        {
            return RedirectToPage("/Index");
        }

        // Fetch the item and its recipe
        CurrentItem = await _context.Items
            .Include(i => i.CraftedFrom)
                .ThenInclude(ri => ri.IngredientItem)
            .FirstOrDefaultAsync(i => i.Name.ToLower() == name.ToLower() 
                                   && i.Status == PageStatus.Approved);

        // If the item doesn't exist, return a 404 Not Found page
        if (CurrentItem == null)
        {
            return NotFound(); 
        }

        return Page();
    }
}