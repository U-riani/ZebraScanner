using Microsoft.Maui.Storage;
using System.Text.Json;
using ZebraSCannerTest1.UI.Services;

namespace ZebraSCannerTest1.UI.Views;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _auth;

    public LoginPage()
    {
        InitializeComponent();

        _auth = MauiProgram.ServiceProvider.GetRequiredService<AuthService>();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text;
        var password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Enter username and password", "OK");
            return;
        }

        var result = await _auth.Login(username, password);

        if (result == null)
        {
            await DisplayAlert("Login failed", $"Invalid credentials {result}", "OK");
            return;
        }

        // save token
        await SecureStorage.SetAsync("token", result.access_token);
        await SecureStorage.SetAsync("user_id", result.user_id.ToString());
        await SecureStorage.SetAsync("username", result.username);

        Preferences.Set("modules", JsonSerializer.Serialize(result.modules));

        Console.WriteLine("Saved modules: " + Preferences.Get("modules", "EMPTY"));        // open main app

        var shell = MauiProgram.ServiceProvider.GetRequiredService<AppShell>();
        Microsoft.Maui.Controls.Application.Current.MainPage = shell;
    }
}