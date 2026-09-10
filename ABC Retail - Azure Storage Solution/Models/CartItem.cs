namespace ABCRetail.AzureStorage.Models
{
    public class CartItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public double UnitPrice { get; set; }
        public int Quantity { get; set; }
        public double Subtotal => UnitPrice * Quantity;
    }

    public class Cart
    {
        public List<CartItem> Items { get; set; } = new();
        public double Total => Items.Sum(i => i.Subtotal);
        public int TotalItems => Items.Sum(i => i.Quantity);
        public int UniqueItems => Items.Count;
    }
}