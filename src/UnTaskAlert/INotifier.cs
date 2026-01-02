using UnTaskAlert.Models;

namespace UnTaskAlert;

public interface INotifier
{
    Task Instruction(Subscriber subscriber);
    Task NoActiveTasksDuringWorkingHours(Subscriber subscriber);
    Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber, ActiveTasksInfo activeTasksInfo);
    Task MoreThanSingleTaskIsActive(Subscriber subscriber, ActiveTasksInfo activeTasksInfo);
    Task Ping(Subscriber subscriber);
    Task SendTimeReport(Subscriber subscriber, TimeReport timeReport);
    Task SendDetailedTimeReport(Subscriber subscriber, TimeReport timeReport, double offsetThreshold, bool includeSummary = true);
    Task Progress(Subscriber subscriber);
    Task ActiveTasks(Subscriber subscriber, ActiveTasksInfo activeTasksInfo);
    Task ActivePullRequests(Subscriber subscriber, ActivePullRequestsInfo activePullRequestsInfo);
    Task ActivePullRequestsReminder(Subscriber subscriber, ActivePullRequestsInfo activePullRequestsInfo);
    Task IncorrectEmail(long chatId);
    Task EmailUpdated(Subscriber subscriber);
    Task NoEmail(long chatId);
    Task AccountVerified(Subscriber subscriber);
    Task CouldNotVerifyAccount(Subscriber subscriber);
    Task Typing(long chatId, CancellationToken cancellationToken);
    Task RequestEmail(long chatId);
    Task Respond(long chatId, string message);
    Task AccountInfo(Subscriber subscriber);
}