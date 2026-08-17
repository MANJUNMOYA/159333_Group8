using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusCoffeeSystem.Models;

public class CustomerOrder
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string OrderNumber { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? CustomerUserId { get; set; }

    [Required, MaxLength(160)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string OrderMethod { get; set; } = string.Empty;

    [MaxLength(80)]
    public string PickupTime { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string SpecialNotes { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Status { get; set; } = OrderStatuses.Pending;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal DeliveryFee { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Total { get; set; }

    public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int CustomerOrderId { get; set; }
    public CustomerOrder CustomerOrder { get; set; } = null!;
    public int ProductId { get; set; }

    [Required, MaxLength(160)]
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }
}

public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

