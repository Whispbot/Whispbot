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
                return Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "dmod.errors.duration_too_long");
            }

            if (context.DurationSeconds < 10)
            {
                return Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "dmod.errors.duration_too_short");
            }

            IGuildUser? member = await context.Guild!.GetUserAsync(context.TargetUser!.Id);
            if (member is null) return Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "dmod.errors.no_member");

            //if (member.communication_disabled_until is not null && DateTimeOffset.Parse(member.communication_disabled_until) > DateTimeOffset.UtcNow)
            //{
            //    return Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "dmod.errors.already_timed_out");
            //}

            if (DiscordPermissions.HasPermission(member,  GuildPermission.Administrator))
            {
                return Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "dmod.errors.target_admin");
            }

             await member.SetTimeOutAsync(TimeSpan.FromSeconds((double)context.DurationSeconds!), new RequestOptions { AuditLogReason = context.Reason });

            return null;
        }
    }
}
