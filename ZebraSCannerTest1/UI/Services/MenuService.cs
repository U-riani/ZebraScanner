using System.Threading.Tasks;

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
                    await Shell.Current.DisplayAlert("Logout", "You’ve been logged out.", "OK");
                    break;
            }
        }
    }
}
