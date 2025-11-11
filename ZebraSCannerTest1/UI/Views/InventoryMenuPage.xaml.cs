using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.UI.Views;

public partial class InventoryMenuPage : ContentPage
{
    public InventoryMenuPage()
    {
        InitializeComponent();
    }

    private async void OnLootsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(InventorizationMenuPage),
            new Dictionary<string, object> { ["Mode"] = InventoryMode.Loots });
    }

    private async void OnBarcodesClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(InventorizationMenuPage),
            new Dictionary<string, object> { ["Mode"] = InventoryMode.Standard });
    }

    private async void OnCombosClicked(object sender, EventArgs e)
    {
        await Shell.Current.DisplayAlert(null, "Will be added soon", "Cancel");
    }
}
