using Discord;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Commands.Staff
{
    public class ViewColor : Command
    {
        public override string Name => "Color";
        public override string Description => "Display a color on an embed";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => null;
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => ["<color:string>"];
        public override List<string> Aliases => ["color"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            string? arg = ctx.args.Get("color")?.GetString();
            if (arg is null || arg.Length != 6)
            {
                await ctx.Reply("Invalid color format. Please provide a 6-character hexadecimal color code.");
                return;
            }

            byte r = byte.Parse(arg[..2],            System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(arg.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(arg.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            Color color = new(r, g, b);

            await ctx.Reply(
                embed: new EmbedBuilder()
                    .WithColor(color)
                    .WithDescription($"#{arg.ToUpper()}\n```cs\nnew Color({r}, {g}, {b})\n```")
                    .Build()
            );
        }
    }
}

