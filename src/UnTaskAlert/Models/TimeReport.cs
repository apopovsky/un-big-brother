using System;
using System.Collections.Generic;

namespace UnTaskAlert.Models
{
	public class TimeReport
	{
		private readonly IList<WorkItemTime> _workItemTimes;

		public TimeReport()
		{
			_workItemTimes = new List<WorkItemTime>();
		}

		public double TotalEstimated { get; set; }
		public double TotalCompleted { get; set; }
		public double TotalActive { get; set; }
        public double Expected { get; set; }
        public DateTime StartDate { get; set; }

		public void AddWorkItem(WorkItemTime workItem)
		{
			TotalActive += workItem.Active;
			TotalEstimated += workItem.Estimated;
			TotalCompleted += workItem.Completed;

			_workItemTimes.Add(workItem);
		}

		public IEnumerable<WorkItemTime> WorkItemTimes => _workItemTimes;
	}

	public class WorkItemTime
	{
		public int Id { get; set; }
		public string Title { get; set; }
		public double Estimated { get; set; }
		public double Completed { get; set; }
		public double Active { get; set; }
		public DateTime Date { get; set; }
	}
}