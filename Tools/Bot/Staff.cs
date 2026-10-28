using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Databases;

namespace Whispbot.Tools.Bot
{
    public static class Staff
    {
        private static List<StaffRole>? _roles;
        public static List<StaffRole> Roles => _roles ??= Postgres.Select<StaffRole>(
            @"SELECT * FROM roles"
        ) ?? throw new InvalidOperationException("Failed to load staff roles.");

        public static bool IsStaff(StaffRole role)
        {
            // To be changed when we add established staff permissions
            // for now just check if the role position is above trial support
            return role.Position >= 148;
        }
    }

    public class StaffRole
    {
        public ulong Id { get; init; }
        public string Name { get; init; } = null!;
        public int Color { get; init; }
        public int Position { get; init; }
        public ulong Permissions { get; init; }
    }
}
