using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;

namespace Whispbot
{
    public static partial class Procedures
    {
        /// <summary>
        /// Insert a moderation action into the database and send the log messages to the log channel and moderated user
        /// </summary>
        /// <param name="guild_id">The ID of the guild in which this moderation is taking place</param>
        /// <param name="moderator_id">The ID of the user which created the moderation</param>
        /// <param name="target_id">The Id of the user who is recieving the moderation</param>
        /// <param name="type">The type of moderation</param>
        /// <param name="reason">The reason for the moderation</param>
        /// <returns><see cref="DiscordModeration?"/> - will be null if failed to log</returns>
        public static async Task<DiscordModeration?> LogDiscordModeration(ulong guild_id, ulong moderator_id, ulong target_id, DiscordModerationType type, string reason)
        {
            var moderation = Postgres.SelectFirst<DiscordModeration>($@"
                INSERT INTO discord_moderations (guild_id, moderator_id, target_id, type, reason)
                VALUES (@1, @2, @3, @4, @5)
                RETURNING *;
            ", [guild_id, moderator_id, target_id, type, reason]);

            return moderation;
        }

        /// <summary>
        /// Insert a moderation action into the database and send the log messages to the log channel and moderated user
        /// </summary>
        /// <param name="guild_id">The ID of the guild in which this moderation is taking place</param>
        /// <param name="moderator_id">The ID of the user which created the moderation</param>
        /// <param name="target_id">The Id of the user who is recieving the moderation</param>
        /// <param name="type">The type of moderation</param>
        /// <param name="reason">The reason for the moderation</param>
        /// <returns><see cref="DiscordModeration?"/> - will be null if failed to log</returns>
        public static async Task<DiscordModeration?> LogDiscordModeration(string guild_id, string moderator_id, string target_id, DiscordModerationType type, string reason)
        { // Instead of converting to longs in the actual code, its cleaner to do it here
            return await LogDiscordModeration(
                ulong.Parse(guild_id), 
                ulong.Parse(moderator_id), 
                ulong.Parse(target_id), 
                type, 
                reason
            );
        }

        public class DiscordModeration
        {
            public int case_id;
            public ulong guild_id;
            public ulong moderator_id;
            public ulong target_id;
            public DiscordModerationType type;
            public string reason = "";
            public DateTimeOffset created_at;
            public DateTimeOffset updated_at;
            public ulong? updated_by;
            public ulong? message_id;
            public bool is_deleted;
        }

        public enum DiscordModerationType
        {
            Warn,
            Mute,
            Kick,
            Softban,
            Ban
        }
    }
}
