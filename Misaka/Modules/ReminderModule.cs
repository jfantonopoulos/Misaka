using Discord.Commands;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Modules
{
    public class ReminderModule : MisakaModuleBase
    {
        private ReminderService ReminderService;
        private EmbedService EmbedService;
        private Config Config;

        public ReminderModule(ReminderService reminderService, EmbedService embedService, Config config, MathService mathService) : base(mathService)
        {
            ReminderService = reminderService;
            EmbedService = embedService;
            Config = config;
        }

        [Command("remind"), Summary("Reminds you after the specified interval with your message.")]
        public async Task Remind([Summary("The name of the reminder.")] string name, [Summary("The specified interval")] TimeSpan interval, [Summary("The message to remind you of.")] [Remainder] string message)
        {
            bool nameFree = ReminderService.CreateReminder(Context.User, Context.Channel, name, message, interval);
            if (!nameFree)
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("A reminder with that name already exists on this channel."), lifeTime: Config.FeedbackMessageLifeTime);
            else
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"Successfully created the reminder {name.Bold().Underline()}, will trigger in {interval.ToNiceTime()}."));
        }

        [Command("cancelreminder"), Summary("Cancels your specified reminder.")]
        public async Task CancelReminder([Summary("The name of the reminder you wish to cancel.")] string name)
        {
            bool didRemove = ReminderService.CancelReminder(Context.User, Context.Channel, name);
            if (!didRemove)
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The specified reminder either could not be found, or you don't own it."), lifeTime: Config.FeedbackMessageLifeTime);
            else
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"The reminder {name.Bold().Underline()} has been canceled successfully."));
        }

        [RequireBotPermission(Discord.ChannelPermission.ManageMessages)]
        [RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
        [Command("clearreminders"), Summary("Clears all your Reminders on the current channel.")]
        public async Task ClearReminders()
        {
            int amt = ReminderService.ClearReminders(Context.User, Context.Channel);
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"Successfully removed {amt.ToString().Bold()} reminders from this channel."));
        }
    }
}
