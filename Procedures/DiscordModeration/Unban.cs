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
        public static async Task<string?> Unban(Context context)
        {
            var error = await context.Guild!.UnbanUser(context.TargetUser!.id, context.Reason!);

            if (error is not null)
            {
                return "{string.errors.dm.failed}";
            }

            return null;
        }
    }
}
