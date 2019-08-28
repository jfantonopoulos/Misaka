using Discord.WebSocket;
using ImageSharp;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Imgur.API.Models;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Classes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Misaka.Services
{
    public struct ImageSearchResult
    {
        public Image<Rgba32> Image;
        public string Url;
        public ImageSearchResult(Image<Rgba32> image, string url)
        {
            Image = image;
            Url = url;
        }
    }

    public struct ImageUploadResult
    {

    }

    public class ImageService : Service
    {
        public ImageService(IServiceProvider provider) : base(provider)
        {
        }

        protected override void Run()
        {
        }

        public async Task<ImageSearchResult> SearchImage(string query, int num = -1)
        {
            ImageSearchResult searchResult = new ImageSearchResult(null, "");
            query = query.Replace(" ", "+");
            string res = await Provider.GetService<HttpService>().Get("https://www.google.com/search?tbm=isch&gs_l=img&safe=1&q=" + query);
            Regex metaRegex = new Regex("<div class=\"rg_meta notranslate\">(.*?)<\\/div>");
            MatchCollection matches = metaRegex.Matches(res);
            num = (num == -1) ? Provider.GetService<MathService>().RandomRange(0, matches.Count - 1) : num;
            if (matches.Count < num )
            {
                return searchResult;
            }
            string randMatch = matches[num].Value.Replace("<div class=\"rg_meta notranslate\">", "").Replace("</div>", "");
            Dictionary<string, string> json = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(randMatch));
            Image<Rgba32> myImg = await Provider.GetService<HttpService>().GetImageBitmap(json["ou"]);
            searchResult.Image = myImg;
            if (searchResult.Image.Width > Provider.GetService<Config>().MaxGeneratedImageSize || searchResult.Image.Height > Provider.GetService<Config>().MaxGeneratedImageSize)
                searchResult.Image = ClampImage(searchResult.Image, Provider.GetService<Config>().MaxGeneratedImageSize, Provider.GetService<Config>().MaxGeneratedImageSize);
            searchResult.Url = json["ou"];

            return searchResult;
        }

        public Image<Rgba32> ClampImage(Image<Rgba32> img, int maxWidth, int maxHeight)
        {
            {
                float widthRatio = (img.Width / maxWidth) - 1;
                float heightRatio = (img.Height / maxHeight) - 1;

                int newWidth = Convert.ToInt32(img.Width - (widthRatio * maxWidth));
                int newHeight = Convert.ToInt32(img.Height - (heightRatio * maxHeight));
                //Image<Rgba32> newImg = img.Resize(newWidth, newHeight);
                //img.Dispose();
                return img.Resize(newWidth, newHeight);
            }
        }
        /*
        public async Task<string> PomfImageUpload(Image<Rgba32> img)
        {
            string hostUrl = "https://mixtape.moe/upload.php";
            MemoryStream imgStream = new MemoryStream();
            img.Save(imgStream);
            imgStream.Seek(0, SeekOrigin.Begin);
            using (var client = new HttpClient())
            using (var formData = new MultipartFormDataContent())
            {
                formData.Add(new StreamContent(imgStream), "files", "filename.png");
                var resp = await client.PostAsync(hostUrl, formData);
                if (resp.IsSuccessStatusCode)
                {
                    string jsonStr = await resp.Content.ReadAsStringAsync();
                    var uploadJson = JsonConvert.DeserializeObject<Dictionary<dynamic, dynamic>>(jsonStr);
                    string uploadUrl = uploadJson["files"][0]["url"];
                    Console.WriteLine("Uploaded success to pomf, url is : " + uploadUrl);
                    return uploadUrl;
                }
                else
                {
                    Console.WriteLine("Failed upload.");
                    return "";
                }
            }
                //string res = await Provider.GetService<HttpService>().Post(hostUrl, $"files: [{imgStream.To}]");
            var uploadJson = JsonConvert.DeserializeObject<Dictionary<dynamic, dynamic>>(res);
            string uploadUrl = uploadJson["files"][0]["url"];
            Console.WriteLine("Uploaded success to pomf, url is : " + uploadUrl);
            return uploadUrl;
        }*/

        public async Task<IImage> UploadImage(Image<Rgba32> img)
        {
            MemoryStream imgStream = new MemoryStream();
            img.Save(imgStream);
            imgStream.Seek(0, SeekOrigin.Begin);
            string imgSize = Provider.GetService<MathService>().BytesToNiceSize(imgStream.Length);
            try
            {
                ImgurClient client = new ImgurClient(Provider.GetService<Config>().ImgurClientId, Provider.GetService<Config>().ImgurClientSecret);
                ImageEndpoint endpoint = new ImageEndpoint(client);
                IImage imgData = await endpoint.UploadImageStreamAsync(imgStream);
                ConsoleEx.WriteColoredLine(Discord.LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Successfully uploaded image to Imgur. [{imgSize}]");
                imgStream.Dispose();
                return imgData;
            }
            catch(Exception ex)
            {
                string failMessage = $"Failed uploading image to Imgur. [{imgSize}];\n{ex.Message}";
                ConsoleEx.WriteColoredLine(Discord.LogSeverity.Critical, ConsoleTextFormat.TimeAndText, failMessage);
                imgStream.Dispose();
                return null;
            }
        }

        public async Task<bool> DeleteImage(string deleteHash)
        {
            try
            {
                ImgurClient client = new ImgurClient(Provider.GetService<Config>().ImgurClientId, Provider.GetService<Config>().ImgurClientSecret);
                ImageEndpoint endpoint = new ImageEndpoint(client);
                bool success = await endpoint.DeleteImageAsync(deleteHash);
                if (success)
                    ConsoleEx.WriteColoredLine(Discord.LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Successfully deleted image from Imgur. Delete Hash:[{deleteHash}]");
                else
                    ConsoleEx.WriteColoredLine(Discord.LogSeverity.Warning, ConsoleTextFormat.TimeAndText, $"Failed to delete image from Imgur. Delete Hash:[{deleteHash}]");
                return success;
            }
            catch(Exception ex)
            {
                ConsoleEx.WriteColoredLine(Discord.LogSeverity.Warning, ConsoleTextFormat.TimeAndText, $"Failed to delete image from Imgur. Delete Hash:[{deleteHash}]", ConsoleColor.Red, $"\n{ex.Message}");
                return false;
            }
            
        }
    }
}
