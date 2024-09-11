using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class DbAccessor : IDbAccessor
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        // The name of the database and container we will create
        private const string DatabaseId = "UnBigBrotherDatabase";
        private const string ContainerId = "UnBigBrotherContainer";

        private readonly Config _config;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DbAccessor> _logger;

        public DbAccessor(CosmosClient cosmosClient, IServiceScopeFactory scopeFactory, IOptions<Config> options, ILoggerFactory loggerFactory)
        {
            _cosmosClient = cosmosClient;
            _config = Arg.NotNull(options.Value, nameof(options));
            _serviceScopeFactory = scopeFactory;
            _loggerFactory = loggerFactory;

            // Initialize the database and container
            var database = InitializeDatabaseAsync().GetAwaiter().GetResult();
            _container = InitializeContainerAsync(database).GetAwaiter().GetResult();
            _logger = _loggerFactory.CreateLogger<DbAccessor>();
        }

        public async Task AddOrUpdateSubscriber(Subscriber subscriber, CancellationToken cancellationToken)
        {
            await _container.UpsertItemAsync(subscriber, cancellationToken: cancellationToken);
        }

        public Task<Subscriber> GetSubscriberById(string telegramId, ILogger logger)
        {
            var result = _container
                .GetItemLinqQueryable<Subscriber>(allowSynchronousQueryExecution: true)
                .Where(i => i.TelegramId == telegramId)
                .ToList();

            var subscriber = result.SingleOrDefault();
            subscriber?.ActiveWorkflow?.Inject(_serviceScopeFactory, _config, _loggerFactory);

            return Task.FromResult(subscriber);
        }

        public Task<List<Subscriber>> GetSubscribers()
        {
            var result = _container.GetItemLinqQueryable<Subscriber>(allowSynchronousQueryExecution: true)
                .ToList();

            return Task.FromResult(result);
        }

        public Task DeleteIfExists(Subscriber subscriber) => throw new NotImplementedException();

        private async Task<Database> InitializeDatabaseAsync()
        {
            try
            {
                // Create the database if it does not exist
                var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
                return databaseResponse.Database;
            }
            catch (CosmosException ex)
            {
                // Log the exception
                _logger.LogError(ex, "Failed to create or retrieve the Cosmos DB database.");
                throw;
            }
        }

        private async Task<Container> InitializeContainerAsync(Database database)
        {
            try
            {
                // Create the container if it does not exist
                var containerResponse = await database.CreateContainerIfNotExistsAsync(ContainerId, "/TelegramId");
                return containerResponse.Container;
            }
            catch (CosmosException ex)
            {
                // Log the exception
                _logger.LogError(ex, "Failed to create or retrieve the Cosmos DB container.");
                throw;
            }
        }
    }
}
