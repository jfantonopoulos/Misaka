using Discord;
using Discord.Commands;
using Misaka.Classes;
using Misaka.Extensions;
using Misaka.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Misaka.Modules
{
    [Name("HelpModule")]
    public class HelpModule : MisakaModuleBase
    {
        private CommandService Commands;
        private MathService MathService;
        private EmbedService EmbedService;

        public HelpModule(CommandService commands, EmbedService embedService, MathService mathService) : base(mathService)
        {
            Commands = commands;
            MathService = mathService;
            EmbedService = embedService;
        }

        [Command("help"), Summary("Lists all of the commands within the modules.")]
        public async Task Help()
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithColor(new Color(100, 175, 45));
            embedBuilder.WithTitle(":computer: Modules & Commands");
            embedBuilder.Description = "Here's a list of the modules and their commands.\nFor more detailed help, **visit**: http://jeezy.click\nThis message will expire in a minute.\n\n";
            var modules = Commands.Modules;
            int cmdCount = 0;
            foreach (var module in modules)
            {
                string moduleStr = $"{module.Name.Replace("Module", " Module").Bold()}: ";
                foreach(var cmd in module.Commands.GroupBy(x => x.Aliases[0]).Select(x => x.First()))
                {
                    cmdCount++;
                    moduleStr += $"{cmd.Name}, ";
                }
                moduleStr = moduleStr.Substring(0, moduleStr.Length - 2);
                moduleStr += "\n";
                if (module.Name != "MisakaModuleBase")
                    embedBuilder.AppendEmbedDescription(moduleStr);
            }

            embedBuilder.WithFooter(x =>
            {
                x.Text = $"{Commands.Modules.Where(y => y.Name != "MisakaModuleBase").Count()} Modules, {cmdCount} Commands.";
            });

            await ReplyAsync("", embed: embedBuilder.Build(), lifeTime: MathService.TimeUnitToMilli(TimeUnit.Minutes, 1));
        }

        [Command("syntax"), Summary("Shows the syntax for the specified command.")]
        public async Task Syntax([Summary("The command name.")] CommandInfo cmdInfo)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            EmbedService.BuildSuccessEmbed(embedBuilder);
            embedBuilder.Title = $"->{cmdInfo.Name.Code()} Command Syntax";
            embedBuilder.Description = cmdInfo.Summary.Code();
            foreach (var arg in cmdInfo.Parameters)
            {
                embedBuilder.AddField(x =>
                {
                    x.Name = $":paperclip: {arg.Name} - {arg.Type.ToString().Bold()}";
                    x.Value = arg.Summary;
                });
            }

            await ReplyAsync("", embed: embedBuilder);
        }
    }
}
