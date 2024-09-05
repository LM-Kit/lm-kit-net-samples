using ChatPlayground.Models;

namespace ChatPlayground.Data;

public interface IChatPlaygroundDatabase
{
    Task<List<ConversationLog>> GetConversations();
    Task<int> SaveConversation(ConversationLog conversationLog);
    Task<int> DeleteConversation(ConversationLog conversationLog);
}
