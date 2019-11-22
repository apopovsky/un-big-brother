using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
	public class SnoozeAlertWorkflow : CommandWorkflow
	{
		protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, IDbAccessor dbAccessor, INotifier notifier, ILogger log, long chatId)
		{
			var inputParts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			int minutes;
			if (inputParts.Length > 1)
			{
				var parsed = int.TryParse(inputParts[1], out minutes);
				if (!parsed || minutes<=0)
				{
					await notifier.Respond(chatId,"Please provide a valid number of minutes to snooze alerts");
					return WorkflowResult.Continue;
				}
			}
			else
			{
				minutes = 30;
			}

			subscriber.SnoozeAlertsUntil = DateTime.UtcNow.AddMinutes(minutes);
			await notifier.Respond(chatId, $"You won't receive any alerts for the next {minutes} minutes.");

			return WorkflowResult.Finished;
		}

		protected override bool DoesAccept(string input)
		{
			return input.StartsWith("/snooze", StringComparison.OrdinalIgnoreCase);
		}
	}
}
