using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CampusCoffeeSystem.Models;

public sealed class CreateCateringRequestViewModel
{
    [BindNever]
    public string CustomerDisplayName { get; set; } = string.Empty;

    [BindNever]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Event date")]
    public DateOnly? EventDate { get; set; }

    [Required, DataType(DataType.Time)]
    [Display(Name = "Event time")]
    public TimeOnly? EventTime { get; set; }

    [Required, MaxLength(240)]
    public string Location { get; set; } = string.Empty;

    [Range(1, 10000)]
    [Display(Name = "Number of guests")]
    public int NumberOfGuests { get; set; } = 1;

    [Range(typeof(decimal), "0", "99999999.99")]
    [Display(Name = "Budget (NZ$, optional)")]
    public decimal? Budget { get; set; }

    [MaxLength(2000)]
    [Display(Name = "Dietary requirements")]
    public string? DietaryRequirements { get; set; }

    [MaxLength(2000)]
    [Display(Name = "Additional requirements / notes")]
    public string? AdditionalRequirements { get; set; }
}

public sealed class CateringStatusUpdateRequest
{
    [Required, MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string CurrentStatus { get; set; } = string.Empty;
}
