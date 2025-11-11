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
    public class ShiftLeaderboard : Command
    {
        public override string Name => "Shift Leaderboard";
        public override string Description => "View the shift leaderboard.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["shift leaderboard"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.UserId is null) return;

            if (ctx.GuildId is null)
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

            var (message, errormessage) = await Procedures.GenerateShiftLeaderboard(ctx.GuildId, ctx.UserId, 1, type?.id);

            if (errormessage is not null)
            {
                await ctx.Reply(errormessage);
            }
            else if (message is not null)
            {
                await ctx.Reply(message);
            }
        }
    }
}
