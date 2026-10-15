using Microsoft.EntityFrameworkCore;
using SurviveWiki.Models;

namespace SurviveWiki.Data;

public class SurviveWikiContext : DbContext
{
    public SurviveWikiContext(DbContextOptions<SurviveWikiContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<GuideStep> GuideSteps => Set<GuideStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure the Recipe many-to-many relationship
        modelBuilder.Entity<RecipeIngredient>()
            .HasOne(ri => ri.ResultItem)
            .WithMany(i => i.CraftedFrom)
            .HasForeignKey(ri => ri.ResultItemId);

        modelBuilder.Entity<RecipeIngredient>()
            .HasOne(ri => ri.IngredientItem)
            .WithMany(i => i.UsedIn)
            .HasForeignKey(ri => ri.IngredientItemId);
    }
}