using System;
using System.Collections.Generic;

namespace UnTaskAlert.Models
{
    public class Subscribers
    {
        public IList<Subscriber> Items { get; set; }
    }

    public class Subscriber
    {
        public TimeSpan StartWorkingHoursUtc { get; set; }
        public TimeSpan EndWorkingHoursUtc { get; set; }
        public string TelegramId { get; set; }
        public string Email { get; set; }
    }
}
