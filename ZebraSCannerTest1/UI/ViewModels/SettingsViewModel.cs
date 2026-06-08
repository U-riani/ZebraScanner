using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZebraSCannerTest1.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isLootsMode; // true when coming from LootsScanningPage

        public IRelayCommand GoToScannedProductsCommand { get; }
        public IRelayCommand GoToLogsCommand { get; }
        public IRelayCommand ShowResultsCommand { get; }
        public IRelayCommand BackCommand { get; }

        public SettingsViewModel()
        {
            // Commands – navigate using Shell routes (adjust if your routes are different)
            GoToScannedProductsCommand = new RelayCommand(async () =>
                await Shell.Current.GoToAsync("///ScannedProductsPage")); // or your custom route

            GoToLogsCommand = new RelayCommand(async () =>
                await Shell.Current.GoToAsync("///LogsPage"));

            ShowResultsCommand = new RelayCommand(async () =>
                await Shell.Current.GoToAsync("///ResultsPage")); // or show popup/alert

            // Back button – correct Shell navigation
            BackCommand = new RelayCommand(async () =>
            {
                await Shell.Current.GoToAsync(".."); // Go back one page
            });
        }

        // Call this method when navigating from LootsScanningPage
        public void SetLootsMode(bool isLoots)
        {
            IsLootsMode = isLoots;
        }
    }
}