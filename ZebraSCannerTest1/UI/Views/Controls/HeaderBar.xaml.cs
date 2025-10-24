using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views.Controls
{
    public partial class HeaderBar : ContentView
    {
        public HeaderBar()
        {
            InitializeComponent();
            BindingContext = MauiProgram.ServiceProvider.GetRequiredService<ShellViewModel>();
        }
    }
}
