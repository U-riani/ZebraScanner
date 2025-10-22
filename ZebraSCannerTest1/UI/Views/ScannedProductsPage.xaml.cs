using Microsoft.Maui.Controls;
using ZebraSCannerTest1.UI.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;
using ZebraSCannerTest1.Views.Popups;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.UI.Views;

public partial class ScannedProductsPage : ContentPage
{
    private readonly ScannedProductsViewModel _vm;
    private bool _navigatingToDetails = false;
    private bool _manualFilterOpen = false; // ✅ new guard flag


    public ScannedProductsPage(ScannedProductsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private async void OnManualFilterClicked(object sender, EventArgs e)
    {
        try
        {
            // mark page as busy while modal is open
            _vm.IsLoading = true;
            _manualFilterOpen = true; // ✅ mark as active

            // open popup
            var popup = new ManualFilterPopup();
            await Navigation.PushModalAsync(popup);

            var result = await popup.Result;

            // if user canceled — just exit quietly
            if (string.IsNullOrWhiteSpace(result))
            {
                return;
            }

            // apply filter normally
            _vm.ApplyManualFilter(result);
            await DisplayAlert("✅ Manual Filter Applied", result, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            _vm.IsLoading = false;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // ✅ skip reload if manual filter just closed
        if (_manualFilterOpen)
            return;

        if (BindingContext is not ScannedProductsViewModel vm)
            return;

        // Only reload when it's actually needed
        if (!vm.NeedsReload)
            return;

        vm.IsInitialLoading = true;

        // small delay to let the navigation animation complete fully
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(800), async () =>
        {
            try
            {
                vm.NeedsReload = false;
                await vm.LoadAsync(reset: true);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                vm.IsInitialLoading = false;
            }
        });

        // make sure UI has settled before triggering DB load
        //await MainThread.InvokeOnMainThreadAsync(async () =>
        //{
        //    try
        //    {
        //        vm.NeedsReload = false;
        //        await vm.LoadAsync(reset: true);
        //    }
        //    catch (Exception ex)
        //    {
        //        await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        //    }
        //    finally
        //    {
        //        vm.IsInitialLoading = false;
        //    }
        //});
    }

    private async void OnDetailsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not ScannedProductsViewModel vm)
            return;

        if ((sender as Button)?.BindingContext is StatsProduct product)
        {
            _navigatingToDetails = true;
            await vm.OpenDetailsAsync(product);
        }
    }


    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (BindingContext is ScannedProductsViewModel vm)
        {
            // Only mark for reload if truly leaving the page (not going to details)
            if (_navigatingToDetails || _manualFilterOpen)
            {
                _navigatingToDetails = false; // reset
            }
            else
            {
                vm.NeedsReload = true; // mark for next time
            }
        }
    }


}
