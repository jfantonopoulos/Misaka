using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using Misaka.Models.MySQL;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Misaka.Services
{
    public class CNNService : Service
    {
        private Config Config;
        private HttpService HttpService;
        private Timer CheckTimer;
        public List<SocketTextChannel> TextChannnels;
        private List<string> PostedArticles;
        private DBContextFactory Factory;

        public CNNService(IServiceProvider provider) : base(provider)
        {
            TextChannnels = new List<SocketTextChannel>();
            Config = provider.GetService<Config>();
            HttpService = provider.GetService<HttpService>();
            Factory = provider.GetService<DBContextFactory>();
            CheckTimer = null;
            PostedArticles = new List<string>();
        }

        protected override void Run()
        {
            bool firstRun = true; //So articles don't spam when I restart the bot.
            bool readyFired = false;
            DiscordSocketClient client = Provider.GetService<DiscordSocketClient>();
            client.Ready += () =>
            {
                if (readyFired)
                    return Task.CompletedTask;

                readyFired = true;
                using(SubDBContext DBContext = Factory.Create<SubDBContext>())
                {
                    var subscribers = DBContext.CNNSubscribers.FromSql("SELECT * FROM CNNSubscribers");
                    foreach(CNNSubscriber sub in subscribers)
                    {
                        SocketTextChannel textChannel = client.GetChannel(ulong.Parse(sub.Id)) as SocketTextChannel;
                        if (!TextChannnels.Contains(textChannel))
                            TextChannnels.Add(textChannel);
                        ConsoleEx.WriteColoredLine(Discord.LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"Attached $[[DarkMagenta]]$CNNService$[[Gray]]$ to text channel $[[White]]${textChannel.Name}.");
                    }
                }

                StaticMethods.FireAndForget(() =>
                {
                    ConsoleEx.WriteColoredLine(Discord.LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Attached CNNService to $[[Green]]${TextChannnels.Count}$[[Gray]]$ channels.");
                    int checkInterval = Provider.GetService<MathService>().TimeUnitToMilli(TimeUnit.Minutes, Config.CNNCheckInterval);
                    CheckTimer = new Timer(async (e) =>
                    {
                        try
                        {
                            string result = await HttpService.Get("http://www.cnn.com/");
                            Regex articlesRegex = new Regex("siblings:(.*?)(?=                     ,)");
                            Match match = articlesRegex.Match(result);
                            string trimmedMatch = match.Value.TrimEnd(',').Replace("siblings:         ", "");
                            Dictionary<string, dynamic> json = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(trimmedMatch));
                            string articleTitle = json["articleList"][0]["headline"].ToString();
                            string articleUrl = json["articleList"][0]["uri"].ToString();
                            if (PostedArticles.FirstOrDefault(x => x == articleUrl) == null)
                            {
                                PostedArticles.Add(articleUrl);
                                if (!firstRun)
                                    foreach (SocketTextChannel textChannel in TextChannnels)
                                        await textChannel.SendMessageAsync($"http://www.cnn.com{articleUrl}");
                            }
                            
                            firstRun = false;
                            await Task.CompletedTask;
                        }
                        catch (Exception ex)
                        {
                            ConsoleEx.WriteColoredLine($"$[[DarkCyan]]$CNNService$[[Gray]]$ has made an exception\n$[[Red]]${ex.Message}");
                            Console.WriteLine(ex.Message);
                        }
                        CheckTimer.Change(checkInterval, 0);
                    }, null, checkInterval, 0);
                });
                return Task.CompletedTask;
            };
        }
    }
}
