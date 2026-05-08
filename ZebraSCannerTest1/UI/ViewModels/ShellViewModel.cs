using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels
{
    public partial class ShellViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool canGoBack;

        public IRelayCommand BackCommand { get; }

        public ShellViewModel()
        {
            BackCommand = new RelayCommand(async () =>
            {
                if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
                    await Shell.Current.GoToAsync("..");
            });

            WatchForShellAsync();
        }

        [RelayCommand]
        private async Task OpenSettings()
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }

        [RelayCommand]
        private async Task OpenAbout()
        {
            string version = AppInfo.Current.VersionString;
            string build = AppInfo.Current.BuildString;

            await Shell.Current.DisplayAlert(
                "About",
                $"ScanMate\nVersion: {version}\nBuild: {build}\n\nWarehouse scanning and inventory assistant.",
                "OK");
        }

        [RelayCommand]
        private async Task Logout()
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Logout",
                "Are you sure you want to logout?",
                "Yes",
                "Cancel");

            if (!confirm)
                return;

            SecureStorage.Remove("token");
            SecureStorage.Remove("user_id");
            SecureStorage.Remove("username");
            Preferences.Remove("modules");

            var loginPage = MauiProgram.ServiceProvider.GetRequiredService<LoginPage>();
            Application.Current.MainPage = loginPage;
        }

        private async void WatchForShellAsync()
        {
            while (Shell.Current == null)
                await Task.Delay(100);

            var shell = Shell.Current;

            shell.Navigated += (_, _) =>
            {
                CanGoBack = shell.Navigation?.NavigationStack?.Count > 1;
            };

            CanGoBack = shell.Navigation?.NavigationStack?.Count > 1;
        }
    }
}