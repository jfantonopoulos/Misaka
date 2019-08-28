using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Services;
using Misaka.Classes;
using Misaka.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Imgur.API.Models;
using ImageSharp;
using System.Threading;
using Discord;
using Discord.Commands;
using System.Linq;
using MoreLinq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Misaka.Extensions
{
    public static class DiscordExtensions
    {
        public struct FuzzyMatch
        {
            public string TextValue;
            public List<int> Positions;
        }

        public static HttpService HttpService;
        public static MathService MathService;
        public static Config Config;
        public static ImageService ImageService;
        public static DiscordSocketClient Client;

        public static async Task UploadAvatar(this DiscordSocketClient self, IServiceProvider provider, string avatarUrl)
        {
            await Task.Run(async () =>
            {
                Image<Rgba32> avatarBitmap = await provider.GetService<HttpService>().GetImageBitmap(avatarUrl);
                MemoryStream avatarStream = new MemoryStream();
                avatarBitmap.Save(avatarStream);
                avatarStream.Seek(0, SeekOrigin.Begin);
                await self.CurrentUser.ModifyAsync(settings =>
                {
                    settings.Avatar = new Discord.Image(avatarStream);
                });
                avatarStream.Dispose();
                avatarBitmap.Dispose();
            });
        }

        public static async Task<IUserMessage> SendMessageAsync(this IMessageChannel self, Embed embed, int lifeTime = 0)
        {
            IUserMessage msg = await self.SendMessageAsync("", embed: embed);
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

        public static async Task<IUserMessage> SendMessageAsync(this IMessageChannel self, string content, bool isTTS = false, Embed embed = null, int lifeTime = 0)
        {
            IUserMessage msg = await self.SendMessageAsync(content, isTTS, embed);
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

        public static async Task<Discord.IUserMessage> SendFileAsync(this Discord.IMessageChannel self, Image<Rgba32> img, System.Drawing.Imaging.ImageFormat format, string name)
        {
            return await Task.Run(async () =>
            {
                MemoryStream imgStream = new MemoryStream();
                img.Save(imgStream);
                imgStream.Seek(0, SeekOrigin.Begin);
                return await self.SendFileAsync(imgStream, name);
            });
        }

        public static async Task<IDisposable> EnterTypingState(this ISocketMessageChannel self, int seconds)
        {
            IDisposable state = self.EnterTypingState();
            Timer stateTimer = null;
            stateTimer = new Timer((e) => {
                if (state != null)
                    state.Dispose();
                stateTimer.Dispose();
                stateTimer = null;
            }, state, MathService.TimeUnitToMilli(TimeUnit.Seconds, seconds), 0);
            await Task.CompletedTask;
            return state;
        }

        public static void AppendEmbedDescription(this EmbedBuilder self, string text)
        {
            self.Description = $"{self.Description}{text}";
        }

        public static CommandInfo FindInfoByName(this CommandService self, string text)
        {
            CommandInfo cmdInfo = self.Commands.FirstOrDefault(x => x.Name.ToLower() == text.ToLower());
            if (cmdInfo == null)
            {
                foreach (CommandInfo cmd in self.Commands)
                {
                    if (cmd.Aliases != null && cmd.Aliases.FirstOrDefault(x => x.ToLower() == text.ToLower()) != null)
                    {
                        return cmd;
                    }
                }
            }
            else
                return cmdInfo;
            return null;
        }

        public static async Task<string> GetAvatarUrl(this IUser client, bool forceDownload)
        {
            string url = client.GetAvatarUrl(ImageFormat.Auto, 128);

            if (!url.Contains(".gif", ".png", ".jpeg"))
            {
                var img = new Image<Rgba32>(await HttpService.GetImageBitmap(url));
                IImage result = await ImageService.UploadImage(img);
                img.Dispose();
                Timer deleteTimer = null;
                deleteTimer = new Timer(async (e) => {
                    if (result != null)
                    {
                        await ImageService.DeleteImage(result.DeleteHash);
                    }
                    deleteTimer.Dispose();
                    deleteTimer = null;
                }, null, MathService.TimeUnitToMilli(TimeUnit.Hours, 1), 0);
                await Task.CompletedTask;
                return result.Link;
            }
            else
                return url;
        }

        //https://gist.github.com/bartlomiejwolk/6cb07b52fbb020cebfbb781a775e64a1
        public static List<FuzzyMatch> FuzzySearch(this CommandService self, string query)
        {
            if (query == string.Empty)
                return null;

            char[] tokens = query.ToCharArray();
            List<FuzzyMatch> matches = new List<FuzzyMatch>();
            foreach (CommandInfo cmdInfo in self.Commands.DistinctBy(x => x.Name))
            {
                int tokenIndex = 0;
                int resultCharIndex = 0;
                List<int> matchedPositions = new List<int>();

                while(resultCharIndex < cmdInfo.Name.Length)
                {
                    if (cmdInfo.Name[resultCharIndex] == tokens[tokenIndex])
                    {
                        matchedPositions.Add(resultCharIndex);
                        tokenIndex++;

                        if (tokenIndex >= tokens.Length)
                        {
                            var match = new FuzzyMatch()
                            {
                                TextValue = cmdInfo.Name,
                                Positions = matchedPositions
                            };
                            matches.Add(match);
                            break;
                        }
                    }
                    resultCharIndex++;
                }
            }
            return matches;
        }

        public static async Task UpdateServerCount(this DiscordSocketClient self)
        {
            using (var httpClient = new HttpClient())
            using (var content = new StringContent($"{{ \"server_count\": {self.Guilds.Count}}}", Encoding.UTF8, "application/json"))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6IjE0Mjc4NzA2ODMwMzUwNzQ1NiIsImlhdCI6MTQ5ODgyNjg4N30.skV9I1bCbA8Q9cD7ayo6WJZ-6r4ou6BEcfnmScf83wM");
                HttpResponseMessage response = await httpClient.PostAsync("https://discordbots.org/api/bots/314821049462030336/stats", content);
                ConsoleEx.WriteColoredLine($"[$[[Blue]]$Discord Bot Listing$[[Gray]]$] Updated server count to {self.Guilds.Count.ToString().Number()}.");
            }
        }

        public static async Task AddReactionAsync(this SocketUserMessage self, IEmote emote, int lifeTime)
        {
            await self.AddReactionAsync(emote);
            SimpleTimer newTimer = new SimpleTimer(async () => { await self.RemoveReactionAsync(emote, Client.CurrentUser); }, MathService.TimeUnitToMilli(TimeUnit.Seconds, lifeTime), false);
            newTimer.Start();
        }
    }
}
