using System.Collections.Generic;
using System.Threading.Tasks;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public interface IDbAccessor
    {
        Task AddOrUpdateSubscriber(Subscriber subscriber);
        Task<Subscriber> GetSubscriberById(string telegramId);
        Task<List<Subscriber>> GetSubscribers();
        Task DeleteIfExists(Subscriber subscriber);
    }
}
