using Discord;
using Misaka.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Misaka.Services
{
    class UserReminder
    {
        private IUser Owner;
        private IMessageChannel Channel;
        private string Message;
        private int Length;
        private Timer ReminderTimer;
        private Action CompleteAction;

        public UserReminder(IUser owner, IMessageChannel channel, string message, int length)
        {
            Owner = owner;
            Channel = channel;
            Message = message;
            Length = length;
            ReminderTimer = new Timer(async (e) =>
            {
                await channel.SendMessageAsync($":alarm_clock: {owner.Mention} {message}");
                ReminderTimer = null;
                CompleteAction();
            }, null, length, 0);
        }

        public bool IsOwner(IUser user)
        {
            return (Owner.Id == user.Id);
        }

        public void Cancel()
        {
            ReminderTimer.Dispose();
            ReminderTimer = null;
        }

        public void SetAction(Action action)
        {
            CompleteAction = action;
        }
    }

    public class ReminderService : Service
    {
        private ConcurrentDictionary<ulong, ConcurrentDictionary<string, UserReminder>> Reminders;

        public ReminderService(IServiceProvider provider) : base(provider)
        {
            Reminders = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, UserReminder>>();
        }

        protected override void Run()
        {
        }

        public bool CreateReminder(IUser user, IMessageChannel channel, string name, string message, TimeSpan timeSpan)
        {
            ulong channelId = channel.Id;

            if (!Reminders.ContainsKey(channelId))
                Reminders.TryAdd(channelId, new ConcurrentDictionary<string, UserReminder>());

            if (Reminders.GetValue(channelId).ContainsKey(name))
                return false;

            Reminders.GetValue(channelId).TryAdd(name, new UserReminder(user, channel, message, (int)timeSpan.TotalMilliseconds));
            Reminders.GetValue(channelId).GetValue(name).SetAction(() => { Reminders.GetValue(channelId).TryRemove(name, out _); });
            return true;
        }

        public bool CancelReminder(IUser user, IMessageChannel channel, string name)
        {
            ulong channelId = channel.Id;

            if (!Reminders.ContainsKey(channelId))
                return false;

            if (!Reminders.GetValue(channelId).ContainsKey(name))
                return false;

            if (!Reminders.GetValue(channelId).GetValue(name).IsOwner(user))
                return false;

            Reminders.GetValue(channelId).GetValue(name).Cancel();
            Reminders.GetValue(channelId).TryRemove(name, out _);
            return true;
        }

        public int ClearReminders(IUser user, IMessageChannel channel)
        {
            ulong channelId = channel.Id;
            if (!Reminders.ContainsKey(channelId))
                return 0;

            List<KeyValuePair<string, UserReminder>> reminders = Reminders.GetValue(channelId).Where(x => x.Value.IsOwner(user)).ToList();
            foreach (KeyValuePair<string, UserReminder> reminder in reminders)
            {
                Reminders.GetValue(channelId).GetValue(reminder.Key).Cancel();
                Reminders.GetValue(channelId).TryRemove(reminder.Key, out _);
            }
            return reminders.Count;
        }
    }
}
