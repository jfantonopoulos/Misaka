using Misaka.Classes;
using Misaka.Services;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Misaka.Models.MySQL;
using System;
using Misaka.Extensions;
using Discord.WebSocket;
using Misaka.Preconditions;
using Discord;

namespace Misaka.Modules
{
    public class SubscriptionsModule : MisakaModuleBase
    {
        private DBContextFactory DBFactory;
        private EmbedService EmbedService;
        private Config Config;
        private CNNService CNNService;
        private RedditService RedditService;

        public SubscriptionsModule(DBContextFactory factory, EmbedService embedService, Config config, CNNService cnnService, RedditService redditService, MathService mathService) : base(mathService)
        {
            DBFactory = factory;
            EmbedService = embedService;
            Config = config;
            CNNService = cnnService;
            RedditService = redditService;
        }

        [RequireCooldown(20)]
        [Command("cnnsubscribe"), Summary("Subscribes this channel to breaking news on CNN.")]
        public async Task CNNSubscribe()
        {
            using (SubDBContext DBContext = DBFactory.Create<SubDBContext>())
            {
                string channelId = Context.Channel.Id.ToString();
                string userId = Context.User.Id.ToString();

                var subExists = (DBContext.CNNSubscribers.FromSql("SELECT * FROM CNNSubscribers WHERE Id = {0} AND Subscriber = {1} LIMIT 1", Context.Channel.Id.ToString(), Context.User.Id.ToString()).AsNoTracking().FirstOrDefault() != null);
                if (subExists)
                {
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("This channel is already subscribed to CNN."), lifeTime: Config.FeedbackMessageLifeTime);
                    return;
                }
                var newUser = new CNNSubscriber(channelId, userId, DateTime.Now);
                await DBContext.CNNSubscribers.AddAsync(newUser);
                await DBContext.SaveChangesAsync();
                CNNService.TextChannnels.Add(Context.Channel as SocketTextChannel);
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"You have subscribed the channel {Context.Channel.Name.Bold()} to CNN breaking news!"));
            }
        }

        [RequireCooldown(20)]
        [Command("cnnunsubscribe"), Summary("Unsubscribes the current channel from breaking news on CNN")]
        public async Task CNNUnsubscribe()
        {
            using (SubDBContext DBContext = DBFactory.Create<SubDBContext>())
            {
                string channelId = Context.Channel.Id.ToString();
                string userId = Context.User.Id.ToString();

                var existingSub = DBContext.CNNSubscribers.FromSql("SELECT * FROM CNNSubscribers WHERE Id = {0} AND Subscriber = {1} LIMIT 1", Context.Channel.Id.ToString(), Context.User.Id.ToString()).AsNoTracking().FirstOrDefault();
                if (existingSub == null)
                {
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("This channel is not subscribed to CNN or you weren't the subscriber."), lifeTime: Config.FeedbackMessageLifeTime);
                    return;
                }

                DBContext.CNNSubscribers.Remove(existingSub);
                await DBContext.SaveChangesAsync();
                CNNService.TextChannnels.Remove(Context.Channel as SocketTextChannel);
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"You have unsubscribed the channel {Context.Channel.Name.Bold()} from CNN breaking news!"));
            }
        }

        [RequireCooldown(20)]
        [Command("redditsubscribe"), Summary("Subscribes this channel to the specified subreddit.")]
        public async Task RedditSubscribe([Summary("The interval between each check, in minutes.")] int minutes, [Summary("The name of the subreddit.")] string subreddit)
        {
            if(minutes < 5)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("Your check interval cannot be shorter than five minutes."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }
            if (minutes > 1440)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("Your check interval cannot be longer than a day."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            using (SubDBContext DBContext = DBFactory.Create<SubDBContext>())
            {
                string channelId = Context.Channel.Id.ToString();
                string userId = Context.User.Id.ToString();

                var subExists = (DBContext.RedditSubscribers.FromSql("SELECT * FROM RedditSubscribers WHERE Id = {0} AND Subscriber = {1} AND Subreddit = {2} LIMIT 1", Context.Channel.Id.ToString(), Context.User.Id.ToString(), subreddit).AsNoTracking().FirstOrDefault() != null);
                if (subExists)
                {
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("This channel is already subscribed to the specified subreddit."), lifeTime: Config.FeedbackMessageLifeTime);
                    return;
                }
                var newUser = new RedditSubscriber(channelId, userId, subreddit, "Text", minutes, DateTime.Now);
                await DBContext.RedditSubscribers.AddAsync(newUser);
                await DBContext.SaveChangesAsync();
                RedditService.AddRedditStream(Context.Channel as SocketTextChannel, subreddit, minutes, Context.User.Id);
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"You have subscribed the channel {Context.Channel.Name.Bold()} to the subreddit {subreddit.Bold()}!"));
            }
        }

        [RequireCooldown(20)]
        [Command("redditunsubscribe"), Summary("Unsubscribes the current channel from the specified subreddit")]
        public async Task RedditUnsubscribe([Summary("The desired subreddit")] string subreddit)
        {
            using (SubDBContext DBContext = DBFactory.Create<SubDBContext>())
            {
                string channelId = Context.Channel.Id.ToString();
                string userId = Context.User.Id.ToString();

                var subExists = DBContext.RedditSubscribers.FromSql("SELECT * FROM RedditSubscribers WHERE Id = {0} AND Subscriber = {1} AND Subreddit = {2} LIMIT 1", Context.Channel.Id.ToString(), Context.User.Id.ToString(), subreddit).AsNoTracking().FirstOrDefault();
                if (subExists == null)
                {
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("This channel is not subscribed to that subreddit or you weren't the subscriber."), lifeTime: Config.FeedbackMessageLifeTime);
                    return;
                }

                DBContext.RedditSubscribers.Remove(subExists);
                await DBContext.SaveChangesAsync();
                RedditService.RemoveRedditStream(Context.Channel as SocketTextChannel, Context.User as SocketGuildUser, subreddit);
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"You have unsubscribed the channel {Context.Channel.Name.Bold()} from the subreddit {subreddit.Bold()}!"));
            }
        }

        [RequireCooldown(10)]
        [Command("subscriptions"), Summary("Lists all the services this channel is subscribed to.")]
        public async Task Subscriptions()
        {
            using (SubDBContext DBContext = DBFactory.Create<SubDBContext>())
            {
                SimpleStopWatch watch = new SimpleStopWatch();
                string channelId = Context.Channel.Id.ToString();
                var cnnSub = DBContext.CNNSubscribers.FromSql("SELECT * FROM CNNSubscribers WHERE Id = {0} LIMIT 1", channelId).AsNoTracking().FirstOrDefault();
                var redditSubs = DBContext.RedditSubscribers.FromSql("SELECT * FROM RedditSubscribers WHERE Id = {0}", channelId).AsNoTracking().ToList();
                EmbedBuilder embedBuilder = new EmbedBuilder();
                EmbedService.BuildFeedbackEmbed(embedBuilder);
                embedBuilder.Title = $"Subscriptions for {Context.Channel.Name.UpperFirstChar()}";
                embedBuilder.Description = "\n\n";
                embedBuilder.Description += $":newspaper: CNN Subscription Status: {(cnnSub == null ? "Not subscribed." : "Currently subscribed.").Bold()}\n\n";
                embedBuilder.Description += ":monkey_face: Reddit Subscriptions: ";
                if (redditSubs == null || redditSubs.Count == 0)
                {
                    embedBuilder.Description += "Not subscribed to any subreddits.".Bold();
                }
                else
                {
                    foreach (RedditSubscriber sub in redditSubs)
                    {
                        embedBuilder.Description += $"{sub.Subreddit.Bold()}, ";
                    }
                    embedBuilder.Description = embedBuilder.Description.Substring(0, embedBuilder.Description.Length - 2);
                }
                TimeSpan timeTook = watch.Stop();
                embedBuilder.WithFooter(footer =>
                {
                    footer.Text = $"⏰ {"Generated in:"}  {Math.Round(timeTook.TotalMilliseconds)}ms";
                });

                await ReplyAsync("", embed: embedBuilder.Build());
            }
        }
    }
}
