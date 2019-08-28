using AngleSharp;
using Discord;
using Discord.Commands;
using ImageSharp;
using Imgur.API.Models;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Preconditions;
using Misaka.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Modules
{
    [Name("ImageModule")]
    public class ImageModule : MisakaModuleBase
    {
        private HttpService HttpService;
        private ImageService ImageService;
        private EmbedService EmbedService;
        private Config Config;

        public ImageModule(HttpService httpService, ImageService imageService, EmbedService embedService, MathService mathService, Config config) : base(mathService)
        {
            HttpService = httpService;
            ImageService = imageService;
            EmbedService = embedService;
            Config = config;
        }

        private bool IsUriInvalid(string url) => StaticMethods.IsUriInvalid(url);

        [RequireCooldown(5)]
        [Command("randomdog"), Summary("Sends a picture of a random dog.")]
        public async Task RandomDog()
        {
            DateTime startTime = DateTime.Now;
            using (var typingState = await Context.Channel.EnterTypingState(Config.ImageManipulateTypeTime))
            {
                string result = await HttpService.Get("http://shibe.online/api/shibes?count=1&urls=true&httpsUrls=true");
                dynamic obj = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<List<string>>(result));
                EmbedBuilder builder = new EmbedBuilder();
                EmbedService.BuildFeedbackEmbed(builder);
                builder.Description = "";
                builder.Title = "Random Dog";
                builder.WithImageUrl(obj[0]);
                builder.WithFooter(footer =>
                {
                    footer.Text = $"⏰ {"Generated in:"}  {Math.Round((DateTime.Now.Subtract(startTime).TotalMilliseconds)).ToString()}ms";
                });
                await ReplyAsync("", embed: builder.Build());
                //await Context.Channel.SendFileAsync((Image<Rgba32>)await HttpService.GetImageBitmap(obj[0]), ImageFormat.Png, "random_dog.png");
            }
        }

        [RequireCooldown(10)]
        [Command("imagesearch"), Summary("Searches Google for the specified Image."), Alias("imgsearch", "gimage"), Priority(2)]
        public async Task ImageSearch([Summary("The desired image index.")] int index, [Summary("The search query.")] [Remainder] string query)
        {
            DateTime startTime = DateTime.Now;
            using (var typingState = await Context.Channel.EnterTypingState(Config.ImageManipulateTypeTime))
            {
                ImageSearchResult result = await ImageService.SearchImage(query, Math.Min(index, 100));
                EmbedBuilder builder = new EmbedBuilder();
                EmbedService.BuildFeedbackEmbed(builder);
                builder.Description = "";
                builder.Title = $"Image Search: [{query}]";
                builder.WithImageUrl(result.Url);
                builder.WithFooter(footer =>
                {
                    footer.Text = $"⏰ {"Generated in:"}  {Math.Round((DateTime.Now.Subtract(startTime).TotalMilliseconds)).ToString()}ms";
                });
                await ReplyAsync("", embed: builder.Build());
                result.Image.Dispose();
            }
        }

        [RequireCooldown(10)]
        [Command("imagesearch"), Summary("Searches Google for the specified Image."), Alias("imgsearch", "gimage"), Priority(1)]
        public async Task ImageSearch([Summary("The search query.")] [Remainder] string query)
        {
            await ImageSearch(-1, query);
        }

        [RequireCooldown(30)]
        [Command("webshot"), Summary("Takes a screenshot of the specified webpage."), Alias("ws", "screenshot"), Priority(1)]
        public async Task Webshot([Summary("The desired url.")] [Remainder] string url)
        {
            if (IsUriInvalid(url))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"The url you provided appears to be invalid."));
                return;
            }
            try
            {
                DateTime startTime = DateTime.Now;
                using (var typingState = await Context.Channel.EnterTypingState(Config.ImageManipulateTypeTime))
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    byte[] imgArray = await client.GetByteArrayAsync($"http://api.screenshotlayer.com/api/capture?access_key=140bdd16496a9e26be0913d668472a0c&url={url}&viewport=1440x900&width=500&height=500");
                    MemoryStream memStream = new MemoryStream(imgArray);
                    memStream.Seek(0, SeekOrigin.Begin);
                    Image<Rgba32> webshot = ImageSharp.Image.Load(memStream);
                    IImage uploadedImage = await ImageService.UploadImage(webshot);
                    webshot.Dispose();
                    memStream.Dispose();
                    EmbedBuilder builder = new EmbedBuilder();
                    EmbedService.BuildFeedbackEmbed(builder);
                    builder.Description = "";
                    builder.Title = $"Webshot of [{url}]";
                    builder.WithImageUrl(uploadedImage.Link);
                    builder.WithFooter(footer =>
                    {
                        footer.Text = $"⏰ {"Generated in:"}  {Math.Round((DateTime.Now.Subtract(startTime).TotalMilliseconds)).ToString()}ms";
                    });
                    await ReplyAsync("", embed: builder.Build());
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"Unable to take webshot, API limit may have been reached.\n{ex.Message}"));
            }
        }
    }
}
