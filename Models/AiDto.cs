namespace CampusCoffeeSystem.Models
{
    public class ChatRequestDto
    {
        public string UserMessage { get; set; } = string.Empty;
    }

    public class ActivityRequestDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string ActivityType { get; set; } = string.Empty;
    }

    public class ChatResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
