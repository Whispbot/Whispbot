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
        public static async Task<string?> Ban(Context context)
        {
            var config = await WhispCache.GuildConfig.Get(context.Guild!.id);
            var deleteMessages = config?.discord_moderation?.delete_messages_duration_s;

            if (context.DurationSeconds > 3600 * 24 * 365 * 67)
            {
                return "{string.errors.dm.toolongban}";
            }

            if (context.DurationSeconds < 60 && context.DurationSeconds != -1)
            {
                return "{string.errors.dm.tooshortban}";
            }

            var error = await context.Guild.BanUser(context.TargetUser!.id, deleteMessages ?? 0, context.Reason!);

            if (error is not null)
            {
                return "{string.errors.dm.failed}";
            }

            return null;
        }
    }
}
