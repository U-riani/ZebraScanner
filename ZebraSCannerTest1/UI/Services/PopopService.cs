using CommunityToolkit.Maui.Views;
using ZebraSCannerTest1.UI.Views.Popups;

namespace ZebraSCannerTest1.UI.Services;

public class PopupService
{
    private ProgressPopup? _popup;

    public async Task ShowProgressAsync(string message = "Loading...")
    {
        if (Application.Current?.MainPage is null) return;

        _popup = new ProgressPopup(message);
        Application.Current?.MainPage?.ShowPopup(_popup);

    }

    public void UpdateMessage(string message)
    {
        _popup?.UpdateMessage(message);
    }

    public void Close()
    {
        _popup?.Close();
        _popup = null;
    }
}
