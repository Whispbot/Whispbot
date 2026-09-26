using Discord;
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
    public class Clockin : Command
    {
        public override string Name => "Clockin";
        public override string Description => "Clock in to the given shift type.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["shift", "start"];
        public override List<SlashCommandArg>? Arguments => [
            new ("type", "The shift type to clock in for. If not provided, the default will be used.", CommandArgType.ShiftType, optional: true)
        ];
        public override List<string> Schema => ["<type:stype?>"];
        public override List<string> Aliases => ["shift start", "clockin"];
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

            string? typeArg = ctx.args.Get("type")?.GetString();
            ShiftType? type = typeArg is not null ? types.Find(t => t.triggers.Contains(typeArg) || t.id.ToString() == typeArg) : types.Find(t => t.is_default); // Find type based on arg or default if no args

            if (type is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.type_not_found")}");
                return;
            }

            (Shift?, string?) result = await Procedures.Clockin(ctx.GuildId, ctx.UserId, type);

            await ctx.Reply(
                embed: new EmbedBuilder()
                    .WithDescription(
                        $"{ctx.Emoji(result.Item1 is not null ? "clockedin" : "cross")} " +
                        $"{(result.Item1 is null ?
                            ctx.String(result.Item2 is not null ? $"shifts.clockin.errors.{result.Item2}" : "shifts.clockin.errors.failed") :
                            ctx.String("shifts.clockin.success", type.name))}"
                    )
                    .WithFooter(result.Item1 is not null ? new EmbedFooterBuilder().WithText($"ID: {result.Item1.id}") : null)
                    .WithColor(result.Item1 is not null ? new Color(0, 150, 0) : Color.Default)
                    .Build()
            );
        }
    }
}

