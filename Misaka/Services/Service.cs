using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Services
{
    abstract public class Service
    {
        protected DiscordSocketClient DiscordClient;
        protected IServiceProvider Provider;

        public Service(DiscordSocketClient client, IServiceProvider provider = null)
        {
            SimpleStopWatch watch = new SimpleStopWatch();
            try
            {
                if (provider != null)
                    this.Provider = provider;

                this.DiscordClient = client;
                Run();

                Task.Run(async() =>
                {
                    await RunAsync();
                    TimeSpan timePassed = watch.Stop();
                    ConsoleEx.WriteColoredLine(Discord.LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, "Finished loading module ", ConsoleColor.Blue, this.GetType().Name, $" in $[[White]]${timePassed.Milliseconds}$[[Gray]]$ms!");
                    
                });
            }
            catch(Exception ex)
            {
                ConsoleEx.WriteColoredLine(Discord.LogSeverity.Critical, ConsoleTextFormat.TimeAndText, "Something has gone terribly wrong! Exception below:\n", ConsoleColor.Red, ex.Message, ConsoleColor.Gray);
            }
        }

        public Service(IServiceProvider provider) : this(provider.GetService<DiscordSocketClient>(), provider)
        {
            /*Service(provider.GetService<DiscordSocketClient>());
            this.DiscordClient = provider.GetService<DiscordSocketClient>();
            this.Provider = provider;
            Run();
            Task.Factory.StartNew(async () =>
            {
                await RunAsync();
            });*/
        }

        virtual protected void Run()
        {
        }

        virtual protected async Task RunAsync()
        {
            await Task.CompletedTask;
        }
    }
}
