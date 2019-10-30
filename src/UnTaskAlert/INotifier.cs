using System.Threading.Tasks;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public interface INotifier
    {
        Task Instruction(Subscriber subscriber);
        Task NoActiveTasksDuringWorkingHours(Subscriber subscriber);
        Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber, ActiveTaskInfo activeTaskInfo);
        Task MoreThanSingleTaskIsActive(Subscriber subscriber);
        Task Ping(Subscriber subscriber);
        Task SendTimeReport(Subscriber subscriber, TimeReport timeReport);
        Task Progress(Subscriber subscriber);
        Task ActiveTasks(Subscriber subscriber, ActiveTaskInfo activeTaskInfo);
        Task IncorrectEmail(string chatId);
        Task EmailUpdated(Subscriber subscriber);
        Task NoEmail(string chatId);
        Task AccountVerified(Subscriber subscriber);
        Task CouldNotVerifyAccount(Subscriber subscriber);
        Task Typing(string chatId);
    }
}
