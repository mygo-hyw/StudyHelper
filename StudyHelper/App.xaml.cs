using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using StudyHelper.ViewModels;

namespace StudyHelper
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;
        private SettingsWindow? _settingsWindow;
        public MainViewModel MainViewModel { get; } = new MainViewModel();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 初始化系统托盘图标
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = "2.5 学习养成计划";
            _notifyIcon.Visible = true;

            _notifyIcon.DoubleClick += (s, args) => ShowSettingsWindow();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("打开设置中心", null, (s, args) => ShowSettingsWindow());
            contextMenu.Items.Add("退出程序", null, (s, args) => ExitApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;

            // 2. 启动并显示桌面主看板
            _mainWindow = new MainWindow();
            _mainWindow.DataContext = MainViewModel;
            _mainWindow.Show();
        }

        public void ShowSettingsWindow()
        {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.DataContext = MainViewModel;
            }

            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        public void ExitApplication()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnExit(e);
        }
    }
}