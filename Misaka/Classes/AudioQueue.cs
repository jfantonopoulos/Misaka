using Misaka.Extensions;
using Misaka.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Misaka.Classes
{
    public class AudioQueue
    {
        private ConcurrentDictionary<ulong, List<AudioInfo>> GuildQueues;
        
        public AudioQueue()
        {
            GuildQueues = new ConcurrentDictionary<ulong, List<AudioInfo>>();
        }

        private void EnsureGuildIndexExists(ulong guildId)
        {
            if (!GuildQueues.ContainsKey(guildId))
                GuildQueues.TryAdd(guildId, new List<AudioInfo>());
        }

        public void Push(ulong guildId, AudioInfo audioInfo)
        {
            EnsureGuildIndexExists(guildId);
            GuildQueues.GetValue(guildId).Add(audioInfo);
        }

        public void Push(ulong guildId, params AudioInfo[] audioInfos)
        {
            EnsureGuildIndexExists(guildId);
            GuildQueues.GetValue(guildId).AddRange(audioInfos);
        }

        public AudioInfo Peek(ulong guildId)
        {
            EnsureGuildIndexExists(guildId);
            AudioInfo currentInfo = GuildQueues.GetValue(guildId).First();
            return currentInfo;
        }

        public void MutateTop(ulong guildId, AudioInfo info)
        {
            EnsureGuildIndexExists(guildId);
            List<AudioInfo> audioList = GuildQueues.GetValue(guildId).ToList();
            audioList[audioList.Count - 1] = info;
            GuildQueues.TryUpdate(guildId, audioList);
        }

        public AudioInfo Pop(ulong guildId)
        {
            EnsureGuildIndexExists(guildId);
            AudioInfo currentInfo = GuildQueues.GetValue(guildId).First();
            GuildQueues.GetValue(guildId).RemoveAt(0);
            return currentInfo;
        }

        public void Clear(ulong guildId)
        {
            EnsureGuildIndexExists(guildId);
            if (GuildQueues.ContainsKey(guildId))
                GuildQueues.GetValue(guildId).Clear();
        }

        public int Count(ulong guildId)
        {
            EnsureGuildIndexExists(guildId);
            return GuildQueues.GetValue(guildId).Count;
        }

        public AudioInfo[] GetQueue(ulong guildId)
        {
            EnsureGuildIndexExists(guildId);
            return GuildQueues.GetValue(guildId).ToArray();
        }

        public void Shuffle(ulong guildId)
        {
            EnsureGuildIndexExists(guildId);
            if (GuildQueues.ContainsKey(guildId))
                GuildQueues.GetValue(guildId).Shuffle();
        }
    }
}
