using System.ComponentModel.DataAnnotations;

namespace CampusCoffeeSystem.Models;

public class LoginRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class CustomerRegistrationRequest
{
    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Phone { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class MerchantRegistrationRequest
{
    [Required, MaxLength(160)]
    public string BusinessName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class MerchantApplicationRequest
{
    [Required, MaxLength(160)]
    public string BusinessName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(240)]
    public string Address { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public class PlaceOrderRequest
{
    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string OrderMethod { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AddressLine2 { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(20)]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9 -]{1,19}$", ErrorMessage = "Enter a valid postcode.")]
    public string Postcode { get; set; } = string.Empty;

    public bool SaveDeliveryAddressAsDefault { get; set; }

    [MaxLength(2000)]
    public string SpecialNotes { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<PlaceOrderItemRequest> Items { get; set; } = [];
}

public class PlaceOrderItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 99)]
    public int Quantity { get; set; }
}

public class StatusUpdateRequest
{
    [Required, MaxLength(30)]
    public string Status { get; set; } = string.Empty;
}

public class StockUpdateRequest
{
    [Range(0, 100000)]
    public int StockQuantity { get; set; }
}

public class ProductRequest
{
    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(80)]
    public string DisplayCategory { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 10000)]
    public decimal Price { get; set; }

    [Range(0, 100000)]
    public int StockQuantity { get; set; }

    [MaxLength(400)]
    public string ImagePath { get; set; } = "/images/menu/Campus Flat White.jpg";
}
