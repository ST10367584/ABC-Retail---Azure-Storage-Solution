using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace ABCRetail.AzureStorage.Models
{
    public class Product : ITableEntity
    {
        public string PartitionKey { get; set; } = "Product";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public string? ProductId { get; set; }
        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Stock quantity is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative")]
        public int StockQuantity { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [StringLength(50)]
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime? DateAdded { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        public Product()
        {
            PartitionKey = "Product";
            RowKey = Guid.NewGuid().ToString();
            DateAdded = DateTime.UtcNow;
        }

        public Product(string productId) : this()
        {
            ProductId = productId;
            RowKey = productId;
        }
    }
}