using CampusCoffeeSystem.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CampusCoffeeSystem.Services;

public sealed class CoffeeAgencyConversations(IMemoryCache cache)
{
    private readonly object _gate = new();

    public Conversation Get(string userId)
    {
        lock (_gate)
        {
            return cache.GetOrCreate("coffee-agency:" + userId, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);
                return new Conversation();
            })!;
        }
    }

    public sealed class Conversation
    {
        private readonly List<CoffeeAgencyMessage> _messages = [];
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public IReadOnlyList<CoffeeAgencyMessage> History => _messages.ToArray();
        public void Clear() => _messages.Clear();
        public void Add(string question, string answer)
        {
            _messages.Add(new("user", question));
            _messages.Add(new("model", answer));
            if (_messages.Count > 8) _messages.RemoveRange(0, _messages.Count - 8);
        }
    }
}
