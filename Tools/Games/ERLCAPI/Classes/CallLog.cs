using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Games.ERLCAPI.Classes
{
    public class ERLCCallLog
    {
        public string Caller { get; init; } = default!;
        public string? Moderator { get; init; }
        public long Timestamp { get; init; } = default!;
    }
}
