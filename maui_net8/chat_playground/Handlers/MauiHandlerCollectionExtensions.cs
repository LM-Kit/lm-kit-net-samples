using ChatPlayground.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPlayground.Handlers
{
    public static class MauiHandlerCollectionExtensions
    {
        public static IMauiHandlersCollection AddCustomHandlers(this IMauiHandlersCollection handlers)
        {
            handlers.AddHandler(typeof(CustomEntry), typeof(CustomEntryHandler));

            return handlers;
        }
    }
}
