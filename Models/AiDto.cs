namespace CampusCoffeeSystem.Models
{
    public class ChatRequestDto
    {
        public string UserMessage { get; set; } = string.Empty;
        public List<Dictionary<string, string>> ConversationHistory { get; set; } = new();
    }

    public class RecommendRequestDto
    {
        public List<string> OrderHistory { get; set; } = new();
        public List<Dictionary<string, object>> CurrentMenu { get; set; } = new();
    }

    public class ChatResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}