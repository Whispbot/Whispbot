using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Commands.ERLCCommands.Commands.Debug
{
    public class PlayerCount: ERLCCommand
    {
        public override string Name => "Player Count";
        public override string Description => "View the number of players currently in the server";
        public override List<string> Aliases => ["players", "playercount", "plrs"];
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(ERLCCommandContext ctx)
        {
            ERLCServerConfig server = ctx.server;
            await ctx.Reply(server.ingame_players.ToString());
        }
    }
}
