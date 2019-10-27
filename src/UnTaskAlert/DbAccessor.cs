using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
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
        private string _databaseId = "UnBigBrotherDatabase";
        private string _containerId = "UnBigBrotherContainer";

        private readonly Config _config;

        public DbAccessor(IOptions<Config> options)
        {
            _config = Arg.NotNull(options.Value, nameof(options));
        }

        public async Task AddSubscriber(Subscriber subscriber)
        {
            await CreateDb();
            await _container.CreateItemAsync<Subscriber>(subscriber, new PartitionKey(subscriber.TelegramId));
        }

        public async Task<Subscriber> GetSubscriberById(string telegramId)
        {
            await CreateDb();
            var result = await _container.ReadItemAsync<Subscriber>(telegramId, new PartitionKey(telegramId));

            return result;
        }

        private async Task CreateDb()
        {
            _cosmosClient = new CosmosClient(_config.CosmosDbEndpointUri, _config.CosmosDbPrimaryKey);
            _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId);
            _container = await _database.CreateContainerIfNotExistsAsync(_containerId, "/TelegramId");

        }
    }
}
