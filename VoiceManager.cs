
using System.Collections.Concurrent;

namespace ChatApp.Services
{
    public class SignalData
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Type { get; set; }
        public string Sdp { get; set; }
        public string Candidate { get; set; }
    }

    public class VoiceManager
    {
        private ConcurrentDictionary<string, List<SignalData>> _signals = new();
        private ConcurrentDictionary<string, DateTime> _users = new();

        public List<string> Join(string nick)
        {
            _users[nick] = DateTime.Now;
            _signals[nick] = new List<SignalData>();

            var others = new List<string>();
            var keys = _users.Keys.ToArray();
            var cutOff = DateTime.Now.AddSeconds(-20);

            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] != nick && _users[keys[i]] > cutOff)
                {
                    others.Add(keys[i]);
                }
            }
            return others;
        }

        public List<SignalData> Poll(string nick)
        {
            _users[nick] = DateTime.Now;
            if (_signals.TryRemove(nick, out var list))
            {
                return list;
            }
            return new List<SignalData>();
        }

        public void Signal(string from, string to, string type, string sdp, string cand)
        {
            if (!_signals.ContainsKey(to))
            {
                _signals[to] = new List<SignalData>();
            }
            _signals[to].Add(new SignalData { From = from, To = to, Type = type, Sdp = sdp, Candidate = cand });
        }

        public void Leave(string nick)
        {
            _users.TryRemove(nick, out _);
            _signals.TryRemove(nick, out _);
        }
    }
}