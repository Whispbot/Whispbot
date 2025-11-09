using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions.Shifts
{
    public class AdminModify : InteractionData
    {
        public override string CustomId => "sa_modify";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId);
            if (types is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.dbfailed}");
                return;
            }

            ShiftType? type = types.Find(t => ctx.args.Count >= 3 && t.id.ToString() == ctx.args[2]);
            if (type is null && ctx.args.Count > 2)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.typenotfound}");
                return;
            }

            string userId = ctx.args[1];

            List<Shift>? shifts = Postgres.Select<Shift>(
                @$"SELECT *
                FROM shifts
                WHERE moderator_id = @1 AND guild_id = @2 AND end_time IS NOT NULL {(type is not null ? "AND type = @3" : "")}
                ORDER BY start_time DESC
                LIMIT 25;",
                [long.Parse(userId), long.Parse(ctx.GuildId), ..(type is not null ? new List<long> { type.id } : [])]
            );

            if (shifts is null)
            {
                await ctx.Respond("{emoji.warning} {string.errors.general.dbfailed}");
                return;
            }

            ModalBuilder modal = new()
            {
                custom_id = $"sa_modify2 {ctx.UserId} {userId} {type?.id}",
                title = "Modify Shift",
                components = [
                    new LabelBuilder
                    {
                        label = "Select a Recent Shift",
                        description = "Shifts that are currently in progress must be ended before being eligible to be edited.",
                        component = new StringSelectBuilder("recent_shift")
                        {
                            options = [..shifts?.Where(
                                s => s.end_time is not null
                            ).ForAll(s => {
                                DateTimeOffset dto = s.end_time!.Value;
                                TimeSpan fromNow = DateTimeOffset.UtcNow - dto;

                                return new StringSelectOption
                                {
                                    @default = shifts.IndexOf(s) == 0,
                                    value = $"{s.id}",
                                    label = $"Shift from {Tools.Time.ConvertMillisecondsToString(fromNow.TotalMilliseconds, ", ", true, 60000)} ago",
                                    description = $"ID: {s.id}{(type is null ? $" | Type: {types.Find(t => t.id == s.type)?.name ?? "unknown"}" : "")} | Duration: {Tools.Time.ConvertMillisecondsToString((s.end_time! - s.start_time).Value.TotalMilliseconds, ", ", true, 60000)}"
                                };
                            }) ?? []],
                            required = false
                        }
                    },
                    new ActionRowBuilder(
                        new TextInputBuilder("Or Enter Shift ID")
                        {
                            custom_id = "shift_id",
                            required = false,
                            min_length = 19,
                            max_length = 21
                        }
                    )
                ]
            };

            await ctx.ShowModal(modal);
        }
    }
}
