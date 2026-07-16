using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        public static async Task<string?> Softban(Context context)
        {
            IGuildUser? member = await context.Guild!.GetUserAsync(context.TargetUser!.Id);
            if (member is null) return "{string.errors.dm.nomember}";

            await member.BanAsync(7, context.Reason);
            await context.Guild.RemoveBanAsync(context.TargetUser.Id, new RequestOptions { AuditLogReason = "Reverting ban for softban" });

            return null;
        }
    }
}
