using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using Misaka.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Misaka.Services
{
    public class TickService : Service
    {
        private DiscordSocketClient Client;
        private Config Config;

        public TickService(IServiceProvider provider) : base(provider)
        {
        }

        protected override void Run()
        {
            Client = Provider.GetService<DiscordSocketClient>();
            Config = Provider.GetService<Config>();
            TriggerAvatarSwapTimer();
        }

        public void TriggerAvatarSwapTimer()
        {
            Timer swapTimer = null;
            int timerFrequency = Provider.GetService<MathService>().TimeUnitToMilli(TimeUnit.Minutes, Config.AvatarSwapFrequency);
            swapTimer = new Timer(async (e) => {
                string newAvatar = Config.MisakaAvatarUrls[Provider.GetService<MathService>().RandomRange(0, Config.MisakaAvatarUrls.Count - 1)];
                try
                {
                    ImageSharp.Image<ImageSharp.Rgba32> avatar = await Provider.GetService<HttpService>().GetImageBitmap(newAvatar);
                    MemoryStream memStream = new MemoryStream();
                    avatar.Save(memStream);
                    memStream.Position = 0;
                    await Client.CurrentUser.ModifyAsync(x =>
                    {
                        x.Username = "Misaka";
                        x.Avatar = new Discord.Image(memStream);
                    });
                    await Client.SetGameAsync($"on {Client.Guilds.Count.ToString()} servers! 👌");
                    await Client.UpdateServerCount();
                    ConsoleEx.WriteColoredLine(Discord.LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, "The bot's avatar has been swapped.");
                }
                catch(Exception ex)
                {
                    ConsoleEx.WriteColoredLine(Discord.LogSeverity.Warning, ConsoleTextFormat.TimeAndText, $"The bot failed to swap avatars. [{newAvatar}].\n[", ConsoleColor.Red, ex.Message, ConsoleColor.Gray, "]");
                    
                }
                swapTimer.Change(timerFrequency, 0);
                await Task.CompletedTask;
            }, null, timerFrequency, 0);
        }
    }
}
