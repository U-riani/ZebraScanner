using Microsoft.Maui.Controls;

namespace ZebraSCannerTest1.Helpers;

public static class ShowingLongPopup
{
    private static ContentPage _loadingPage;
    private static Label _messageLabel;

    public static async Task ShowAsync(string message)
    {
        _messageLabel = new Label
        {
            Text = message,
            TextColor = Colors.White,
            FontSize = 18,
            HorizontalOptions = LayoutOptions.Center
        };

        _loadingPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Content = new VerticalStackLayout
            {
                Padding = 40,
                Spacing = 20,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new ActivityIndicator
                    {
                        IsRunning = true,
                        Color = Colors.White,
                        WidthRequest = 60,
                        HeightRequest = 60
                    },
                    _messageLabel
                }
            }
        };

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Shell.Current.Navigation.PushModalAsync(_loadingPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Popup] Failed to show: {ex.Message}");
            }

        });
    }

    // ✅ Dynamically update the popup text
    public static async Task UpdateMessageAsync(string newMessage)
    {
        if (_messageLabel == null)
            return; // popup not currently shown

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _messageLabel.Text = newMessage;
        });
    }

    public static async Task CloseAsync()
    {
        if (_loadingPage != null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await Shell.Current.Navigation.PopModalAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Popup] Failed to close: {ex.Message}");
                }

            });
        }
    }
}
