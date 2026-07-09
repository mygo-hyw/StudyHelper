using System.Windows;
using StudyHelper.ViewModels;

namespace StudyHelper
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = ((App)System.Windows.Application.Current).MainViewModel;
        }
    }
}
