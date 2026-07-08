using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.PRC.PRC.Classes
{
    public class ERLCEmergencyCall
    {
        public string Team { get; init; } = default!;
        public long Caller { get; init; } = default!;
        public List<long> Players { get; init; } = [];
        public float[] Position { get; init; } = new float[2];
        public long StartedAt { get; init; } = default!;
        public int CallNumber { get; init; } = 0;
        public string Description { get; init; } = default!;
        public string PositionDescriptor { get; init; } = default!;
    }
}
