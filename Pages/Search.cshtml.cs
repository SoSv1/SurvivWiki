using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;

namespace SurviveWiki.Pages;

public class SearchModel : PageModel
{
    private readonly SurviveWikiContext _context;

    public SearchModel(SurviveWikiContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public string Query { get; set; } = string.Empty;

    public List<Item> SearchResults { get; set; } = new();
    public bool HasSearched { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!string.IsNullOrWhiteSpace(Query))
        {
            HasSearched = true;
            
            // 1. Try to find an exact match first
            var exactMatch = await _context.Items
                .FirstOrDefaultAsync(i => i.Name.ToLower() == Query.ToLower() 
                                       && i.Status == PageStatus.Approved);

            // If an exact match is found, send them straight to the Item page!
            if (exactMatch != null)
            {
                return RedirectToPage("/Item", new { name = exactMatch.Name });
            }
            
            // 2. If no exact match, find partial matches (e.g. typing "ax" finds "Axe")
            SearchResults = await _context.Items
                .Where(i => i.Name.ToLower().Contains(Query.ToLower()) 
                         && i.Status == PageStatus.Approved)
                .ToListAsync();
        }
        
        return Page();
    }
}