using Microsoft.Maui.Controls;

namespace ZebraSCannerTest1.Views.Popups
{
    public partial class ExportProgressPopup : ContentView
    {
        public ExportProgressPopup()
        {
            InitializeComponent();
        }

        public void UpdateProgress(double progress, string status)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ProgressBar.Progress = progress;
                StatusLabel.Text = status;
            });
        }
    }
}
