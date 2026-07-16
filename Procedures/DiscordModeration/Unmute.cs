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
        public static async Task<string?> Unmute(Context context)
        {
            IGuildUser? member = await context.Guild!.GetUserAsync(context.TargetUser!.Id);
            if (member is null) return "{string.errors.dm.nomember}";

            //if (member.communication_disabled_until is null || DateTimeOffset.Parse(member.communication_disabled_until) < DateTimeOffset.UtcNow)
            //{
            //    return "{string.errors.dm.nottimedout}";
            //}

            await member.RemoveTimeOutAsync(new RequestOptions { AuditLogReason = context.Reason });

            return null;
        }
    }
}
