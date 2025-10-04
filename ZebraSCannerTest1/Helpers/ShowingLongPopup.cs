using Microsoft.Maui.Controls;

namespace ZebraSCannerTest1.Helpers;

public static class ShowingLongPopup
{
    private static ContentPage _loadingPage;

    public static async Task ShowAsync(string message)
    {
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
                    new Label
                    {
                        Text = message,
                        TextColor = Colors.White,
                        FontSize = 18,
                        HorizontalOptions = LayoutOptions.Center
                    }
                }
            }
        };

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.Navigation.PushModalAsync(_loadingPage);
        });
    }

    public static async Task CloseAsync()
    {
        if (_loadingPage != null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.Navigation.PopModalAsync();
                _loadingPage = null;
            });
        }
    }
}
