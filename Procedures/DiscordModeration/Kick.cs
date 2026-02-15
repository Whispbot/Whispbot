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
        public static async Task<string?> Kick(Context context)
        {
            Member? member = await context.Guild!.members.Get(context.TargetUser!.id);
            if (member is null) return "{string.errors.dm.nomember}";

            var error = await member.Kick(context.Reason);

            if (error is not null)
            {
                return "{string.errors.dm.failed}";
            }

            return null;
        }
    }
}
