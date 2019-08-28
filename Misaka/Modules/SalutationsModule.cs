using Misaka.Classes;
using System;
using System.Collections.Generic;
using System.Text;
using Misaka.Services;
using Discord.Commands;
using System.Threading.Tasks;
using Misaka.Preconditions;

namespace Misaka.Modules
{
    public class SalutationsModule : MisakaModuleBase
    {
        private EmbedService EmbedService;
        private SalutationsService SalutationsService;
        private Config Config;

        public SalutationsModule(EmbedService embedService, SalutationsService salutationsService, Config config, MathService mathService) : base(mathService)
        {
            EmbedService = embedService;
            SalutationsService = salutationsService;
            Config = config;
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("addwelcomemessage"), Summary("Adds a welcome message to the current guild."), Priority(1)]
        public async Task AddWelcomeMessage([Summary("The welcome message displayed to chat, {{user-mention}} will be autoreplaced.")] [Remainder] string message)
        {
            if(await SalutationsService.SalutationExists(Context.Guild.Id.ToString(), true))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("There is already a welcome message for this guild."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await SalutationsService.AddSalutation(new TextSalutation(Context.Guild.Id.ToString(), message, true));
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed("A Welcome message has successfully been created for this guild."));
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("addwelcomemessage"), Summary("Adds a welcome message to the current guild."), Priority(2)]
        public async Task AddWelcomeMessage([Summary("The red value of the color. 0-255")] byte r, [Summary("The green value of the color. 0-255")] byte g, [Summary("The blue value of the color. 0-255")] byte b, [Summary("The url to set the Embed image to.")] string imgUrl, [Remainder] string message)
        {
            if (await SalutationsService.SalutationExists(Context.Guild.Id.ToString(), true))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("There is already a welcome message for this guild."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await SalutationsService.AddSalutation(new EmbedSalutation(Context.Guild.Id.ToString(), message, true, new byte[3] {r, g, b}, imgUrl));
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed("A Welcome message has successfully been created for this guild."));
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("removewelcomemessage"), Summary("Removes the current guild's welcome message if one exists.")]
        public async Task RemoveWelcomeMessage()
        {
            if (!(await SalutationsService.SalutationExists(Context.Guild.Id.ToString(), true)))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("This guild currently does not have a welcome message."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await SalutationsService.RemoveSalutation(Context.Guild.Id.ToString(), true);
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed("The welcome message for this guild was removed successfully."));
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("addgoodbyemessage"), Summary("Adds a goodbye message to the current guild."), Priority(1)]
        public async Task AddGoodbyeMessage([Summary("The goodbye message displayed to chat, {{user-mention}} will be autoreplaced.")] [Remainder] string message)
        {
            if (await SalutationsService.SalutationExists(Context.Guild.Id.ToString(), false))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("There is already a goodbye message for this guild."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await SalutationsService.AddSalutation(new TextSalutation(Context.Guild.Id.ToString(), message, false));
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed("A goodbye message has successfully been created for this guild."));
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("addgoodbyemessage"), Summary("Adds a goodbye message to the current guild."), Priority(2)]
        public async Task AddGoodbyeMessage([Summary("The red value of the color. 0-255")] byte r, [Summary("The green value of the color. 0-255")] byte g, [Summary("The blue value of the color. 0-255")] byte b, [Summary("The url to set the Embed image to.")] string imgUrl, [Remainder] string message)
        {
            if (await SalutationsService.SalutationExists(Context.Guild.Id.ToString(), false))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("There is already a goodbye message for this guild."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await SalutationsService.AddSalutation(new EmbedSalutation(Context.Guild.Id.ToString(), message, false, new byte[3] { r, g, b }, imgUrl));
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed("A goodbye message has successfully been created for this guild."));
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("removegoodbyemessage"), Summary("Removes the current guild's goodbye message if one exists.")]
        public async Task RemoveGoodbyeMessage()
        {
            if (!(await SalutationsService.SalutationExists(Context.Guild.Id.ToString(), false)))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("This guild currently does not have a goodbye message."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await SalutationsService.RemoveSalutation(Context.Guild.Id.ToString(), false);
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed("The goodbye message for this guild was removed successfully."));
        }
    }
}
