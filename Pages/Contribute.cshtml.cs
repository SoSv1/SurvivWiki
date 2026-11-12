using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;

namespace SurviveWiki.Pages;

/// <summary>
/// Task C1: a contributor adds a new item page with a structured recipe.
/// The flow is fill in -> preview -> submit. A submitted page is saved as
/// Pending and stays hidden from players until a moderator approves it (task A1).
/// </summary>
public class ContributeModel : PageModel
{
    public const int MaxIngredients = 8;
    private const int StartingIngredientRows = 2;

    private readonly SurviveWikiContext _context;

    public ContributeModel(SurviveWikiContext context)
    {
        _context = context;
    }

    /// <summary>
    /// A fixed list, so every contributor files pages the same way.
    /// The imported data has 86 overlapping categories; contributors pick from these instead.
    /// </summary>
    public static readonly string[] Categories =
    {
        "Tools", "Light", "Survival", "Food", "Weapons", "Armor", "Clothing",
        "Structures", "Science", "Magic", "Refined Materials", "Resources", "Item",
    };

    /// <summary>Value stored in Item.CraftingStation, and the wording shown to the contributor.</summary>
    public static readonly (string Value, string Label)[] Stations =
    {
        ("None", "No station – crafted by hand"),
        ("Science Machine", "Science Machine"),
        ("Alchemy Engine", "Alchemy Engine"),
        ("Not specified", "Not sure / cannot be crafted"),
    };

    public class IngredientInput
    {
        [StringLength(60)]
        public string? Name { get; set; }

