using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ChatPlayground.Blazor.CustomEvents
{
    [EventHandler("contentIsScrolledToEnd", typeof(bool), enableStopPropagation: true, enablePreventDefault: true)]
    public class ContentScrolledToEndEventArgs : EventArgs
    {
        public bool IsScrolledToEnd { get; set; }
    }

    public class ContentScrolledToEndEventHelper
    {
        private readonly Func<EventArgs, Task> _callback;

        public ContentScrolledToEndEventHelper(Func<EventArgs, Task> callback)
        {
            _callback = callback;
        }

        [JSInvokable]
        public Task OnCustomEvent(EventArgs args) => _callback(args);
    }

    public class ContentScrolledToEndEventInterop : IDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private DotNetObjectReference<ContentScrolledToEndEventHelper>? Reference;

        public ContentScrolledToEndEventInterop(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public ValueTask<string> SetupCustomEventCallback(Func<EventArgs, Task> callback)
        {
            Reference = DotNetObjectReference.Create(new ContentScrolledToEndEventHelper(callback));
            // addCustomEventListener will be a js function we create later
            return _jsRuntime.InvokeAsync<string>("addCustomEventListener", Reference);
        }

        public void Dispose()
        {
            Reference?.Dispose();
        }
    }
}
