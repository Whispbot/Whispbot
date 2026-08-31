using Discord;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;

namespace Whispbot.Interactions.Shifts
{
    public class AdminModify : InteractionCommandData
    {
        public override string CustomId => "sa_modify";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId.Value);
            if (types is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.failed_get_shift_data")}");
                return;
            }

            ShiftType? type = types.Find(t => ctx.args.Count >= 3 && t.id.ToString() == ctx.args[2]);
            if (type is null && ctx.args.Count > 2)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.type_not_found")}");
                return;
            }

            string userId = ctx.args[1];

            List<Shift>? shifts = Postgres.Select<Shift>(
                @$"SELECT *
                FROM shifts
                WHERE moderator_id = @1 AND guild_id = @2 AND end_time IS NOT NULL {(type is not null ? "AND type = @3" : "")}
                ORDER BY start_time DESC
                LIMIT 25;",
                [long.Parse(userId), ctx.GuildId.Value, ..(type is not null ? new List<ulong> { type.id } : [])]
            );

            if (shifts is null)
            {
                await ctx.Respond($"{ctx.Emoji("warning")} {ctx.String("errors.dbfailed")}");
                return;
            }

            var modal = new ModalBuilder()
                .WithCustomId($"sa_modify2 {ctx.UserId} {userId} {type?.id}")
                .WithTitle("Modify Shift")
                .AddSelectMenu(
                    label: "Select a Recent Shift",
                    description: "Shifts that are currently in progress must be ended before being eligible to be edited.",
                    customId: "recent_shift",
                    options: [.. shifts.Where(s => s.end_time is not null).Select(s => {
                        DateTimeOffset dto = s.end_time!.Value;
                        TimeSpan fromNow = DateTimeOffset.UtcNow - dto;

                        return new SelectMenuOptionBuilder(
                            label: $"Shift from {Tools.Time.ConvertMillisecondsToString(fromNow.TotalMilliseconds, ", ", true, 60000)} ago",
                            value: $"{s.id}",
                            description: $"ID: {s.id}{(type is null ? $" | Type: {types.Find(t => t.id == s.type)?.name ?? "unknown"}" : "")} | Duration: {Tools.Time.ConvertMillisecondsToString((s.end_time! - s.start_time).Value.TotalMilliseconds, ", ", true, 60000)}",
                            isDefault: shifts.IndexOf(s) == 0
                        );
                    })],
                    required: false
                )
                .AddTextInput(
                    label: "Or Enter Shift ID",
                    customId: "shift_id",
                    required: false,
                    minLength: 19,
                    maxLength: 21
                )
                .Build();

            await ctx.ShowModal(modal);
        }
    }
}
