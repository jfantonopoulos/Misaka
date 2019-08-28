using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Misaka.Services;
using Misaka.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using static Misaka.Extensions.DiscordExtensions;
using Misaka.TypeReaders;
using System.Collections.Concurrent;
using Misaka.Classes.API;
using Misaka.Preconditions;
using Misaka.Models.MySQL;

namespace Misaka.Classes
{
    public class CommandHandler : MisakaBaseClass
    {
        private readonly IServiceProvider Provider;
        private readonly CommandService Commands;
        private readonly DiscordSocketClient Client;
        private Cleverbot CleverbotAPI;
        private readonly Config Config;
        private Dictionary<Type, ModuleInfo> BaseModules;
        private readonly PrefixService PrefixService;

        public CommandHandler(IServiceProvider provider)
        {
            Provider = provider;
            Client = provider.GetService<DiscordSocketClient>();
            Config = provider.GetService<Config>();
            Client.MessageReceived += ProcessCommandAsync;
            Commands = provider.GetService<CommandService>();
            BaseModules = new Dictionary<Type, ModuleInfo>();
            CleverbotAPI = new Cleverbot(Config.CleverbotApiKey);
            PrefixService = provider.GetService<PrefixService>();
        }

        public Type FindModuleByName(string name)
        {
            foreach (KeyValuePair<Type, ModuleInfo> pair in BaseModules)
            {
                if (pair.Key.Name == name)
                    return pair.Key;
            }
            bool externalExists = Provider.GetService<ExecuteService>().ExternalModuleExists(name);
            if (externalExists)
                return Provider.GetService<ExecuteService>().GetExternalModule(name).Key;

            return null;
        }

