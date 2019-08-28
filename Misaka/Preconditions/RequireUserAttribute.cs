using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Preconditions
{
    class RequireUserAttribute : PreconditionAttribute
    {
        public ulong UserId { get; }
        public string ErrorMessage { get; }

        public RequireUserAttribute(ulong userId)
        {
            UserId = userId;
            ErrorMessage = "You cannot execute this command!";
        }

        public RequireUserAttribute(ulong userId, string errorMessage)
        {
            UserId = userId;
            ErrorMessage = errorMessage;
        }

        public override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.User.Id == UserId)
                return Task.FromResult(PreconditionResult.FromSuccess());
            else
                return Task.FromResult(PreconditionResult.FromError(ErrorMessage));
        }
    }
}
