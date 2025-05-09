using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace UnTaskAlert.Models;

public class ActiveTasksInfo
{
    public string User { get; set; }
    public bool HasActiveTasks => ActiveTaskCount > 0;
    public int ActiveTaskCount { get; set; }
    public List<TaskInfo> TasksInfo { get; set; }
}

public class TaskInfo
{
    public TaskInfo(){}
    public TaskInfo(WorkItem workItem)
    {
        if(workItem==null) return;

        Id=workItem.Id.GetValueOrDefault();
        Title = workItem.Fields["System.Title"].ToString();
    }

    public int Id { get; set; }
    public string Title { get; set; }
    public double ActiveTime { get; set; }
    public TaskInfo Parent { get; set; }
}