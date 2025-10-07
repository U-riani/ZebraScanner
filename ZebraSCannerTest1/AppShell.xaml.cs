using ZebraSCannerTest1.Views;
using ZebraSCannerTest1.ViewModels;

namespace ZebraSCannerTest1
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(DetailsPage), typeof(DetailsPage));
            Routing.RegisterRoute(nameof(LogsPage), typeof(LogsPage));
            Routing.RegisterRoute(nameof(ScannedProductsPage), typeof(ScannedProductsPage));

            Navigating += OnShellNavigating;
        }

        private void OnShellNavigating(object sender, ShellNavigatingEventArgs e)
        {
            // Ignore forward navigations
            if (e.Source == ShellNavigationSource.Push)
                return;

            if (CurrentPage is DetailsPage detailsPage)
            {
                if (detailsPage.BindingContext is DetailsViewModel vm && vm.HasUnsavedChanges)
                {
                    // 🛑 Cancel navigation FIRST, before any await
                    e.Cancel();

                    // Then show popup after a tiny delay to ensure cancel is registered
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        bool stay = await Shell.Current.DisplayAlert(
                            "Unsaved Changes",
                            "You have unsaved changes.\n\nPress 'Save' to keep your edits, or 'Leave' to discard.",
                            "Stay", "Leave");

                        if (!stay)
                        {
                            vm.HasUnsavedChanges = false;
                            await Shell.Current.GoToAsync("..");
                        }
                    });
                }
            }
        }

    }
}
