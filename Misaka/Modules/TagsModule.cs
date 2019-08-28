using Discord;
using Discord.Commands;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Models.MySQL;
using Misaka.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Modules
{
    public class TagsModule : ModuleBase<SocketCommandContext>
    {
        private DBContextFactory DBFactory;
        private EmbedService EmbedService;

        public TagsModule(DBContextFactory dbFactory, EmbedService embedService)
        {
            DBFactory = dbFactory;
            EmbedService = embedService;
        }

        [Command("maketag"), Summary("Attaches a tag to the current channel.")]
        public async Task MakeTag([Summary("The name of the Tag.")] string name, [Summary("The content of the tag.")] [Remainder] string content)
        {
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                var tagExists = DBContext.ChannelTags.Where(x => x.ChannelId == Context.Channel.Id.ToString()).FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
                if (tagExists == null)
                {
                    ChannelTag channelTag = new ChannelTag(name, content, Context.Channel.Id.ToString(), Context.User.Id.ToString(), DateTime.Now);
                    await DBContext.AddAsync(channelTag);
                    await DBContext.SaveChangesAsync();
                    await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"You have successfully attached the tag {name} to this channel."));
                }
                else
                {
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("A tag already exists in this channel with that name."));
                }
            }
        }

        [Command("tag"), Summary("Attaches a tag to the current channel.")]
        public async Task Tag([Summary("Retrieves the specified tag.")] string name)
        {
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                ChannelTag selectedTag = DBContext.ChannelTags.FirstOrDefault(x => x.ChannelId == Context.Channel.Id.ToString() && x.Name.ToLower() == name.ToLower());
                if (selectedTag != null)
                {
                    DiscordUser user = DBContext.Users.FirstOrDefault(x => x.Id.ToString() == selectedTag.OwnerId);
                    if (user == null)
                    {
                        await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("You are not the owner of that!"));
                        return;

                    }
                    EmbedBuilder embedBuilder = new EmbedBuilder();
                    embedBuilder.WithAuthor(x =>
                    {
                        x.Name = user.Username;
                        x.IconUrl = user.AvatarUrl;
                    });
                    embedBuilder.WithTimestamp(selectedTag.CreatedBy);
                    EmbedService.BuildFeedbackEmbed(embedBuilder);
                    embedBuilder.Description = "";
                    embedBuilder.AddField(x =>
                    {
                        x.Name = name;
                        x.Value = $":notepad_spiral:{selectedTag.Content}";
                    });
                    await ReplyAsync("", embed: embedBuilder.Build());
                }
                else
                {
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("There's no tag on this channel with that name."));
                }
            }
        }

        [Command("tags"), Summary("Gets all the tags on the current channel.")]
        public async Task Tags()
        {
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                List<ChannelTag> selectedTags = DBContext.ChannelTags.Where(x => x.ChannelId == Context.Channel.Id.ToString()).ToList();
                if (selectedTags.Count > 0)
                {
                    EmbedBuilder embedBuilder = new EmbedBuilder();
                    embedBuilder.WithFooter(x => x.Text = $"{selectedTags.Count.ToString()} total tags.");
                    EmbedService.BuildFeedbackEmbed(embedBuilder);
                    string builtString = "";
                    foreach (ChannelTag tag in selectedTags)
                    {
                        builtString += $"{tag.Name}, ";
                    }
                    builtString = builtString.Substring(0, builtString.Length - 2);
                    embedBuilder.Description = $":paperclip: {builtString}";
                    await ReplyAsync("", embed: embedBuilder.Build());
                }
                else
                {
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("There's no tags on this channel."));
                }
            }
        }

        [Command("deletetag"), Summary("Deletes the specified tag.")]
        public async Task DeleteTag([Summary("The tag name.")] string name)
        {
            using (BotDBContext DBContext = DBFactory.Create<BotDBContext>())
            {
                ChannelTag selectedTag = DBContext.ChannelTags.Where(x => x.ChannelId == Context.Channel.Id.ToString() && x.Name.ToLower() == name.ToLower() && x.OwnerId == Context.User.Id.ToString()).FirstOrDefault();
                if (selectedTag != null)
                {
                    DBContext.ChannelTags.Remove(selectedTag);
                    await DBContext.SaveChangesAsync();
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"The tag {name.Bold()} has been deleted."));
                }
                else
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The specified tag either doesn't exist or you don't own it."));
            }
        }
    }
}
