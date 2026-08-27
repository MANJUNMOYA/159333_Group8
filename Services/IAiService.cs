using CampusCoffeeSystem.Models;

namespace CampusCoffeeSystem.Services
{
    public interface IAiService
    {
        Task<string> GenerateAsync(
            string systemInstruction,
            IReadOnlyList<AiConversationTurn> conversation,
            CancellationToken cancellationToken);
    }
}
