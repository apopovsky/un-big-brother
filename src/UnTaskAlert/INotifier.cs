using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public interface INotifier
    {
        Task Instruction(Subscriber subscriber);
        Task NoActiveTasksDuringWorkingHours(Subscriber subscriber);
        Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber, ActiveTasksInfo activeTasksInfo);
        Task MoreThanSingleTaskIsActive(Subscriber subscriber, ActiveTasksInfo tasksInfo);
        Task Ping(Subscriber subscriber);
        Task SendTimeReport(Subscriber subscriber, TimeReport timeReport);
        Task SendDetailedTimeReport(Subscriber subscriber, TimeReport timeReport, double offsetThreshold, bool includeSummary = true);
        Task Progress(Subscriber subscriber);
        Task ActiveTasks(Subscriber subscriber, ActiveTasksInfo activeTasksInfo);
        Task IncorrectEmail(string chatId);
        Task EmailUpdated(Subscriber subscriber);
        Task NoEmail(string chatId);
        Task AccountVerified(Subscriber subscriber);
        Task CouldNotVerifyAccount(Subscriber subscriber);
        Task Typing(string chatId, CancellationToken cancellationToken);
        Task RequestEmail(string chatId);
        Task Respond(long chatId, string message);
        Task AccountInfo(Subscriber subscriber);
    }
}
