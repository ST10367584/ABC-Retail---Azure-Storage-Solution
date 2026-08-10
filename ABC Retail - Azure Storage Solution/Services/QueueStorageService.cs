using ABCRetail.AzureStorage.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace ABCRetail.AzureStorage.Services
{
    public class QueueStorageService : IQueueStorageService
    {
        private readonly QueueServiceClient _queueServiceClient;
        private readonly IConfiguration _configuration;
        private QueueClient? _queueClient;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;

        public QueueStorageService(QueueServiceClient queueServiceClient, IConfiguration configuration)
        {
            _queueServiceClient = queueServiceClient;
            _configuration = configuration;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                var queueName = _configuration["AzureStorage:QueueName"] ?? "orderprocessing";
                _queueClient = _queueServiceClient.GetQueueClient(queueName);
                await _queueClient.CreateIfNotExistsAsync();
                _isInitialized = true;
                Console.WriteLine($"Queue '{queueName}' initialized successfully.");
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
        }

        public async Task SendOrderMessageAsync(OrderMessage order)
        {
            await EnsureInitializedAsync();

            if (_queueClient == null)
                throw new InvalidOperationException("Queue not initialized");

            var message = order.ToJson();
            await _queueClient.SendMessageAsync(message);
        }

        public async Task<OrderMessage?> ReceiveOrderMessageAsync()
        {
            await EnsureInitializedAsync();

            if (_queueClient == null) return null;

            var response = await _queueClient.ReceiveMessagesAsync(maxMessages: 1);
            var message = response.Value.FirstOrDefault();

            if (message == null) return null;

            var order = OrderMessage.FromJson(message.MessageText);
            order.OrderId = message.MessageId;
            return order;
        }

        public async Task<List<OrderMessage>> PeekOrderMessagesAsync(int maxMessages = 10)
        {
            await EnsureInitializedAsync();

            var orders = new List<OrderMessage>();
            if (_queueClient == null) return orders;

            var response = await _queueClient.PeekMessagesAsync(maxMessages);
            foreach (var message in response.Value)
            {
                var order = OrderMessage.FromJson(message.MessageText);
                orders.Add(order);
            }
            return orders;
        }

        public async Task DeleteOrderMessageAsync(string messageId, string popReceipt)
        {
            await EnsureInitializedAsync();

            if (_queueClient == null) return;
            await _queueClient.DeleteMessageAsync(messageId, popReceipt);
        }

        public async Task<int> GetQueueLengthAsync()
        {
            await EnsureInitializedAsync();

            if (_queueClient == null) return 0;
            var properties = await _queueClient.GetPropertiesAsync();
            return properties.Value.ApproximateMessagesCount;
        }
    }
}