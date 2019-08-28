using AngleSharp;
using AngleSharp.Dom.Html;
using AngleSharp.Network.Default;
using AngleSharp.Parser.Html;
using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Models.MySQL;
using Misaka.Preconditions;
using Misaka.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Modules
{
    public class DataModule : MisakaModuleBase
    {
        private HttpService HttpService;
        private EmbedService EmbedService;
        private TrackingService TrackingService;
        private ImageService ImageService;
        private DBContextFactory DBFactory;
        private Config Config;

        public DataModule(HttpService httpService, EmbedService embedService, TrackingService trackingService, ImageService imageService, DBContextFactory dbFactory, Config config, MathService mathService) : base(mathService)
        {
            HttpService = httpService;
            EmbedService = embedService;
            TrackingService = trackingService;
            ImageService = imageService;
            DBFactory = dbFactory;
            Config = config;
        }

        [RequireCooldown(5)]
        [Command("btc"), Summary("Gets the current worth of BTC in USD.")]
        public async Task BTC()
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            EmbedService.BuildSuccessEmbed(embedBuilder);
            string httpContent = await HttpService.Get("https://www.google.com/search?q=btc+to+usd&oq=btc+to+usd");
            var parser = new HtmlParser(Configuration.Default.WithDefaultLoader(x => x.IsResourceLoadingEnabled = true));
            var document = parser.Parse(httpContent);
            var gCardSelector = document.QuerySelector("div.currency.g.vk_c.obcontainer");
            if (gCardSelector != null)
            {
                var amtText = gCardSelector.QuerySelector("div.curtgt").TextContent;
                var imgSrc = gCardSelector.QuerySelector("img#ccw_chart").Attributes["data-src"].Value + "&chs=400x250&chsc=1";
                embedBuilder.Title = "💵 Current Bitcoin Worth in USD";
                embedBuilder.Description = $"**1** Bitcoin = {amtText.Replace(" US Dollar", "").Bold()} USD";
                embedBuilder.WithImageUrl(imgSrc);
                await ReplyAsync("", embed: embedBuilder.Build());
            }
        }

        [RequireCooldown(10)]
        [Command("guildinfo"), Summary("Retrieves brief information about the current guild.")]
        public async Task GuildInfo()
        {
            DateTime startTime = DateTime.Now;
            var orderedRoles = Context.Guild.Roles.Where(x => x.IsMentionable).ToList().OrderByDescending(x => x.Permissions.ToString());
            EmbedBuilder embedBuilder = new EmbedBuilder();
            EmbedService.BuildFeedbackEmbed(embedBuilder);
            embedBuilder.Title = $"{Context.Guild.Name.ToString()} Info";
            embedBuilder.Description = $"{Context.Guild.DefaultChannel.Topic.SpliceText(50)}\n\n{"Roles:".Bold()} ";

            foreach(IRole role in orderedRoles)
            {
                embedBuilder.AppendEmbedDescription($"{role.Mention} ({Context.Guild.Users.Where(x => x.Roles.Contains(role)).Count().ToString().Bold()}), ");
            }
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                embedBuilder.AddField(x => { x.Name = ":desktop: Default Channel"; x.Value = Context.Guild.DefaultChannel.Name; x.IsInline = true; });
                embedBuilder.AddField(x => { x.Name = ":man: Users"; x.Value = Context.Guild.MemberCount; x.IsInline = true; });
                embedBuilder.AddField(x => { x.Name = ":abc: Text Channels"; x.Value = Context.Guild.TextChannels.Count(); x.IsInline = true; });
                embedBuilder.AddField(x => { x.Name = ":speaking_head: Voice Channels"; x.Value = Context.Guild.VoiceChannels.Count(); x.IsInline = true; });
                embedBuilder.AddField(x => { x.Name = ":love_letter: Stored Messages"; x.Value = DBContext.Messages.FromSql("SELECT Messages.Id, Messages.ChannelId, TextChannels.GuildId FROM Messages INNER JOIN TextChannels ON TextChannels.Id = Messages.ChannelId WHERE TextChannels.GuildId = {0}", Context.Guild.Id.ToString()).AsNoTracking().Count().ToString(); x.IsInline = true; });                
                //embedBuilder.AddField(x => { x.Name = ":love_letter: Stored Messages"; x.Value = DBContext.Messages.Where(y => Context.Guild.Channels.FirstOrDefault(z => z.Id.ToString() == y.ChannelId) != null).Count().ToString(); x.IsInline = true; });
                embedBuilder.AddField(x => { x.Name = ":camera_with_flash:  Multifactor Authentication Level"; x.Value = Enum.GetName(typeof(MfaLevel), Context.Guild.MfaLevel); x.IsInline = true; });
                embedBuilder.AddField(x => { x.Name = ":tools: Commands Executed"; x.Value = DBContext.CommandLogs.Where(y => y.GuildId == Context.Guild.Id.ToString()).Count().ToString().Number().Bold(); x.IsInline = true; });
            }
            embedBuilder.WithThumbnailUrl(Context.Guild.IconUrl);
            embedBuilder.WithFooter(x =>
            {
                x.Text = $"⏰ Generated in:  {Math.Round((DateTime.Now.Subtract(startTime).TotalMilliseconds)).ToString()}ms";
            });
            await ReplyAsync("", embed: embedBuilder.Build());
        }

        [RequireCooldown(10)]
        [Command("weather"), Summary("Gets the forecast for the specified city.")]
        public async Task Weather([Summary("The state.")] string state, [Summary("The city.")] [Remainder] string city)
        {
            DateTime startTime = DateTime.Now;
            string apiUrl = $"https://query.yahooapis.com/v1/public/yql?q=select%20*%20from%20weather.forecast%20where%20woeid%20in%20(select%20woeid%20from%20geo.places(1)%20where%20text%3D%22{city}%2C%20{state}%22)&format=json&env=store%3A%2F%2Fdatatables.org%2Falltableswithkeys";
            string res = await HttpService.Get(apiUrl);
            Dictionary<string, dynamic> json = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(res));
            EmbedBuilder embedBuilder = new EmbedBuilder();
            EmbedService.BuildSuccessEmbed(embedBuilder);
            embedBuilder.Title = $"Forecast For {state.ToUpper()}, {city.UpperFirstChar()}";
            embedBuilder.Description = "";
            for (int i = 0; i < json["query"]["results"]["channel"]["item"]["forecast"].Count / 2; i++)
            {
                string emoji;
                var dayForecast = json["query"]["results"]["channel"]["item"]["forecast"][i];
                switch ((string)dayForecast["text"])
                {
                    case "Snow":
                        emoji = ":cloud_snow:";
                        break;
                    case "Breezy":
                        emoji = ":cloud_tornado:";
                        break;
                    case "Scattered Thunderstorms":
                    case "Thunderstorms":
                        emoji = ":thunder_cloud_rain:";
                        break;
                    case "Partly Cloudy":
                    case "Mostly Cloudy":
                        emoji = ":cloud:";
                        break;
                    case "Mostly Sunny":
                        emoji = ":white_sun_small_cloud:";
                        break;
                    default:
                        emoji = ":sunny:";
                        break;
                }
                string day = $"{dayForecast["day"].ToString()}, {dayForecast["date"].ToString()}";
                string text = $"{((string)dayForecast["text"]).Bold()}, High of {((string)dayForecast["high"]).Bold()}F; Low of {((string)dayForecast["low"]).Bold()}F";
                embedBuilder.AddField(field =>
                {
                    field.Name = $"{emoji} {day}";
                    field.Value = text;
                });
            }
            embedBuilder.WithFooter(x =>
            {
                x.Text = $"⏰ Generated in:  {Math.Round((DateTime.Now.Subtract(startTime).TotalMilliseconds)).ToString()}ms";
            });
            await ReplyAsync("", embed: embedBuilder.Build());
        }

        [Command("statustime"), Summary("Gets the amount of time the user has kept their status."), Alias("status", "userstatus")]
        public async Task GetStatusTime([Summary("The specified user")] IUser user)
        {
            if (!TrackingService.UserStatusTimes.ContainsKey(user.Id))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("No status data has been collected for that user."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }
            StatusTime statusTime = TrackingService.UserStatusTimes.GetValue(user.Id);
            string statusName = Enum.GetName(typeof(UserStatus), statusTime.Status);
            TimeSpan timeDiff = DateTime.UtcNow.Subtract(statusTime.TimeStarted);
            await ReplyAsync("", embed: EmbedService.MakeFeedbackEmbed($":clock1: {user.Username.Bold()} has been {statusName} for {timeDiff.ToNiceTime()}."));
        }

        [RequireCooldown(10)]
        [Command("userreactions"), Summary("Gets the top number of reactions the user has received."), Alias("reactions", "topreactions")]
        public async Task UserReactions([Summary("The specified user.")] IUser user)
        {
            DateTime startTime = DateTime.Now;
            EmbedBuilder embedBuilder = new EmbedBuilder()
            {
                Title = "Most Received Reactions"
            };
            embedBuilder.WithAuthor(author =>
            {
                author.Name = user.Username;
                author.IconUrl = user.GetAvatarUrl();
            });
            EmbedService.BuildSuccessEmbed(embedBuilder);
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                List<DiscordReaction> myReactions = DBContext.Reactions.Where(x => x.ReceiverId == user.Id.ToString()).ToList();
                var reactionCount = myReactions.GroupBy(x => new { x.ReactionName, x.ReactionId }).Where(x => x.Count() > 0).ToDictionary(x => x.Key, y => y.Count()).OrderByDescending(x => x.Value);
                embedBuilder.Description = $"{myReactions.Count().ToString().Bold()} {"reactions received".Italics()}.\n\n";
                for (int i = 0; i < Math.Min(Config.MaxReactionsToFetch, reactionCount.Count()); i++)
                {
                    var emoteCount = reactionCount.ElementAt(i);
                    var emote = "";
                    //Emote serverEmote = Context.Guild.Emotes.FirstOrDefault(x => x.Id.ToString() == emoteCount.Key.ReactionId && x.Name == emoteCount.Key.ReactionName);
                    if (!string.IsNullOrEmpty(emoteCount.Key.ReactionId))
                        emote = Emote.Parse($"<:{emoteCount.Key.ReactionName}:{emoteCount.Key.ReactionId}>").ToString();
                    else
                        emote = new Emoji(emoteCount.Key.ReactionName).Name;
                    embedBuilder.Description += $"{emote} x{emoteCount.Value.ToString().Bold()}\n";
                }

                embedBuilder.WithFooter(footer =>
                {
                    footer.Text = $"⏰ {"Generated in:"}  {Math.Round((DateTime.Now.Subtract(startTime).TotalMilliseconds)).ToString()}ms";
                });
            }
            await ReplyAsync("", embed: embedBuilder);
        }

        [RequireCooldown(10)]
        [Command("usergametime"), Summary("Gets the user's most played games."), Alias("gametime")]
        public async Task UserGameTime([Summary("The specified user.")] IUser user)
        {
            DateTime startTime = DateTime.Now;
            EmbedBuilder embedBuilder = new EmbedBuilder()
            {
                Title = $"Most Played Games"
            };
            embedBuilder.WithAuthor(author =>
            {
                author.Name = user.Username;
                author.IconUrl = user.GetAvatarUrl();
            });
            EmbedService.BuildSuccessEmbed(embedBuilder);
            embedBuilder.Description = "";
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                List<DiscordGameTime> myGameTime = DBContext.GameTime.FromSql("SELECT * FROM GameTime WHERE Id = {0} ORDER BY Minutes DESC LIMIT 5;", user.Id.ToString()).AsNoTracking().ToList();
                int gameCount = myGameTime.Count();

                for (int i = 0; i < Math.Min(5, gameCount); i++)
                {
                    var gameTime = myGameTime.ElementAt(i);
                    embedBuilder.AddField(x =>
                    {
                        x.Name = "#" + (i + 1) + " " + gameTime.Name;
                        x.Value = new TimeSpan(0, (int)gameTime.Minutes, 0).ToNiceTime();
                    });
                }

                var totalGameTime = DBContext.GameTime.FromSql("SELECT * FROM GameTime WHERE Id = {0}", user.Id.ToString()).AsNoTracking().Sum(x => x.Minutes);

                int totalMinutes = (int)totalGameTime;

                TimeSpan time = new TimeSpan(0, totalMinutes, 0);
                embedBuilder.WithFooter(footer =>
                {
                    footer.Text = $"⏰ {"Generated in:"}  {Math.Round((DateTime.Now.Subtract(startTime).TotalMilliseconds)).ToString()}ms";
                });
                embedBuilder.Description = $"{"Total Gametime:".Code()} {time.ToNiceTime()}\n";
            }
            await ReplyAsync("", embed: embedBuilder);
        }

        [RequireCooldown(10)]
        [Command("topgametime"), Summary("Gets the users with the highest game time."), Alias("topusers")]
        public async Task TopGameTime([Summary("The specified game.")] [Remainder] string name)
        {
            try
            {
                DateTime startTime = DateTime.Now;
                EmbedBuilder embedBuilder = new EmbedBuilder()
                {
                    Title = $"{name} Leaderboard"
                };
                EmbedService.BuildSuccessEmbed(embedBuilder);
                embedBuilder.Description = "";
                ImageSearchResult result = await ImageService.SearchImage($"{name} icon", 1);
                embedBuilder.ThumbnailUrl = result.Url;
                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    SimpleStopWatch watch = new SimpleStopWatch();
                    List<DiscordGameTime> playerGameTime = DBContext.GameTime.FromSql("SELECT * FROM GameTime WHERE Name = {0} ORDER BY Minutes DESC LIMIT 5", name).AsNoTracking().ToList();
                    if (playerGameTime.Count == 0)
                    {
                        await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"No Gametime found for the game [{name.Bold()}]."));
                        return;
                    }

                    for (int i = 0; i < Math.Min(5, playerGameTime.Count()); i++)
                    {
                        var gameTime = playerGameTime.ElementAt(i);
                        DiscordUser user = DBContext.Users.FromSql("SELECT * FROM Users WHERE Id = {0} LIMIT 1", gameTime.Id.ToString()).AsNoTracking().ToList().SingleOrDefault();
                        if (user == null)
                            continue;

                        string text = StandardExtensions.GetAgeText(DateTime.Now.AddMinutes(-gameTime.Minutes));
                        embedBuilder.AddField(x =>
                        {
                            x.Name = "#" + (i + 1) + " " + user.Username;
                            x.Value = new TimeSpan(0, (int)gameTime.Minutes, 0).ToNiceTime();
                        });

                    }

                    var totalGameTime = DBContext.GameTime.FromSql("SELECT * FROM GameTime WHERE Name = {0}", name).AsNoTracking().Sum(x => x.Minutes);

                    TimeSpan totalMinutes = new TimeSpan(0, (int)totalGameTime, 0);

                    //string txt = StandardExtensions.GetAgeText(DateTime.Now.AddMinutes(-totalMinutes));
                    //TimeSpan time = new TimeSpan(0, totalMinutes, 0);
                    embedBuilder.WithFooter(footer =>
                    {
                        footer.Text = $"⏰ {"Generated in:"}  {watch.Stop().TotalMilliseconds}ms";
                    });
                    embedBuilder.Description = $"{"Total Gametime:".Code()} {totalMinutes.ToNiceTime().ToString()} \n";
                }
                await ReplyAsync("", embed: embedBuilder);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
