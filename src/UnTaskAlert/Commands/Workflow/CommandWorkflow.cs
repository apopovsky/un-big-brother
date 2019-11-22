using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
	public abstract class CommandWorkflow
	{
		protected CommandWorkflow()
		{
			Expiration=DateTime.UtcNow.AddMinutes(5);
		}

		[JsonIgnore]
		public bool IsExpired => DateTime.UtcNow > Expiration;
		public DateTime Expiration { get; set; }
		public int CurrentStep { get; set; }

		public async Task<WorkflowResult> Step(string input, Subscriber subscriber, IDbAccessor dbAccessor, INotifier notifier, ILogger log, long chatId)
		{
			if (input.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
			{
				await notifier.Respond(chatId, $"Command cancelled");
				return WorkflowResult.Finished;
			}

			return await PerformStep(input, subscriber, dbAccessor, notifier, log, chatId);
		}

		public bool Accepts(string input)
		{
			if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return DoesAccept(input);
		}

		//TODO: an evil todo. This could be a serialized object of different classes using the type name stored by json.net
		public string Data { get; set; }

		protected abstract bool DoesAccept(string input);
		protected abstract Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, IDbAccessor dbAccessor, INotifier notifier, ILogger log, long chatId);


	}

	public enum WorkflowResult
	{
		Continue,
		Finished
	}
}