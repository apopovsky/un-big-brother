using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class DbAccessor : IDbAccessor
    {
        private CosmosClient _cosmosClient;
        private Database _database;
        private Container _container;

        // The name of the database and container we will create
        private const string DatabaseId = "UnBigBrotherDatabase";
        private const string ContainerId = "UnBigBrotherContainer";

        private readonly Config _config;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public DbAccessor(IServiceScopeFactory scopeFactory, IOptions<Config> options)
        { 
            Arg.NotNull(scopeFactory, nameof(scopeFactory));
            _config = Arg.NotNull(options.Value, nameof(options));
            _serviceScopeFactory = scopeFactory;
        }

        public async Task AddOrUpdateSubscriber(Subscriber subscriber, CancellationToken cancellationToken)
        {
            await CreateDb();
            await _container.UpsertItemAsync(subscriber, cancellationToken: cancellationToken);
        }

        public async Task<Subscriber> GetSubscriberById(string telegramId, ILogger logger)
        {
            await CreateDb();
            var result = _container
                .GetItemLinqQueryable<Subscriber>(allowSynchronousQueryExecution: true)
                .Where(i => i.TelegramId == telegramId)
                .ToList();

            var subscriber = result.SingleOrDefault();
            subscriber?.ActiveWorkflow?.Inject(_serviceScopeFactory, _config, logger);

            return subscriber;
        }

        public async Task<List<Subscriber>> GetSubscribers()
        {
            await CreateDb();

            var result = _container.GetItemLinqQueryable<Subscriber>(allowSynchronousQueryExecution: true)
                .ToList();

            return result;
        }

        public async Task DeleteIfExists(Subscriber subscriber)
        {
            await CreateDb();

            throw new NotImplementedException();
        }

        private async Task CreateDb()
        {
            // todo: move to ctor? any other place?

            if (_container != null && _database != null)
            {
                return;
            }

            var cosmosClientOptions = new CosmosClientOptions
            {
                Serializer = new CosmosJsonNetSerializer(new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                })
            };

            _cosmosClient = new CosmosClient(_config.CosmosDbConnectionString, cosmosClientOptions);
            //_database = _cosmosClient.GetDatabase(_databaseId);
            //_container = _cosmosClient.GetContainer(_databaseId, _containerId);
            _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
            _container = await _database.CreateContainerIfNotExistsAsync(ContainerId, "/TelegramId");
        }
    }
}
