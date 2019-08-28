using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Preconditions
{
    class RequireOSAttribute : PreconditionAttribute
    {
        public string PlatformName { get; }

        public RequireOSAttribute(string platform)
        {
            PlatformName = platform;
        }

        public override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            switch (PlatformName)
            {
                case "Windows":
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return Task.FromResult(PreconditionResult.FromSuccess());
                    }
                    break;
                case "Linux":
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        return Task.FromResult(PreconditionResult.FromSuccess());
                    }
                    break;
                case "OSX":
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        return Task.FromResult(PreconditionResult.FromSuccess());
                    }
                    break;
            }

            return Task.FromResult(PreconditionResult.FromError($"You can only use this command on {PlatformName}!"));
        }
    }
}
