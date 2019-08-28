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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Services
{
    public class HttpService : Service
    {
        public class ErrorPayload
        {
            [JsonProperty("Title")]
            public string Title;

            [JsonProperty("ErrorCode")]
            public string ErrorCode;

            [JsonProperty("Description")]
            public string Description;
        }

        public class ResponseStatus
        {
            public bool Success
            {
                get;
                private set;
            }

            public string Message
            {
                get;
                private set;
            }

            public ResponseStatus(bool success, string message)
            {
                Success = success;
                Message = message;
            }
        }

        public class PostBitmapResponse
        {

            public Image<Rgba32> Image
            {
                get;
                private set;
            }

            public ResponseStatus Status
            {
                get;
                private set;
            }

            public PostBitmapResponse(Image<Rgba32> img, ResponseStatus status)
            {
                Image = img;
                Status = status;
            }
        }

        public HttpService(IServiceProvider provider) : base(provider)
        {
        }

        protected override void Run()
        {
            //Do Things
        }

        public async Task<CookieCollection> GetCookies(string url)
        {
            CookieContainer cookies = new CookieContainer();
            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.CookieContainer = cookies;
                using (HttpClient client = new HttpClient(handler))
                {
                    HttpResponseMessage resp = await client.GetAsync(url);
                    return cookies.GetCookies(new Uri(url));
                }
            }
        }

        public async Task<String> Get(string url, Dictionary<string, string> headers = null, List<Cookie> cookieList = null)
        {
            if (headers == null)
                headers = new Dictionary<string, string>();

            var cookieContainer = new CookieContainer();
            using (var handler = new HttpClientHandler { CookieContainer = cookieContainer })
            using (HttpClient client = new HttpClient(handler))
            {
                var collection = new CookieCollection();
                if (cookieList != null)
                    cookieList.ForEach(x => collection.Add(x));
                cookieContainer.Add(new Uri(url), collection);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/55.0.2883.87 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                foreach (KeyValuePair<string, string> entry in headers)
                {
                    client.DefaultRequestHeaders.Add(entry.Key, entry.Value);
                }
                using (HttpResponseMessage resp = await client.GetAsync(url))
                using (HttpContent content = resp.Content)
                {
                    resp.EnsureSuccessStatusCode();
                    string result = await content.ReadAsStringAsync();
                    return result;
                }
            }
        }

        public async Task<string> Post(string url, Dictionary<string, string> Data, List<Cookie> cookieList = null)
        {
            return await Task.Run(async () =>
            {
                var cookieContainer = new CookieContainer();
                using (var handler = new HttpClientHandler { CookieContainer = cookieContainer })
                using (HttpClient client = new HttpClient(handler))
                {
                    var collection = new CookieCollection();
                    if (cookieList != null)
                        cookieList.ForEach(x => collection.Add(x));
                    cookieContainer.Add(new Uri(url), collection);

                    FormUrlEncodedContent content = new FormUrlEncodedContent(Data);
                    HttpResponseMessage resp = await client.PostAsync(url, content);
                    resp.EnsureSuccessStatusCode();
                    string result = await resp.Content.ReadAsStringAsync();
                    return result;
                }
            });
        }
        public async Task<string> Post(string url, string data)
        {
            return await Task.Run(async () =>
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders
                        .Accept
                        .Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    StringContent content = new StringContent(data, Encoding.UTF8, "application/json");
                    HttpResponseMessage resp = await client.PostAsync(url, content);
                    resp.EnsureSuccessStatusCode();
                    string result = await resp.Content.ReadAsStringAsync();
                    return result;
                }
            });
        }

        public async Task<PostBitmapResponse> PostBitmap(string url, Dictionary<string, string> json)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders
                    .Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/json"));

                StringContent content = new StringContent(JsonConvert.SerializeObject(json).ToString(), Encoding.UTF8, "application/json");
                HttpResponseMessage resp = await client.PostAsync(url, content);
                int statusCode = (int)resp.StatusCode;
                if (statusCode == 400 || statusCode == 429 || statusCode == 500)
                {
                    ErrorPayload err = JsonConvert.DeserializeObject<ErrorPayload>(await resp.Content.ReadAsStringAsync());
                    string errorText = $"[{err.Title}] - {err.ErrorCode} : {err.Description}";
                    ConsoleEx.WriteColoredLine(Discord.LogSeverity.Critical, ConsoleTextFormat.TimeAndText, errorText);
                    return new PostBitmapResponse(null, new ResponseStatus(false, errorText));
                }
                Stream stream = await resp.Content.ReadAsStreamAsync();
                stream.Seek(0, SeekOrigin.Begin);
                Image<Rgba32> img = Image.Load(stream);
                return new PostBitmapResponse(img, new ResponseStatus(true, ""));
            }
        }

        public async Task<Image<Rgba32>> GetImageBitmap(string url, int width = -1, int height = -1)
        {
            return await Task.Run(async () =>
            {
                Image<Rgba32> imgBitmap = null;
                using (HttpClient httpClient = new HttpClient())
                {
                    using (var resp = await httpClient.GetAsync(url))
                    {
                        resp.EnsureSuccessStatusCode();
                        MemoryStream memStream = new MemoryStream(await httpClient.GetByteArrayAsync(url));
                        memStream.Seek(0, SeekOrigin.Begin);
                        imgBitmap = Image.Load(memStream);
                    }
                    if (width != -1 || height != -1)
                    {
                        int newWidth = width == -1 ? imgBitmap.Width : width;
                        int newHeight = height == -1 ? imgBitmap.Height : height;
                        imgBitmap = imgBitmap.Resize(newWidth, newHeight);
                    }
                }
                return imgBitmap;
            });
        }
    }
}
