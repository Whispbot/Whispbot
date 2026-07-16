using Discord;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Tools.Disc
{
    public static class DiscordPermissions
    {
        /// <summary>
        /// Check if a member has specific  GuildPermission.
        /// </summary>
        /// <param name="member">The <see cref="Member"/> to check permissions for.</param>
        /// <param name="permissions">The permissions to check.</param>
        /// <returns>A <seealso cref="bool"/> representing whether the user has the  GuildPermission.</returns>
        public static bool HasPermission(IGuildUser member, GuildPermission permissions)
        {
            return member.GuildPermissions.Has(permissions);
        }

        /// <summary>
        /// Check if a member has specific permissions OR administrator (all permissions).
        /// </summary>
        /// <param name="member">The <see cref="Member"/> to check permissions for.</param>
        /// <param name="permissions">The permissions to check.</param>
        /// <returns>A <seealso cref="bool"/> representing wherther the user has the  GuildPermission.</returns>
        public static bool HasPermissionOrAdmin(IGuildUser member, GuildPermission permissions)
        {
            return member.GuildPermissions.Has(permissions | GuildPermission.Administrator);
        }

        /// <summary>
        /// Check if a user in the given guild has specific  GuildPermission. If you already have a <see cref="Member"/> object, use the other overload.
        /// </summary>
        /// <param name="guild">The <see cref="Guild"/> to fetch the member from.</param>
        /// <param name="user_id">The ID of the user to fetch the member for.</param>
        /// <param name="permissions">The permissions to check.</param>
        /// <returns>A <seealso cref="bool"/> representing whether the user has the  GuildPermission.</returns>
        public static async Task<bool> HasPermission(IGuild guild, ulong user_id, GuildPermission permissions)
        {
            var member = await guild.GetUserAsync(user_id);
            if (member is null) return false;

            return HasPermission(member, permissions);
        }

        /// <summary>
        /// Check if a user in the given guild has specific permissions OR administrator (all permissions). If you already have a <see cref="Member"/> object, use the other overload.
        /// </summary>
        /// <param name="guild">The <see cref="Guild"/> to fetch the member from.</param>
        /// <param name="user_id">The ID of the user to fetch the member for.</param>
        /// <param name="permissions">The permission to check.</param>
        /// <returns>A <seealso cref="bool"/> representing whether the user has the  GuildPermission.</returns>
        public static async Task<bool> HasPermissionOrAdmin(IGuild guild, ulong user_id, GuildPermission permissions)
        {
            var member = await guild.GetUserAsync(user_id);
            if (member is null) return false;

            return HasPermissionOrAdmin(member, permissions);
        }

        /// <summary>
        /// Check if a user in the specified guild has specific  GuildPermission. If you already have a <see cref="Guild"/> or even the <seealso cref="Member"/>, use the other overloads.
        /// </summary>
        /// <param name="guild_id">The ID of the guild the member is in.</param>
        /// <param name="user_id">The ID of the user to fetch the member for.</param>
        /// <param name="permissions">The permissions to check.</param>
        /// <returns>A <seealso cref="bool"/> representing whether the user has the  GuildPermission.</returns>
        public static async Task<bool> HasPermission(ulong guild_id, ulong user_id, GuildPermission permissions)
        {
            var guild = Config.client?.GetGuild(guild_id);
            if (guild is null) return false;

            return await HasPermission(guild, user_id, permissions);
        }

        /// <summary>
        /// Check if a user in the specified guild has specific permissions OR administrator (all permissions). If you already have a <see cref="Guild"/> or even the <seealso cref="Member"/>, use the other overloads.
        /// </summary>
        /// <param name="guild_id">The ID of the guild the member is in.</param>
        /// <param name="user_id">The ID of the user to fetch the member for.</param>
        /// <param name="permissions">The permissions to check.</param>
        /// <returns>A <seealso cref="bool"/> representing whether the user has the  GuildPermission.</returns>
        public static async Task<bool> HasPermissionOrAdmin(ulong guild_id, ulong user_id, GuildPermission permissions)
        {
            var guild = Config.client?.GetGuild(guild_id);
            if (guild is null) return false;

            return await HasPermissionOrAdmin(guild, user_id, permissions);
        }
    }
}
