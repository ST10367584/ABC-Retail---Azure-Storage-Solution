using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Functions.Models
{
    public class Product : ITableEntity
    {
        public string PartitionKey { get; set; } = "Product";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public double Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime? DateAdded { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}