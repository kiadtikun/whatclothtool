using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace mjxClothTool.Views
{
    public interface ISplashScreen
    {
        void ReportProgress(double progress);
        Task LoadComplete();
    }

    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : Window, ISplashScreen
    {
        public SplashScreen()
        {
            InitializeComponent();
        }

        public void ReportProgress(double progress)
        {
            Dispatcher.InvokeAsync(() => LoadingProgress.Value = progress);
        }

        public async Task LoadComplete()
        {
            await Dispatcher.InvokeAsync(() => LoadingProgress.Value = LoadingProgress.Maximum);
            Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);

            var mainWindow = Application.Current.MainWindow;
            mainWindow.Visibility = Visibility.Visible;
            // Allow the splash to close and the main window to render before taking focus.
            await Task.Delay(250);
            mainWindow.Activate();
        }
    }
}

