using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace ZebraSCannerTest1.UI.Views;

public partial class SectionPopup : Popup
{
    public static readonly BindableProperty SectionTextProperty =
        BindableProperty.Create(nameof(SectionText), typeof(string), typeof(SectionPopup), string.Empty);

    public string SectionText
    {
        get => (string)GetValue(SectionTextProperty);
        set => SetValue(SectionTextProperty, value);
    }

    public SectionPopup(string section)
    {
        InitializeComponent();
        SectionText = section ?? "(empty)";
        BindingContext = this;
    }

    private async void OnLabelTapped(object sender, TappedEventArgs e)
    {
        if (sender is Label lbl && !string.IsNullOrWhiteSpace(lbl.Text))
        {
            await Clipboard.SetTextAsync(lbl.Text);
            var toast = CommunityToolkit.Maui.Alerts.Toast.Make("Copied to clipboard");
            await toast.Show();
        }
    }
}
