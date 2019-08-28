using System;
using System.Collections.Generic;
using System.Text;
using Discord.WebSocket;
using Discord;
using System.Threading.Tasks;
using Misaka.Classes;

namespace Misaka.Services
{
    class LoggerService : Service
    {
        public LoggerService(IServiceProvider provider) : base(provider)
        {
        }

        protected override void Run()
        {
            DiscordClient.Log += async (logMessage) =>
            {
                if (!logMessage.Message.Contains("PRESENCE_UPDATE") && logMessage.Severity != LogSeverity.Debug)
                    ConsoleEx.WriteColoredLine(logMessage.Severity, ConsoleTextFormat.TimeAndText, ConsoleColor.DarkMagenta, logMessage.Source, " ", ConsoleColor.Gray, logMessage.Message);
                await Task.CompletedTask;
            };
        }
    }
}
