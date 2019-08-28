using Discord.Commands;
using Misaka.Classes;
using Misaka.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.TypeReaders
{
    class ModuleTypeReader : TypeReader
    {
        private CommandService Commands;
        private CommandHandler Handler;

        public ModuleTypeReader(CommandService commands, CommandHandler handler)
        {
            Commands = commands;
            Handler = handler;
        }

        public override Task<TypeReaderResult> Read(ICommandContext context, string input, IServiceProvider services)
        {
            Type moduleType = Handler.FindModuleByName(input);

            if (moduleType == null)
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ObjectNotFound, "Specified Module not found."));
            else
                return Task.FromResult(TypeReaderResult.FromSuccess(moduleType));
        }
    }
}
