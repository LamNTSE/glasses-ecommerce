namespace OpticalStore.API.Requests.Chatbot;

public sealed class ChatbotChatRequest
{
    public List<ChatbotMessageItem>? Messages { get; set; }
}

public sealed class ChatbotMessageItem
{
    public string? Role { get; set; }

    public string? Content { get; set; }
}

public sealed class ChatbotReplyDto
{
    public string Reply { get; set; } = string.Empty;
}
