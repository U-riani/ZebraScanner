using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace ZebraSCannerTest1.Core.Services
{
    public class ClipboardService
    {
        public async Task CopyAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            await Clipboard.SetTextAsync(text);

            var toast = Toast.Make($"Copied: {text}", ToastDuration.Short, 14);
            await toast.Show();
        }
    }
}
