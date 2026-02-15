using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        public static async Task<string?> Unmute(Context context)
        {
            Member? member = await context.Guild!.members.Get(context.TargetUser!.id);
            if (member is null) return "{string.errors.dm.nomember}";

            //if (member.communication_disabled_until is null || DateTimeOffset.Parse(member.communication_disabled_until) < DateTimeOffset.UtcNow)
            //{
            //    return "{string.errors.dm.nottimedout}";
            //}

            var error = await member.RemoveTimeout(context.Reason);

            if (error is not null)
            {
                return "{string.errors.dm.failed_unmute}";
            }

            return null;
        }
    }
}
