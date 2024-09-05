using ChatPlayground.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPlayground.Data
{
    public sealed class ChatPlaygroundDatabase : IChatPlaygroundDatabase
    {
        private SQLiteAsyncConnection? _sqlDatabase;

        private async Task Init()
        {
            if (_sqlDatabase is not null)
            {
                return;
            }

            try
            {
                _sqlDatabase = new SQLiteAsyncConnection(AppConstants.DatabasePath, AppConstants.Flags);

                await _sqlDatabase.CreateTableAsync<ConversationLog>();
            }
            catch (Exception ex)
            {
            }
        }

        public async Task<List<ConversationLog>> GetConversations()
        {
            await Init();
            return await _sqlDatabase!.Table<ConversationLog>().ToListAsync();
        }

        public async Task<int> SaveConversation(ConversationLog conversation)
        {
            await Init();

            if (conversation.ID != 0)
            {
                return await _sqlDatabase!.UpdateAsync(conversation);
            }
            else
            {
                return await _sqlDatabase!.InsertAsync(conversation);
            }
        }

        public async Task<int> DeleteConversation(ConversationLog conversation)
        {
            await Init();
            return await _sqlDatabase!.DeleteAsync(conversation);
        }
    }
}
