using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1
{
    public partial class App : Application
    {
        public App(MainViewModel vm)
        {
            InitializeComponent();
            MainPage = new AppShell();
        }
    }
}