        [Range(1, 99, ErrorMessage = "Amount must be between 1 and 99.")]
        public int? Quantity { get; set; } = 1;
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Enter the item name.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "The name must be 2 to 60 characters.")]
        [Display(Name = "Item name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose a category.")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose where the item is crafted.")]
        [Display(Name = "Crafting station")]
        public string CraftingStation { get; set; } = "None";

        public List<IngredientInput> Ingredients { get; set; } = new();

        [Required(ErrorMessage = "Describe what the item is used for.")]
        [StringLength(600, MinimumLength = 10, ErrorMessage = "The description must be 10 to 600 characters.")]
        public string Description { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Keep the tip under 200 characters.")]
        [Display(Name = "Beginner tip")]
        public string? BeginnerTip { get; set; }
    }

    /// <summary>An ingredient row after it has been matched to a real item.</summary>
    public record ResolvedIngredient(int ItemId, string Name, int Quantity);

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Set after a successful submit; survives the redirect so a refresh cannot submit twice.</summary>
    [TempData]
    public string? SubmittedName { get; set; }

    /// <summary>Names offered by the ingredient autocomplete.</summary>
    public List<string> IngredientOptions { get; private set; } = new();

    public bool ShowPreview { get; private set; }

    public List<ResolvedIngredient> PreviewIngredients { get; private set; } = new();

    public string StationLabel(string value) =>
        Stations.FirstOrDefault(station => station.Value == value).Label ?? value;

    public async Task OnGetAsync()
    {
        await LoadOptionsAsync();
        EnsureIngredientRows();
    }

    /// <summary>Live duplicate check used while the contributor types the name.</summary>
    public async Task<IActionResult> OnGetCheckNameAsync(string? name)
    {
        var existing = await FindByNameAsync(name);
        return new JsonResult(new
        {
            status = existing is null || existing.Status == PageStatus.Rejected ? "available"
                : existing.Status == PageStatus.Approved ? "exists"
                : "waiting",
            name = existing?.Name,
        });
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        await LoadOptionsAsync();
        PreviewIngredients = await ValidateAsync();
        ShowPreview = ModelState.IsValid;
        EnsureIngredientRows();
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitAsync()
    {
        var ingredients = await ValidateAsync();
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            EnsureIngredientRows();
            return Page();
        }

        // A rejected page keeps its name, so a corrected version replaces it
        // instead of creating a second page with the same name.
        var name = Input.Name.Trim();
        var item = await _context.Items
            .Include(candidate => candidate.CraftedFrom)
            .FirstOrDefaultAsync(candidate => candidate.Status == PageStatus.Rejected &&
                                              candidate.Name.ToLower() == name.ToLower());
        if (item is null)
        {
            item = new Item();
            _context.Items.Add(item);
        }

        item.Name = name;
        item.Category = Input.Category;
        item.CraftingStation = Input.CraftingStation;
        item.Description = Input.Description.Trim();
        item.BeginnerTip = Input.BeginnerTip?.Trim() ?? string.Empty;
        item.Status = PageStatus.Pending;
        item.CraftedFrom.Clear();
        item.CraftedFrom.AddRange(ingredients.Select(ingredient => new RecipeIngredient
        {
            IngredientItemId = ingredient.ItemId,
            QuantityRequired = ingredient.Quantity,
        }));

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty,
                "Your page could not be saved. Nothing was lost – please try submitting again.");
            await LoadOptionsAsync();
            EnsureIngredientRows();
            return Page();
        }

        SubmittedName = item.Name;
        return RedirectToPage();
    }

    /// <summary>
    /// Checks everything the attributes cannot: duplicates, fixed lists and ingredient rows.
    /// Errors are added to ModelState next to the field that caused them.
    /// </summary>
    private async Task<List<ResolvedIngredient>> ValidateAsync()
    {
        var resolved = new List<ResolvedIngredient>();
        var name = Input.Name?.Trim() ?? string.Empty;

        // Names must stay unique: the seeder and the item URLs both look pages up by name.
        var existing = await FindByNameAsync(name);
        if (existing is not null && existing.Status != PageStatus.Rejected)
        {
            ModelState.AddModelError("Input.Name", existing.Status == PageStatus.Approved
                ? $"A page for {existing.Name} already exists. Search for it instead of adding it again."
                : $"A page for {existing.Name} was already submitted and is waiting for review.");
        }

        if (!string.IsNullOrEmpty(Input.Category) && !Categories.Contains(Input.Category))
        {
            ModelState.AddModelError("Input.Category", "Choose a category from the list.");
        }

        if (!string.IsNullOrEmpty(Input.CraftingStation) &&
            !Stations.Any(station => station.Value == Input.CraftingStation))
        {
            ModelState.AddModelError("Input.CraftingStation", "Choose a crafting station from the list.");
        }

        if (Input.Ingredients.Count > MaxIngredients)
        {
            ModelState.AddModelError(string.Empty, $"A recipe can have at most {MaxIngredients} ingredients.");
            return resolved;
        }

        for (var row = 0; row < Input.Ingredients.Count; row++)
        {
            var ingredientName = Input.Ingredients[row].Name?.Trim();
            if (string.IsNullOrEmpty(ingredientName))
            {
                continue; // Empty rows are simply ignored.
            }

            var nameKey = $"Input.Ingredients[{row}].Name";
            var quantity = Input.Ingredients[row].Quantity;
            if (quantity is null)
            {
                ModelState.AddModelError($"Input.Ingredients[{row}].Quantity", "Enter an amount.");
                continue;
            }

            if (string.Equals(ingredientName, name, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameKey, "An item cannot be an ingredient of itself.");
                continue;
            }

            var ingredient = await _context.Items
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Status == PageStatus.Approved &&
                                             item.Name.ToLower() == ingredientName.ToLower());
            if (ingredient is null)
            {
                ModelState.AddModelError(nameKey,
                    $"\"{ingredientName}\" is not an item in the wiki. Pick one of the suggestions.");
                continue;
            }

            if (resolved.Any(other => other.ItemId == ingredient.Id))
            {
                ModelState.AddModelError(nameKey,
                    $"{ingredient.Name} is listed twice. Use one row and change the amount.");
                continue;
            }

            resolved.Add(new ResolvedIngredient(ingredient.Id, ingredient.Name, quantity.Value));
        }

        return resolved;
    }

    private async Task<Item?> FindByNameAsync(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return await _context.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Name.ToLower() == trimmed.ToLower());
    }

    private async Task LoadOptionsAsync()
    {
        IngredientOptions = await _context.Items
            .AsNoTracking()
            .Where(item => item.Status == PageStatus.Approved)
            .OrderBy(item => item.Name)
            .Select(item => item.Name)
            .ToListAsync();
    }

    private void EnsureIngredientRows()
    {
        while (Input.Ingredients.Count < StartingIngredientRows)
        {
            Input.Ingredients.Add(new IngredientInput());
        }
    }
}
