using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public class AccountWorkflow : CommandWorkflow
    {
        private enum Steps
        {
            Start = 0,
            EnterEmail = 1,
            VerifyEmail = 2,
        }

        private IPinGenerator _pinGenerator;
        private IMailSender _mailSender;

        public override bool IsVerificationRequired => false;

        protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
        {
            var serviceProvider = serviceScopeFactory.CreateScope().ServiceProvider;
            _pinGenerator = Arg.NotNull(serviceProvider.GetService<IPinGenerator>(), $"{nameof(IPinGenerator)} is not resolved");
            _mailSender = Arg.NotNull(serviceProvider.GetService<IMailSender>(), $"{nameof(IMailSender)} is not resolved");
        }

        protected override bool DoesAccept(string input) => input.StartsWith("/email", StringComparison.OrdinalIgnoreCase);

        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            if (CurrentStep == (int) Steps.Start)
            {
                await Notifier.RequestEmail(chatId.ToString());

                CurrentStep = (int) Steps.EnterEmail;
                return WorkflowResult.Continue;
            }

            if (CurrentStep == (int)Steps.EnterEmail)
            {
                if (!IsEmail(input) || !input.EndsWith(Config.EmailDomain))
                {
                    await Notifier.IncorrectEmail(subscriber.TelegramId);
                    CurrentStep = (int)Steps.EnterEmail;

                    return WorkflowResult.Continue;
                }
                
                subscriber.Email = input;
                subscriber.IsVerified = false;
                subscriber.Pin = _pinGenerator.GetRandomPin();

                await Notifier.EmailUpdated(subscriber);

                _mailSender.SendMessage("UN Big Brother bot verification code",
                    $"Please send the following PIN to the bot through the chat: {subscriber.Pin}",
                    subscriber.Email);

                CurrentStep = (int)Steps.VerifyEmail;
                return WorkflowResult.Continue;
            }

            if (CurrentStep == (int) Steps.VerifyEmail)
            {
                var isNumeric = int.TryParse(input, out var code);
                if (isNumeric)
                {
                    if (await VerifyAccount(subscriber, code))
                    {
                        return WorkflowResult.Finished;
                    }

                    return WorkflowResult.Continue;
                }

                await Notifier.CouldNotVerifyAccount(subscriber);

                return WorkflowResult.Continue;
            }

            return WorkflowResult.Finished;
        }

        private static bool IsEmail(string input) => !string.IsNullOrWhiteSpace(input) && input.Contains('@');

        private const int MaxVerificationAttempts = 3;

        private async Task<bool> VerifyAccount(Subscriber subscriber, int code)
        {
            Logger.LogInformation("Verifying account for {Email}, entered PIN is '{Code}'.", subscriber.Email, code);

            if (subscriber.IsVerified)
            {
                // no need to verify anything
                return true;
            }

            subscriber.VerificationAttempts++;

            if (subscriber.Pin == code && subscriber.VerificationAttempts <= MaxVerificationAttempts)
            {
                subscriber.IsVerified = true;
                subscriber.VerificationAttempts = 0;
                await Notifier.AccountVerified(subscriber);

                return true;
            }

            await Notifier.CouldNotVerifyAccount(subscriber);

            return false;
        }
    }
}
