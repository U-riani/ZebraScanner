using CommunityToolkit.Maui.Views;

namespace ZebraSCannerTest1.UI.Views.Popups;

public partial class ProgressPopup : Popup
{
    public ProgressPopup(string message = "Loading...")
    {
        InitializeComponent();
        MessageLabel.Text = message;
    }

    public void UpdateMessage(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MessageLabel.Text = message;
        });
    }
}
