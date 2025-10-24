using CommunityToolkit.Mvvm.Input;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels
{
    internal class HomeViewModel
    {
        public IRelayCommand NavigateToInventoryCommand { get; }

        public HomeViewModel()
        {
            NavigateToInventoryCommand = new RelayCommand(async () =>
            {
                await Shell.Current.GoToAsync(nameof(InventoryMenuPage));
            });
        }

    }
}
