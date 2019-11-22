using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public interface IDbAccessor
    {
        Task AddOrUpdateSubscriber(Subscriber subscriber);
        Task<Subscriber> GetSubscriberById(string telegramId, ILogger logger);
        Task<List<Subscriber>> GetSubscribers();
        Task DeleteIfExists(Subscriber subscriber);
    }
}
