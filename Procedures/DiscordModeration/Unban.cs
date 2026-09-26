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
        public static async Task<string?> Unban(Context context)
        {
            await context.Guild!.RemoveBanAsync(context.TargetUser!.Id, new RequestOptions { AuditLogReason = context.Reason! });

            return null;
        }
    }
}
