using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Staff
{
    public class ViewColor : Command
    {
        public override string Name => "Color";
        public override string Description => "Display a color on an embed";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["color"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            string? arg = ctx.args.FirstOrDefault();
            if (arg is null)
            {
                await ctx.Reply("No color provided.");
                return;
            }

            Color color = new(255, 255, 255)
            {
                Hex = arg
            };

            await ctx.Reply(new MessageBuilder
            {
                embeds = [
                    new EmbedBuilder()
                    .SetColor(color)
                    .SetDescription($"#{color.Hex}\n```cs\nnew Color({color.r}, {color.g}, {color.b})\n```")
                ]
            });
        }
    }
}
