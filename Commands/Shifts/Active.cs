using Serilog;
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
    public class ShiftActive : Command
    {
        public override string Name => "Shift Active";
        public override string Description => "View the users currently on shift.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["shift active", "onduty", "od", "shift onduty", "shift od"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.UserId is null) return;

            if (ctx.GuildId is null || ctx.Guild is null)
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

            List<Shift>? activeShifts = Postgres.Select<Shift>(
                "SELECT * FROM shifts WHERE guild_id = @1 AND end_time IS NULL ORDER BY start_time;",
                [long.Parse(ctx.GuildId)]
            );

            if (activeShifts is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockin.dbfailed}."); // Database failed
                return;
            }

            await ctx.Reply(
                new MessageBuilder
                {
                    embeds = [
                        new EmbedBuilder
                        {
                            title = $"{{string.title.shiftactive}} ({activeShifts.Count})",
                            description = activeShifts.Count == 0 ? "{string.errors.shiftactive.nousersonshift}." : null,
                            fields = [
                                ..activeShifts.GroupBy(s => s.type).Select(g => {
                                    StringBuilder sb = new();

                                    foreach (var shift in g)
                                    {
                                        sb.AppendLine($"> <@{shift.moderator_id}> - {Time.ConvertMillisecondsToString((DateTimeOffset.UtcNow - shift.start_time).TotalMilliseconds, ", ", true, 60000)}");
                                    }

                                    return new EmbedField {
                                        name = $"{types.Find(t => t.id == g.Key)?.name ?? "Unknown Type"} [{g.Count()}]",
                                        value = sb.ToString()
                                    };
                                })
                            ]
                        }
                    ]
                }
            );
        }
    }
}
