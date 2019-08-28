using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ImageSharp;
using Imgur.API.Models;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Preconditions;
using Misaka.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Misaka.Services.HttpService;

namespace Misaka.Modules
{
    using StandardImageFormat = System.Drawing.Imaging.ImageFormat;

    public class MagickModule : ModuleBase<SocketCommandContext>
    {
        private HttpService HttpService;
        private ImageService ImageService;
        private EmbedService EmbedService;
        private Config Config;
        private string MagickServerUrl = "http://ec2-54-191-18-215.us-west-2.compute.amazonaws.com:8080";

        public MagickModule(HttpService httpService, ImageService imageService, EmbedService embedService, Config config)
        {
            HttpService = httpService;
            ImageService = imageService;
            EmbedService = embedService;
            Config = config;
        }

        private bool IsUriInvalid(string url) => StaticMethods.IsUriInvalid(url);

        public async Task<bool> RequestImageAPI(string apiEndpoint, Dictionary<string, string> parameters, bool getUserAvatar)
        {
            DateTime startTime = DateTime.Now;
            if ((!parameters.ContainsKey("Url") || (parameters.ContainsKey("Url") && parameters["Url"] == null) && !getUserAvatar))
            {
                string targetUrl = null;
                var messages = await Context.Channel.GetMessagesAsync(50).Flatten();
                foreach(var msg in messages)
                {
                    if (msg.Embeds.Count > 0 && msg.Embeds.FirstOrDefault().Image.HasValue)
                    {
                        targetUrl = msg.Embeds.FirstOrDefault().Image?.Url;
                        break;
                    }
                    if (msg.Attachments.Count > 0 && !string.IsNullOrEmpty(msg.Attachments.FirstOrDefault().Url))
                    {
                        targetUrl = msg.Attachments.FirstOrDefault().Url;
                        break;
                    }
                }
                //string targetUrl = (await Context.Channel.GetMessagesAsync(50).Flatten()).Where(x => x.Attachments.Count > 0).FirstOrDefault().Attachments.FirstOrDefault().Url;
                if (targetUrl == null)
                    return false;

                if (!parameters.ContainsKey("Url"))
                    parameters.Add("Url", targetUrl);
                else
                    parameters["Url"] = targetUrl;
            }

            using (await Context.Channel.EnterTypingState(Config.ImageManipulateTypeTime))
            {
                PostBitmapResponse resp = null;
                IImage imgData = null;
                //string uploadUrl = "";
                if (getUserAvatar)
                {
                    resp = await HttpService.PostBitmap($"{MagickServerUrl}/api/{apiEndpoint}/", parameters);
                    if (resp.Status.Success && resp.Image != null)
                    {
                        imgData = await ImageService.UploadImage(resp.Image);
                        //uploadUrl = await ImageService.PomfImageUpload(resp.Image);
                        resp.Image.Dispose();
                    }
                }
                else
                {
                    Image<Rgba32> targetImage = await HttpService.GetImageBitmap(parameters["Url"]);
                    imgData = await ImageService.UploadImage(ImageService.ClampImage(targetImage, Config.MaxGeneratedImageSize, Config.MaxGeneratedImageSize));
                    parameters["Url"] = imgData.Link;
                    //uploadUrl = await ImageService.PomfImageUpload(ImageService.ClampImage(targetImage, Config.MaxGeneratedImageSize, Config.MaxGeneratedImageSize));
                    //parameters["Url"] = uploadUrl;
                    resp = await HttpService.PostBitmap($"{MagickServerUrl}/api/{apiEndpoint}/", parameters);
                    if (resp.Status.Success && resp.Image != null)
                    {
                        imgData = await ImageService.UploadImage(resp.Image);
                        resp.Image.Dispose();
                        targetImage.Dispose();
                    }
                    else
                    {
                        targetImage.Dispose();
                    }
                } 

                if (!resp.Status.Success)
                {
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed(resp.Status.Message));
                    return false;
                }

                EmbedBuilder builder = new EmbedBuilder();
                EmbedService.BuildFeedbackEmbed(builder);
                builder.Description = "";
                builder.WithImageUrl(imgData.Link);
                
                //await Context.Channel.SendFileAsync(resp.Image, System.Drawing.Imaging.ImageFormat.Gif, $"{apiEndpoint}.gif");

                builder.WithFooter(footer =>
                {
                    footer.Text = $"⏰ {"Generated in:"}  {(DateTime.Now.Subtract(startTime)).ToNiceTime(false)}";
                });

                await ReplyAsync("", embed: builder.Build());
                imgData = null;

                //if (!getUserAvatar)
                //    await ImageService.DeleteImage(imgData.DeleteHash);

                return true;
            }
        }

