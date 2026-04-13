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
        public static async Task<string?> Softban(Context context)
        {
            Member? member = await context.Guild!.members.Get(context.TargetUser!.id);
            if (member is null) return "{string.errors.dm.nomember}";

            var error = await member.Ban(604800, context.Reason);

            if (error is not null)
            {
                return "{string.errors.dm.failed}";
            }

            var attempt = 0;
            while (attempt < 3)
            {
                attempt++;

                var unbanError = await context.Guild.UnbanUser(context.TargetUser.id, "Reverting ban for softban");
                if (unbanError is null)
                {
                    break;
                }

                if (attempt >= 3)
                {
                    return "{string.errors.dm.failedunban}";
                }

                Thread.Sleep(attempt * 1000);
            }

            return null;
        }
    }
}
