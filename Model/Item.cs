namespace SurviveWiki.Models;

public enum PageStatus { Pending, Approved, Rejected }

public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // e.g., Tool, Light, Survival
    public string Description { get; set; } = string.Empty;
    public string BeginnerTip { get; set; } = string.Empty;
    public string CraftingStation { get; set; } = "None"; 
    public PageStatus Status { get; set; } = PageStatus.Pending;

    // Navigation properties for the recipe (What this item makes, and what makes it)
    public List<RecipeIngredient> CraftedFrom { get; set; } = new();
    public List<RecipeIngredient> UsedIn { get; set; } = new();
}