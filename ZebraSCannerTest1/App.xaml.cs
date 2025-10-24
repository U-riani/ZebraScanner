using ZebraSCannerTest1.UI.ViewModels;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1
{
    public partial class App : Application
    {
        public App(ShellViewModel vm)
        {
            InitializeComponent();

            var shell = new AppShell(vm);
            shell.FlyoutBehavior = FlyoutBehavior.Disabled; // single, safe instance
            MainPage = shell;



        }
    }
}
