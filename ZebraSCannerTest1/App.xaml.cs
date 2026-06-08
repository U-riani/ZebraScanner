using ZebraSCannerTest1.UI.ViewModels;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1
{
    public partial class App : Application
    {
        public App(ShellViewModel vm)
        {
            InitializeComponent();

            //var shell = new AppShell(vm);

            // REMOVE OR COMMENT THIS LINE – it disables the flyout and custom icon won't show properly
            // shell.FlyoutBehavior = FlyoutBehavior.Disabled;

            MainPage = MauiProgram.ServiceProvider.GetRequiredService<LoginPage>();
        }
    }
}