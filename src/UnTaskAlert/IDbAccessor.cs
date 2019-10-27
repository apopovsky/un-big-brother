using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public interface IDbAccessor
    {
        Task AddSubscriber(Subscriber subscriber);
        Task<Subscriber> GetSubscriberById(string telegramId);
    }
}
