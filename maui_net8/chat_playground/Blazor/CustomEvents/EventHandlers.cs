using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPlayground.Blazor.CustomEvents
{
    [EventHandler("oncontentIsScrolledToEnd", typeof(ContentScrolledToEndEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
    public static class EventHandlers
    {
    }
}
