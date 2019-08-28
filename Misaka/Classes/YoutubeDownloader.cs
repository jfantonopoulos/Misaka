using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Misaka.Classes
{
    public class YoutubeDownloader
    {
        private string YtDlPath;
        private string YtDlOptions;

        public YoutubeDownloader(string ytDlPath, string ytDlOptions)
        {
            YtDlPath = ytDlPath;
            YtDlOptions = ytDlOptions;
        }

        public List<string> GetDownloadUrls(string url)
        {
            List<string> urls = new List<string>();
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = YtDlPath,
                Arguments = $"{YtDlOptions} --get-url \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null || string.IsNullOrEmpty(e.Data))
                    return;

                urls.Add(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null || string.IsNullOrEmpty(e.Data))
                    return;

                ConsoleEx.WriteColoredLine(Discord.LogSeverity.Error, ConsoleTextFormat.TimeAndText, $"[Youtube-DL Error] {e.Data}");
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
            process.Dispose();

            return urls;
        }
    }
}
