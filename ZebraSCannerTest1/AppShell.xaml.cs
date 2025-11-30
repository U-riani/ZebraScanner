using System.Windows.Input;
using ZebraSCannerTest1.UI.ViewModels;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1
{
    public partial class AppShell : Shell
    {
        public AppShell(ShellViewModel vm)
        {
            FlyoutBehavior = FlyoutBehavior.Disabled;
            InitializeComponent();


            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(InventoryMenuPage), typeof(InventoryMenuPage));
            Routing.RegisterRoute(nameof(InventorizationPage), typeof(InventorizationPage));
            Routing.RegisterRoute(nameof(ScannedProductsPage), typeof(ScannedProductsPage));
            Routing.RegisterRoute(nameof(LogsPage), typeof(LogsPage));
            Routing.RegisterRoute(nameof(DetailsPage), typeof(DetailsPage));
            Routing.RegisterRoute(nameof(InventorizationByLootsMenuPage), typeof(InventorizationByLootsMenuPage));
            Routing.RegisterRoute(nameof(InventorizationByLootsPage), typeof(InventorizationByLootsPage));
            Routing.RegisterRoute(nameof(LootsScanningPage), typeof(LootsScanningPage));
            Routing.RegisterRoute(nameof(InventorizationMenuPage), typeof(InventorizationMenuPage));
            Routing.RegisterRoute(nameof(SalesMenuPage), typeof(SalesMenuPage));
            Routing.RegisterRoute(nameof(SalesPage), typeof(SalesPage));

            BindingContext = vm;
        }
        public ICommand ToggleFlyoutCommand => new Command(() =>
        {
            Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
        });


    }
}
