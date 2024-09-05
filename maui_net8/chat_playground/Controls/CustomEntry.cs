using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Windows.System;
#endif

namespace ChatPlayground.Controls
{
    class CustomEntry : Entry
    {
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

#if WINDOWS
            TextBox textBox = (TextBox)Handler.PlatformView;

            textBox.KeyDown += OnKeyDown;
#endif
        }

#if WINDOWS
        private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                Unfocus();
            }
        }
#endif
    }
}
