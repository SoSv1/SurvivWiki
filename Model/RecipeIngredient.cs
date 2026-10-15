namespace SurviveWiki.Models;

public class RecipeIngredient
{
    public int Id { get; set; }
    
    public int ResultItemId { get; set; }
    public Item ResultItem { get; set; } = null!;

    public int IngredientItemId { get; set; }
    public Item IngredientItem { get; set; } = null!;

    public int QuantityRequired { get; set; }
}