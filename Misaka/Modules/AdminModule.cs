using Misaka.Classes;
using System;
using System.Collections.Generic;
using System.Text;
using Misaka.Services;
using Discord.Commands;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.Linq;
using Misaka.Extensions;
using Discord;
using Misaka.Preconditions;
using System.Reflection;

namespace Misaka.Modules
{
    public class AdminModule : MisakaModuleBase
    {
        private CommandService CommandService;
        private EmbedService EmbedService;
        private ExecuteService ExecuteService;
        private Config Config;

        public AdminModule(CommandService commandService, MathService mathService, EmbedService embedService, ExecuteService executeService, Config config) : base(mathService)
        {
            CommandService = commandService;
            EmbedService = embedService;
            ExecuteService = executeService;
            Config = config;
        }

        private MethodInfo GetModuleMethod(string name)
        {
            return CommandService.GetType().GetMethods().FirstOrDefault(x => x.Name.ToLower() == name.ToLower() && x.IsGenericMethod);
        }

        [RequireBotPermission(Discord.GuildPermission.KickMembers)]
        [RequireUserPermission(Discord.GuildPermission.KickMembers)]
        [Command("kick"), Summary("Kicks the specified user.")]
        public async Task Kick([Summary("The user's name.")] [Remainder] SocketGuildUser user)
        {
            await user.KickAsync();
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($":outbox_tray: Kicked user {user.Username.Bold()}."));
        }

        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Command("ban"), Summary("Bans the specified user.")]
        public async Task Ban([Summary("The user's name")] SocketGuildUser user)
        {
            if ((await Context.Guild.GetBansAsync()).FirstOrDefault(x => x.User.Id == user.Id) == null)
            {
                await Context.Guild.AddBanAsync(user.Id);
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($":no_entry_sign: Banned user {user.Username.Bold()}."));
            }
            else
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"That user is already banned."), lifeTime: Config.FeedbackMessageLifeTime);
        }

        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Command("unban"), Summary("Unbans the specified user.")]
        public async Task Unban([Summary("The user's name")] ulong id)
        {
            if ((await Context.Guild.GetBansAsync()).FirstOrDefault(x => x.User.Id == id) == null)
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"That user is not banned."), lifeTime: Config.FeedbackMessageLifeTime);
            else
            {
                await Context.Guild.RemoveBanAsync(id);
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"Unbanned user {id.ToString().Bold()}."));
            }
        }

        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.ManageRoles)]
        [Command("addrole"), Summary("Adds the role to the specified user.")]
        public async Task AddRole([Summary("Role mention or name.")] IRole role, [Summary("The user's name.")] SocketGuildUser user)
        {
            if (user.Roles.FirstOrDefault(x => x.Id == role.Id) != null)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"{user.Username.Bold()} already has the role {role.Name.Bold()}."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }
            await user.AddRoleAsync(role);
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"Added the role {role.Name.Bold()} to {user.Username.Bold()}."));
        }

        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.ManageRoles)]
        [Command("removerole"), Summary("Removes the role from the specified user.")]
        public async Task RemoveRole([Summary("Role mention or name.")] IRole role, [Summary("The user's name.")] SocketGuildUser user)
        {
            if (user.Roles.FirstOrDefault(x => x.Id == role.Id) != null)
            {
                await user.RemoveRoleAsync(role);
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"Removed the role {role.Name.Bold()} from {user.Username.Bold()}."));
            }
            else
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"{user.Username.Bold()} already doesn't have the role {role.Name.Bold()}."), lifeTime: Config.FeedbackMessageLifeTime);
        }

        [RequireBotPermission(ChannelPermission.ManageMessages)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        [Command("purgemessages"), Summary("Purges the specified amount of messages from the current channel."), Alias("purge", "deletemessages")]
        public async Task PurgeMessages([Summary("The number of messages to delete.")] int num, [Summary("Whether to only remove the bot's messages.")] bool mine = false)
        {
            var messages = await Context.Channel.GetMessagesAsync(num).Flatten();
            if (mine)
                await Context.Channel.DeleteMessagesAsync(messages.Where(x => x.Author.Id == Context.Client.CurrentUser.Id));
            else
                await Context.Channel.DeleteMessagesAsync(messages);
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($":e_mail: Removed {messages.Count()} messages from this channel."));
        }

        [RequireCooldown(1)]
        [Command("purgelast"), Summary("Purges the last message in the current channel.")]
        public async Task PurgeLast([Summary("Whether to only remove the bot's messages.")] bool mine = false)
        {
            try
            {
                var lastFewMessages = (await Context.Channel.GetMessagesAsync(100).Flatten()).Skip(1);
                if (mine)
                    await lastFewMessages.FirstOrDefault(x => x.Id == Context.Client.CurrentUser.Id).DeleteAsync();
                else
                    await lastFewMessages.FirstOrDefault().DeleteAsync();

                await Context.Message.AddReactionAsync(new Emoji("✅"), Config.FeedbackMessageLifeTime);
            }
            catch(Exception ex)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed(ex.Message));
            }
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("reloadscripts"), Summary("Reloads all the external scripts.")]
        public async Task ReloadScripts()
        {
            ExecuteScriptsResult res = await ExecuteService.LoadScripts();
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($":leftwards_arrow_with_hook: Reloaded scripts, {res.SuccessfulScripts.ToString().Bold()} scripts loaded successfully, {res.FailedScripts.ToString().Bold()} scripts failed."));
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("loadmodule"), Summary("Loads the specified module.")]
        public async Task LoadModule([Summary("The name of the Module to load.")] [Remainder] Type module)
        {
            if (CommandService.Modules.FirstOrDefault(x => x.Name == module.Name.ToString()) != null)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"The specified Module is already loaded."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            Task result = (Task)GetModuleMethod("AddModuleAsync")
                .MakeGenericMethod(module)
                .Invoke(CommandService, new object[] { });
            await result;
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"Successfully loaded [{module.Name.ToString().Bold()}]!"));
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("unloadmodule"), Summary("Unloads the specified module.")]
        public async Task UnloadModule([Summary("The name of the Module to unload.")] [Remainder] Type module)
        {
            if (module == typeof(AdminModule))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"That module cannot be unloaded."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }
            if (CommandService.Modules.FirstOrDefault(x => x.Name == module.Name.ToString()) == null && !ExecuteService.ExternalModuleExists(module.Name))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"The specified Module is not loaded."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            KeyValuePair<Type, ModuleInfo> mdlInfo = ExecuteService.GetExternalModule(module.Name);

            Task result = (Task)GetModuleMethod("RemoveModuleAsync")
                .MakeGenericMethod(mdlInfo.Key ?? module)
                .Invoke(CommandService, new object[] { });
            await result;

            if (ExecuteService.ExternalModuleExists(module.Name))
                ExecuteService.RemoveExternalModule(module);

            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"Successfully unloaded [{module.Name.ToString().Bold()}]!"));
        }
    }
}
