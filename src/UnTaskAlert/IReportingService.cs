using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
	public interface IReportingService
	{
		Task CreateReport(Subscriber subscriber, string url, string token, ILogger log);
	}
}