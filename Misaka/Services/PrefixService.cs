using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Models.MySQL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Services
{
    public class PrefixResponseData
    {
        public bool WasSuccess;
        public string Message;

        public PrefixResponseData(bool success, string message)
        {
            WasSuccess = success;
            Message = message;
        }
    }

    public class PrefixService : Service
    {   
        private ConcurrentDictionary<string, List<DiscordCustomPrefix>> PrefixDictionary;
        private DiscordSocketClient Client;

        public PrefixService(IServiceProvider provider) : base(provider)
        {
            PrefixDictionary = new ConcurrentDictionary<string, List<DiscordCustomPrefix>>();
            Client = provider.GetService<DiscordSocketClient>();
        }
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        override protected async Task RunAsync()
        {
            bool readyRan = false;
            Client.Ready += async () =>
            {
                if (readyRan)
                    return;

                readyRan = true;
                Task.Run(async () =>
                {
                    using (BotDBContext DBContext = Provider.GetService<DBContextFactory>().Create<BotDBContext>())
                    {
                        foreach (IGuild guild in Client.Guilds)
                        {
                            if (!PrefixDictionary.ContainsKey(guild.Id.ToString()))
                            {
                                PrefixDictionary.TryAdd(guild.Id.ToString(), new List<DiscordCustomPrefix>());
                            }
                            List<DiscordCustomPrefix> customPrefixes = DBContext.DiscordCustomPrefixes.Where(x => x.Id == guild.Id.ToString()).AsNoTracking().ToList();
                            foreach (DiscordCustomPrefix prefix in customPrefixes)
                            {
                                PrefixDictionary.GetValue(guild.Id.ToString()).Add(prefix);
                            }
                            ConsoleEx.WriteColoredLine(LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Added #{customPrefixes.Count} custom prefixes to the guild {guild.Name}!");
                        }
                    }
                    await Task.CompletedTask;
                });
                await Task.CompletedTask;
            };
            await Task.CompletedTask;
        }

#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        public bool GuildHasPrefix(IGuild guild, string prefix)
        {
            if (PrefixDictionary.ContainsKey(guild.Id.ToString()))
            {
                if (PrefixDictionary.TryGetValue(guild.Id.ToString(), out List<DiscordCustomPrefix> customPrefixBag))
                {
                    foreach (var prefixObj in customPrefixBag)
                    {
                        if (prefixObj.Prefix == prefix)
                            return true;
                    }
                }
                else
                    return false;
            }
            else
                return false;

            return false;
        }

        public PrefixResponseData CanAddPrefix(IGuild guild, IUser user, string prefix)
        {
            if (PrefixDictionary.ContainsKey(guild.Id.ToString()))
            {
                if (PrefixDictionary.TryGetValue(guild.Id.ToString(), out List<DiscordCustomPrefix> customPrefixBag))
                {
                    foreach (DiscordCustomPrefix customPrefix in customPrefixBag)
                    {
                        if (customPrefix.Prefix == prefix)
                            return new PrefixResponseData(false, "The specified prefix already exists in this guild");
                    }
                    return new PrefixResponseData(true, "Prefix is able to be added.");
                }
                else
                    return new PrefixResponseData(true, "Prefix is able to be added.");
            }
            else
            {
                PrefixDictionary.TryAdd(guild.Id.ToString(), new List<DiscordCustomPrefix>());
                return new PrefixResponseData(true, "Prefix is able to be added.");
            }
                
        }

        public async Task<PrefixResponseData> TryAddPrefix(IGuild guild, IUser user, string prefix)
        {
            var resp = CanAddPrefix(guild, user, prefix);
            if (!resp.WasSuccess)
                return new PrefixResponseData(false, resp.Message);

            using (BotDBContext DBContext = Provider.GetService<DBContextFactory>().Create<BotDBContext>())
            {
                var newPrefix = new DiscordCustomPrefix(guild.Id.ToString(), user.Id.ToString(), prefix, "0", DateTime.Now);
                PrefixDictionary.GetValue(guild.Id.ToString()).Add(newPrefix);
                await DBContext.DiscordCustomPrefixes.AddAsync(newPrefix);
                await DBContext.SaveChangesAsync();
                return new PrefixResponseData(true, $"Successfully added new server prefix to {guild.Name}");
            }
        }

        public async Task<List<DiscordCustomPrefix>> GetGuildPrefixes(IGuild guild)
        {
            await Task.CompletedTask;
            List<DiscordCustomPrefix> prefixList = new List<DiscordCustomPrefix>();
            if (PrefixDictionary.ContainsKey(guild.Id.ToString()))
            {
                foreach (var customPrefix in PrefixDictionary.GetValue(guild.Id.ToString()))
                {
                    prefixList.Add(customPrefix);
                }
                return prefixList;
            }
            else
                return prefixList;
        }

        public async Task<PrefixResponseData> ClearGuildPrefixes(IGuild guild)
        {
            if (!PrefixDictionary.ContainsKey(guild.Id.ToString()))
                return new PrefixResponseData(false, "The current guild does not have any custom prefixes at the moment.");
            else
            {
                int items = PrefixDictionary.GetValue(guild.Id.ToString()).Count;
                using (BotDBContext DBContext = Provider.GetService<DBContextFactory>().Create<BotDBContext>())
                {
                    DBContext.DiscordCustomPrefixes.RemoveRange(PrefixDictionary.GetValue(guild.Id.ToString()).ToList());
                    await DBContext.SaveChangesAsync();
                }
                List<DiscordCustomPrefix> newBag = new List<DiscordCustomPrefix>();
                PrefixDictionary[guild.Id.ToString()] = newBag;
                return new PrefixResponseData(true, $"Cleared {items.ToString().Number()} custom prefixes from the current guild.");
            }
        }

        public async Task<PrefixResponseData> TryRemovePrefix(IGuild guild, IUser user, string prefix)
        {
            if (PrefixDictionary.ContainsKey(guild.Id.ToString()))
            {
                List<DiscordCustomPrefix> customPrefixBag = PrefixDictionary.GetValue(guild.Id.ToString());
                if (customPrefixBag == null || customPrefixBag.Count == 0)
                {
                    return new PrefixResponseData(false, "No custom prefixes exist on the current guild.");
                }
                else
                {
                    using (BotDBContext DBContext = Provider.GetService<DBContextFactory>().Create<BotDBContext>())
                    {
                        foreach (DiscordCustomPrefix prefixObj in customPrefixBag)
                        {
                            if (prefixObj.Prefix == prefix && user.Id.ToString() == prefixObj.CreatorId)
                            {
                                PrefixDictionary.GetValue(guild.Id.ToString()).Remove(prefixObj);
                                DBContext.DiscordCustomPrefixes.Remove(prefixObj);
                                await DBContext.SaveChangesAsync();
                                return new PrefixResponseData(true, "Successfully removed the specified prefix from this guild.");
                            }
                        }
                    }
                }
            }
            else
                return new PrefixResponseData(false, $"Unable to remove the specified prefix, it does not exist in this guild.");

            return new PrefixResponseData(false, $"Unable to remove the specified prefix from the guild for an unknown reason.");
        }
    }
}
