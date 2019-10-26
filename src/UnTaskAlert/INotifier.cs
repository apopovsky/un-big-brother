using System.Threading.Tasks;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public interface INotifier
    {
        Task NoActiveTasksDuringWorkingHours(Subscriber subscriber);
        Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber);
        Task MoreThanSingleTaskIsActive(Subscriber subscriber);
        Task Ping(Subscriber subscriber);
        Task SendTimeReport(Subscriber subscriber, TimeReport timeReport);
    }
}
