using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Misaka.Classes
{
    //I wanted to avoid static methods completely, but in some cases I would be forced to rewrite code that already exists.
    //For example, before dependency injection is initiated, or areas I cannot access the IServiceProvider easily.
    public static class StaticMethods
    {
        public static string GetOS()
        {
            var osName = "Windows ";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                osName = "Linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                osName = "OSX ";

            return osName;
        }

        public static string GetLibraryVersion()
        {
            return typeof(DiscordSocketClient).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        }

        public static string GetArchitecture()
        {
            return Enum.GetName(typeof(Architecture), RuntimeInformation.OSArchitecture);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase GetCurrentMethod()
        {
            StackTrace stackTrace = new StackTrace(null, false);
            StackFrame stackFrame = stackTrace.GetFrames()[1];

            return stackFrame.GetMethod();
        }

        public static ConsoleColor ConsoleColorFromName(string name)
        {
            foreach(ConsoleColor color in Enum.GetValues(typeof(ConsoleColor)))
            {
                string enumName = Enum.GetName(typeof(ConsoleColor), color);
                if (enumName.ToLower() == name.ToLower())
                    return color;
            }

            return ConsoleColor.Black;
        }

        public static void FireAndForget(Action action)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(x =>
            {
                try
                {
                    action();
                }
                catch(Exception ex)
                {
                    ConsoleEx.WriteColoredLine(Discord.LogSeverity.Warning, null, $"$[[Red]]$Threaded action $[[White]]$[{action.GetMethodInfo().Name}]$[[Red]]$ has thrown an exception.\n$[[DarkRed]]${ex.Message}");
                }
                
            }));
        }

        public static bool IsUriInvalid(string url)
        {
            bool res = (Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme.ToLower() == "http" || uriResult.Scheme.ToLower() == "https"));
            return (!res);
        }
    }
}
