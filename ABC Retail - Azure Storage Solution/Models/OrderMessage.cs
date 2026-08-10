using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ABCRetail.AzureStorage.Models
{
    public class OrderMessage
    {
        public string OrderId { get; set; } = Guid.NewGuid().ToString();
        [Required(ErrorMessage = "Customer ID is required")]
        public string CustomerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer name is required")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "At least one item is required")]
        [MinLength(1, ErrorMessage = "Order must have at least one item")]
        public List<OrderItem> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
        [Required(ErrorMessage = "Shipping address is required")]
        public string ShippingAddress { get; set; } = string.Empty;

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
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal => Quantity * UnitPrice;
    }
}