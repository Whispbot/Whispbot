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
    public class ShiftManage : Command
    {
        public override string Name => "Shifts";
        public override string Description => "View information about your shifts.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["shifts", "shift", "shift manage"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.UserId is null) return;

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

            ShiftType? type = ctx.args.Count > 0 ? types.Find(t => t.triggers.Contains(ctx.args[0])) : null;

            if (ctx.args.Count > 0 && type is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockin.typenotfound}.");
                return;
            }

            ShiftsData? data = ShiftsData.Get(long.Parse(ctx.UserId), long.Parse(ctx.GuildId), type);

            if (data is null)
            {
                await ctx.Reply("{emoji.warning} {string.errors.shifts.dbfailed}");
                return;
            }

            await ctx.Reply(
                data.generateMessage(ctx.UserId!, type)
            );
        }
    }

    public class ShiftsData
    {
        public int totalCount;
        public double totalDuration;
        public int weeklyCount;
        public double weeklyDuration;
        public DateTimeOffset? currentShiftStart;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status">false for none, true for just clocked out</param>
        /// <returns></returns>
        public MessageBuilder generateMessage(string userId, ShiftType? type = null, bool status = false, Shift? shift = null) {
            return new MessageBuilder
            {
                components = [
                    new ContainerBuilder
                    {
                        components = [
                            new TextDisplayBuilder("## {string.title.shift}"),
                            ..(currentShiftStart is not null ? [
                                new TextDisplayBuilder($"{{emoji.clockedin}} {{string.content.shift.clockedin}} <t:{currentShiftStart.Value.ToUnixTimeSeconds()}:R>."),
                                new Seperator()
                            ] : status && shift?.end_time is not null ? new List<Component> {
                                new TextDisplayBuilder($"{{emoji.clockedout}} {{string.content.shift.clockedout}} {Time.ConvertMillisecondsToString((shift.end_time - shift.start_time).Value.TotalMilliseconds)}."),
                                new Seperator()
                            } : []),
                            new TextDisplayBuilder($"{{string.title.shift.alltime}}: {totalCount} ({Time.ConvertMillisecondsToString(totalDuration * 1000, ", ", true, 60000)})\n{{string.title.shift.weekly}}: {weeklyCount} ({Time.ConvertMillisecondsToString(weeklyDuration * 1000, ", ", true, 60000)})"),
                            new TextDisplayBuilder($"-# Type: {type?.name ?? "all"}")
                        ],
                        accent_color = (status ? new Color(150, 0, 0) : currentShiftStart is not null ? new Color(0, 150, 0) : null)?.ToInt(),
                    },
                    new ActionRowBuilder
                    {
                        components = [
                            new ButtonBuilder
                            {
                                label = "{string.button.shift.clockin}",
                                style = ButtonStyle.Success,
                                custom_id = $"clockin {userId} {type?.id}",
                                disabled = currentShiftStart is not null
                            },
                            new ButtonBuilder
                            {
                                label = "{string.button.shift.clockout}",
                                style = ButtonStyle.Danger,
                                custom_id = $"clockout {userId} {type?.id}",
                                disabled = currentShiftStart is null
                            }
                        ]
                    }
                ],
                flags = MessageFlags.IsComponentsV2
            };
        }

        public static ShiftsData? Get(long userid, long guildid, ShiftType? type = null)
        {
            return Postgres.SelectFirst<ShiftsData>(
                @"
                    SELECT
                        COUNT(*) AS totalCount,
                        COALESCE(SUM(EXTRACT(EPOCH FROM (COALESCE(end_time, now()) - start_time))), 0) AS totalDuration,
                        COUNT(CASE WHEN start_time >= now() - INTERVAL '7 days' THEN 1 END) AS weeklyCount,
                        COALESCE(SUM(CASE WHEN start_time >= now() - INTERVAL '7 days' THEN EXTRACT(EPOCH FROM (COALESCE(end_time, now()) - start_time)) END), 0) AS weeklyDuration,
                        CASE WHEN EXISTS (
                            SELECT 1 FROM shifts
                            WHERE moderator_id = @1 AND guild_id = @2" + (type is not null ? " AND type = @3" : "") + @" AND end_time IS NULL
                        ) THEN (
                            SELECT start_time FROM shifts
                            WHERE moderator_id = @1 AND guild_id = @2" + (type is not null ? " AND type = @3" : "") + @" AND end_time IS NULL
                            LIMIT 1
                        ) ELSE NULL END AS currentShiftStart
                    FROM shifts
                    WHERE moderator_id = @1 AND guild_id = @2" + (type is not null ? " AND type = @3" : ""),
                [userid, guildid, .. (type is not null ? new long[] { type.id } : [])]
            );
        }
    }
}
