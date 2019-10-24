namespace UnTaskAlert
{
    public class ActiveTaskInfo
    {
        public string User { get; set; }
        public bool HasActiveTasks => ActiveTaskCount > 0;
        public int ActiveTaskCount { get; set; }
    }
}
