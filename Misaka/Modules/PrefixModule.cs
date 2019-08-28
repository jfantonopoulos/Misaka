using Misaka.Classes;
using Misaka.Services;
using System.Threading.Tasks;
using Discord.Commands;
using Misaka.Preconditions;
using Misaka.Models.MySQL;
using System.Collections.Generic;
using Discord;
using System;
using Misaka.Extensions;

namespace Misaka.Modules
{
    public class PrefixModule : MisakaModuleBase
    {
        private PrefixService PrefixService;
        private EmbedService EmbedService;
        private Config Config;

        public PrefixModule(PrefixService prefixService, EmbedService embedService, Config config, MathService mathService) : base(mathService)
        {
            PrefixService = prefixService;
            EmbedService = embedService;
            Config = config;
        }

        [RequireCooldown(15)]
        [Command("addprefix"), Summary("Adds the specified prefix to the current guild.")]
        public async Task AddPrefix([Summary("The prefix to add to the guild.")] string prefix)
        {
            if (prefix.Contains(" "))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("Your prefix cannot have spaces."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }
            PrefixResponseData resp = await PrefixService.TryAddPrefix(Context.Guild, Context.User, prefix);
            if (resp.WasSuccess)
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed(resp.Message));
            else
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed(resp.Message), lifeTime: Config.FeedbackMessageLifeTime);
        }

        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireCooldown(15)]
        [Command("removeprefix"), Summary("Adds the specified prefix to the current guild.")]
        public async Task RemovePrefix([Summary("The prefix to add to the guild.")] string prefix)
        {
            PrefixResponseData resp = await PrefixService.TryRemovePrefix(Context.Guild, Context.User, prefix);
            if (resp.WasSuccess)
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed(resp.Message));
            else
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed(resp.Message), lifeTime: Config.FeedbackMessageLifeTime);
        }

        [RequireCooldown(15)]
        [Command("prefixes"), Summary("Lists all the custom prefixes this guild is using.")]
        public async Task Prefixes()
        {
            DateTime startTime = DateTime.Now;
            List<DiscordCustomPrefix> prefixList = await PrefixService.GetGuildPrefixes(Context.Guild);
            if (prefixList.Count == 0)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The current guild has no custom prefixes."), lifeTime: Config.FeedbackMessageLifeTime);
            }
            else
            {
                EmbedBuilder embedBuilder = new EmbedBuilder();
                EmbedService.BuildSuccessEmbed(embedBuilder);
                embedBuilder.Title = $"This guild has {prefixList.Count.ToString()} custom prefixes.";
                embedBuilder.Description += " ";
                foreach(DiscordCustomPrefix prefix in prefixList)
                {
                    embedBuilder.Description += $"{{  {prefix.Prefix.Bold()}  }}, ";
                }
                embedBuilder.Description = embedBuilder.Description.Substring(0, embedBuilder.Description.Length - 2);
                embedBuilder.WithFooter(footer =>
                {
                    footer.Text = $"⏰ {"Generated in:"}  {Math.Round((DateTime.Now.Subtract(startTime).TotalMilliseconds)).ToString()}ms";
                });
                await ReplyAsync("", embed: embedBuilder.Build());
            }   
        }

        [RequireBotPermission(GuildPermission.Administrator)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireCooldown(15)]
        [Command("clearprefixes"), Summary("Clears all the custom prefixes from the current guild.")]
        public async Task ClearPrefixes()
        {
            List<DiscordCustomPrefix> prefixList = await PrefixService.GetGuildPrefixes(Context.Guild);
            if (prefixList.Count == 0)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The current guild has no custom prefixes."), lifeTime: Config.FeedbackMessageLifeTime);
            }
            else
            {
                PrefixResponseData resp = await PrefixService.ClearGuildPrefixes(Context.Guild);
                if (resp.WasSuccess)
                    await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed(resp.Message));
                else
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed(resp.Message), lifeTime: Config.FeedbackMessageLifeTime);
            }
        }
    }
}
