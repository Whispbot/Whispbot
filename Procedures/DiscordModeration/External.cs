using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands;
using static Sentry.MeasurementUnit;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        private static readonly Dictionary<ActionType, DiscordModerationType> _moderationTypes = new()
        {
            { ActionType.Ban, DiscordModerationType.Ban },
            { ActionType.Unban, DiscordModerationType.Unban },
            { ActionType.Kick, DiscordModerationType.Kick },
            { ActionType.MemberUpdated, DiscordModerationType.Mute }
        };

        public static void RegisterClient(DiscordShardedClient client)
        {
            client.AuditLogCreated += async (log, guild) =>
            {
                if (!_moderationTypes.TryGetValue(log.Action, out DiscordModerationType mType)) return; // Not an audit log we care about

                // Bot already logs its own actions so ignore from events to avoid duplicates
                if (log.User.Id == client.CurrentUser.Id) return;

                var guildConfig = await WhispCache.GuildConfig.Get(guild.Id);
                if (guildConfig is null || guildConfig.version != Config.EnvId) return;

                var duration = -1L;
                SocketUser? target = null;
                if (mType == DiscordModerationType.Mute && log.Data is SocketMemberUpdateAuditLogData data)
                {
                    var before = data.Before.TimedOutUntil;
                    var after = data.After.TimedOutUntil;

                    if (after is null)
                    {
                        mType = DiscordModerationType.Unmute;
                    }
                    else
                    {
                        // Ceiling otherwise we end up with e.g. 59 minutes, 59 seconds instead of 1 hour
                        duration = (long)Math.Ceiling((after - DateTimeOffset.UtcNow).Value.TotalSeconds);
                    }

                    target = data.Target.Value;
                }
                else if (log.Data is SocketBanAuditLogData banData) target = banData.Target.Value;
                else if (log.Data is SocketUnbanAuditLogData unbanData) target = unbanData.Target.Value;
                else if (log.Data is SocketKickAuditLogData kickData) target = kickData.Target.Value;

                if (target is null) return; // :(

                var moderator = log.User;

                var context = new Context(
                    target,
                    log.Reason ?? "No reason provided",
                    duration,
                    guild,
                    moderator,
                    mType,
                    null
                );

                var (mcase, transaction) = await CreateCase(context);
                if (mcase is null)
                {
                    transaction?.Rollback();
                    return;
                }
                else
                {
                    transaction?.Commit();
                }

                await Log(mcase);
            };
        }
    }
}
