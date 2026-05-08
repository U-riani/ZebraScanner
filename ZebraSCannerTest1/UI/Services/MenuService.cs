using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.Services
{
    public interface IMenuService
    {
        Task ShowSettingsAsync();
        Task ShowAboutAsync();
        Task LogoutAsync();
    }

    public class MenuService : IMenuService
    {
        public async Task ShowSettingsAsync()
        {
            await Shell.Current.DisplayAlert(
                "Settings",
                "Settings coming soon.",
                "OK");
        }

        public async Task ShowAboutAsync()
        {
            string version = AppInfo.Current.VersionString;
            string build = AppInfo.Current.BuildString;

            await Shell.Current.DisplayAlert(
                "About",
                $"ScanMate\nVersion: {version}\nBuild: {build}",
                "OK");
        }

        public async Task LogoutAsync()
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
    }
}