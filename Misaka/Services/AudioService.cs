using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using Misaka.Classes.API;
using Misaka.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Misaka.Services
{
    public struct VoiceConnection
    {
        public IVoiceChannel VoiceChannel;
        public IAudioClient AudioClient;
        public CancellationTokenSource CancelToken;
        public AudioOutStream AudioStream;

        public VoiceConnection(IVoiceChannel voiceChannel, IAudioClient audioClient)
        {
            VoiceChannel = voiceChannel;
            AudioClient = audioClient;
            CancelToken = null;
            AudioStream = AudioClient.CreatePCMStream(AudioApplication.Mixed, voiceChannel.Bitrate);
        }
    }

    public enum AudioSource
    {
        YouTube,
        SoundCloud,
        TTS,
        URL,
        Instant,
        Recording,
        Unknown
    }

    public struct AudioInfo
    {
        public AudioSource Source;
        public string Url;
        public string DownloadUrl;
        public string ItemInfo;
        public Stream AudioStream;

        public AudioInfo(AudioSource source, string url, string downloadUrl, Stream audioStream = null)
        {
            Source = source;
            Url = url;
            DownloadUrl = downloadUrl;
            AudioStream = audioStream;
            switch (source)
            {
                case AudioSource.YouTube:
                    ItemInfo = "YouTube Video";
                    break;
                case AudioSource.SoundCloud:
                    ItemInfo = "SoundCloud Song";
                    break;
                case AudioSource.TTS:
                    ItemInfo = "TTS Clip";
                    break;
                case AudioSource.URL:
                    ItemInfo = "URL";
                    break;
                case AudioSource.Instant:
                    ItemInfo = "MyInstant Clip";
                    break;
                case AudioSource.Recording:
                    ItemInfo = "Recording";
                    break;
                default:
                    ItemInfo = "Unknown";
                    break;
            }
        }
    }

    public class AudioService : Service
    {
        private ConcurrentDictionary<ulong, VoiceConnection> ServerVoiceConnections;
        private ConcurrentDictionary<ulong, AudioInStream> UserVoiceConnections;
        private AudioQueue AudioQueue;
        private AcaBoxTTS AcaBoxTTS;
        private SoundCloud SoundCloud;
        private YoutubeDownloader Youtube;

        public ConcurrentDictionary<ulong, VoiceConnection> Connections
        {
            get { return ServerVoiceConnections; }
        }

        public AudioQueue Queue
        {
            get { return AudioQueue; }
        }

        public AcaBoxTTS AcaBoxTTSAPI
        {
            get { return AcaBoxTTS; }
        }

        public SoundCloud SoundCloudAPI
        {
            get { return SoundCloud; }
        }

        public YoutubeDownloader YoutubeDownloader
        {
            get { return Youtube; }
        }

        public AudioService(IServiceProvider provider) : base(provider)
        {
        }

        protected override void Run()
        {
            ServerVoiceConnections = new ConcurrentDictionary<ulong, VoiceConnection>();
            UserVoiceConnections = new ConcurrentDictionary<ulong, AudioInStream>();
            AudioQueue = new AudioQueue();
            AcaBoxTTS = new AcaBoxTTS(Provider.GetService<HttpService>(), Provider.GetService<MathService>());
            SoundCloud = new SoundCloud(Provider.GetService<Config>().SoundCloudClientId, Provider.GetService<HttpService>());
            Youtube = new YoutubeDownloader("youtube-dl", $"-f mp4 --playlist-end {Provider.GetService<Config>().YouTubePlaylistMaxItems.ToString()} --ignore-errors");
        }

        public void AddInStream(ulong id, AudioInStream stream)
        {
            UserVoiceConnections.TryAdd(id, stream);
        }

        public void RemoveInStream(ulong id)
        {
            UserVoiceConnections.TryRemove(id, out _);
        }

        public bool InStreamExists(ulong id)
        {
            return UserVoiceConnections.ContainsKey(id);
        }

        public AudioInStream GetInStream(ulong id)
        {
            return UserVoiceConnections.GetValue(id);
        }

        public bool IsInVoiceChannel(ulong guildId)
        {
            return ServerVoiceConnections.ContainsKey(guildId);
        }

        public bool IsAudioStreaming(ulong guildId)
        {
            if (!IsInVoiceChannel(guildId))
                return false;
            return ServerVoiceConnections.GetValue(guildId).CancelToken != null;
        }

        private async Task<byte[]> BufferIncomingStream(AudioInStream e, int time = 3)
        {
            ConcurrentQueue<byte> voiceInQueue = new ConcurrentQueue<byte>();
            SemaphoreSlim queueLock = new SemaphoreSlim(1, 1);
            return await Task.Run(async () =>
            {
                DateTime nowTime = DateTime.Now;
                while (DateTime.Now.Subtract(nowTime).TotalSeconds <= time)
                {
                    if (e.AvailableFrames > 0)
                    {
                        queueLock.Wait();
                        RTPFrame frame = await e.ReadFrameAsync(CancellationToken.None);
                        for (int i = 0; i < frame.Payload.Length; i++)
                        {
                            voiceInQueue.Enqueue(frame.Payload[i]);
                        }
                        queueLock.Release();
                    }
                }
                return voiceInQueue.ToArray();
            });
        }

        public async Task<bool> JoinVoice(ulong guildId, IVoiceChannel voiceChannel)
        {
            if (IsInVoiceChannel(guildId))
            {
                AudioQueue.Clear(guildId);
                DeleteCancelToken(guildId);
                VoiceConnection voiceConn = ServerVoiceConnections.GetValue(guildId);
                ServerVoiceConnections.TryRemove(guildId, out _);
                await voiceConn.AudioClient.StopAsync();
                voiceConn.AudioClient.Dispose();
            }

            IAudioClient audioClient = await voiceChannel.ConnectAsync();
            ServerVoiceConnections.TryAdd(guildId, new VoiceConnection(voiceChannel, audioClient));

            audioClient.StreamCreated += async (id, stream) =>
            {
                SocketGuildUser user = Provider.GetService<DiscordSocketClient>().GetGuild(guildId).GetUser(id);
                AddInStream(id, stream);
                ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"AudioStream created for {user.Username}.");
                await Task.CompletedTask;
            };

            audioClient.StreamDestroyed += async (id) =>
            {
                SocketGuildUser user = Provider.GetService<DiscordSocketClient>().GetGuild(guildId).GetUser(id);
                RemoveInStream(id);
                ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"AudioStream destroyed for {user.Username}.");
                await Task.CompletedTask;
            };

            foreach(SocketGuildUser user in Provider.GetService<DiscordSocketClient>().GetGuild(guildId).Users)
            {
                if (user.VoiceChannel != null && !InStreamExists(user.Id) && user.AudioStream != null)
                {
                    AddInStream(user.Id, user.AudioStream);
                    ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"Added {user.Username}'s AudioStream.");
                }
            }
            return true;
        }

        public async Task<bool> LeaveVoice(ulong guildId)
        {
            if (!IsInVoiceChannel(guildId))
                return false;

            AudioQueue.Clear(guildId);
            DeleteCancelToken(guildId);
            VoiceConnection voiceConn = ServerVoiceConnections.GetValue(guildId);
            ServerVoiceConnections.TryRemove(guildId, out _);
            await voiceConn.AudioClient.StopAsync();
            voiceConn.AudioStream.Dispose();

            return true;
        }

        public bool StopAudio(ulong guildId)
        {
            if (!IsInVoiceChannel(guildId))
                return false;

            if (!IsAudioStreaming(guildId))
                return false;

            VoiceConnection voiceConn = ServerVoiceConnections.GetValue(guildId);
            AudioQueue.Clear(guildId);
            DeleteCancelToken(guildId);

            return true;
        }

        public async Task<bool> Skip(ulong guildId)
        {
            if (AudioQueue.Count(guildId) > 0)
            {
                await AdvanceQueue(guildId);
                return true;
            }
            else
                return false;
        }

        private void DeleteCancelToken(ulong guildId)
        {
            if (!IsInVoiceChannel(guildId))
                return;

            VoiceConnection voiceConn = ServerVoiceConnections.GetValue(guildId);
            if (voiceConn.CancelToken != null && !voiceConn.CancelToken.IsCancellationRequested)
            {
                voiceConn.CancelToken.Cancel();
                voiceConn.CancelToken.Dispose();
                voiceConn.CancelToken = null;

            }

            ServerVoiceConnections.TryUpdate(guildId, voiceConn);
        }

        public async Task AdvanceQueue(ulong guildId)
        {
            DeleteCancelToken(guildId);
            if (Queue.Count(guildId) > 0)
            {
                VoiceConnection voiceConn = ServerVoiceConnections.GetValue(guildId);
                AudioInfo nextAudioInfo = Queue.Pop(guildId);

                if (nextAudioInfo.AudioStream != null)
                {
                    var outStream = voiceConn.AudioStream;
                    await (nextAudioInfo.AudioStream).CopyToAsync(outStream, 81920, voiceConn.CancelToken.Token).ContinueWith(task =>
                    {
                        if (!task.IsCanceled && task.IsFaulted)
                            Console.WriteLine(task.Exception);
                    });
                    await outStream.FlushAsync();
                }
                else
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(async () => await PlayUrl(nextAudioInfo.DownloadUrl, guildId));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            }
        }

        public async Task<bool> Record(SocketGuildUser guildUser, ulong guildId, int seconds)
        {
            if (!IsInVoiceChannel(guildId))
                return false;

            if (!InStreamExists(guildUser.Id))
                return false;

            CancellationTokenSource cancelSource = new CancellationTokenSource();

            VoiceConnection voiceConn = ServerVoiceConnections.GetValue(guildId);
            ConsoleEx.WriteColoredLine(LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Recording User: {guildUser.Username}");
            MemoryStream byteStream = new MemoryStream(await BufferIncomingStream(GetInStream(guildUser.Id), seconds));

            if (IsAudioStreaming(guildId))
            {
                ConsoleEx.WriteColoredLine(LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Recording Queued: Length = {byteStream.Length.ToString()}");
                AudioQueue.Push(guildId, new AudioInfo(AudioSource.Recording, $"{seconds.ToString().Bold()} Recording of {guildUser.Username.Bold()}", "", byteStream));
                return false;
            }

            voiceConn.CancelToken = cancelSource;
            ServerVoiceConnections.TryUpdate(guildId, voiceConn);
            ConsoleEx.WriteColoredLine(LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Recording Playing: Length = {byteStream.Length.ToString()}");
            await byteStream.CopyToAsync(voiceConn.AudioStream, 81920, voiceConn.CancelToken.Token).ContinueWith(task =>
            {
                if (!task.IsCanceled && task.IsFaulted)
                    Console.WriteLine(task.Exception);
            });
            await AdvanceQueue(guildId);
            await voiceConn.AudioStream.FlushAsync();

            return true;
        }

        public async Task<bool> PlayUrl(string url, ulong guildId)
        {
            if (!IsInVoiceChannel(guildId))
                return false;

            StaticMethods.FireAndForget(async () => 
            {
                CancellationTokenSource cancelSource = new CancellationTokenSource();

                VoiceConnection voiceConn = ServerVoiceConnections.GetValue(guildId);
                var stream = voiceConn.AudioStream;
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{url}\" -f s16le -ar 48000 -ac 2 pipe:1 -loglevel quiet -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 2",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                voiceConn.CancelToken = cancelSource;
                ServerVoiceConnections.TryUpdate(guildId, voiceConn);
                await process.StandardOutput.BaseStream.CopyToAsync(stream, 81920, cancelSource.Token).ContinueWith(task =>
                {
                    if (!task.IsCanceled && task.IsFaulted)
                        Console.WriteLine(task.Exception);
                });

                process.WaitForExit();
                await AdvanceQueue(guildId);
                await stream.FlushAsync();
                process.Dispose();
            });
            
            return true;
        }
    }
}
