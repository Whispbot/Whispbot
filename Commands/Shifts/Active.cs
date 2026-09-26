using Discord;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Databases;
using Whispbot.Tools;

namespace Whispbot.Commands.Shifts
{
    public class ShiftActive : Command
    {
        public override string Name => "Shift Active";
        public override string Description => "View the users currently on shift.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["shift", "active"];
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => [];
        public override List<string> Aliases => ["shift active", "onduty", "od", "shift onduty", "shift od"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.Shifts)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseShifts)) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId); // Fetch shift types from cache

            if (types is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.failed_get_types")}"); // Database failed (does not mean no shift types)
                return;
            }

            List<Shift>? activeShifts = Postgres.Select<Shift>(
                "SELECT * FROM shifts WHERE guild_id = @1 AND end_time IS NULL ORDER BY start_time;",
                [ctx.GuildId]
            );

            if (activeShifts is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("shifts.active.errors.failed")}"); // Database failed
                return;
            }

            await ctx.Reply(
                embed: new EmbedBuilder()
                    .WithTitle($"{ctx.String("shifts.active.title")} ({activeShifts.Count})")
                    .WithDescription(activeShifts.Count == 0 ? $"{ctx.String("shifts.active.errors.none")}." : null)
                    .WithFields(
                        activeShifts.GroupBy(s => s.type).Select(g =>
                        {
                            StringBuilder sb = new();
                            foreach (var shift in g)
                            {
                                sb.AppendLine($"> <@{shift.moderator_id}> - {Time.ConvertMillisecondsToString((DateTimeOffset.UtcNow - shift.start_time).TotalMilliseconds, ", ", true, 60000, ctx.Language)}");
                            }
                            return new EmbedFieldBuilder
                            {
                                Name = $"{types.Find(t => t.id == g.Key)?.name ?? "Unknown Type"} [{g.Count()}]",
                                Value = sb.ToString()
                            };
                        })
                    )
                    .Build()
            );
        }
    }
}

