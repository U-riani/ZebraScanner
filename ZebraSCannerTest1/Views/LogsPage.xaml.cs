using CommunityToolkit.Mvvm.Messaging;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.ViewModels;

namespace ZebraSCannerTest1.Views;

public partial class LogsPage : ContentPage
{
    private readonly LogsViewModel _viewModel;
    public LogsPage(LogsViewModel vm)
    {
        InitializeComponent();
        //BindingContext = vm; // <-- DI-injected ViewModel
        BindingContext = _viewModel = vm;

        // Subscribe to new ScanLog messages
        //WeakReferenceMessenger.Default.Register<NewScanLogMessage>(this, (r, m) =>
        //{
        //    // Scroll to top
        //    MainThread.BeginInvokeOnMainThread(() =>
        //    {
        //        var logsVm = (LogsViewModel)BindingContext;

        //        // Insert new log at the top
        //        logsVm.Logs.Insert(0, m.Value);

        //        // Scroll to top
        //        logsCollectionView.ScrollTo(logsVm.Logs[0], position: ScrollToPosition.Start, animate: true);
        //    });
        //});
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var vm = BindingContext as LogsViewModel;
        if (vm != null && vm.Logs.Count > 0)
        {
            logsCollectionView.ScrollTo(vm.Logs[0], position: ScrollToPosition.Start, animate: false);
        }
        //if (((LogsViewModel)BindingContext).Logs.Count > 0)
        //    logsCollectionView.ScrollTo(((LogsViewModel)BindingContext).Logs[0], position: ScrollToPosition.Start, animate: false);
    }

}
