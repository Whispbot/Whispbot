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
    public class ShiftManage : Command
    {
        public override string Name => "Shifts";
        public override string Description => "View information about your shifts.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["shift", "manage"];
        public override List<SlashCommandArg>? Arguments => [
            new ("type", "The shift type to view. If not provided, all types will be shown.", CommandArgType.ShiftType, optional: true)
        ];
        public override List<string> Schema => ["<type:stype?>"];
        public override List<string> Aliases => ["shifts", "shift", "shift manage"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.Shifts)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseShifts)) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId); // Fetch shift types from cache

            if (types is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockin.dbfailed}."); // Database failed (does not mean no shift types)
                return;
            }

            string? typeArg = ctx.args.Get("type")?.GetString();
            ShiftType? type = typeArg is not null ? types.Find(t => t.triggers.Contains(typeArg) || t.id.ToString() == typeArg) : null;

            if (ctx.args.Count > 0 && type is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockin.typenotfound}.");
                return;
            }

            ShiftsData? data = ShiftsData.Get(ctx.UserId, ctx.GuildId, type);

            if (data is null)
            {
                await ctx.Reply("{emoji.warning} {string.errors.shifts.dbfailed}");
                return;
            }

            await ctx.Reply(
                components: data.GenerateMessage(ctx.UserId, type),
                flags: MessageFlags.ComponentsV2
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
        public MessageComponent GenerateMessage(ulong userId, ShiftType? type = null, bool status = false, Shift? shift = null) {
            return new ComponentBuilderV2()
                .WithContainer(
                    new ContainerBuilder()
                        .WithTextDisplay($"## {{string.title.shift}}")
                        .WithTextDisplay(
                            currentShiftStart is not null ? $"{{emoji.clockedin}} {{string.content.shift.clockedin}} <t:{currentShiftStart.Value.ToUnixTimeSeconds()}:R>." 
                            : status && shift?.end_time is not null ? $"{{emoji.clockedout}} {{string.content.shift.clockedout}} {Time.ConvertMillisecondsToString((shift.end_time - shift.start_time).Value.TotalMilliseconds)}." : ""
                        )
                        .WithSeparator()
                        .WithTextDisplay($"{{string.title.shift.alltime}}: {totalCount} ({Time.ConvertMillisecondsToString(totalDuration * 1000, ", ", true, 60000)})\n{{string.title.shift.weekly}}: {weeklyCount} ({Time.ConvertMillisecondsToString(weeklyDuration * 1000, ", ", true, 60000)})")
                        .WithTextDisplay($"-# Type: {type?.name ?? "all"}")
                        .WithAccentColor(status ? new Color(150, 0, 0) : currentShiftStart is not null ? new Color(0, 150, 0) : null)
                )
                .WithActionRow(
                    new ActionRowBuilder()
                        .WithButton("{string.button.shift.clockin}", $"clockin {userId} {type?.id}", ButtonStyle.Success, disabled: currentShiftStart is not null)
                        .WithButton("{string.button.shift.clockout}", $"clockout {userId} {type?.id}", ButtonStyle.Danger, disabled: currentShiftStart is null)
                )
                .Build();
        }

        public static ShiftsData? Get(ulong userid, ulong guildid, ShiftType? type = null)
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
                [userid, guildid, .. (type is not null ? new ulong[] { type.id } : [])]
            );
        }
    }
}

