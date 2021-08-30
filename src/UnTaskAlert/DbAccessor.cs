using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
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
        private readonly IServiceProvider _serviceProvider;

        // The name of the database and container we will create
        private string _databaseId = "UnBigBrotherDatabase";
        private string _containerId = "UnBigBrotherContainer";

        private readonly Config _config;

        public DbAccessor(IServiceProvider serviceProvider, IOptions<Config> options)
        {
            _config = Arg.NotNull(options.Value, nameof(options));
            _serviceProvider = Arg.NotNull(serviceProvider, nameof(serviceProvider));
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
            subscriber?.ActiveWorkflow?.Inject(_serviceProvider, _config, logger);

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
            _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId);
            _container = await _database.CreateContainerIfNotExistsAsync(_containerId, "/TelegramId");
        }
    }
}
