using ZebraSCannerTest1.Views;

namespace ZebraSCannerTest1
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            //Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
            Routing.RegisterRoute(nameof(DetailsPage), typeof(DetailsPage));
            Routing.RegisterRoute(nameof(LogsPage), typeof(LogsPage));

        }
    }
}
