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
    public struct GameTime
    {
        public string Game;
        public DateTime TimeStarted;
        public GameTime(string game, DateTime timeStarted)
        {
            Game = game;
            TimeStarted = timeStarted;
        }
    }

    public struct StatusTime
    {
        public UserStatus Status;
        public DateTime TimeStarted;
        public StatusTime(UserStatus status, DateTime timeStarted)
        {
            Status = status;
            TimeStarted = timeStarted;
        }
    }

    public class TrackingService : Service
    {
        private DiscordSocketClient Client;
        private DBContextFactory DBFactory;

        public ConcurrentDictionary<ulong, GameTime> UserGameTimes
        { get; private set; }
        public ConcurrentDictionary<ulong, StatusTime> UserStatusTimes
        { get; private set; }

        public TrackingService(IServiceProvider provider) : base(provider)
        {
            UserGameTimes = new ConcurrentDictionary<ulong, GameTime>();
            UserStatusTimes = new ConcurrentDictionary<ulong, StatusTime>();
            Client = provider.GetService<DiscordSocketClient>();
            DBFactory = provider.GetService<DBContextFactory>();
        }

        protected override void Run()
        {
        }

        public async Task InitializeCollection()
        {
            GameTracker();
            MessageTracker();
            ReactionTracker();
            GuildTracker();
            await Task.Run(async () => 
            {
                await ProcessGuilds();
            });
            
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"$[[Cyan]]${DBContext.Guilds.Count()}$[[Gray]]$ Guilds in the database.");
                ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"$[[Cyan]]${DBContext.TextChannels.Count()}$[[Gray]]$ Text Channels in the database.");
                ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"$[[Cyan]]${DBContext.Users.Count()}$[[Gray]]$ Users in the database.");
            }
        }

        private void GuildTracker()
        {
            Client.JoinedGuild += async (guild) =>
            {
                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    var existingGuild = DBContext.Guilds.FromSql("SELECT * FROM Guilds WHERE Id = {0} LIMIT 1", guild.Id.ToString()).AsNoTracking().FirstOrDefault();
                    var newGuild = new DiscordGuild(guild.Id, guild.Name, guild.OwnerId, guild.CreatedAt, guild.IconUrl, guild.SplashUrl);
                    if (existingGuild == null)
                    {
                        await DBContext.AddAsync(newGuild);
                        ConsoleEx.WriteColoredLine($"Joined new Guild $[[DarkCyan]]${guild.Name}$[[Gray]]$!");
                        foreach (ITextChannel channel in guild.TextChannels)
                        {
                            var newChannel = new DiscordTextChannel(channel.Id.ToString(), channel.GuildId.ToString(), channel.Name, channel.Topic, channel.CreatedAt, ((channel as ISocketPrivateChannel) != null));
                            ConsoleEx.WriteColoredLine($"Joined new Channel $[[DarkCyan]]${channel.Name}$[[Gray]]$ within the Guild $[[DarkCyan]]${guild.Name}$[[Gray]]$!");
                        }
                    }
                    else
                    {
                        DBContext.DetachLocal(newGuild, existingGuild.Id);
                        ConsoleEx.WriteColoredLine($"Joined existing Guild $[[DarkCyan]]${guild.Name}$[[Gray]]$, updating data.");
                        await ProcessTextChannels(guild);
                        await ProcessUsers(guild);
                    }
                    await DBContext.SaveChangesAsync();
                }
            };

            Client.GuildUpdated += async (oldGuild, newGuild) =>
            {
                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    DiscordGuild foundGuild = DBContext.Guilds.FromSql("SELECT * FROM Guilds WHERE Id = {0} LIMIT 1", oldGuild.Id.ToString()).AsNoTracking().First();
                    var addedGuild = new DiscordGuild(newGuild.Id, newGuild.Name, newGuild.OwnerId, newGuild.CreatedAt, newGuild.IconUrl, newGuild.SplashUrl);
                    if (foundGuild == null)
                    {
                        
                        await DBContext.AddAsync(addedGuild);
                        await ProcessTextChannels(newGuild);
                        await ProcessUsers(newGuild);
                    }
                    else
                    {
                        DBContext.DetachLocal(addedGuild, oldGuild.Id.ToString());
                        ConsoleEx.WriteColoredLine($"Updating existing Guild $[[DarkCyan]]${oldGuild.Name}$[[Gray]]$, adjusting record.");
                    }
                    await DBContext.SaveChangesAsync();
                }
            };

            Client.ChannelCreated += async (channel) =>
            {
                ITextChannel textChannel = channel as ITextChannel;
                if (textChannel == null)
                    return;

                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    var existingTextChannel = DBContext.TextChannels.FromSql("SELECT * FROM TextChannels WHERE Id = {0} LIMIT 1", channel.Id.ToString()).AsNoTracking().FirstOrDefault();
                    if (existingTextChannel == null)
                    {
                        var newChannel = new DiscordTextChannel(textChannel.Id.ToString(), textChannel.GuildId.ToString(), textChannel.Name, textChannel.Topic, textChannel.CreatedAt, ((textChannel as ISocketPrivateChannel) != null));
                        await DBContext.AddAsync(newChannel);
                        ConsoleEx.WriteColoredLine($"Added new TextChannel $[[DarkCyan]]${textChannel.Name}$[[Gray]]$!");
                    }
                    else
                    {
                        DBContext.DetachLocal(existingTextChannel, existingTextChannel.Id);
                        ConsoleEx.WriteColoredLine($"Channel already exists somehow, $[[DarkCyan]]${textChannel.Name}$[[Gray]]$, updating records.");
                    }
                    await DBContext.SaveChangesAsync();
                }
            };

            Client.ChannelUpdated += async (oldChan, newChan) =>
            {
                ITextChannel oldTextChannel = oldChan as ITextChannel;
                ITextChannel newTextChannel = newChan as ITextChannel;
                if (oldTextChannel == null || newTextChannel == null)
                    return;

                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    var existingChannel = DBContext.TextChannels.FromSql("SELECT * FROM TextChannels WHERE Id = {0} LIMIT 1", oldChan.Id.ToString()).FirstOrDefault();

                    var newChannel = new DiscordTextChannel(newTextChannel.Id.ToString(), newTextChannel.GuildId.ToString(), newTextChannel.Name, newTextChannel.Topic, newTextChannel.CreatedAt, ((newTextChannel as ISocketPrivateChannel) != null));
                    if (existingChannel == null)
                    {
                        await DBContext.AddAsync(newChannel);
                        ConsoleEx.WriteColoredLine($"Added new TextChannel $[[DarkCyan]]${newTextChannel.Name}$[[Gray]]$ that $[[Red]]$should$[[Gray]]$ have existed!");
                    }
                    else
                    {
                        DBContext.DetachLocal(newChannel, newTextChannel.Id.ToString());
                        ConsoleEx.WriteColoredLine($"Updating existing TextChannel $[[DarkCyan]]${oldTextChannel.Name}$[[Gray]]$, adjusting record.");
                    }
                    await DBContext.SaveChangesAsync();
                }
            };
        }

        private void GameTracker()
        {
            Client.UserJoined += async (user) =>
            {
                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    DiscordUser newDiscordUser = new DiscordUser(user.Id.ToString(), (short)user.DiscriminatorValue, user.Username, user.GetAvatarUrl(ImageFormat.Gif), user.CreatedAt, user.IsBot);
                    DiscordUser searchedUser = DBContext.Users.FirstOrDefault(x => x.Id == user.Id.ToString());

                    if (searchedUser == null)
                        await DBContext.AddAsync(newDiscordUser);
                    else
                        DBContext.DetachLocal(newDiscordUser, user.Id.ToString());

                    await DBContext.SaveChangesAsync();
                }
            };

            Client.GuildAvailable += async (guild) =>
            {
                foreach (IGuildUser user in guild.Users)
                {
                    UserStatusTimes.TryAdd(user.Id, new StatusTime(user.Status, DateTime.UtcNow));
                    if (user.Game.HasValue)
                        UserGameTimes.TryAdd(user.Id, new GameTime(user.Game.Value.Name, DateTime.UtcNow));
                }
                await Task.CompletedTask;
            };

            Client.GuildMemberUpdated += async (oldUser, newUser) =>
            {
                try
                {
                    using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                    {
                        DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                        DiscordUser newDiscordUser = new DiscordUser(newUser.Id.ToString(), (short)newUser.DiscriminatorValue, newUser.Username, newUser.GetAvatarUrl(ImageFormat.Gif), newUser.CreatedAt, newUser.IsBot);
                        DiscordUser searchedUser = await DBContext.Users.FindAsync(oldUser.Id.ToString());

                        if (searchedUser == null)
                            await DBContext.AddAsync(newDiscordUser);
                        else
                            DBContext.DetachLocal(newDiscordUser, newUser.Id.ToString());

                        await DBContext.SaveChangesAsync();

                        if (oldUser.Status != newUser.Status)
                            UserStatusTimes.TryUpdate(oldUser.Id, new StatusTime(newUser.Status, DateTime.UtcNow));

                        if (!oldUser.Game.HasValue && !newUser.Game.HasValue)
                            return;

                        if ((oldUser.Game.HasValue && newUser.Game.HasValue) && (oldUser.Game.Value.Name == newUser.Game.Value.Name))
                            return;

                        if (!oldUser.Game.HasValue && newUser.Game.HasValue)
                            UserGameTimes.TryUpdate(oldUser.Id, new GameTime(newUser.Game.Value.Name, DateTime.UtcNow));
                        else if (oldUser.Game.HasValue && UserGameTimes.ContainsKey(oldUser.Id))
                        {
                            TimeSpan timeDiff = DateTime.UtcNow - UserGameTimes.GetValue(oldUser.Id).TimeStarted;
                            int minutes = (int)timeDiff.TotalMinutes;
                            if (minutes == 0)
                                return;

                            using (BotDBContext innerContext = Provider.GetService<DBContextFactory>().Create<BotDBContext>())
                            {
                                DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                                UserGameTimes.TryRemove(oldUser.Id, out _);
                                DiscordGameTime currentTime = await innerContext.GameTime.FindAsync(oldUser.Id.ToString(), oldUser.Game.Value.Name);
                                if (currentTime == null)
                                    await innerContext.GameTime.AddAsync(new DiscordGameTime(oldUser.Id.ToString(), oldUser.Game.Value.Name, minutes, DateTime.Now));
                                else
                                {
                                    currentTime.Minutes += minutes;
                                    currentTime.LastPlayed = DateTime.Now;
                                }

                                if (newUser.Game.HasValue)
                                {
                                    UserGameTimes.TryUpdate(oldUser.Id, new GameTime(newUser.Game.Value.Name, DateTime.UtcNow));
                                }
                                await innerContext.SaveChangesAsync();
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            };
        }

        private void MessageTracker()
        {
            try
            {
                Client.MessageReceived += async (msg) =>
                {
                if (msg.Source == MessageSource.Webhook || msg.Source == MessageSource.System)
                    return;

                if (msg.Content == "" && msg.Attachments.Count == 0)
                    return;

                DiscordMessage msgObj = new DiscordMessage(msg.Id.ToString(), msg.Author.Id.ToString(), msg.Channel.Id.ToString(), msg.Content, DateTime.Now);
                DiscordMessageAttachment attachmentObj = null;
                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    if (msg.Attachments.Count > 0)
                    {
                        foreach (Attachment attachment in msg.Attachments)
                        {
                            using (BotDBContext innerContext = Provider.GetService<DBContextFactory>().Create<BotDBContext>())
                            {
                                innerContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;    
                                attachmentObj = new DiscordMessageAttachment(msgObj.Id, attachment.Url);
                                await innerContext.Attachments.AddAsync(attachmentObj);
                                await innerContext.SaveChangesAsync();
                                //innerContext.GameTime.Include()
                            }
                                
                        }
                    }

                    await DBContext.Messages.AddAsync(msgObj);
                    await DBContext.SaveChangesAsync();
                }
            };

                Client.MessageUpdated += async (cachedMsg, msg, channel) =>
                {
                    using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                    {
                        if (msg != null)
                        {
                            DiscordMessage msgObj = new DiscordMessage(msg.Id.ToString(), msg.Author.Id.ToString(), msg.Channel.Id.ToString(), msg.Content ?? "", DateTime.Now);
                            DBContext.DetachLocal<DiscordMessage>(msgObj, msg.Id.ToString());
                            await DBContext.SaveChangesAsync();
                            await Task.CompletedTask;
                        }
                    }
                };
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task ProcessGuilds()
        {
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                foreach (SocketGuild guild in Client.Guilds)
                {
                    var guildObj = new DiscordGuild(guild.Id, guild.Name, guild.OwnerId, guild.CreatedAt, guild.IconUrl, guild.SplashUrl);
                    var guildRecord = await DBContext.Guilds.FindAsync(guildObj.Id);
                    if (guildRecord == null)
                    {
                        ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, "Adding new Guild ", ConsoleColor.Cyan, guild.Name, ConsoleColor.Gray, " to the database.");
                        await DBContext.AddAsync(guildObj);
                    }
                    else
                        DBContext.DetachLocal(guildObj, guildObj.Id);

                    await DBContext.SaveChangesAsync();

                    await ProcessTextChannels(guild);
                    await ProcessUsers(guild);
                }
            }
        }

        private async Task ProcessTextChannels(SocketGuild guild)
        {
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                foreach (SocketTextChannel textChannel in guild.TextChannels)
                {
                    DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    var channelObj = new DiscordTextChannel(textChannel.Id.ToString(), guild.Id.ToString(), textChannel.Name, textChannel.Topic, textChannel.CreatedAt, ((textChannel as ISocketPrivateChannel) != null));
                    var channelRecord = await DBContext.TextChannels.FindAsync(channelObj.Id);
                    if (channelRecord == null)
                    {
                        ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, "Adding new Text Channel ", ConsoleColor.Cyan, textChannel.Name, ConsoleColor.Gray, " to the database.");
                        await DBContext.AddAsync(channelObj);
                    }
                    else
                        DBContext.DetachLocal(channelObj, channelObj.Id);

                    await DBContext.SaveChangesAsync();
                }
            }
        }

        private async Task ProcessUsers(SocketGuild guild)
        {
            foreach (SocketGuildUser user in guild.Users)
            {
                if (guild.Users.Count > 100)
                    return;

                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    var userObj = new DiscordUser(user.Id.ToString(), (short)user.DiscriminatorValue, user.Username, user.GetAvatarUrl(ImageFormat.Gif), user.CreatedAt, user.IsBot);
                    var userRecord = await DBContext.Users.FindAsync(userObj.Id);
                    if (userRecord == null)
                    {
                        ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, "Adding new User ", ConsoleColor.Cyan, user.Username + $"#{userObj.Discriminator}", ConsoleColor.Gray, " to the database.");
                        await DBContext.AddAsync(userObj);
                    }
                    else
                        DBContext.DetachLocal(userObj, userObj.Id);

                    await DBContext.SaveChangesAsync();
                }
            }
        }

        private void ReactionTracker()
        {
            try
            {
                Client.ReactionAdded += async (cachedMsg, msg, reaction) =>
                {
                    if (reaction.User.Value.IsBot)
                        return;

                    IMessage reactedMessage = await reaction.Channel.GetMessageAsync(cachedMsg.Id);
                    if (reaction.User.Value.Id == reactedMessage.Author.Id)
                        return;

                    DiscordReaction reactionObj;

                    if ((reaction.Emote as Emoji) == null)
                        reactionObj = new DiscordReaction(reaction.MessageId.ToString(), reaction.Channel.Id.ToString(), reactedMessage.Author.Id.ToString(), reaction.UserId.ToString(), (reaction.Emote as Emote).Id.ToString(), reaction.Emote.Name, DateTime.Now);
                    else
                        reactionObj = new DiscordReaction(reaction.MessageId.ToString(), reaction.Channel.Id.ToString(), reactedMessage.Author.Id.ToString(), reaction.UserId.ToString(), (reaction.Emote as Emoji).Name, DateTime.Now);

                    using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                    {
                        DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                        await DBContext.AddAsync(reactionObj);
                        await DBContext.SaveChangesAsync();
                    }
                };

                Client.ReactionRemoved += async (cachedMsg, msg, reaction) =>
                {
                    if (reaction.User.Value.IsBot)
                        return;

                    IMessage reactedMessage = await reaction.Channel.GetMessageAsync(cachedMsg.Id);
                    if (reaction.User.Value.Id == reactedMessage.Author.Id)
                        return;

                    using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                    {
                        DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                        DiscordReaction existingReaction = DBContext.Reactions.FirstOrDefault(x => x.ReceiverId == reactedMessage.Author.Id.ToString() && x.ReactorId == reaction.UserId.ToString() && x.ReactionName == reaction.Emote.Name && x.Id == reaction.MessageId.ToString());
                        if (existingReaction != null)
                            DBContext.Remove(existingReaction);

                        await DBContext.SaveChangesAsync();
                    }
                };

                Client.ReactionsCleared += async (msgCache, msgChannel) =>
                {
                    using(BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                    {
                        DBContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                        IUserMessage fetchedMsg = await msgCache.GetOrDownloadAsync();
                        DBContext.Reactions.RemoveRange(DBContext.Reactions.FromSql("SELECT * FROM Reactions WHERE Id = {0}", fetchedMsg.Id).AsNoTracking().ToList());
                        ConsoleEx.WriteColoredLine($"Reactions cleared from message ID($[[DarkCyan]]${fetchedMsg.Id}$[[Gray]]$.");
                        await DBContext.SaveChangesAsync();
                    }
                };
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
