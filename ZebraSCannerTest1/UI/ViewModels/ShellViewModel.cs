using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels
{
    public partial class ShellViewModel : ObservableObject
    {
        private readonly IMenuService _menu;

        [ObservableProperty]
        private bool canGoBack;

        public IRelayCommand BackCommand { get; }
        public IRelayCommand OpenMenuCommand { get; }
        [RelayCommand]
        private async Task OpenSettings()
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }

        [RelayCommand]
        private async Task OpenAbout()
        {
            await Shell.Current.GoToAsync("AboutPage"); // or show alert
        }

        [RelayCommand]
        private Task Logout()
        {
            SecureStorage.Remove("token");
            SecureStorage.Remove("user_id");
            SecureStorage.Remove("username");
            Preferences.Remove("modules");

            var loginPage = MauiProgram.ServiceProvider.GetRequiredService<LoginPage>();
            Application.Current.MainPage = loginPage;

            return Task.CompletedTask;
        }

        public ShellViewModel(IMenuService menu)
        {
            _menu = menu;

            BackCommand = new RelayCommand(async () =>
            {
                if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
                    await Shell.Current.GoToAsync("..");
            });

            OpenMenuCommand = new RelayCommand(async () => await _menu.ShowMenuAsync());

            // Kick off a safe background watcher
            WatchForShellAsync();

            // Defer Shell event hookup to avoid null at startup
            
        }

        private async void WatchForShellAsync()
        {
            // Wait until Shell.Current exists (check every 100 ms)
            while (Shell.Current == null)
                await Task.Delay(100);

            // Now we’re safe to subscribe
            var shell = Shell.Current;
            shell.Navigated += (_, _) =>
            {
                CanGoBack = shell.Navigation?.NavigationStack?.Count > 1;
            };

            // Set the initial state
            CanGoBack = shell.Navigation?.NavigationStack?.Count > 1;
        }
    }
}
