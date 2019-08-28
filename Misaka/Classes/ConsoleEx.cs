using Discord;
using Misaka.Classes;
using Misaka.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Misaka.Classes
{
    public enum ConsoleTextFormat
    {
        Text = 0,
        TimeAndText = 1
    }

    public static class ConsoleEx
    {
        private static ConsoleColor DefaultColor = ConsoleColor.Gray;
        private static Object _lockObj = new Object();

        /*private struct ConsoleWriteInfo
        {
            public LogSeverity? Severity;
            public ConsoleTextFormat? Format;
            public DateTime Time;
            public object[] Args;

            public ConsoleWriteInfo(LogSeverity? severity, ConsoleTextFormat? format, DateTime time, object[] args)
            {
                Severity = severity;
                Format = format;
                Time = time;
                Args = args;
            }
        }*/

        private static ConsoleColor SeverityToColor(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Debug:
                    return ConsoleColor.Gray;
                case LogSeverity.Info:
                case LogSeverity.Verbose:
                    return ConsoleColor.Green;
                case LogSeverity.Warning:
                    return ConsoleColor.DarkYellow;
                case LogSeverity.Error:
                    return ConsoleColor.Red;
                case LogSeverity.Critical:
                    return ConsoleColor.DarkRed;
                default:
                    return ConsoleColor.Gray;
            }
        }

        public static void WriteColoredLine(LogSeverity? severity, ConsoleTextFormat? format, params object[] args)
        {
            lock(_lockObj)
            {
                severity = severity ?? LogSeverity.Info;
                format = format ?? ConsoleTextFormat.TimeAndText;

                Console.ForegroundColor = SeverityToColor(severity.Value);
                Console.Write($"[{Enum.GetName(typeof(LogSeverity), severity).ToUpper()}] ");
                Console.Out.FlushAsync();

                if (format == ConsoleTextFormat.TimeAndText)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"[{DateTime.Now.ToLocalTime().ToString()}] ");
                    Console.Out.FlushAsync();
                }

                Console.ForegroundColor = DefaultColor;

                for (int i = 0; i < args.Length; i++)
                {
                    object arg = args[i];
                    if (arg is ConsoleColor)
                    {
                        ConsoleColor? newColor = arg as ConsoleColor?;
                        if (newColor == null)
                            Console.ForegroundColor = ConsoleColor.Gray;
                        else
                            Console.ForegroundColor = newColor.Value;

                        Console.ForegroundColor = newColor.Value;
                    }
                    else if (arg is string)
                    {
                        string str = arg as string;
                        Regex colorRegex = new Regex(@"\$[[{[A-Za-z]+]]\$"); //Matches colors within $[[Black]]$
                        var matches = colorRegex.Matches(str);
                        if (matches.Count > 0)
                        {
                            string[] words = colorRegex.Split(str);
                            for (int y = 0; y < words.Length; y++)
                            {
                                string word = words[y];
                                Console.Write(words[y]);
                                Console.Out.FlushAsync();

                                if (y < matches.Count)
                                {
                                    string color = matches[y].Value.Replace("$[[", "").Replace("]]$", "");
                                    ConsoleColor parsedColor = (ConsoleColor)Enum.Parse(typeof(ConsoleColor), matches[y].Value.Replace("$[[", "").Replace("]]$", ""), true);
                                    Console.ForegroundColor = parsedColor; 
                                }
                            }
                        }
                        else
                        {
                            Console.Write(str);
                            Console.Out.FlushAsync();
                        }
                        Console.ForegroundColor = DefaultColor;
                    }
                }

                Console.Write("\n");
                Console.Out.FlushAsync();
            }
        }

        public static void WriteColoredLine(params object[] args)
        {
            lock(_lockObj)
            {
                WriteColoredLine(null, null, args);
            }
        }

        public static void AwaitInput(params object[] args)
        {
            WriteColoredLine(LogSeverity.Info, null, args);
            ConsoleKeyInfo keyInfo;
            while( keyInfo.Key != ConsoleKey.Spacebar)
            {
                keyInfo = Console.ReadKey(true);
            }
        }
    }
}
