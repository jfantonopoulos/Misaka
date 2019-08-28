using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Preconditions;
using Misaka.Services;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Misaka.Modules
{

    public class StatsModule : MisakaModuleBase
    {
        private EmbedService EmbedService;
        private TrackingService TrackingService;
        private MathService MathService;
        private Config Config;
        private ImageService ImageService;
        private DBContextFactory DBFactory;

        public StatsModule(EmbedService embedService, Config config, TrackingService trackingService, MathService mathService, ImageService imageService, DBContextFactory dbFactory) : base(mathService)
        {
            EmbedService = embedService;
            Config = config;
            TrackingService = trackingService;
            MathService = mathService;
            ImageService = imageService;
            DBFactory = dbFactory;
        }

        [Command("invite"), Summary("Sends an invite link for the bot to chat.")]
        public async Task Invite()
        {
            string inviteLink = "https://discordapp.com/api/oauth2/authorize?client_id=314821049462030336&scope=bot&permissions=0";
            Embed inviteEmbed = EmbedService.MakeSuccessFeedbackEmbed($"You may invite me to your server using the link below:\n:paperclip: {inviteLink}");
            await ReplyAsync("", embed: inviteEmbed);
        }

        [Command("uptime"), Summary("Gets the bot's uptime.")]
        public async Task UpTime()
        {
            TimeSpan diffTime = DateTime.Now.Subtract(Process.GetCurrentProcess().StartTime);
            await ReplyAsync("", embed: EmbedService.MakeFeedbackEmbed($"Misaka has been up for {diffTime.ToNiceTime()}."));
        }

        [RequireCooldown(5)]
        [Command("ping"), Summary("Pings the Discord servers and returns the time it took.")]
        public async Task Ping([Summary("The amount of times to ping.")] int amt = 1)
        {
            try
            {
                using (Context.Channel.EnterTypingState())
                {
                    string args = "discordapp.com";
                    if (StaticMethods.GetOS() == "Linux")
                        args = "-c 4 discordapp.com";

                    var pInfo = new ProcessStartInfo()
                    {
                        FileName = "ping",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        Arguments = args
                    };
                    Process PingProcess = new Process()
                    {
                        StartInfo = pInfo
                    };
                    PingProcess.Start();
                    string strOut = await PingProcess.StandardOutput.ReadToEndAsync();
                    Regex timeRegex = new Regex(@"time=[0-9]+(.|[0-9]){5}");
                    var times = timeRegex.Matches(strOut);
                    string feedbackStr = "\n";
                    for (int i = 0; i < times.Count; i++)
                    {
                        feedbackStr += $":clock{i + 1}: {times[i].Value}\n";
                    }
                    await ReplyAsync("", embed: EmbedService.MakeFeedbackEmbed($"{feedbackStr}"));
                    PingProcess.Dispose();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            } 
        }

        [Command("latency"), Summary("Pings the bot and sends time in milliseconds.")]
        public async Task Latency()
        {
            int latency = Context.Client.Latency;
            IUserMessage msg = await ReplyAsync("", embed: EmbedService.MakeFeedbackEmbed($" :incoming_envelope: Heartbeat Latency: {latency.ToString()}ms"));
        }

        [RequireCooldown(5), RequireOS("Linux")]
        [Command("stats"), Summary("Returns the system's stats.")]
        public async Task Stats()
        {
            var pInfo = new ProcessStartInfo()
            {
                FileName = "bash",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Arguments = $"{Directory.GetCurrentDirectory()}/BashScripts/ServerStats.sh"
            };
            Process bashProcess = new Process()
            {
                StartInfo = pInfo
            };
            bashProcess.Start();
            string strOut = await bashProcess.StandardOutput.ReadToEndAsync();
            bashProcess.WaitForExit();
            var osName = "Windows ";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                osName = "Linux ";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                osName = "OSX ";

            osName = $"{osName}{Enum.GetName(typeof(Architecture), RuntimeInformation.OSArchitecture)}";
            var libVersion = typeof(DiscordSocketClient).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            string embedMessage = $"OS: {osName}\nLibrary Version: Discord.Net - {libVersion}\n{strOut}";
            Regex titleRegex = new Regex(@"[A-Za-z].+\:");
            foreach(Match match in titleRegex.Matches(embedMessage))
                embedMessage = embedMessage.Replace(match.Value, $"{match.Value.Bold()}");
            using(BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            using (MySqlConnection conn = new MySqlConnection(DBContext.ConnectionString))
            {
                MySqlCommand mySQLCommand = new MySqlCommand(File.ReadAllText("SQLScripts/dbsize.txt"), conn);
                mySQLCommand.CommandType = System.Data.CommandType.Text;
                double totalSize = 0;

                await conn.OpenAsync();
                using (var rowReader = await mySQLCommand.ExecuteReaderAsync())
                {
                    while (await rowReader.ReadAsync())
                        totalSize += double.Parse(rowReader[4].ToString());
                }
                conn.Close();
                embedMessage += $"**Database Size**: {totalSize}MB";
            }
            await ReplyAsync("", embed: EmbedService.MakeFeedbackEmbed(embedMessage));
            bashProcess.Dispose();
        }

        [RequireCooldown(20), RequireOS("Linux")]
        [Command("speedtest"), Summary("Runs a speedtest using speedtest-cli.")]
        public async Task Speedtest()
        {
            IUserMessage holdMessage = await ReplyAsync("", embed: EmbedService.MakeFeedbackEmbed(":satellite: Conducting speedtest, please hold..."));
            bool hasFailed = false;
            using (await Context.Channel.EnterTypingState(30))
            {
                Process process = new Process();
                process.StartInfo.FileName = "speedtest-cli";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                EmbedBuilder embed = new EmbedBuilder();

                process.OutputDataReceived += (sendingProcess, outLine) =>
                {
                    if (outLine == null || string.IsNullOrEmpty(outLine.Data))
                        return;

                    string line = outLine.Data;
                    if (line.StartsWith("Hosted by"))
                    {
                        embed.AddField(x =>
                        {
                            x.Name = ":house: Closest Server";
                            x.Value = line.Replace("Hosted by", "");
                        });
                    }
                    else if (line.StartsWith("Download:"))
                    {
                        embed.AddField(x =>
                        {
                            x.Name = ":arrow_down: Download";
                            x.Value = line.Replace("Download: ", "");
                        });
                    }
                    else if (line.StartsWith("Upload:"))
                    {
                        embed.AddField(x =>
                        {
                            x.Name = ":arrow_up: Upload";
                            x.Value = line.Replace("Upload: ", "");
                        });
                    }
                };

                process.ErrorDataReceived += async (sendingProcess, outLine) =>
                {
                    if (outLine == null || string.IsNullOrEmpty(outLine.Data))
                        return;

                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"Failed to conduct speedtest: {outLine.Data}"), lifeTime: Config.FeedbackMessageLifeTime);
                    ConsoleEx.WriteColoredLine(LogSeverity.Warning, ConsoleTextFormat.TimeAndText, outLine.ToString());
                    hasFailed = true;
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (hasFailed)
                    return;

                EmbedService.BuildSuccessEmbed(embed);
                embed.Description = "";
                await ReplyAsync(embed);
                if (holdMessage != null)
                    await holdMessage.DeleteAsync();
            }
        }
    }
}