        public async Task ConfigureAsync()
        {
            Commands.AddTypeReader<CommandInfo>(new CommandInfoTypeReader(Commands));
            Commands.AddTypeReader<Type>(new ModuleTypeReader(Commands, this));
            await Commands.AddModulesAsync(Assembly.GetEntryAssembly());
            var typedModules = typeof(CommandService).GetField("_typedModuleDefs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Commands) as ConcurrentDictionary<Type, ModuleInfo>;
            foreach(KeyValuePair<Type, ModuleInfo> pair in typedModules)
            {
                if (typeof(ModuleBase<SocketCommandContext>).IsAssignableFrom(pair.Key))
                    BaseModules.Add(pair.Key, pair.Value);
            }
        }

        public async Task ProcessCommandAsync(SocketMessage msg)
        {
            var message = msg as SocketUserMessage;
            if (message == null) return;

            int argPos = 0;
            var context = new SocketCommandContext(Client, message);
            List<DiscordCustomPrefix> guildPrefixes = await PrefixService.GetGuildPrefixes(context.Guild);
            string FinalPrefix = Config.Prefix;
            foreach (DiscordCustomPrefix prefix in guildPrefixes)
            {
                argPos = 0;
                if (message.HasStringPrefix(prefix.Prefix, ref argPos))
                {
                    FinalPrefix = prefix.Prefix;
                    break;
                }
                argPos = 0;
            }
            if (message.HasStringPrefix(FinalPrefix, ref argPos))
            {
                string commandExecuted = message.Content.Split(' ')[0].Replace(FinalPrefix, "");
                if (!Provider.GetService<CooldownService>().IsCooldownExpired(context.Guild.Id, commandExecuted))
                {
                    CommandInfo cmdInfo = Commands.FindInfoByName(commandExecuted);
                    Embed cooldownEmbed = Provider.GetService<EmbedService>().MakeFailFeedbackEmbed($"The command `{cmdInfo.Name}` cannot be used for another {Math.Max(Provider.GetService<CooldownService>().GetRemainingTime(context.Guild.Id, cmdInfo), 1)} seconds!");
                    await context.Channel.SendMessageAsync(cooldownEmbed, lifeTime: Provider.GetService<Config>().FeedbackMessageLifeTime);
                    return;
                }
                var result = await Commands.ExecuteAsync(context, argPos, Provider);
                if (result.IsSuccess)
                {
                    CommandInfo cmdInfo = Commands.FindInfoByName(commandExecuted);
                    if (cmdInfo != null)
                    {
                        foreach(var attr in cmdInfo.Preconditions)
                        {
                            if (attr.GetType() == typeof(RequireCooldownAttribute))
                            {
                                RequireCooldownAttribute cooldownAttr = attr as RequireCooldownAttribute;
                                Provider.GetService<CooldownService>().SetCooldown(context.Guild.Id, cmdInfo, cooldownAttr.Seconds);
                            }
                        }
                        
                        using (BotDBContext DBContext = Provider.GetService<DBContextFactory>().Create<BotDBContext>())
                        {
                            string arguments = message.Content.Replace($"{FinalPrefix}{commandExecuted} ", "").Replace($"{FinalPrefix}{commandExecuted}", "");
                            DBContext.CommandLogs.Add(new DiscordCommandLog(message.Author.Id.ToString(), cmdInfo.Name, arguments, context.Channel.Id.ToString(), context.Guild.Id.ToString(), DateTime.Now));
                            await DBContext.SaveChangesAsync();
                        }
                    }
                }
                else
                {
                    ConsoleEx.WriteColoredLine(LogSeverity.Error, ConsoleTextFormat.TimeAndText, ConsoleColor.Red, $"[Command Error - {result.Error?.ToString()}] ", ConsoleColor.White, result.ErrorReason?.ToString());
                    int lifeTime = Provider.GetService<Config>().FeedbackMessageLifeTime;
                    EmbedService embedService = Provider.GetService<EmbedService>();
                    switch (result.Error?.ToString())
                    {
                        case "UnmetPrecondition":
                            Embed unmetEmbed = embedService.MakeFailFeedbackEmbed(result.ErrorReason);
                            await context.Channel.SendMessageAsync("", embed: unmetEmbed, lifeTime: lifeTime);
                            break;
                        case "UnknownCommand":
                            string err = "";
                            err = result.ErrorReason?.ToString();
                            bool badArgs = (err.Contains("This input does not match any overload."));
                            if (badArgs)
                            {
                                await TriggerBadArgEmbed(commandExecuted, context, FinalPrefix);
                                return;
                            }
                            List<FuzzyMatch> matches = Commands.FuzzySearch(commandExecuted);
                            if (matches.Count > 0)
                            {
                                string builtString = "";
                                foreach(FuzzyMatch match in matches)
                                    builtString += $"{match.TextValue.Bold()}, ";

                                builtString = builtString.Substring(0, builtString.Length - 2);
                                Embed suggestEmbed = embedService.MakeFeedbackEmbed($"That command wasn't found.\nDid you mean {builtString}?");
                                await context.Channel.SendMessageAsync("", embed: suggestEmbed);
                            }
                            break;
                        case "BadArgCount":
                            await TriggerBadArgEmbed(commandExecuted, context, FinalPrefix);
                            break;
                        case "ParseFailed":
                            Embed badParseEmbed = embedService.MakeFailFeedbackEmbed("Failed to parse one of the arguments for that command.");
                            await context.Channel.SendMessageAsync("", embed: badParseEmbed, lifeTime: lifeTime);
                            break;
                        case "MultipleMatches":
                            Embed multiMatchEmbed = embedService.MakeFailFeedbackEmbed("Multiple matches were found.");
                            await context.Channel.SendMessageAsync("", embed: multiMatchEmbed, lifeTime: lifeTime);
                            break;
                        case "ObjectNotFound":
                            Embed notFoundEmbed = embedService.MakeFailFeedbackEmbed("One of your specified objects could not be found.");
                            await context.Channel.SendMessageAsync("", embed: notFoundEmbed, lifeTime: lifeTime);
                            break;
                    }
                }
            }
            else if (message.HasMentionPrefix(Provider.GetService<DiscordSocketClient>().CurrentUser, ref argPos))
            {
                using (var typingState = message.Channel.EnterTypingState())
                {
                    string query = message.Content.Replace(Provider.GetService<DiscordSocketClient>().CurrentUser.Mention + " ", "");
                    string resp = await CleverbotAPI.AskAsync(query);
                    await message.Channel.SendMessageAsync(resp);
                    typingState.Dispose();
                }
            }
        }

        private async Task TriggerBadArgEmbed(string commandExecuted, SocketCommandContext context, string finalPrefix)
        {
            EmbedService embedService = Provider.GetService<EmbedService>();
            CommandInfo cmd = Commands.FindInfoByName(commandExecuted);
            if (cmd.Parameters.Count == 0)
            {
                await context.Channel.SendMessageAsync("", embed: Provider.GetService<EmbedService>().MakeFailFeedbackEmbed("That command takes no arguments."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }
            EmbedBuilder badArgEmbed = new EmbedBuilder();
            string errorString = $"You used the wrong amount of arguments.\nCheck below for the correct syntax.\n\n{finalPrefix}{cmd.Name.Bold()}\n";
            embedService.BuildFailEmbed(badArgEmbed);
            badArgEmbed.AppendEmbedDescription(errorString);
            foreach (var arg in cmd.Parameters)
            {
                badArgEmbed.AddField(x =>
                {
                    x.Name = $"{arg.Name} - {arg.Type.ToString().Bold()}{(arg.IsOptional ? " - Optional" : "")}";
                    x.Value = arg.Summary;
                });
            }
            await context.Channel.SendMessageAsync("", embed: badArgEmbed, lifeTime: Config.FeedbackMessageLifeTime);
        }
    }
}
