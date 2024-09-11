using SQLite;

namespace ChatPlayground.Models
{
    public sealed class ConversationLog
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string? Title { get; set; }

        public DateTime Date { get; set; }

        public byte[]? ChatHistoryData { get; set; }

        public string? MessageListBlob { get; set; }

        public string? LastUsedModel { get; set; }

        [Ignore]
        public List<Message> MessageList { get; } = new List<Message>();

        public ConversationLog()
        {
        }

        public ConversationLog(string title)
        {
            Title = title;
            Date = DateTime.Now;
            MessageList = new List<Message>();
        }
    }
}
