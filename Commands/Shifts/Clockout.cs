using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Extensions;
using Whispbot.Languages;
using Whispbot.Tools;

namespace Whispbot.Commands.Shifts
{
    public class Clockout : Command
    {
        public override string Name => "Clockout";
        public override string Description => "Clock out of the given shift type.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["shift", "end"];
        public override List<SlashCommandArg>? Arguments => [
            new ("type", "The shift type to clock out from. If not provided, the default will be used.", CommandArgType.ShiftType, optional: true)
        ];
        public override List<string> Schema => ["<type:stype?>"];
        public override List<string> Aliases => ["shift end", "clockout"];
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

            if (types.Count == 0) // Not clocked in as no shift types exist
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("shifts.clockout.errors.not_on_shift")}."); // User is not clocked in
            }

            string? typeArg = ctx.args.Get("type")?.GetString();
            ShiftType? type = typeArg is not null ? types.Find(t => t.triggers.Contains(typeArg) || t.id.ToString() == typeArg) : types.Find(t => t.is_default); // Find type based on arg or default if no args

            if (type is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.type_not_found")}");
                return;
            }

            (Shift?, string?) result = await Procedures.Clockout(ctx.GuildId, ctx.UserId, type);

            await ctx.Reply(
                embed: new EmbedBuilder()
                    .WithDescription(
                        $"{ctx.Emoji(result.Item1 is not null ? "clockedout" : "cross")} " +
                        $"{(result.Item1 is null ? 
                            ctx.String(result.Item2 is not null ? $"shifts.clockout.errors.{result.Item2}" : "shifts.clockout.errors.failed") : 
                            ctx.String("shifts.clockout.success", type.name, Time.ConvertMillisecondsToString(
                                (result.Item1.end_time - result.Item1.start_time)?.TotalMilliseconds ?? 0,
                                RoundTo: 60_000,
                                language: ctx.Language
                        )))}"
                    )
                    .WithFooter(result.Item1 is not null ? new EmbedFooterBuilder().WithText($"ID: {result.Item1.id}") : null)
                    .WithColor(result.Item1 is not null ? new Color(150, 0, 0) : Color.Default)
                    .Build()
            );
        }
    }
}

