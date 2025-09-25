using ZebraSCannerTest1.ViewModels;

namespace ZebraSCannerTest1
{
    public partial class App : Application
    {
        public App(MainViewModel vm)
        {
            InitializeComponent();
            // MainPage created via DI elsewhere; Shell is fine to construct directly
            MainPage = new AppShell();
        }
    }
}
