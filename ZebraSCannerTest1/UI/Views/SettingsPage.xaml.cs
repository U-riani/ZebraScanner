using ZebraSCannerTest1.UI.ViewModels; // ? This fixes CS0246

namespace ZebraSCannerTest1.UI.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}