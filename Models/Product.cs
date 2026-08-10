using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusCoffeeSystem.Models;

public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(80)]
    public string DisplayCategory { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(400)]
    public string ImagePath { get; set; } = string.Empty;

    [MaxLength(80)]
    public string DietaryLabel { get; set; } = string.Empty;

    [MaxLength(30)]
    public string DietaryCssClass { get; set; } = string.Empty;

    [MaxLength(160)]
    public string AllergenLabel { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