        [RequireCooldown(25)]
        [Command("noise"), Summary("Adds noise to the specified user's avatar."), Priority(2)]
        public async Task Noise([Summary("The desired user.")] IUser user)
        {
            await RequestImageAPI("noise", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) }
            }, true);
        }

        [RequireCooldown(25)]
        [Command("noise"), Summary("Adds noise to the specified link."), Priority(1)]
        public async Task Noise([Summary("The desired url.")] [Remainder] string url = null)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("noise", new Dictionary<string, string>
            {
                { "Url", url }
            }, false);
        }

        [RequireCooldown(25)]
        [Command("charcoal"), Summary("Adds a charcoal type effect to the specified user's avatar."), Priority(2)]
        public async Task Charcoal([Summary("The desired user.")] IUser user, [Summary("The sigma of the charcoal effect.")] int sigma = 5)
        {
            await RequestImageAPI("charcoal", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
                { "Sigma", sigma.ToString() }
            }, true);
        }

        [RequireCooldown(25)]
        [Command("charcoal"), Summary("Applies charcoal effect to the specified url."), Priority(1)]
        public async Task Charcoal([Summary("The desired url.")] string url = null, [Summary("The sigma of the charcoal effect.")] int sigma = 5)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("charcoal", new Dictionary<string, string>
            {
                { "Url", url },
                { "Sigma", sigma.ToString() }
            }, false);
        }

        [RequireCooldown(25)]
        [Command("idk"), Summary("I don't even know"), Priority(2)]
        public async Task Idk([Summary("The desired user.")] IUser user, [Summary("The strength of this manipulation.")] int strength = 10)
        {
            await RequestImageAPI("sinfuck", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
                { "Strength", strength.ToString() }
            }, true);
        }

        [RequireCooldown(25)]
        [Command("idk"), Summary("I don't even know"), Priority(1)]
        public async Task Idk([Summary("The desired url.")] string url = null, [Summary("The strength of this manipulation.")] int strength = 10)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("idk", new Dictionary<string, string>
            {
                { "Url", url },
                { "Strength", strength.ToString() }
            }, false);
        }

        [RequireCooldown(25)]
        [Command("implode"), Summary("Implodes the specified user's avatar."), Priority(2)]
        public async Task Implode([Summary("The desired user.")] IUser user, [Summary("The strength of the softening.")] int soften = 2)
        {
            await RequestImageAPI("implode", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
                { "Soften", soften.ToString() }
            }, true);
        }

        [RequireCooldown(25)]
        [Command("implode"), Summary("Implodes the specified user's avatar."), Priority(1)]
        public async Task Implode([Summary("The desired url.")] string url = null, [Summary("The strength of the softening.")] int soften = 2)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("implode", new Dictionary<string, string>
            {
                { "Url", url},
                { "Soften", soften.ToString() }
            }, false);
        }

        [RequireCooldown(25)]
        [Command("sharpen"), Summary("Sharpens the specified user's avatar."), Priority(2)]
        public async Task Sharpen([Summary("The desired user.")] IUser user, [Summary("The strength of the sharpening.")] int strength = 2)
        {
            await RequestImageAPI("sharpen", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
                { "Strength", strength.ToString() }
            }, true);
        }

        [RequireCooldown(25)]
        [Command("sharpen"), Summary("Sharpens the specified user's avatar."), Priority(1)]
        public async Task Sharpen([Summary("The desired url.")] string url = null, [Summary("The strength of the sharpening.")] int strength = 2)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("sharpen", new Dictionary<string, string>
            {
                { "Url", url},
                { "Strength", strength.ToString() }
            }, false);
        }

        [RequireCooldown(25)]
        [Command("contrastfuzz"), Summary("Subtracts contrast and adds fuzz to the specified user's avatar."), Priority(2)]
        public async Task Contrastfuzz([Summary("The desired user.")] IUser user)
        {
            await RequestImageAPI("contrastfuzz", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
            }, true);
        }

        [RequireCooldown(25)]
        [Command("contrastfuzz"), Summary("Subtracts contrast and adds fuzz to the specified user's avatar."), Priority(1)]
        public async Task Contrastfuzz([Summary("The desired url.")] [Remainder] string url = null)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("contrastfuzz", new Dictionary<string, string>
            {
                { "Url", url},
            }, false);
        }

        [RequireCooldown(25)]
        [Command("happening"), Summary("IT'S HAPPENING!"), Priority(2)]
        public async Task Happening([Summary("The desired user.")] IUser user)
        {
            await RequestImageAPI("happening", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
            }, true);
        }

        [RequireCooldown(25)]
        [Command("happening"), Summary("IT'S HAPPENING!"), Priority(1)]
        public async Task Happening([Summary("The desired url.")] [Remainder] string url = null)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("happening", new Dictionary<string, string>
            {
                { "Url", url},
            }, false);
        }

        [RequireCooldown(25)]
        [Command("oilpaint"), Summary("Oil paints the specified user's avatar."), Priority(1)]
        public async Task OilPaint([Summary("The desired user.")] IUser user, [Summary("The sigma of the oilpaint.")] int sigma = 5)
        {
            await RequestImageAPI("oilpaint", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
                { "Sigma", sigma.ToString() }
            }, true);
        }

        [RequireCooldown(25)]
        [Command("oilpaint"), Summary("Oil paints the specified user's avatar."), Priority(1)]
        public async Task OilPaint([Summary("The desired url.")] string url = null, [Summary("The sigma of the oilpaint.")] int sigma = 5)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("oilpaint", new Dictionary<string, string>
            {
                { "Url", url},
                { "Sigma", sigma.ToString() }
            }, false);
        }

        [RequireCooldown(25)]
        [Command("wave"), Summary("Makes the specified user's avatar wavey."), Priority(2)]
        public async Task Wave([Summary("The desired user.")] IUser user, [Summary("The length of the waves.")] int length = 15)
        {
            await RequestImageAPI("wave", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
                { "Length", length.ToString() }
            }, true);
        }

        [RequireCooldown(25)]
        [Command("wave"), Summary("Makes the specified user's avatar wavey."), Priority(1)]
        public async Task Wave([Summary("The desired url.")] string url = null, [Summary("The length of the waves.")] int length = 15)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("wave", new Dictionary<string, string>
            {
                { "Url", url},
                { "Length", length.ToString() }
            }, false);
        }

        [RequireCooldown(25)]
        [Command("blur"), Summary("Blurs the specified user's avatar."), Priority(2)]
        public async Task Blur([Summary("The desired user.")] IUser user, [Summary("The blur strength.")] int strength = 3)
        {
            await RequestImageAPI("blur", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
                { "Strength", strength.ToString() }
            }, true);
        }

        [RequireCooldown(25)]
        [Command("blur"), Summary("Blurs the specified user's avatar."), Priority(1)]
        public async Task Blur([Summary("The desired url.")] string url = null, [Summary("The blur strength.")] int strength = 3)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("blur", new Dictionary<string, string>
            {
                { "Url", url},
                { "Strength", strength.ToString() }
            }, false);
        }

        [RequireCooldown(25)]
        [Command("triggered"), Summary("Created a triggered gif for the specified user."), Priority(2)]
        public async Task Triggered([Summary("The user to trigger.")] IUser user)
        {
            await RequestImageAPI("triggered", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl(ImageFormat.Auto, (ushort)Config.DesiredAvatarSize) },
            }, true);
        }

        [RequireCooldown(25)]
        [Command("triggered"), Summary("Created a triggered gif for the specified user."), Priority(1)]
        public async Task Triggered([Summary("The desired url.")] [Remainder] string url = null)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("triggered", new Dictionary<string, string>
            {
                { "Url", url },
            }, false);
        }

        [RequireCooldown(25)]
        [Command("spherize"), Summary("Spherifies the specified URL."), Priority(1)]
        public async Task Spherize([Summary("The desired url.")] string url = null, float radius = 0.7f)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("spherize", new Dictionary<string, string>
            {
                { "Url", url },
                { "Radius", radius.ToString() }
            }, false);
        }

        /*
        [RequireCooldown(25)]
        [Command("spherize"), Summary("Spherizes the player's avatar."), Priority(1)]
        public async Task Spherize(string url = null)
        {
            if (url != null && IsUriInvalid(url))
            {
                await Context.Channel.SendMessageAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The url provided appears to be invalid."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await RequestImageAPI("spherize", new Dictionary<string, string>
            {
                { "Url", url },
                { "Radius", "0.7" }
            }, false);
        }*/

        [RequireCooldown(25)]
        [Command("spherize"), Summary("Spherizes the player's avatar."), Priority(2)]
        public async Task Spherize(IUser user, float radius = 0.7f)
        {
            await RequestImageAPI("spherize", new Dictionary<string, string>
            {
                { "Url", user.GetAvatarUrl() },
                { "Radius", radius.ToString() }
            }, false);
        }
    }
}
