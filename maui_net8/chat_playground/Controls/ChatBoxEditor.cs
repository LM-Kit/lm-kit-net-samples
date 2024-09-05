#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
#elif ANDROID
using Android.Graphics.Drawables;
using AndroidX.AppCompat.Widget;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
#endif
using System.Windows.Input;

namespace ChatPlayground.Controls
{
    internal class ChatBoxEditor : Editor
    {
        private bool _shiftKeyIsHeld = false;

        public event EventHandler? EntryKeyReleased;

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

#if WINDOWS
            TextBox textBox = (TextBox)Handler.PlatformView;
            textBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            textBox.Style = null;
            textBox.KeyUp += OnKeyUp;
            textBox.AcceptsReturn = false;
            textBox.KeyUp += OnKeyUp;
            textBox.KeyDown += OnKeydown;

#elif ANDROID

            AppCompatEditText appCompatEditText = (AppCompatEditText)Handler.PlatformView;
            using (var gradientDrawable = new GradientDrawable())
            {
                gradientDrawable.SetColor(global::Android.Graphics.Color.Transparent);
                appCompatEditText.SetBackground(gradientDrawable);
                appCompatEditText.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToAndroid());
            }
#endif
        }



#if WINDOWS
        private void OnKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Shift)
            {
                _shiftKeyIsHeld = false;
            }
        }

        private void OnKeydown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Shift)
            {
                _shiftKeyIsHeld = true;
            }
            else if (e.Key == VirtualKey.Enter)
            {
                if (!_shiftKeyIsHeld)
                {
                    EntryKeyReleased?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Text += "\r";
                }
            }
        }
#endif

    }
}
