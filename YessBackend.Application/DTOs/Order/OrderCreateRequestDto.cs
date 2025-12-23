using System.ComponentModel.DataAnnotations;

namespace YessBackend.Application.DTOs.Order;

public class OrderCreateRequestDto
{
    [Required]
    public int PartnerId { get; set; }
    
    [Required]
    public List<OrderItemDto> Items { get; set; } = new();
    
    public string? DeliveryAddress { get; set; }
    public string DeliveryType { get; set; } = "pickup";
    public string? DeliveryNotes { get; set; }
    public string? IdempotencyKey { get; set; }

    // Новое поле для выбора оплаты: "card" или "yescoin"
    public string PaymentMethod { get; set; } = "card";
}
