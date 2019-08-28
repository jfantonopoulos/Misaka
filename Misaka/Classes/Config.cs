using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Classes
{
    public class Config : MisakaBaseClass
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("MashapeKey")]
        public string MashapeKey { get; private set; }

        [JsonProperty("Prefix")]
        public string Prefix { get; private set; }

        [JsonProperty("ImgurClientId")]
        public string ImgurClientId { get; private set; }

        [JsonProperty("ImgurClientSecret")]
        public string ImgurClientSecret { get; private set; }

        [JsonProperty("MaxGeneratedImageSize")]
        public int MaxGeneratedImageSize { get; private set; }

        [JsonProperty("DesiredAvatarSize")]
        public int DesiredAvatarSize { get; private set; }

        [JsonProperty("ImageManipulateTypeTime")]
        public int ImageManipulateTypeTime { get; private set; }

        [JsonProperty("MisakaAvatarUrls")]
        public List<string> MisakaAvatarUrls { get; private set; }

        [JsonProperty("AvatarSwapFrequency")]
        public int AvatarSwapFrequency { get; private set; }

        [JsonProperty("FeedbackMessageLifeTime")]
        public int FeedbackMessageLifeTime { get; private set; }

        [JsonProperty("MySQLServerAddress")]
        public string MySQLServerAddress { get; private set; }

        [JsonProperty("MySQLDatabase")]
        public string MySQLDatabase { get; private set; }

        [JsonProperty("MySQLUsername")]
        public string MySQLUsername { get; private set; }

        [JsonProperty("MySQLPassword")]
        public string MySQLPassword { get; private set; }

        [JsonProperty("MaxReactionsToFetch")]
        public int MaxReactionsToFetch { get; private set; }

        [JsonProperty("SoundCloudClientId")]
        public string SoundCloudClientId { get; private set; }

        [JsonProperty("YouTubePlaylistMaxItems")]
        public int YouTubePlaylistMaxItems { get; private set; }

        [JsonProperty("RedditCheckInterval")]
        public int RedditCheckInterval { get; private set; }

        [JsonProperty("CNNCheckInterval")]
        public int CNNCheckInterval { get; private set; }

        [JsonProperty("CNNTextChannels")]
        public List<string> CNNTextChannels { get; private set; }

        [JsonProperty("CleverbotApiKey")]
        public string CleverbotApiKey { get; private set; }

        [JsonProperty("PerspectiveApiKey")]
        public string PerspectiveApiKey { get; private set; }

        public Config()
        {
            Token = "";
            MashapeKey = "";
            Prefix = "->";
            ImgurClientId = "";
            ImgurClientSecret = "";
            MaxGeneratedImageSize = 1024;
            DesiredAvatarSize = 256;
            ImageManipulateTypeTime = 20;
            MisakaAvatarUrls = new List<string> {};
            AvatarSwapFrequency = 5; // 5 Minutes
            FeedbackMessageLifeTime = 8; // 8 Seconds
            MySQLServerAddress = "";
            MySQLDatabase = "";
            MySQLUsername = "";
            MySQLPassword = "";
            MaxReactionsToFetch = 10;
            SoundCloudClientId = "";
            YouTubePlaylistMaxItems = 5;
            RedditCheckInterval = 5;
            CNNCheckInterval = 5;
            CNNTextChannels = new List<string>();
            CleverbotApiKey = "";
            PerspectiveApiKey = "";
        }

        public async Task ConfigAsync()
        {
            if (File.Exists("config.json"))
            {
                string json = File.ReadAllText("config.json");
                Config loadedConfig = JsonConvert.DeserializeObject<Config>(json);
                Token = loadedConfig.Token;
                MashapeKey = loadedConfig.MashapeKey;
                Prefix = loadedConfig.Prefix;
                ImgurClientId = loadedConfig.ImgurClientId;
                ImgurClientSecret = loadedConfig.ImgurClientSecret;
                MaxGeneratedImageSize = loadedConfig.MaxGeneratedImageSize;
                DesiredAvatarSize = loadedConfig.DesiredAvatarSize;
                ImageManipulateTypeTime = loadedConfig.ImageManipulateTypeTime;
                MisakaAvatarUrls = loadedConfig.MisakaAvatarUrls;
                if (MisakaAvatarUrls.Count == 0)
                    MisakaAvatarUrls = new List<string> { "http://rs790.pbsrc.com/albums/yy182/MisakaMikotoFC/Misaka%20Mikoto/4.jpg~c200", "http://rs790.pbsrc.com/albums/yy182/MisakaMikotoFC/Misaka%20Mikoto/2.png~c200", "http://cdn.akamai.steamstatic.com/steamcommunity/public/images/avatars/71/71d4b4a1ccc7dbcf381e80ffac39107eeddcd21f_full.jpg", "https://static.comicvine.com/uploads/original/11111/111112793/3417788-2957188508-mikot.jpg" };
                AvatarSwapFrequency = loadedConfig.AvatarSwapFrequency;
                FeedbackMessageLifeTime = loadedConfig.FeedbackMessageLifeTime;
                MySQLServerAddress = loadedConfig.MySQLServerAddress;
                MySQLDatabase = loadedConfig.MySQLDatabase;
                MySQLUsername = loadedConfig.MySQLUsername;
                MySQLPassword = loadedConfig.MySQLPassword;
                MaxReactionsToFetch = loadedConfig.MaxReactionsToFetch;
                SoundCloudClientId = loadedConfig.SoundCloudClientId;
                YouTubePlaylistMaxItems = loadedConfig.YouTubePlaylistMaxItems;
                RedditCheckInterval = loadedConfig.RedditCheckInterval;
                CNNCheckInterval = loadedConfig.CNNCheckInterval;
                CNNTextChannels = loadedConfig.CNNTextChannels;
                CleverbotApiKey = loadedConfig.CleverbotApiKey;
                PerspectiveApiKey = loadedConfig.PerspectiveApiKey;
            }
            else
            {
                File.WriteAllText("config.json", JsonConvert.SerializeObject(this, Formatting.Indented));
            }

            await Task.CompletedTask;
        }

        public async Task Save()
        {
            File.WriteAllText("config.json", JsonConvert.SerializeObject(this, Formatting.Indented));
            await Task.CompletedTask;
        }

        public string GetConnectionString()
        {
            return $"server={this.MySQLServerAddress};database={this.MySQLDatabase};uid={this.MySQLUsername};pwd={this.MySQLPassword};";
        }
    }
}
