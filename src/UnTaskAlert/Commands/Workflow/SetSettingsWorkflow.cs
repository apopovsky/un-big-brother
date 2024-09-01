using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public class SetSettingsWorkflow : CommandWorkflow
    {
        private const string WorkingHoursSetting = "Workhours";
        private const string HoursperdaySetting = "HoursPerDay";

        enum Steps
        {
            Start = 0,
            PreferenceName = 1,
            PreferenceValue = 2
        }

        private static readonly string[] SettingNames = {WorkingHoursSetting, HoursperdaySetting};
        
        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            string settingName = null, settingValue = null;
            if (CurrentStep == (int) Steps.Start)
            {
                var settingsList = SettingNames.Aggregate("", (prev, next) => prev + "-" + next + Environment.NewLine);
                await Notifier.Respond(chatId, $"Please enter one of the following preferences to change:{Environment.NewLine}" +
                                               $"{settingsList}" +
                                               $"You can send setting and value together using this format: SettingX=value");

                CurrentStep = (int) Steps.PreferenceName;
                return WorkflowResult.Continue;
            }

            if (CurrentStep == (int) Steps.PreferenceName)
            {
                var inputParts = input.Split('=', StringSplitOptions.RemoveEmptyEntries);
                settingName = inputParts[0];
                if (!IsSettingNameValid(settingName))
                {
                    await Notifier.Respond(chatId, "Please provide a valid setting name");
                    
                    return WorkflowResult.Continue;
                }

                if (inputParts.Length == 1)
                {
                    Data = settingName;
                    CurrentStep = (int) Steps.PreferenceValue;

                    await Notifier.Respond(chatId, $"Please provide a new setting value. Actual={GetActualSetting(subscriber, settingName)}");

                    return WorkflowResult.Continue;
                }

                settingValue = inputParts[1];
            }
            else if (CurrentStep==(int) Steps.PreferenceValue)
            {
                settingName = Data;
                settingValue = input;
            }

            var success = TryChangeSetting(subscriber, settingName, settingValue, chatId);
			if (success)
            {
                await Notifier.Respond(chatId, $"From now on {settingName}={settingValue}");
                Logger.LogInformation($"Changing setting {settingName}. New value: {settingValue}");
            
                return WorkflowResult.Finished;
            }
            else
            {
                await Notifier.Respond(chatId, $"Could not change {settingName}={settingValue}. Please try again.");
                CurrentStep = (int) Steps.PreferenceName;

				return WorkflowResult.Continue;
            }
		}

        private string GetActualSetting(Subscriber subscriber, string settingName)
        {
            switch (settingName)
            {
				case HoursperdaySetting:
                    return subscriber.HoursPerDay.ToString();
				case WorkingHoursSetting:
                    return $"{subscriber.StartWorkingHoursUtc:hh\\:mm}-{subscriber.EndWorkingHoursUtc:hh\\:mm}";
				default:
                    return string.Empty;
            }
        }

        private bool IsSettingNameValid(string settingName) => !string.IsNullOrWhiteSpace(settingName) || SettingNames.Contains(settingName);

        private bool TryChangeSetting(Subscriber subscriber,string settingName, string settingValue, long chatId)
        {
            var success = false;
            if (settingName == HoursperdaySetting)
            {
                if(int.TryParse(settingValue, out var intValue))
                {
                    subscriber.HoursPerDay = Convert.ToInt32(intValue);
                    success = true;
                }
            }
            else if (settingName == WorkingHoursSetting)
            {
                var values = settingValue.Split(new[]{'-',' '},StringSplitOptions.RemoveEmptyEntries);
                if (values.Length == 2)
                {
                    var startProvided = TimeSpan.TryParseExact(values[0], "hh\\:mm", CultureInfo.InvariantCulture, TimeSpanStyles.None, out var startValue);
                    var endProvided = TimeSpan.TryParseExact(values[1], "hh\\:mm", CultureInfo.InvariantCulture, TimeSpanStyles.None, out var endValue);
                    if (startProvided && endProvided && endValue > startValue)
                    {
                        subscriber.StartWorkingHoursUtc = startValue;
                        subscriber.EndWorkingHoursUtc = endValue;
                        success = true;
                    }
                }
            }

            return success;
        }

        protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
        {
		}

        protected override bool DoesAccept(string input) => input.StartsWith("/setsettings", StringComparison.OrdinalIgnoreCase);
    }
}