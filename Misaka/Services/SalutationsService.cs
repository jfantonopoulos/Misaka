using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using Misaka.Models.MySQL;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Services
{
    public interface ISalutation
    {
        string GuildId
        {
            get;
            set;
        }
        bool OnJoin
        {
            get;
            set;
        }
        string Message
        {
            get;
            set;
        }
    }

    public struct TextSalutation : ISalutation
    {
        public string GuildId { get; set; }
        public string Message { get; set; }
        public bool OnJoin { get; set; }

        public TextSalutation(string guildId, string message, bool onJoin)
        {
            GuildId = guildId;
            Message = message;
            OnJoin = onJoin;
        }
    }

    public struct EmbedSalutation : ISalutation
    {
        public string GuildId { get; set; }
        public string Message { get; set; }
        public bool OnJoin { get; set; }
        public byte[] EmbedColor { get; set; }
        public string ImgUrl { get; set; }

        public EmbedSalutation(string guildId, string message, bool onJoin, byte[] embedColor = null, string imgUrl = null)
        {
            GuildId = guildId;
            Message = message;
            OnJoin = onJoin;
            if (embedColor == null)
                EmbedColor = new byte[3] { 255, 255, 255 };
            else
                EmbedColor = embedColor;
            ImgUrl = imgUrl;
        }
    }

    public class SalutationsService : Service
    {
        private DiscordSocketClient Client;
        private DBContextFactory DBFactory;
        private EmbedService EmbedService;

        public SalutationsService(IServiceProvider provider) : base(provider)
        {
            Client = Provider.GetService<DiscordSocketClient>();
            DBFactory = Provider.GetService<DBContextFactory>();
            EmbedService = Provider.GetService<EmbedService>();
        }

        private async Task ProcessMessage(string guildId, SocketGuildUser guildUser, DiscordSalutationMessage message)
        {
            if (message != null)
            {
                if (!message.IsEmbed)
                {
                    string salutation = message.Message.Replace("{{user-mention}}", guildUser.Mention);
                    await Client.GetGuild(ulong.Parse(message.Id)).DefaultChannel.SendMessageAsync(salutation);
                }
                else
                {
                    var embedBuilder = new EmbedBuilder()
                    {
                        Description = message.Message.Replace("{{user-mention}}", guildUser.Mention),
                        Color = new Color(message.EmbedColor[0], message.EmbedColor[1], message.EmbedColor[2])
                    };
                    if (message.ImgUrl != null)
                        embedBuilder.ImageUrl = message.ImgUrl;
                    await Client.GetGuild(ulong.Parse(message.Id)).DefaultChannel.SendMessageAsync("", embed: embedBuilder.Build());
                }
            }
        }

        override protected Task RunAsync()
        {
            Client.UserJoined += async (guildUser) =>
            {
                string guildId = guildUser.Guild.Id.ToString();
                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    var message = await DBContext.DiscordSalutationMessages.FromSql("SELECT * FROM DiscordSalutationMessages WHERE Id = {0} AND OnJoin = 1 LIMIT 1", guildUser.Guild.Id.ToString()).AsNoTracking().SingleAsync();
                    await ProcessMessage(guildId, guildUser, message);
                }
            };

            Client.UserLeft += async (guildUser) =>
            {
                string guildId = guildUser.Guild.Id.ToString();
                using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
                {
                    var message = await DBContext.DiscordSalutationMessages.FromSql("SELECT * FROM DiscordSalutationMessages WHERE Id = {0} AND OnJoin = 0 LIMIT 1", guildUser.Guild.Id.ToString()).AsNoTracking().SingleAsync();
                    await ProcessMessage(guildId, guildUser, message);
                }
            };

            return Task.CompletedTask;
        }

        public async Task<bool> SalutationExists(string guildId, bool onJoin)
        {
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                var msg = await DBContext.DiscordSalutationMessages.FindAsync(guildId, onJoin);
                if (msg == null)
                    return false;
                else
                    return true;
            }
        }

        public async Task<bool> AddSalutation(TextSalutation salutation)
        {
            if (await SalutationExists(salutation.GuildId, salutation.OnJoin))
                return false;

            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                await DBContext.DiscordSalutationMessages.AddAsync(new DiscordSalutationMessage(salutation.GuildId, salutation.Message, salutation.OnJoin, false, new byte[3] {0, 0, 0}, null));
                await DBContext.SaveChangesAsync();
                return true;
            }
        }

        public async Task<bool> AddSalutation(EmbedSalutation salutation)
        {
            if (await SalutationExists(salutation.GuildId, salutation.OnJoin))
                return false;

            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                await DBContext.DiscordSalutationMessages.AddAsync(new DiscordSalutationMessage(salutation.GuildId, salutation.Message, salutation.OnJoin, true, salutation.EmbedColor, salutation.ImgUrl));
                await DBContext.SaveChangesAsync();
                return true;
            }
        }

        public async Task<bool> RemoveSalutation(string guildId, bool onJoin)
        {
            if (!(await SalutationExists(guildId, onJoin)))
                return false;

            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                var msg = await DBContext.DiscordSalutationMessages.FindAsync(guildId, onJoin);
                DBContext.DiscordSalutationMessages.Remove(msg);
                await DBContext.SaveChangesAsync();
                return true;
            }
        }
    }
}
