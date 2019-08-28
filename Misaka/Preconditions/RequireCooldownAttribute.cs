using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Misaka.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Preconditions
{
    public class RequireCooldownAttribute : PreconditionAttribute
    {
        public int Seconds { get; }

        public RequireCooldownAttribute(int seconds)
        {
            Seconds = seconds;
        }

        public override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo cmdInfo, IServiceProvider services)
        {
            CooldownService cooldownService = services.GetService<CooldownService>();
            if (cooldownService.IsCooldownExpired(context.Guild.Id, cmdInfo))
            {
                //cooldownService.SetCooldown(context.Guild.Id, cmdInfo, Seconds);
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            else
                return Task.FromResult(PreconditionResult.FromError($"The command `{cmdInfo.Name}` cannot be used for another {Math.Max(cooldownService.GetRemainingTime(context.Guild.Id, cmdInfo), 1)} seconds!"));
        }
    }
}
