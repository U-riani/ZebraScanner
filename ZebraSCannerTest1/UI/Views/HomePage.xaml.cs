using ZebraSCannerTest1.UI.ViewModels;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.Views;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
        BindingContext = new HomeViewModel();
    }

    protected override async void OnAppearing()
    {
        var readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
        var writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();

        var mediaStatus = await Permissions.RequestAsync<Permissions.Media>();


        if (readStatus != PermissionStatus.Granted ||
            writeStatus != PermissionStatus.Granted ||
            mediaStatus != PermissionStatus.Granted)

        {
            await DisplayAlert("Permission needed", "Storage access is required to export Excel files.", "OK");
        }
    }
}
