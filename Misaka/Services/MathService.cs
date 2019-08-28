using System;
using System.Collections.Generic;
using System.Text;
using Discord.WebSocket;

namespace Misaka.Services
{
    public enum ByteClass
    {
        Kilobyte,
        Megabyte,
        Gigabyte,
        Terabyte
    }

    public enum TimeUnit
    {
        Seconds,
        Minutes,
        Hours,
        Days
    }

    public class MathService : Service
    {
        private int bytesInKB = 1024;
        private int bytesInMB = 1048576;
        private int bytesInGB = 1073741824;
        private long bytesInTB = 1099511627776;
        private Random randObj;

        public MathService(DiscordSocketClient client) : base(client)
        {
        }

        protected override void Run()
        {
            randObj = new Random();
        }

        public int RandomRange(int min, int max)
        {
            return randObj.Next(min, max);

        }

        public double RandomRange(double min, double max)
        {
            return min + (randObj.NextDouble() * (max - min));
        }

        public double ConvertBytes(double bytes, ByteClass typ)
        {
            switch (typ)
            {
                case ByteClass.Kilobyte:
                    return (bytes / bytesInKB);
                case ByteClass.Megabyte:
                    return (bytes / bytesInMB);
                case ByteClass.Gigabyte:
                    return (bytes / bytesInGB);
                case ByteClass.Terabyte:
                    return (bytes / bytesInTB);
                default:
                    return bytes;
            }
        }

        public string BytesToNiceSize(double bytes)
        {
            double megaBytes = ConvertBytes(bytes, ByteClass.Megabyte);
            double gigaBytes = ConvertBytes(bytes, ByteClass.Gigabyte);
            double teraBytes = ConvertBytes(bytes, ByteClass.Terabyte);
            string dbSize = "";
            if (teraBytes >= 1)
                dbSize = $"{teraBytes:F2}TB";
            else if (gigaBytes >= 1)
                dbSize = $"{gigaBytes:F2}GB";
            else if (megaBytes >= 1)
                dbSize = $"{megaBytes:F2}MB";
            else
                dbSize = $"{this.ConvertBytes(bytes, ByteClass.Kilobyte):F2}KB";

            return dbSize;
        }

        public int TimeUnitToMilli(TimeUnit timeUnit, int num)
        {
            switch (timeUnit)
            {
                case TimeUnit.Seconds:
                    return 1000 * num;
                case TimeUnit.Minutes:
                    return (1000 * 60) * num;
                case TimeUnit.Hours:
                    return ((1000 * 60) * 60) * num;
                case TimeUnit.Days:
                    return (((1000 * 60) * 60) * 24) * num;
                default:
                    return 0;
            }
        }
    }
}
