using ZebraSCannerTest1.Core.Interfaces;

namespace ZebraSCannerTest1.UI.Services;

public class MauiDialogService : IDialogService
{
    public Task ShowMessageAsync(string title, string message)
        => Shell.Current.DisplayAlert(title, message, "OK");

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
        => Shell.Current.DisplayAlert(title, message, accept, cancel);
}
