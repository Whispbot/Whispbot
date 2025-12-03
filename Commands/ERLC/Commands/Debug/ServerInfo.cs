using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Commands.ERLC.Commands.Debug
{
    public class ServerInfo: ERLCCommand
    {
        public override string Name => "ER:LC Server Info";
        public override string Description => "View information about the server";
        public override List<string> Aliases => ["serverinfo", "aboutserver", "server"];
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(ERLCCommandContext ctx)
        {
            ERLCServerConfig server = ctx.server;
            await ctx.Reply($"{{string.content.erlccommand.serverinfo:name={server.name},code={server.code},players={server.ingame_players-1}}}");
        }
    }
}
