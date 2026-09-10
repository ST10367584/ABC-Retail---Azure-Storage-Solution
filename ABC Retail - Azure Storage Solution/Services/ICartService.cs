using ABCRetail.AzureStorage.Models;

namespace ABCRetail.AzureStorage.Services
{
    public interface ICartService
    {
        Cart GetCart();
        void SaveCart(Cart cart);
        void AddItem(CartItem item);
        void RemoveItem(string productId);
        void UpdateQuantity(string productId, int quantity);
        void ClearCart();
    }
}