
namespace ChatPlayground.Models;

public sealed class Message
{
    public string Id { get; set; }

    public string Text { get; set; }

    public MessageSender Sender { get; set; }
}

public enum MessageSender
{
    Undefined,
    User,
    Assistant
}
