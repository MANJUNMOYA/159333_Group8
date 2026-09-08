using System.ComponentModel.DataAnnotations;

namespace CampusCoffeeSystem.Models;

public sealed class CustomerProfileViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImagePath { get; set; }

    [Required, Phone, MaxLength(40)]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Address Line 1")]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Address Line 2")]
    public string AddressLine2 { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(20)]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9 -]{1,19}$", ErrorMessage = "Enter a valid postcode.")]
    public string Postcode { get; set; } = string.Empty;

    [Display(Name = "Profile photo")]
    public IFormFile? ProfileImage { get; set; }

    public bool HasDefaultAddress =>
        !string.IsNullOrWhiteSpace(AddressLine1) &&
        !string.IsNullOrWhiteSpace(City) &&
        !string.IsNullOrWhiteSpace(Postcode);
}

public sealed class CheckoutViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string AddressLine2 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public bool HasSavedDeliveryAddress { get; init; }
    public bool IsAuthenticatedCustomer { get; init; }
}
