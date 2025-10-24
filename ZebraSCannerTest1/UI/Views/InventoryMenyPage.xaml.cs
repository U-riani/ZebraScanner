namespace ZebraSCannerTest1.UI.Views;

public partial class InventoryMenuPage : ContentPage
{
    public InventoryMenuPage()
    {
        InitializeComponent();
    }

    private async void OnLootsClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Coming soon", "LOOTS feature is under construction.", "OK");
    }

    private async void OnBarcodesClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(InventorizationPage));
    }
}
