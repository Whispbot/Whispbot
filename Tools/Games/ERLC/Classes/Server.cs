using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Games.ERLC.Classes
{
    public class ERLCServer
    {
        public string Name { get; init; } = default!;
        public long OwnerId { get; init; } = default!;
        public List<long> CoOwnerIds { get; init; } = [];
        public byte CurrentPlayers { get; init; } = default!;
        public byte MaxPlayers { get; init; } = default!;
        public string JoinKey { get; init; } = default!;
        public string AccVerifiedReq { get; init; } = default!;
        public bool TeamBalance { get; init; } = default!;
        public List<ERLCPlayer>? Players { get; init; }
        public ERLCStaff? Staff { get; init; }
        public List<ERLCJoinLog>? JoinLogs { get; init; }
        public List<long>? Queue { get; init; }
        public List<ERLCKillLog>? KillLogs { get; init; }
        public List<ERLCCommandLog>? CommandLogs { get; init; }
        public List<ERLCCallLog>? ModCalls { get; init; }
        public List<ERLCVehicle>? Vehicles { get; init; }
    }
}
