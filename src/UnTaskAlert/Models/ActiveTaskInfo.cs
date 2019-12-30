using System;
using System.Collections.Generic;

namespace UnTaskAlert.Models
{
    public class ActiveTasksInfo
    {
        public string User { get; set; }
        public bool HasActiveTasks => ActiveTaskCount > 0;
        public int ActiveTaskCount { get; set; }
        public List<TaskInfo> TasksInfo { get; set; }
    }

    public class TaskInfo
    {
        public int Id { get; set; }
        public double ActiveTime { get; set; }
    }
}
