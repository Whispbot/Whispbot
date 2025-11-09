using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Shifts
{
    public class Clockin : Command
    {
        public override string Name => "Clockin";
        public override string Description => "Clock in to the given shift type.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["shift start", "clockin"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.User?.id is null) return;

            if (ctx.GuildId is null) // Make sure ran in server
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.guildonly}.");
                return;
            }

            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.Shifts)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseShifts)) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId); // Fetch shift types from cache

            if (types is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockin.dbfailed}."); // Database failed (does not mean no shift types)
                return;
            }

            ShiftType? type = ctx.args.Count > 0 ? types.Find(t => t.triggers.Contains(ctx.args[0])) : types.Find(t => t.is_default); // Find type based on arg or default if no args

            if (type is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockin.typenotfound}.");
                return;
            }

            (Shift?, string?) result = await Procedures.Clockin(long.Parse(ctx.GuildId), long.Parse(ctx.User.id), type);

            await ctx.Reply(
                new MessageBuilder()
                {
                    embeds = [
                        new EmbedBuilder()
                        {
                            color = result.Item1 is not null ? (int)(new Color(0, 150, 0)) : null,
                            description = $"{(result.Item1 is not null ? "{emoji.clockedin}" : "{emoji.cross}")} {result.Item2 ?? (result.Item1 is null ? "{string.errors.clockin.failed}" : $"{{string.success.clockin}} '{type.name}'")}.",
                            footer = result.Item1 is not null ? new EmbedFooter() { text = $"ID: {result.Item1.id}" } : null
                        }
                    ]
                }
            );
        }
    }
}
