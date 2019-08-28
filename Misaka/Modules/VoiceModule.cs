using Misaka.Classes;
using System;
using System.Collections.Generic;
using System.Text;
using Misaka.Services;
using Misaka.Extensions;
using Discord.Commands;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.Linq;
using Misaka.Preconditions;
using AngleSharp;
using System.Text.RegularExpressions;

namespace Misaka.Modules
{
    public class VoiceModule : MisakaModuleBase
    {
        private AudioService AudioService;
        private EmbedService EmbedService;
        private MathService MathService;
        private Config Config;

        public VoiceModule(AudioService audioService, EmbedService embedService, Config config, MathService mathService) : base(mathService)
        {
            AudioService = audioService;
            EmbedService = embedService;
            MathService = mathService;
            Config = config;
        }

        private async Task PlayUrlOrQueue(string url, AudioInfo audioInfo)
        {
            ulong guildId = Context.Guild.Id;

            if (!(await IsInVoiceChannel()))
                return;
                
            if (!AudioService.IsAudioStreaming(guildId))
            {
                await AudioService.PlayUrl(url, guildId);
            }
            else
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"Already streaming audio, queued {audioInfo.ItemInfo.Bold()} item."), lifeTime: Config.FeedbackMessageLifeTime);
                AudioService.Queue.Push(guildId, audioInfo);
            }
        }

        public async Task<bool> IsInVoiceChannel()
        {
            if (!AudioService.IsInVoiceChannel(Context.Guild.Id))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("Not currently in a voice channel."), lifeTime: Config.FeedbackMessageLifeTime);
                return false;
            }

            return true;
        }

        [Command("joinvoice"), Summary("Joins the specified voice channel.")]
        public async Task JoinVoice([Summary("The voice channel's name.")] IVoiceChannel channel = null)
        {
            ulong guildId = Context.Guild.Id;
            if (channel == null)
            {
                channel = (Context.Guild as SocketGuild).Channels.OfType<IVoiceChannel>().FirstOrDefault();
                if (channel == null)
                {
                    await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("You must specify a voice channel!"), lifeTime: Config.FeedbackMessageLifeTime);
                    return;
                }
            }

            await AudioService.JoinVoice(guildId, channel);
            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"Joining voice channel {channel.Name.Bold()}."));
        }

        [Command("leavevoice"), Summary("Leaves the current voice channel.")]
        public async Task LeaveVoice()
        {
            ulong guildId = Context.Guild.Id;
            if (!AudioService.IsInVoiceChannel(guildId))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("Not currently in a voice channel."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"Leaving voice channel {AudioService.Connections.GetValue(guildId).VoiceChannel.Name.Bold()}."));
            await AudioService.LeaveVoice(guildId);
        }

        [Command("stopsound"), Summary("Stops the currently playing audio."), Alias("shutup")]
        public async Task StopSound()
        {
            ulong guildId = Context.Guild.Id;
            if (!AudioService.IsInVoiceChannel(guildId))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("Not currently in a voice channel."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            if (!AudioService.IsAudioStreaming(guildId))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("Not currently streaming any audio."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($":mute: Stopped current audio, advancing queue."));
            AudioService.StopAudio(guildId);
        }

        [Command("skip"), Summary("Skips the current audio and advances the queue.")]
        public async Task Skip()
        {
            ulong guildId = Context.Guild.Id;
            bool skipped = await AudioService.Skip(guildId);
            if (skipped)
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($":fast_forward: Skipping current audio, {AudioService.Queue.Count(guildId).ToString().Bold()} items left."), lifeTime: Config.FeedbackMessageLifeTime);
            else
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("The queue for this channel is empty."), lifeTime: Config.FeedbackMessageLifeTime);
        }

        [RequireCooldown(1)]
        [Command("myinstant"), Summary("Plays a random MyInstant found with the specified search query.")]
        async Task PlayInstant([Summary("The search query.")] [Remainder] string query)
        {
            string baseUrl = "https://www.myinstants.com";
            string searchPath = "/search/?name=";
            string url = "";
            string name = "";

            var document = await BrowsingContext.New(Configuration.Default.WithDefaultLoader()).OpenAsync($"{baseUrl}{searchPath}{query}");
            var myInstantSelector = document.QuerySelectorAll("div.instant");
            if (myInstantSelector.Length == 0)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("No results were found for that query."));
                return;
            }
            var randomNode = myInstantSelector[MathService.RandomRange(0, myInstantSelector.Length - 1)];
            string path = randomNode.QuerySelector("div.small-button").Attributes["onmousedown"].Value;
            name = randomNode.QuerySelector("a").TextContent;
            
            Regex urlRegex = new Regex("\\/media\\/sounds\\/(.*?)'");
            Match urlMatch = urlRegex.Match(path);
            path = urlMatch.Value.Substring(0, urlMatch.Value.Length - 1);
            url = $"{baseUrl}{path}";

            await PlayUrlOrQueue(url, new AudioInfo(AudioSource.Instant, name, url));
        }

        [RequireCooldown(5)]
        [Command("playurl"), Summary("Plays the specified url.")]
        public async Task PlayUrl([Summary("The url to play.")] string url)
        {
            await PlayUrlOrQueue(url, new AudioInfo(AudioSource.URL, url, url));
        }

        [RequireCooldown(5)]
        [Command("shuffle"), Summary("Shuffles the current audio queue.")]
        public async Task Shuffle()
        {
            if (AudioService.Queue.Count(Context.Guild.Id) > 0)
            {
                AudioService.Queue.Shuffle(Context.Guild.Id);
                await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed("Shuffled the Audio Queue."));
            }
            else
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed("There are no queued items to shuffle."));
            }
        }

        [RequireCooldown(5)]
        [Command("soundcloud"), Summary("Plays the specified SoundCloud song."), Alias("playsc", "playsoundcloud", "sc")]
        public async Task PlaySoundCloud([Summary("The SoundClound url.")] [Remainder] string url)
        {
            await ReplyAsync("", embed: EmbedService.MakeFeedbackEmbed($":satellite: Downloading specified SoundCloud song {url.Code()}."), lifeTime: Config.FeedbackMessageLifeTime);
            string downloadUrl = await AudioService.SoundCloudAPI.GetDownloadUrl(url);
            await PlayUrlOrQueue(downloadUrl, new AudioInfo(AudioSource.SoundCloud, url, downloadUrl));
        }

        [RequireCooldown(5)]
        [Command("tts"), Summary("Plays the specified text to speech.")]
        public async Task PlayTTS([Summary("The text to speak.")] [Remainder] string text)
        {
            string ttsUrl = await AudioService.AcaBoxTTSAPI.GetTTSUrl(text);
            await PlayUrlOrQueue(ttsUrl, new AudioInfo(AudioSource.TTS, text, ttsUrl));
        }

        [RequireCooldown(5)]
        [Command("youtube"), Summary("Plays the specified YouTube song."), Alias("playyt", "playyoutube", "yt")]
        public async Task PlayYoutube([Summary("The YouTube url.")] [Remainder] string url)
        {
            if (!(await IsInVoiceChannel()))
                return;

            await ReplyAsync("", embed: EmbedService.MakeFeedbackEmbed($":satellite: Downloading specified YouTube song {url.Code()}."), lifeTime: Config.FeedbackMessageLifeTime);
            List<string> downloadUrls = AudioService.YoutubeDownloader.GetDownloadUrls(url);
            if (downloadUrls.Count > 0)
            {
                if (downloadUrls.Count > 1)
                {
                    string firstUrl = downloadUrls.FirstOrDefault();
                    downloadUrls.RemoveAt(0);
                    if (downloadUrls.Count == Config.YouTubePlaylistMaxItems)
                    {
                        await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"YouTube Playlist detected, exceeds max length, queueing first {Config.YouTubePlaylistMaxItems.ToString().Bold()} items."));
                        for(int i = 0; i < Config.YouTubePlaylistMaxItems; i++)
                            AudioService.Queue.Push(Context.Guild.Id, new AudioInfo(AudioSource.YouTube, $"Playlist [{url.Code()}]\nItem #{(i + 2).ToString().Bold()}", downloadUrls[i]));
                    }
                    else
                    {
                        await ReplyAsync("", embed: EmbedService.MakeSuccessFeedbackEmbed($"YouTube Playlist detected, queueing {downloadUrls.Count.ToString().Bold()} items."));
                        for (int i = 0; i < downloadUrls.Count; i++)
                            AudioService.Queue.Push(Context.Guild.Id, new AudioInfo(AudioSource.YouTube, $"Playlist [{url.Code()}]\nItem #{(i + 2).ToString().Bold()}", downloadUrls[i]));
                            
                    }
                    await PlayUrlOrQueue(firstUrl, new AudioInfo(AudioSource.YouTube, url, firstUrl));
                }
                else
                {
                    await PlayUrlOrQueue(downloadUrls.FirstOrDefault(), new AudioInfo(AudioSource.YouTube, url, downloadUrls.FirstOrDefault()));
                }
            }
            else
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"Failed to download specified YouTube song."), lifeTime: Config.FeedbackMessageLifeTime);
        }

        [RequireUser(142787068303507456, "You are not my Senpai!")]
        [Command("record"), Summary("Records the user for the specified amount of time, and then plays it back.")]
        public async Task Record([Summary("The user to record")] IGuildUser user, [Summary("The number of seconds to record for.")] int seconds)
        {
            if (!(await IsInVoiceChannel()))
                return;

            if (!AudioService.InStreamExists(user.Id))
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"{user.Username} is not in this voice channel."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            await AudioService.Record(user as SocketGuildUser, Context.Guild.Id, seconds);
        }

        [Command("queue"), Summary("Displays the currently queued items.")]
        public async Task Queue()
        {
            ulong guildId = Context.Guild.Id;
            if (AudioService.Queue.Count(guildId) == 0)
            {
                await ReplyAsync("", embed: EmbedService.MakeFailFeedbackEmbed($"The queue is currently empty."), lifeTime: Config.FeedbackMessageLifeTime);
                return;
            }

            EmbedBuilder embedBuilder = new EmbedBuilder();
            EmbedService.BuildSuccessEmbed(embedBuilder);
            embedBuilder.Description = "";
            embedBuilder.Title = "Audio Queue";
            int counter = 1;
            int limit = 7;

            foreach(AudioInfo audioInfo in AudioService.Queue.GetQueue(guildId))
            {
                if (counter > limit)
                    break;

                embedBuilder.AddField(x =>
                {
                    x.Name = $"#{counter.ToString().Bold()} {audioInfo.ItemInfo}";
                    x.Value = audioInfo.Url;
                });
                counter++;
            }

            embedBuilder.WithFooter(x => x.Text = $"{AudioService.Queue.Count(guildId)} item(s) in queue.");
            await ReplyAsync("", embed: embedBuilder.Build());
        }
    }
}
