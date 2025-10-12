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

            //Navigating += OnShellNavigating;
        }

        //private void OnShellNavigating(object sender, ShellNavigatingEventArgs e)
        //{
        //    if (e.Source == ShellNavigationSource.Pop && e.Current?.Location.OriginalString.Contains(nameof(DetailsPage)) == true)
        //    {
        //        // 🚫 We're navigating back from DetailsPage → mark flag
        //        if (Application.Current?.MainPage is AppShell shell)
        //        {
        //            var page = shell.CurrentPage as ScannedProductsPage;
        //            if (page != null)
        //                page.IsReturningFromDetails = true;
        //        }
        //    }

        //}

    }
}
