using CommunityToolkit.Mvvm.ComponentModel;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using ChatPlayground.Models;
using ChatPlayground.ViewModels;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Collections.Specialized;
using System.Threading;
using LMKit.TextGeneration.Chat;

namespace ChatPlayground.Services
{
    public partial class LMKitService
    {
        public sealed class Conversation
        {
            public ModelInfo? LastUsedModel { get; set; }

            public ChatHistory? ChatHistory { get; set; }

            public byte[]? LatestChatHistoryData { get; set; }

            public Conversation(byte[]? latestChatHistoryData = null)
            {
                LatestChatHistoryData = latestChatHistoryData;
            }
        }
    }
}
