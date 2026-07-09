using System.Windows;
using StudyHelper.ViewModels;

namespace StudyHelper
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow() : this(new MainViewModel())
        {
        }

        public SettingsWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = ((App)System.Windows.Application.Current).MainViewModel;
        }
    }
}