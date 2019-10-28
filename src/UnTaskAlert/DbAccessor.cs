using System.Collections.Generic;
using System.Linq;
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

        public async Task AddOrUpdateSubscriber(Subscriber subscriber)
        {
            await CreateDb();
            await _container.UpsertItemAsync<Subscriber>(subscriber);
        }

        public async Task<Subscriber> GetSubscriberById(string telegramId)
        {
            await CreateDb();
            var result = _container
                .GetItemLinqQueryable<Subscriber>(allowSynchronousQueryExecution: true)
                .Where(i => i.TelegramId == telegramId)
                .ToList();

            return result.SingleOrDefault();
        }

        public async Task<List<Subscriber>> GetSubscribers()
        {
            await CreateDb();

            var result = _container.GetItemLinqQueryable<Subscriber>(allowSynchronousQueryExecution: true)
                .ToList();

            return result;
        }

        private async Task CreateDb()
        {
            _cosmosClient = new CosmosClient(_config.CosmosDbConnectionString);
            _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId);
            _container = await _database.CreateContainerIfNotExistsAsync(_containerId, "/TelegramId");

        }
    }
}
