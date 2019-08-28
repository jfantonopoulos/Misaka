using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Models.MySQL;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Misaka.Services
{
    public class RedditService : Service
    {
        private List<RedditStream> RedditStreams;
        private Config Config;
        private DBContextFactory DBFactory;
        private DiscordSocketClient Client;

        public enum RedditType
        {
            Text,
            Image
        }

        public class RedditStream
        {
            private IServiceProvider Provider;
            public Timer CheckTimer;
            public RedditType PostType;
            public string Subreddit;
            public ulong StreamOwner;
            public SocketTextChannel TextChannel;
            private int PostCount;
            private int CheckInterval;
            public string FullUrl;

            Int32 UnixTimeStampUTC()
            {
                Int32 unixTimeStamp;
                DateTime currentTime = DateTime.Now;
                DateTime zuluTime = currentTime.ToUniversalTime();
                DateTime unixEpoch = new DateTime(1970, 1, 1);
                unixTimeStamp = (Int32)(zuluTime.Subtract(unixEpoch)).TotalSeconds;
                return unixTimeStamp;
            }

            public RedditStream(string subreddit, ulong streamOwner, int checkInterval, IServiceProvider provider, SocketTextChannel textChannel, RedditType postType = RedditType.Text, int postCount = 20)
            {
                string baseUrl = "https://www.reddit.com/r/";
                string apiRoute = $"/new/.json?count={PostCount}";
                FullUrl = $"{baseUrl}{subreddit}{apiRoute}";
                Provider = provider;
                PostType = postType;
                Subreddit = subreddit;
                StreamOwner = streamOwner;
                PostCount = postCount;
                TextChannel = textChannel;
                CheckInterval = checkInterval;
                int minutes = Provider.GetService<MathService>().TimeUnitToMilli(TimeUnit.Minutes, checkInterval);
                CheckTimer = new Timer(async (e) => {
                    try
                    {

                        string content = await Provider.GetService<HttpService>().Get(FullUrl);
                        if (content.Contains("Ow!"))
                        {
                            CheckTimer.Change(minutes, 0);
                            return;
                        }

                        dynamic obj = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(content));
                        ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"Checking for new posts on $[[DarkMagenta]]$/r/{subreddit}$[[DarkGray]]$ for channel $[[White]]${textChannel.Name.Bold()}");
                        for (int i = 0; i < obj["data"]["children"].Count; i++)
                        {
                            dynamic child = obj["data"]["children"][i];
                            string title = child["data"]["title"];
                            string desc = child["data"]["selftext"];

                            Int32.TryParse(child["data"]["created_utc"].ToString(), out int utc);

                            bool nsfw = child["data"]["over_18"];
                            string author = child["data"]["author"];
                            string permaLink = $"https://reddit.com{child["data"]["permalink"].ToString()}";

                            if (utc > (UnixTimeStampUTC() - (60 * (checkInterval))))
                            {
                                string imgUrl = "";
                                if (PostType == RedditType.Image)
                                    imgUrl = child["data"]["url"].ToString().Replace("&amp;", "&");

                                EmbedBuilder embedBuilder = new EmbedBuilder();
                                Provider.GetService<EmbedService>().BuildFeedbackEmbed(embedBuilder);
                                embedBuilder.Description = "";
                                embedBuilder.WithTitle(title);
                                embedBuilder.WithUrl(permaLink);
                                embedBuilder.Description += $"\nAuthor: {author.ToString().Bold()}\nNSFW: {(nsfw ? "🚫 TRUE".Bold() : "💚 FALSE".Bold())}\n\n{desc}";
                                if (!string.IsNullOrEmpty(imgUrl))
                                    embedBuilder.WithImageUrl(imgUrl.Replace(@"\", ""));
                                await TextChannel.SendMessageAsync("", embed: embedBuilder.Build());
                            }
                        } 
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"$[[Red]]$Error thrown in reddit service.\n{ex.Message}");
                    }
                    CheckTimer.Change(minutes, 0);
                }, null, minutes, 0);
            }
        }

        public RedditService(IServiceProvider provider) : base(provider)
        {
            RedditStreams = new List<RedditStream>();
            Client = provider.GetService<DiscordSocketClient>();
            Config = provider.GetService<Config>();
            DBFactory = provider.GetService<DBContextFactory>();
        }

        public bool RemoveRedditStream(SocketTextChannel channel, SocketGuildUser user, string subreddit)
        {
            var foundStream = RedditStreams.FirstOrDefault(x => x.TextChannel.Id.ToString() == channel.Id.ToString() && x.Subreddit == subreddit);
            if (foundStream == null)
                return false;
            else
            {
                foundStream.CheckTimer.Dispose();
                RedditStreams.Remove(foundStream);
                return true;
            }
        }

        public bool AddRedditStream(SocketTextChannel channel, string subreddit, int interval, ulong ownerId, RedditType postType = RedditType.Text)
        {
            var foundStream = RedditStreams.FirstOrDefault(x => x.TextChannel.Id.ToString() == channel.Id.ToString() && x.Subreddit == subreddit);
            if (foundStream != null)
                return false;
            else
            {
                RedditStreams.Add(new RedditStream(subreddit, ownerId, interval, Provider, channel));
                return true;
            } 
        }

        protected override void Run()
        {
            DiscordSocketClient socketClient = Provider.GetService<DiscordSocketClient>();
            socketClient.Ready += async () =>
            {
                using (SubDBContext DBContext = Provider.GetService<DBContextFactory>().Create<SubDBContext>())
                {
                    var subscribers = DBContext.RedditSubscribers.FromSql("SELECT * FROM RedditSubscribers").AsNoTracking().ToList();
                    foreach (RedditSubscriber sub in subscribers)
                    {
                        SocketTextChannel textChannel = Client.GetChannel(ulong.Parse(sub.Id)) as SocketTextChannel;
                        if (textChannel != null)
                        {
                            AddRedditStream(textChannel, sub.Subreddit, sub.Interval, ulong.Parse(sub.Subscriber));
                            ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"Attached $[[DarkMagenta]]$Reddit Stream$[[Gray]]$ ($[[Yellow]]${sub.Subreddit}$[[Gray]]$ to text channel $[[White]]${textChannel.Name}.");
                        }
                        else
                        {
                            Console.WriteLine("wtf");
                        }
                    }
                }

                //Some channels I always want added.
                SocketTextChannel animeIrlChannel = socketClient.GetChannel(247490683194048513) as SocketTextChannel;
                SocketTextChannel lptChannel = socketClient.GetChannel(241651878633406474) as SocketTextChannel;
                ulong neosId = 140271751572619265;
                if (animeIrlChannel != null)
                {
                    AddRedditStream(animeIrlChannel, "anime_irl", Config.RedditCheckInterval, neosId, RedditType.Image);
                    ConsoleEx.WriteColoredLine(LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Attached $[[DarkMagenta]]$Reddit Stream$[[Gray]]$ $[[Yellow]]$(anime_irl)$[[Gray]]$ to {animeIrlChannel.Name}.");
                }
                if (lptChannel != null)
                {
                    AddRedditStream(lptChannel, "LifeProTips", Config.RedditCheckInterval, neosId, RedditType.Text);
                    ConsoleEx.WriteColoredLine(LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Attached $[[DarkMagenta]]$Reddit Stream$[[Gray]]$ $[[Yellow]]$(LifeProTips)$[[Gray]]$ to {animeIrlChannel.Name}.");
                }

                await Task.CompletedTask;
            };
            
            //return Task.CompletedTask;
        //};
            
        }
    }
}
