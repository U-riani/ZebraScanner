using Microsoft.Maui.Storage;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.Services
{
    public interface IMenuService
    {
        Task ShowMenuAsync();
    }

    public class MenuService : IMenuService
    {
        public async Task ShowMenuAsync()
        {
            string choice = await Shell.Current.DisplayActionSheet(
                "Menu", "Cancel", null,
                "Settings", "About", "Logout");

            switch (choice)
            {
                case "Settings":
                    await Shell.Current.DisplayAlert("Settings", "Settings coming soon.", "OK");
                    break;

                case "About":
                    await Shell.Current.DisplayAlert("About", "ScanMate v1.0", "OK");
                    break;

                case "Logout":

                    bool confirm = await Shell.Current.DisplayAlert(
                        "Logout",
                        "Are you sure you want to logout?",
                        "Yes",
                        "Cancel");

                    if (!confirm)
                        return;

                    // remove token
                    SecureStorage.Remove("token");

                    // go back to login page
                    var loginPage = MauiProgram.ServiceProvider.GetRequiredService<LoginPage>();
                    Application.Current.MainPage = loginPage;

                    break;
            }
        }
    }
}