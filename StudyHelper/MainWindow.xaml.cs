using System.Windows;
using StudyHelper.ViewModels;

namespace StudyHelper
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 将前端 XAML 与后端的控制器 MainViewModel 数据中心进行绑定 [COMMON]
            this.DataContext = new MainViewModel();
        }
    }
}