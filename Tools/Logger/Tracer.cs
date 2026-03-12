using Amazon.S3.Model;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Whispbot.Databases;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot
{
    public sealed class Tracer
    {
        private const string _sourceName = "Whispbot";

        public static readonly ActivitySource ActivitySource = new(_sourceName);

        private static readonly AsyncLocal<CommandState?> _state = new();

        private static readonly ConcurrentQueue<CommandState> _commandActivity = new();

        private static bool _processing = false;
        private static readonly Random _random = new();
        private static readonly float _sampleRate = Environment.GetEnvironmentVariable("TRACE_SAMPLE_RATE") is string tsr ? float.Parse(tsr) : 0f;

        public static ActivityListener CreateListener()
        {
            var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == _sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,

                ActivityStarted = activity =>
                {
                    var st = _state.Value ??= new CommandState();

                    st.Stack.Push(activity);

                    var id = activity.Id ?? Guid.NewGuid().ToString("n");
                    var nb = new NodeBuilder { Id = id, Name = activity.DisplayName };
                    st.NodesById[id] = nb;

                    var parent = st.Stack.Skip(1).FirstOrDefault();
                    if (parent is null)
                    {
                        st.RootId = id;
                    }
                    else if (parent.Id is { } pid && st.NodesById.TryGetValue(pid, out var pnb))
                    {
                        pnb.Children.Add(nb);
                    }
                },

                ActivityStopped = activity =>
                {
                    var st = _state.Value;
                    if (st is null)
                        return;

                    if (activity.Id is { } id && st.NodesById.TryGetValue(id, out var nb))
                    {
                        nb.StartTime = activity.StartTimeUtc;
                        nb.Duration = activity.Duration;
                    }

                    if (st.Stack.Count > 0 && ReferenceEquals(st.Stack.Peek(), activity))
                        st.Stack.Pop();

                    if (activity.Id is { } stoppedId && st.RootId == stoppedId)
                    {
                        if (st.NodesById.Values.FirstOrDefault(n => n.Name.StartsWith("Command: ")) is NodeBuilder n)
                        {
                            if ((_random.NextDouble() * 100) <= _sampleRate)
                            {
                                _commandActivity.Enqueue(st);
                                if (!_processing) Task.Run(() => ProcessCommandActivities());
                            }
                        }
                    }
                }
            };

            ActivitySource.AddActivityListener(listener);
            return listener;
        }

        private static IEnumerable<DatabaseNode> IterableChildren(NodeBuilder builder)
        {
            return [
                ..builder.Children.Select(n => new DatabaseNode(n.Id, n.Name, n.StartTime, n.EndTime, builder.Id)),
                ..builder.Children.SelectMany(IterableChildren)
            ];
        }

        public static void ProcessCommandActivities()
        {
            //if (_processing) return;
            //_processing = true;

            //Thread.Sleep(5000);

            //if (!_commandActivity.IsEmpty)
            //{
            //    List<CommandState> states = [.. _commandActivity];
            //    _commandActivity.Clear();

            //    if (states.Count > 100)
            //    {
            //        states = [.. states.Take(100)];
            //        Log.Warning("Too many traces sampled, reduced to 100");
            //    }

            //    List<NodeBuilder> primaryNodes = [.. states.Select(s => s.NodesById!.GetValueOrDefault(s.RootId, new()))];
            //    List<NodeBuilder> traces = [.. states.SelectMany(s => s.NodesById.Values.SelectMany(n => n.Children))];

            //    Postgres.Select("INSERT INTO command_traces ()");

            //    Log.Verbose($"Sampled {states.Count} traces and logged to databse.");
            //}
            //else
            //{
            //    Log.Verbose("No traces sampled???");
            //}

            //_processing = false;
        }

        public static Activity? Start(string name) => ActivitySource.StartActivity(name);

        public static string? DumpAndReset(string? title = null)
        {
            var st = _state.Value;
            _state.Value = null;

            if (st?.RootId is null)
                return "<no activity data>";

            return DumpInternal(st, title);
        }

        private static string? DumpAndResetInternal(CommandState st, string? title)
        {
            _state.Value = null;
            return DumpInternal(st, title);
        }

        private static string? DumpInternal(CommandState st, string? title)
        {
            if (st.RootId is null || !st.NodesById.TryGetValue(st.RootId, out var root))
                return "<no root activity>";

            if (root.Name != "Message") return null;

            static Node Build(NodeBuilder b) =>
                new(b.Name, b.StartTime, b.Duration, [.. b.Children.Select(Build)]);

            var rootNode = Build(root);

            var header = title is null ? "" : $"{title}\n";
            return header + Format(rootNode, 0, root);
        }

        private static string Format(Node n, int indent, NodeBuilder root)
        {
            var pad = new string(' ', indent * 2);

            int chars = 50;
            double fraction = n.Duration.TotalMilliseconds / root.Duration.TotalMilliseconds;
            int length = (int)(chars * fraction);
            int startChars = (int)(chars * (n.Start - root.StartTime).TotalMilliseconds / root.Duration.TotalMilliseconds);

            var lines = new List<string>
            {
                $"{pad}{n.Name} — {FormatDuration(n.Duration)}".PadRight(50, ' ') + new String(' ', startChars) + new string('█', length)
            };

            foreach (var c in n.Children.OrderBy(x => x.Start))
                lines.Add(Format(c, indent + 1, root));

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatDuration(TimeSpan d)
        {
            if (d.TotalMilliseconds >= 1)
                return $"{d.TotalMilliseconds:F3} ms";

            var us = d.TotalMilliseconds * 1000.0;
            if (us >= 1)
                return $"{us:F2} µs";

            var ns = us * 1000.0;
            return $"{ns:F0} ns";
        }

        private sealed record Node(string Name, DateTime Start, TimeSpan Duration, List<Node> Children);

        private sealed class CommandState
        {
            public Stack<Activity> Stack { get; } = new();
            public Dictionary<string, NodeBuilder> NodesById { get; } = new();

            public string? RootId { get; set; }
        }

        private sealed class NodeBuilder
        {
            public string Id { get; init; } = "";
            public string Name { get; init; } = "";
            public DateTime StartTime { get; set; }
            public DateTime EndTime => StartTime + Duration;
            public TimeSpan Duration { get; set; }
            public List<NodeBuilder> Children { get; } = new();
        }

        private sealed record DatabaseNode(string Id, string Name, DateTime Start, DateTime End, string ParentId);
    }
}