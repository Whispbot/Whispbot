using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;
using Whispbot.Tools.Discord;
using YellowMacaroni.Discord.Core;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        public static async Task<string?> Mute(Context context)
        {
            if (context.DurationSeconds > 3600 * 24 * 28)
            {
                return "{string.errors.dm.toolong}";
            }

            if (context.DurationSeconds < 10)
            {
                return "{string.errors.dm.tooshort}";
            }

            Member? member = await context.Guild!.members.Get(context.TargetUser!.id);
            if (member is null) return "{string.errors.dm.nomember}";

            //if (member.communication_disabled_until is not null && DateTimeOffset.Parse(member.communication_disabled_until) > DateTimeOffset.UtcNow)
            //{
            //    return "{string.errors.dm.alreadytimedout}";
            //}

            if (DiscordPermissions.HasPermission(member, Permissions.Administrator))
            {
                return "{string.errors.dm.hasadmin}";
            }

            var error = await member.Timeout(DateTimeOffset.UtcNow + TimeSpan.FromSeconds((double)context.DurationSeconds!), context.Reason);

            if (error is not null)
            {
                return "{string.errors.dm.failed_mute}";
            }

            return null;
        }
    }
}
