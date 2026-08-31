using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;
using Whispbot.Tools.Bot;

namespace Whispbot.Commands.Staff
{
    public class Test: Command
    {
        public override string Name => "Test";
        public override string Description => "A test command for staff.";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => null;
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => [];
        public override List<string> Aliases => ["test"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            var robloxUser = await Roblox.GetUserById("221782955");

            await ctx.Reply($"{robloxUser?.CreateTime?.ToUnixTimeSeconds() ?? 0}\n```json\n{JsonConvert.SerializeObject(robloxUser, Formatting.Indented)}\n```");
        }
    }
}

