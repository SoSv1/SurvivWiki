using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;

namespace SurviveWiki.Pages;

/// <summary>
/// Task A1: a moderator reviews pages submitted by contributors.
/// Pending -> Approved (visible to players) or Rejected (hidden).
/// A decision can be moved back to Pending, so a wrong click is never final.
/// Only contributed pages can be reviewed; pages imported from the source wiki are left alone.
/// </summary>
public class ReviewModel : PageModel
{
    private readonly SurviveWikiContext _context;

    public ReviewModel(SurviveWikiContext context)
    {
        _context = context;
    }

    public List<Item> Pending { get; private set; } = new();

    public List<Item> Decided { get; private set; } = new();

    /// <summary>Feedback about the last action, shown once after the redirect.</summary>
    [TempData]
    public string? Notice { get; set; }

    /// <summary>The page the last action changed, so the notice can offer "Undo".</summary>
    [TempData]
    public int? UndoItemId { get; set; }

    public async Task OnGetAsync()
    {
        var contributed = await Contributed()
            .AsNoTracking()
            .Include(item => item.CraftedFrom)
                .ThenInclude(recipe => recipe.IngredientItem)
            .OrderBy(item => item.Id)
            .ToListAsync();

        Pending = contributed.Where(item => item.Status == PageStatus.Pending).ToList();
        Decided = contributed.Where(item => item.Status != PageStatus.Pending)
            .OrderByDescending(item => item.Id)
            .ToList();
    }

    public Task<IActionResult> OnPostApproveAsync(int id) =>
        DecideAsync(id, PageStatus.Pending, PageStatus.Approved,
            name => $"Approved {name}. Players can now find it.");

    public Task<IActionResult> OnPostRejectAsync(int id) =>
        DecideAsync(id, PageStatus.Pending, PageStatus.Rejected,
            name => $"Rejected {name}. It stays hidden from players.");

    public async Task<IActionResult> OnPostReopenAsync(int id)
    {
        var item = await Contributed().FirstOrDefaultAsync(candidate => candidate.Id == id);
        if (item is null || item.Status == PageStatus.Pending)
        {
            Notice = "That page is already back in the queue or no longer exists.";
            return RedirectToPage();
        }

        item.Status = PageStatus.Pending;
        await _context.SaveChangesAsync();
        Notice = $"Moved {item.Name} back to the queue.";
        return RedirectToPage();
    }

    private async Task<IActionResult> DecideAsync(
        int id, PageStatus expected, PageStatus decision, Func<string, string> notice)
    {
        var item = await Contributed().FirstOrDefaultAsync(candidate => candidate.Id == id);

        // The page may have been reviewed in another tab or by another moderator.
        if (item is null || item.Status != expected)
        {
            Notice = "That page was already reviewed or no longer exists. The queue below is up to date.";
            return RedirectToPage();
        }

        item.Status = decision;
        await _context.SaveChangesAsync();

        Notice = notice(item.Name);
        UndoItemId = item.Id;
        return RedirectToPage();
    }

    /// <summary>Pages written by contributors. Imported pages always carry a source URL.</summary>
    private IQueryable<Item> Contributed() =>
        _context.Items.Where(item => item.SourceUrl == null || item.SourceUrl == "");
}
