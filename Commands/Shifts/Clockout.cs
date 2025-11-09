using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Extensions;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;

namespace Whispbot.Commands.Shifts
{
    public class Clockout : Command
    {
        public override string Name => "Clockout";
        public override string Description => "Clock out of the given shift type.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["shift end", "clockout"];
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

            if (types.Count == 0) // Not clocked in as no shift types exist
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockout.notalready}.");
            }

            ShiftType? type = ctx.args.Count > 0 ? types.Find(t => t.triggers.Contains(ctx.args[0])) : types.Find(t => t.is_default); // Find type based on arg or default if no args

            if (type is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockin.typenotfound}.");
                return;
            }

            (Shift?, string?) result = await Procedures.Clockout(long.Parse(ctx.GuildId), long.Parse(ctx.User.id), type);

            await ctx.Reply(
                new MessageBuilder()
                {
                    embeds = [
                        new EmbedBuilder()
                        {
                            color = result.Item1 is not null ? (int)(new Color(150, 0, 0)) : null,
                            description = $"{(result.Item1 is not null ? "{emoji.clockedout}" : "{emoji.cross}")} {result.Item2 ?? (result.Item1 is null ? "{string.errors.clockout.failed}" : $"{{string.success.clockout}}".Process(ctx.Language, new Dictionary<string, string> { { "type_name", type.name }, { "duration", Time.ConvertMillisecondsToString((result.Item1.end_time - result.Item1.start_time)?.TotalMilliseconds ?? 0) } }))}.",
                            footer = result.Item1 is not null ? new EmbedFooter() { text = $"ID: {result.Item1.id}" } : null
                        }
                    ]
                }
            );
        }
    }
}
