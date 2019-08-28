using Discord;
using Discord.Commands;
using Misaka.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Misaka.Classes
{
    public class MisakaModuleBase : ModuleBase<SocketCommandContext>
    {
        private MathService MathService;
        private EmbedService EmbedService;

        public MisakaModuleBase(MathService mathService)
        {
            MathService = mathService;
        }

        public MisakaModuleBase(MathService mathService, EmbedService embedService)
        {
            MathService = mathService;
            EmbedService = embedService;
        }

        public virtual async Task<IUserMessage> ReplyAsync(EmbedBuilder embed)
        {
            return await Context.Channel.SendMessageAsync("", embed: embed);
        }

        public virtual async Task<IUserMessage> ReplyAsync(string content, bool isTTS = false, EmbedBuilder embedBuilder = null, int lifeTime = 0)
        {
            return await ReplyAsync(content, isTTS, embedBuilder.Build(), lifeTime);
        }

        public virtual async Task<IUserMessage> ReplyAsync(string content, bool isTTS = false, Embed embed = null, int lifeTime = 0)
        {
            IUserMessage msg = await Context.Channel.SendMessageAsync(content, isTTS, embed);
            if (lifeTime > 0)
            {
                Timer msgTimer = null;
                msgTimer = new Timer((e) => {
                    if (msg != null)
                        msg.DeleteAsync();
                    msgTimer.Dispose();
                    msgTimer = null;
                }, null, MathService.TimeUnitToMilli(TimeUnit.Seconds, lifeTime), 0);
            }
            return msg;
        }
    }
}
