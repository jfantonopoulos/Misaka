using Discord;
using Discord.Commands;
using Misaka.Classes;
using Misaka.Services;
using Misaka.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Modules
{
    [Name("MashapeModule")]
    public class MashapeModule : MisakaModuleBase
    {
        private EmbedService EmbedService;
        private HttpService HttpService;
        private MathService MathService;
        private Config Config;

        public MashapeModule(EmbedService embedService, HttpService httpService, MathService mathService, Config config) : base(mathService)
        {
            EmbedService = embedService;
            HttpService = httpService;
            MathService = mathService;
            Config = config;
        }

        [Command("yodaspeak"), Summary("Translates the specified text to yoda speak.")]
        public async Task YodaSpeak([Summary("The text to translate.")] [Remainder] string text)
        {
            string result = await HttpService.Get($"https://yoda.p.mashape.com/yoda?sentence={text}", new Dictionary<string, string> { { "X-Mashape-Key", Config.MashapeKey } });
            await ReplyAsync("", embed: EmbedService.MakeFeedbackEmbed(result));
        }

        [Command("urbandefine"), Summary("Gets the Urban Dictionary defintion for the specified word.")]
        public async Task UrbanDefine([Summary("The word to define.")] [Remainder] string word)
        {
            string result = await HttpService.Get($"https://mashape-community-urban-dictionary.p.mashape.com/define?term={word}", new Dictionary<string, string> { { "X-Mashape-Key", Config.MashapeKey } });
            Dictionary<string, dynamic> json = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(result));
            if (json["result_type"].ToString() != "no_results")
            {
                dynamic randWord = json["list"][MathService.RandomRange(0, json["list"].Count)];
                EmbedBuilder embedBuilder = new EmbedBuilder();
                EmbedService.BuildFeedbackEmbed(embedBuilder);
                embedBuilder.Title = $"Urban Dictionary Definition";
                embedBuilder.ThumbnailUrl = "http://www.userlogos.org/files/logos/pln_xp/ud.png";
                embedBuilder.Description = "";

                embedBuilder.AddField(field =>
                {
                    field.Name = $":pencil2: {word}";
                    field.Value = randWord["definition"];
                });

                embedBuilder.AddField(field =>
                {
                    field.Name = ":paperclip: Example";
                    field.Value = randWord["example"];
                });

                embedBuilder.WithFooter(footer =>
                {
                    footer.Text = $"Written By: {randWord["author"]}";
                });

                await ReplyAsync("", embed: embedBuilder.Build());
            }
            else
            {
                Embed failEmbed = EmbedService.MakeFailFeedbackEmbed($"{word.ToString().Bold()} could not be found.");
                await ReplyAsync("", embed: failEmbed, lifeTime: Config.FeedbackMessageLifeTime);
            }
        }
    }
}
