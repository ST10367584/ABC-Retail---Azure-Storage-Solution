using ABCRetail.AzureStorage.Models;

namespace ABCRetail.AzureStorage.Services
{
    public interface IQueueStorageService
    {
        Task InitializeAsync();
        Task SendOrderMessageAsync(OrderMessage order);
        Task<OrderMessage?> ReceiveOrderMessageAsync();
        Task<List<OrderMessage>> PeekOrderMessagesAsync(int maxMessages = 10);
        Task DeleteOrderMessageAsync(string messageId, string popReceipt);
        Task<int> GetQueueLengthAsync();
    }
}