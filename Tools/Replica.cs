using Serilog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;

namespace Whispbot
{
    public class Replica
    {
        private readonly ISubscriber _subscriber;
        private readonly string _id;
        private readonly string _deployment;

        private int _currentTerm = 0;
        private string? _votedFor = null;
        private bool _isLeader = false;
        private double _lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private CancellationTokenSource? _cancellationTokenSource = null;
        private int _failedElections = 0;
        private bool _currentlyRunning = false;

        private readonly Random _random = new();

        public readonly int total_replicas;
        public bool active = false;
        public bool IsLeader => _isLeader;

        // CONFIG
        public readonly int heartbeatInterval = Config.IsDev ? 1000 : 100;
        public readonly int electionTimeout = Config.IsDev ? 5000 : 250;
        public readonly int electionDuration = Config.IsDev ? 1000 : 200;
        public readonly int timeoutMin = Config.IsDev ? 500 : 100;
        public readonly int timeoutMax = Config.IsDev ? 2000 : 300;

        public event EventHandler? OnElected;
        public event EventHandler? OnLostLeadership;

        public Replica(string deployment, string replica, int total_replicas)
        {
            _id = replica;
            _deployment = deployment;

            ISubscriber? sub = null;
            int count = 0;
            while (sub is null)
            {
                sub = Redis.GetSubscriber(); // Wait for redis subsub to be ready
                if (sub is null)
                {
                    count++;
                    if (count >= 10)
                    {
                        throw new Exception("Failed to connect to redis for cluster manager");
                    }
                    Thread.Sleep(100 * count * count);
                }
            }
            _subscriber = sub;

            this.total_replicas = total_replicas;
        }

        private string LogPrefix => $"[{_currentTerm.ToString().PadLeft(3, '0')}]";

        public async Task Start()
        {
            await _subscriber.SubscribeAsync("request_vote", (channel, message) => RecievedElection(message));
            await _subscriber.SubscribeAsync("heartbeat", (channel,message) => RecievedHeartbeat(message));

            _ = Task.Run(ElectionTimeoutLoop);

            Log.Information($"{LogPrefix} Ready.");
        }

        private async Task ElectionTimeoutLoop()
        {
            while (true)
            {
                int timeout = _random.Next(timeoutMin, timeoutMax);
                Thread.Sleep(timeout);

                if (!_isLeader && (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastHeartbeat) > electionTimeout && _votedFor is null) // Is not the leader and hasnt had heartbeat in 500ms
                {
                    Log.Information($"{LogPrefix} No heartbeat in {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastHeartbeat}ms, starting election.");
                    await StartElection();
                }
            }
        }

        private void Elected()
        {
            _isLeader = true;
            _currentlyRunning = false;
            _subscriber.UnsubscribeAsync("vote");
            _failedElections = 0;
            OnElected?.Invoke(this, EventArgs.Empty);
            Log.Information($"{LogPrefix} Elected as new leader.");
            StartHeartbeat();
        }

        private async Task StartElection()
        {
            _currentTerm++;
            int majority = (total_replicas / 2) + 1 - (_failedElections);
            int votes = 1; // Vote for self

            if (majority <= 1)
            {
                Log.Information($"{LogPrefix} Majority is {majority}, no election needed.");
                Elected();
                return;
            }
            
            _currentlyRunning = true;

            await _subscriber.SubscribeAsync("vote", (channel, message) =>
            {
                string[] parts = message.ToString().Split(':');
                string deployment = parts[0];
                int term = int.Parse(parts[1]);
                string replica = parts[2];

                if (deployment != _deployment) return; // Ignore messages from other deployments

                if (term == _currentTerm && replica == _id)
                {
                    votes++;

                    if (votes >= majority && !_isLeader && _currentlyRunning)
                    {
                        Elected();
                    }
                }
            });

            await _subscriber.PublishAsync("request_vote", $"{_deployment}:{_currentTerm}:{_id}");

            Thread.Sleep(electionTimeout);

            if (!_isLeader && _currentlyRunning)
            {
                _failedElections++;
                _currentlyRunning = false;
                _lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await _subscriber.UnsubscribeAsync("vote");
                Log.Warning($"{LogPrefix} Not elected as new leader, failure {_failedElections}.");
            }
        }

        private void StartHeartbeat()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await _subscriber.PublishAsync("heartbeat", $"{_deployment}:{_id}:{_currentTerm}");
                    Thread.Sleep(heartbeatInterval);
                }
            });
        }

        private void RecievedHeartbeat(RedisValue message)
        {
            string[] parts = message.ToString().Split(':');
            string deployment = parts[0];
            string replica = parts[1];
            int term = int.Parse(parts[2]);

            if (deployment != _deployment) return; // Ignore messages from other deployments

            active = true;

            if (replica == _id) return; // Ignore own heartbeat

            if (term > _currentTerm)
            {
                _currentTerm = term;
                _isLeader = false;
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = null;
                _votedFor = null;
                _lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _failedElections = 0; // Reset failed elections
                OnLostLeadership?.Invoke(this, EventArgs.Empty);
                Log.Information($"{LogPrefix} Accepted {replica} as leader.");
            } 
            else if (term == _currentTerm)
            {
                if (_isLeader)
                {
                    _isLeader = false;
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource = null;
                    OnLostLeadership?.Invoke(this, EventArgs.Empty);
                    Log.Error($"{LogPrefix} Lost leadership to {replica}.");
                }
                if (_failedElections > 0 || _currentlyRunning)
                {
                    Log.Error($"{LogPrefix} Lost election to {replica}.");
                }
                _failedElections = 0; // Reset failed elections
                _currentlyRunning = false; // Reset running state
                _votedFor = null; // Reset vote
                _lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }

        private void RecievedElection(RedisValue message)
        {
            string[] parts = message.ToString().Split(':');
            string deployment = parts[0];
            int term = int.Parse(parts[1]);
            string replica = parts[2];

            if (deployment != _deployment) return; // Ignore messages from other deployments

            if (term > _currentTerm && replica != _id && !_currentlyRunning) // vote
            {
                _currentTerm = term;
                _votedFor = replica;
                _isLeader = false;
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = null;

                _subscriber.PublishAsync("vote", $"{_deployment}:{_currentTerm}:{replica}");

                Log.Information($"{LogPrefix} Accepted {replica} as leader.");
            } // otherwise no vote
        }
    }
}
