using Newtonsoft.Json;
using UnTaskAlert.Commands.Workflow;

namespace UnTaskAlert.Models;

public class Subscriber
{
    public TimeSpan StartWorkingHoursUtc { get; set; }
    public TimeSpan EndWorkingHoursUtc { get; set; }
    [Newtonsoft.Json.JsonProperty(PropertyName="id")]
    public string TelegramId { get; set; }
    public string Email { get; set; }
    public int HoursPerDay { get; set; }
    public int Pin { get; set; }
    public bool IsVerified { get; set; }
    public int VerificationAttempts { get; set; }
    public DateTime LastNoActiveTasksAlert { get; set; }
    public DateTime LastActiveTaskOutsideOfWorkingHoursAlert { get; set; }
    public DateTime LastMoreThanSingleTaskIsActiveAlert { get; set; }
    public DateTime LastActivePullRequestsStartSlotAlertUtc { get; set; }
    public DateTime LastActivePullRequestsMidSlotAlertUtc { get; set; }
    [Newtonsoft.Json.JsonProperty(PropertyName = "projects")]
    public IList<string> AzureDevOpsProjects { get; set; }

    [JsonIgnore]
    public CommandWorkflow ActiveWorkflow { get; set; }

    public DateTime? SnoozeAlertsUntil { get; set; }
    public IList<TimeOff> TimeOff { get; set; }
    public bool IsAdmin { get; set; }
}