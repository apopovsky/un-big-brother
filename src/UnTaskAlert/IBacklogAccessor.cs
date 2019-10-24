using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.WebApi;

namespace UnTaskAlert
{
    public interface IBacklogAccessor
    {
        Task<ActiveTaskInfo> GetActiveWorkItems(VssConnection connection, string name, ILogger log);
    }
}
