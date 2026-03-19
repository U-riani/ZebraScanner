using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        [ObservableProperty]
        bool inventorizationVisible;

        [ObservableProperty]
        bool transferVisible;

        [ObservableProperty]
        bool receiveVisible;

        [ObservableProperty]
        bool salesVisible;

        public IRelayCommand NavigateToInventoryCommand { get; }
        public IRelayCommand NavigateToSalesCommand { get; }

        public HomeViewModel()
        {
            //LoadPermissions();

            NavigateToInventoryCommand = new RelayCommand(async () =>
            {
                await Shell.Current.GoToAsync(nameof(InventoryMenuPage));
            });

            NavigateToSalesCommand = new RelayCommand(async () =>
            {
                await Shell.Current.GoToAsync(nameof(SalesMenuPage));
            });
        }

        public void LoadPermissions()
        {
            var modulesJson = Preferences.Get("modules", "{}");

            var modules = JsonSerializer.Deserialize<Dictionary<string, bool>>(modulesJson)
                          ?? new Dictionary<string, bool>();

            InventorizationVisible = modules.GetValueOrDefault("inventorization");
            TransferVisible = modules.GetValueOrDefault("transfer");
            ReceiveVisible = modules.GetValueOrDefault("receive");
            SalesVisible = modules.GetValueOrDefault("sales");
        }
    }
}