using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.TypeReaders
{
    class CommandInfoTypeReader : TypeReader
    {
        private CommandService CommandService;

        public CommandInfoTypeReader(CommandService commandService)
        {
            this.CommandService = commandService; 
        }

        public override Task<TypeReaderResult> Read(ICommandContext context, string input, IServiceProvider services)
        {
            IEnumerable<CommandInfo> commands = CommandService.Commands;
            CommandInfo commandInfo = commands.FirstOrDefault(x => x.Name.ToLower() == input.ToLower() || x.Aliases.FirstOrDefault(y => y.ToLower() == input.ToLower()) != null);
            if (commandInfo != null)
                return Task.FromResult(TypeReaderResult.FromSuccess(commandInfo));
            else
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Failed to locate the specified command"));
        }
    }
}
