using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;
using Whispbot.Tools.Disc;

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

            IGuildUser? member = await context.Guild!.GetUserAsync(context.TargetUser!.Id);
            if (member is null) return "{string.errors.dm.nomember}";

            //if (member.communication_disabled_until is not null && DateTimeOffset.Parse(member.communication_disabled_until) > DateTimeOffset.UtcNow)
            //{
            //    return "{string.errors.dm.alreadytimedout}";
            //}

            if (DiscordPermissions.HasPermission(member,  GuildPermission.Administrator))
            {
                return "{string.errors.dm.hasadmin}";
            }

             await member.SetTimeOutAsync(TimeSpan.FromSeconds((double)context.DurationSeconds!), new RequestOptions { AuditLogReason = context.Reason });

            return null;
        }
    }
}
