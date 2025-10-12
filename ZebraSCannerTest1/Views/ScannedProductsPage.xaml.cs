using Microsoft.Maui.Controls;
using ZebraSCannerTest1.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Views;

public partial class ScannedProductsPage : ContentPage
{
    private readonly ScannedProductsViewModel _vm;

    public ScannedProductsPage(ScannedProductsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private async void OnManualFilterClicked(object sender, EventArgs e)
    {
        var popup = new ManualFilterPopup();
        await Navigation.PushModalAsync(popup);

        var result = await popup.Result;

        if (!string.IsNullOrWhiteSpace(result))
        {
            _vm.ApplyManualFilter(result);
            await DisplayAlert("✅ Manual Filter Applied", result, "OK");
        }
        else
        {
            await DisplayAlert("❌ Cancelled", "No filter applied.", "OK");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is not ScannedProductsViewModel vm)
            return;

        // show immediately, start spinner
        vm.IsInitialLoading = true;

        if (!vm.NeedsReload)
            return;

        vm.NeedsReload = false;

        // start background task after slight delay (so UI renders first)
        await Task.Delay(200);

        var cts = new CancellationTokenSource();
        vm.GetType().GetField("_loadCts",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(vm, cts);

        var token = cts.Token;

        // run in background, never block UI
        _ = Task.Run(async () =>
        {
            try
            {
                await vm.LoadAsync(true, token);
            }
            catch (OperationCanceledException) { /* ignore */ }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Shell.Current.DisplayAlert("Error", ex.Message, "OK"));
            }
        }, token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (BindingContext is ScannedProductsViewModel vm)
        {
            var field = vm.GetType().GetField("_loadCts",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field?.GetValue(vm) is CancellationTokenSource cts)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }
}
