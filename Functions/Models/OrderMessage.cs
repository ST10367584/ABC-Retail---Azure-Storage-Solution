using System.Text.Json;

namespace ABCRetail.Functions.Models
{
    public class OrderMessage
    {
        public string? OrderId { get; set; }
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string? Status { get; set; } = "Pending";
        public string? ShippingAddress { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        public static OrderMessage FromJson(string json)
        {
            return JsonSerializer.Deserialize<OrderMessage>(json) ?? new OrderMessage();
        }
    }

    public class OrderItem
    {
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal => Quantity * UnitPrice;
    }
}