using Misaka.Classes;
using Misaka.Services;
using System.Threading.Tasks;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Newtonsoft.Json;
using Misaka.Preconditions;
using Misaka.Extensions;

namespace Misaka.Modules
{
    public class FunModule : MisakaModuleBase
    {
        private EmbedService EmbedService;
        private HttpService HttpService;
        private Config Config;

        public FunModule(MathService mathService, EmbedService embedService, HttpService httpService, Config config) : base(mathService, embedService)
        {
            EmbedService = embedService;
            HttpService = httpService;
            Config = config;
        }

        [Command("text2emoji"), Summary("Translates all the characters in your query to emojis.")]
        public async Task TextToEmoji([Summary("Your desired query.")] [Remainder] string query)
        {
            string builder = "";
            char[] charArray = query.ToCharArray();
            for(int i = 0; i < charArray.Length; i++)
            {
                bool nullOrWhitespace = string.IsNullOrWhiteSpace(charArray[i].ToString());
                if (!Char.IsLetter(charArray[i]) && !nullOrWhitespace)
                    continue;

                if (string.IsNullOrWhiteSpace(charArray[i].ToString()))
                {
                    builder += "    ";
                    continue;
                } 

                builder += $":regional_indicator_{charArray[i]}: ";
            }
            string finalBuild = builder.Substring(0, builder.Length - 1);
            await ReplyAsync(finalBuild, embed: null);
        }

        [Command("review"), Summary("Reviews the toxicity and other aspects of the last message.")]
        public async Task Toxicity()
        {
            string content = (await Context.Channel.GetMessagesAsync(10).Flatten()).ToList()[1].Content;
            try
            {
                string resp = await HttpService.Post($"https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key={Config.PerspectiveApiKey}", "{comment: {text: \"" + content + "\"}, languages: [\"en\"], requestedAttributes: {TOXICITY:{}, INCOHERENT:{}, OBSCENE:{}, SPAM:{}, INFLAMMATORY:{}}}");
                Dictionary<string, dynamic> json = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(resp));
                EmbedBuilder embedBuilder = new EmbedBuilder();
                embedBuilder.Title = "Google Perspective API";
                EmbedService.BuildSuccessEmbed(embedBuilder);
                embedBuilder.Description = "";
                embedBuilder.Description += $"Reviewing Message [{content.Code()}]";
                embedBuilder.AddField(x =>
                {
                    x.Name = ":skull_crossbones: Toxic";
                    x.Value = ((float)json["attributeScores"]["TOXICITY"]["summaryScore"]["value"] * 100).ToString() + "%";
                    x.IsInline = true;
                });
                embedBuilder.AddField(x =>
                {
                    x.Name = ":tropical_drink: Incoherent";
                    x.Value = ((float)json["attributeScores"]["INCOHERENT"]["summaryScore"]["value"] * 100).ToString() + "%";
                    x.IsInline = true;
                });
                embedBuilder.AddField(x =>
                {
                    x.Name = ":warning: Obscene";
                    x.Value = ((float)json["attributeScores"]["OBSCENE"]["summaryScore"]["value"] * 100).ToString() + "%";
                });
                embedBuilder.AddField(x =>
                {
                    x.Name = ":love_letter: Spam";
                    x.Value = ((float)json["attributeScores"]["SPAM"]["summaryScore"]["value"] * 100).ToString() + "%";
                    x.IsInline = true;
                });
                embedBuilder.AddField(x =>
                {
                    x.Name = ":fire: Inflammatory";
                    x.Value = ((float)json["attributeScores"]["INFLAMMATORY"]["summaryScore"]["value"] * 100).ToString() + "%";
                    x.IsInline = true;
                });
                await ReplyAsync("", embed: embedBuilder);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        [RequireCooldown(5)]
        [Command("inspire"), Summary("Generates an inspirational image.")]
        public async Task Inspire()
        {
            string resp = await HttpService.Get("http://inspirobot.me/api?generate=true");
            await ReplyAsync(resp, embed: null);
        }
    }
}
