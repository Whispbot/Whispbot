using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Staff
{
    public class Test: Command
    {
        public override string Name => "Test";
        public override string Description => "A test command for staff.";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["test"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            string? guildId = ctx.message.channel?.guild_id;

            if (guildId is null)
            {
                await ctx.Reply("This command can only be used in a server.");
                return;
            }

            await Tools.Google.Search("What is the uk minimum wage?");
        }
    }
}
