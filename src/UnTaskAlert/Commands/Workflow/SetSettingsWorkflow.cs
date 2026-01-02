using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow;

public class SetSettingsWorkflow : CommandWorkflow
{
    private const string WorkingHoursSetting = "Workhours";
    private const string HoursPerDaySetting = "HoursPerDay";
    private const string ProjectsSetting = "Projects";

    enum Steps
    {
        Start = 0,
        PreferenceName = 1,
        PreferenceValue = 2,
    }

    private static readonly string[] SettingNames = [WorkingHoursSetting, HoursPerDaySetting, ProjectsSetting];
        
    protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
    {
        string settingName = null, settingValue = null;

        if (CurrentStep == (int)Steps.Start)
        {
            // Allow one-shot usage: "/setsettings Setting=value"
            var afterCommand = input.Trim();
            if (afterCommand.StartsWith("/setsettings", StringComparison.OrdinalIgnoreCase))
            {
                afterCommand = afterCommand["/setsettings".Length..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(afterCommand) && afterCommand.Contains('=', StringComparison.Ordinal))
            {
                var inputParts = afterCommand.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (inputParts.Length == 2)
                {
                    settingName = inputParts[0].Trim();
                    settingValue = inputParts[1].Trim();

                    if (IsSettingNameValid(settingName))
                    {
                        var success = TryChangeSetting(subscriber, settingName, settingValue);
                        if (success)
                        {
                            await Notifier.Respond(chatId, $"From now on {settingName}={settingValue}");
                            Logger.LogInformation($"Changing setting {settingName}. New value: {settingValue}");
                            return WorkflowResult.Finished;
                        }

                        await Notifier.Respond(chatId, $"Could not change {settingName}={settingValue}. Please try again.");
                        CurrentStep = (int)Steps.PreferenceName;
                        return WorkflowResult.Continue;
                    }
                }
            }

            var settingsList = SettingNames.Aggregate("", (prev, next) => prev + "-" + next + Environment.NewLine);
            await Notifier.Respond(chatId, $"Please enter one of the following preferences to change:{Environment.NewLine}" +
                                           $"{settingsList}" +
                                           $"You can send setting and value together using this format: SettingX=value");

            CurrentStep = (int)Steps.PreferenceName;
            return WorkflowResult.Continue;
        }

        if (CurrentStep == (int)Steps.PreferenceName)
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
                CurrentStep = (int)Steps.PreferenceValue;

                await Notifier.Respond(chatId, $"Please provide a new setting value. Actual={GetActualSetting(subscriber, settingName)}");

                return WorkflowResult.Continue;
            }

            settingValue = inputParts[1];
        }
        else if (CurrentStep == (int)Steps.PreferenceValue)
        {
            settingName = Data;
            settingValue = input;
        }

        var success2 = TryChangeSetting(subscriber, settingName, settingValue);
        if (success2)
        {
            await Notifier.Respond(chatId, $"From now on {settingName}={settingValue}");
            Logger.LogInformation($"Changing setting {settingName}. New value: {settingValue}");

            return WorkflowResult.Finished;
        }
        else
        {
            await Notifier.Respond(chatId, $"Could not change {settingName}={settingValue}. Please try again.");
            CurrentStep = (int)Steps.PreferenceName;

            return WorkflowResult.Continue;
        }
    }

    private static string GetActualSetting(Subscriber subscriber, string settingName)
    {
        return settingName switch
        {
            HoursPerDaySetting => subscriber.HoursPerDay.ToString(),
            WorkingHoursSetting => $"{subscriber.StartWorkingHoursUtc:hh\\:mm}-{subscriber.EndWorkingHoursUtc:hh\\:mm}",
            ProjectsSetting => subscriber.AzureDevOpsProjects == null || subscriber.AzureDevOpsProjects.Count == 0
                ? "(all)"
                : string.Join(",", subscriber.AzureDevOpsProjects),
            _ => string.Empty
        };
    }

    private static bool IsSettingNameValid(string settingName) =>
        !string.IsNullOrWhiteSpace(settingName) && SettingNames.Contains(settingName);

    private static bool TryChangeSetting(Subscriber subscriber,string settingName, string settingValue)
    {
        var success = false;
        switch (settingName)
        {
            case HoursPerDaySetting:
            {
                if(int.TryParse(settingValue, out var intValue))
                {
                    subscriber.HoursPerDay = Convert.ToInt32(intValue);
                    success = true;
                }

                break;
            }
            case WorkingHoursSetting:
            {
                var values = settingValue.Split(['-',' '],StringSplitOptions.RemoveEmptyEntries);
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

                break;
            }
            case ProjectsSetting:
            {
                // Format: Projects=ProjA,ProjB. Use '*', 'all' or empty value to clear the filter.
                var raw = (settingValue ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(raw) ||
                    string.Equals(raw, "*", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, "all", StringComparison.OrdinalIgnoreCase))
                {
                    subscriber.AzureDevOpsProjects = null;
                    success = true;
                    break;
                }

                var projects = raw
                    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (projects.Count > 0)
                {
                    subscriber.AzureDevOpsProjects = projects;
                    success = true;
                }

                break;
            }
        }

        return success;
    }

    protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
    {
    }

    protected override bool DoesAccept(string input) => input.StartsWith("/setsettings", StringComparison.OrdinalIgnoreCase);
}