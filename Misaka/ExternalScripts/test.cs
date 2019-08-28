using System;
using Discord;
using Discord.Commands;
using Misaka.Services;
using System.Runtime;
using System.Threading.Tasks;

namespace Misaka.Modules
{
    public class TestModule : ModuleBase<SocketCommandContext>
    {
        private MathService MathService;
        public TestModule(MathService mathService)
        {
            MathService = mathService;
        }

        [Command("test"), Summary("a test command")]
        public async Task Test([Summary("max int")] int max)
        {
            await ReplyAsync(MathService.RandomRange(0, max).ToString());
        }
    };
}
