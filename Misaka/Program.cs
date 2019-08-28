using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Misaka
{
    using Static = StaticMethods;

    internal class Program
    {
        private static void Main(string[] args) => new Program().RunAsync().GetAwaiter().GetResult();

        //i need to figure out why this is null in the provider when the reddit service is going
        public static DiscordSocketClient Client;
        private CommandHandler Handler;
        private Config Config;
        private DiscordSocketConfig SocketConfig;
        private bool IsBooted = false;

        private async Task RunAsync()
        {
            //Do not start up the bot until input is received.
            ConsoleEx.AwaitInput(ConsoleColor.Green, $"\nYou have begun booting $[[Blue]]$Misaka$[[Gray]]$!\nCurrently running: Discord.NET {Static.GetLibraryVersion()} on {Static.GetOS()}{Static.GetArchitecture()}\n", ConsoleColor.Cyan, "You may begin the bot by starting spacebar.");
            SocketConfig = new DiscordSocketConfig { LogLevel = LogSeverity.Debug };
            Client = new DiscordSocketClient(SocketConfig);

            Config = new Config();
            await Config.ConfigAsync();

            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.StartAsync();

            Client.Ready += async () =>
            {
                if (IsBooted)
                    return;

                IsBooted = true;

                var serviceProvider = ConfigureServices();
                DiscordExtensions.HttpService = serviceProvider.GetService<HttpService>();
                DiscordExtensions.MathService = serviceProvider.GetService<MathService>();
                DiscordExtensions.Config = Config;
                DiscordExtensions.ImageService = serviceProvider.GetService<ImageService>();
                DiscordExtensions.Client = Client;

                Handler = new CommandHandler(serviceProvider);
                await Handler.ConfigureAsync();

                await Client.SetGameAsync($"on {Client.Guilds.Count.ToString()} servers! 👌");
                await Client.UpdateServerCount();
                try
                {
                    Static.FireAndForget(async () => await serviceProvider.GetService<TrackingService>().InitializeCollection());
                }
                catch (Exception ex)
                {
                    ConsoleEx.WriteColoredLine(LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"$[[Yellow]]$An exception occured while loading guilds.\n$[[Red]]${ex.Message}");
                }

                ConsoleEx.WriteColoredLine(LogSeverity.Info, ConsoleTextFormat.TimeAndText, "$[[Green]]$Client is ready!"); ;
            };

            await Task.Delay(-1);
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection()
                .AddSingleton(Client)
                .AddSingleton<LoggerService>()
                .AddSingleton(new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false, DefaultRunMode = RunMode.Async, ThrowOnError = false }))
                .AddSingleton<MathService>()
                .AddSingleton(Config)
                .AddSingleton<DBContextFactory>()
                .AddSingleton<ImageService>()
                .AddSingleton<HttpService>()
                .AddSingleton<TickService>()
                .AddSingleton<CooldownService>()
                .AddSingleton<EmbedService>()
                .AddSingleton<TrackingService>()
                .AddSingleton<AudioService>()
                .AddSingleton<ReminderService>()
                .AddSingleton<RedditService>()
                .AddSingleton<CNNService>()
                .AddSingleton<SalutationsService>()
                .AddSingleton<ExecuteService>()
                .AddSingleton<PrefixService>();
            IServiceProvider provider = null;
            provider = services.BuildServiceProvider();
            //We want this injected first.
            provider.GetService<DiscordSocketClient>();
            string connString = $"server={Config.MySQLServerAddress};database={Config.MySQLDatabase};uid={Config.MySQLUsername};pwd={Config.MySQLPassword};";
            provider.GetService<DBContextFactory>().ConnectionString = connString;
            provider.GetService<RedditService>();
            // Instantiate and autowire these services.

            foreach (ServiceDescriptor service in services.ToList())
            {
                var nsWhitelist = new List<string> { "Discord", "Misaka" };
                var serviceType = service.ServiceType;
                string nsBase = serviceType.Namespace.Split('.').FirstOrDefault();
                
                if (serviceType.GetGenericArguments().Count() == 0 && nsWhitelist.Contains(nsBase))
                {
                    provider.GetService(serviceType);
                    ConsoleEx.WriteColoredLine($"Autowiring $[[DarkCyan]]${serviceType}$[[Gray]]$.");
                }
            }
            
            /*provider.GetService<MathService>();
            provider.GetService<HttpService>();
            provider.GetService<Config>();
            provider.GetService<TickService>();
            provider.GetService<CooldownService>();
            provider.GetService<EmbedService>();
            provider.GetService<DBContextFactory>();*/
            
            //provider.GetService<BotDBContext>().ConnectionString = connString;
            //provider.GetService<BotDBContext>().Database.EnsureCreated();
            /*provider.GetService<TrackingService>();
            provider.GetService<AudioService>();
            provider.GetService<ReminderService>();
            provider.GetService<RedditService>();
            provider.GetService<CNNService>();
            provider.GetService<SalutationsService>();
            provider.GetService<PrefixService>();*/
            return provider;
        }
    }
}