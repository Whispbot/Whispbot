using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Tools;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        public static async Task<string?> Ban(Context context)
        {
            var config = await WhispCache.GuildConfig.Get(context.Guild!.Id);
            var deleteMessages = config?.discord_moderation?.delete_messages_duration_s;

            if (context.DurationSeconds > 3600 * 24 * 365 * 67)
            {
                return Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "dmod.errors.ban_duration_too_long");
            }

            if (context.DurationSeconds < 60 && context.DurationSeconds != -1)
            {
                return Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "dmod.errors.ban_duration_too_short");
            }

            await context.Guild.BanUserAsync(context.TargetUser, (uint)(deleteMessages ?? 0), new RequestOptions { AuditLogReason = context.Reason! });

            return null;
        }
    }
}
