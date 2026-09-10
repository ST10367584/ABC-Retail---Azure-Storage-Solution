using ABCRetail.AzureStorage.Models;
using System.Text.Json;

namespace ABCRetail.AzureStorage.Services
{
    public class CartService : ICartService
    {
        private const string CartKey = "ShoppingCart";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ISession Session => _httpContextAccessor.HttpContext!.Session;

        public Cart GetCart()
        {
            var json = Session.GetString(CartKey);
            if (string.IsNullOrEmpty(json))
                return new Cart();

            try
            {
                return JsonSerializer.Deserialize<Cart>(json) ?? new Cart();
            }
            catch
            {
                return new Cart();
            }
        }

        public void SaveCart(Cart cart)
        {
            Session.SetString(CartKey, JsonSerializer.Serialize(cart));
        }

        public void AddItem(CartItem item)
        {
            var cart = GetCart();
            var existing = cart.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
            if (existing != null)
            {
                existing.Quantity += item.Quantity;
            }
            else
            {
                cart.Items.Add(item);
            }
            SaveCart(cart);
        }

        public void RemoveItem(string productId)
        {
            var cart = GetCart();
            cart.Items.RemoveAll(i => i.ProductId == productId);
            SaveCart(cart);
        }

        public void UpdateQuantity(string productId, int quantity)
        {
            var cart = GetCart();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                if (quantity <= 0)
                    cart.Items.Remove(item);
                else
                    item.Quantity = quantity;
                SaveCart(cart);
            }
        }

        public void ClearCart()
        {
            Session.Remove(CartKey);
        }
    }
}