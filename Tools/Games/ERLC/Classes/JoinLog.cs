using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Games.ERLC.Classes
{
    public class ERLCJoinLog
    {
        public bool Join { get; init; } = default!;
        public long Timestamp { get; init; } = default!;
        public string Player { get; init; } = default!;
    }
}
