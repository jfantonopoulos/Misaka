using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using Misaka.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Misaka.Services
{
    public class CooldownService : Service
    {
        private ConcurrentDictionary<ulong, ConcurrentDictionary<CommandInfo, int>> ServerCooldowns;

        public CooldownService(IServiceProvider provider) : base(provider)
        {
        }

        protected override void Run()
        {
            ServerCooldowns = new ConcurrentDictionary<ulong, ConcurrentDictionary<CommandInfo, int>>();
        }

        public void SetCooldown(ulong serverId, CommandInfo cmdInfo, int seconds)
        {
            TimeSpan elapsedTime = DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            int currentSeconds = (int)elapsedTime.TotalSeconds;
            if (ServerCooldowns.ContainsKey(serverId))
            {
                if (ServerCooldowns[serverId].ContainsKey(cmdInfo))
                {
                    int endTime = ServerCooldowns[serverId][cmdInfo];
                    if (currentSeconds > endTime)
                        ServerCooldowns[serverId][cmdInfo] = currentSeconds + seconds;
                }
                else
                {
                    ServerCooldowns[serverId].TryAdd(cmdInfo, currentSeconds + seconds);
                }
            }
            else
            {
                ServerCooldowns.TryAdd(serverId, new ConcurrentDictionary<CommandInfo, int>());
                ServerCooldowns[serverId][cmdInfo] = currentSeconds + seconds;
            }
        }

        public bool IsCooldownExpired(ulong serverId, string commandName)
        {
            CommandInfo cmdInfo = Provider.GetService<CommandService>().FindInfoByName(commandName);
            if (cmdInfo == null)
                return true;

            if (!ServerCooldowns.ContainsKey(serverId))
                return true;
            
            var foundCmd = ServerCooldowns.GetValue(serverId).FirstOrDefault(x => x.Key.Name.ToLower() == cmdInfo.Name.ToLower());
            if (foundCmd.Key == null)
                return true;
            if (foundCmd.Equals(default(KeyValuePair<CommandInfo, int>)))
                return true;

            return IsCooldownExpired(serverId, foundCmd.Key);
        }

        public bool IsCooldownExpired(ulong serverId, CommandInfo cmdInfo)
        {
            cmdInfo = HasCommandName(serverId, cmdInfo);
            if (cmdInfo == null)
                return true;

            TimeSpan elapsedTime = DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            int currentSeconds = (int)elapsedTime.TotalSeconds;
            if (!ServerCooldowns.ContainsKey(serverId) || !ServerCooldowns[serverId].ContainsKey(cmdInfo))
                return true;
            else
            {
                int endTime = ServerCooldowns[serverId][cmdInfo];
                if (currentSeconds > endTime)
                    return true;
                else if (currentSeconds <= endTime)
                    return false;
            }

            return true;
        }

        public int GetRemainingTime(ulong serverId, CommandInfo cmdInfo)
        {
            cmdInfo = HasCommandName(serverId, cmdInfo);
            if (cmdInfo == null)
                return 0;

            if (!ServerCooldowns.ContainsKey(serverId) || !ServerCooldowns[serverId].ContainsKey(cmdInfo))
                return 0;

            TimeSpan span = DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            int currentSeconds = (int)span.TotalSeconds;
            int secondsLeft = ServerCooldowns.GetValue(serverId)[cmdInfo] - currentSeconds;

            return secondsLeft;
        }

        private CommandInfo HasCommandName(ulong serverId, CommandInfo cmdInfo)
        {
            if (!ServerCooldowns.ContainsKey(serverId))
                return null;

            string name = cmdInfo.Name;
            var locatedInfo = ServerCooldowns.GetValue(serverId).FirstOrDefault(x => x.Key.Name.ToLower() == name.ToLower());
            if (locatedInfo.Key == null)
                return null;
            else
                return locatedInfo.Key;
        }
    }
}
