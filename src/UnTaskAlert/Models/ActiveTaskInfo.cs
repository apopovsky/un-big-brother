using System.Collections.Generic;

namespace UnTaskAlert.Models
{
    public class ActiveTaskInfo
    {
        public string User { get; set; }
        public bool HasActiveTasks => ActiveTaskCount > 0;
        public int ActiveTaskCount { get; set; }
        public List<int> WorkItemsIds { get; set; }
    }
}
