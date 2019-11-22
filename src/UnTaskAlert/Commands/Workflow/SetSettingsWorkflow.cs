using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
	public class SetSettingsWorkflow : CommandWorkflow
	{
		enum Steps
		{
			Start = 0,
			PreferenceName = 1,
			PreferenceValue = 2
		}

		protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, IDbAccessor dbAccessor, INotifier notifier, ILogger log, long chatId)
		{
			string settingName = null, settingValue = null;
			if (CurrentStep == (int) Steps.Start)
			{
				await notifier.Respond(chatId, $"Please enter one of the following preferences to change:{Environment.NewLine}" +
				                               $"-Setting1{Environment.NewLine}" +
				                               $"-Setting2{Environment.NewLine}" +
				                               $"You can send setting and value together using this format: SettingX=value");

				CurrentStep = (int) Steps.PreferenceName;
				return WorkflowResult.Continue;
			}

			if (CurrentStep == (int) Steps.PreferenceName)
			{
				var inputParts = input.Split('=', StringSplitOptions.RemoveEmptyEntries);
				settingName = inputParts[0];
				if (string.IsNullOrWhiteSpace(settingName))
				{
					await notifier.Respond(chatId, "Please provide a valid setting name");
					
					return WorkflowResult.Continue;
				}

				if (inputParts.Length == 1)
				{
					Data = settingName;
					CurrentStep = (int) Steps.PreferenceValue;
					await notifier.Respond(chatId, "Please provide a new setting value");

					return WorkflowResult.Continue;
				}

				settingValue = inputParts[1];
			}
			else if (CurrentStep==(int) Steps.PreferenceValue)
			{
				settingName = Data;
				settingValue = input;
			}

			await notifier.Respond(chatId, $"From now on {settingName}={settingValue}");
			log.LogInformation($"Changing setting {settingName}. New value: {settingValue}");

			return WorkflowResult.Finished;
		}

		protected override bool DoesAccept(string input)
		{
			return input.StartsWith("/setsettings", StringComparison.OrdinalIgnoreCase);
		}
	}
}