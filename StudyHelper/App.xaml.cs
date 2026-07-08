using System;
using System.Windows;
using System.Windows.Forms; // 导入托盘组件命名空间 [COMMON]
using System.Drawing;

namespace StudyHelper
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow; // 桌面内嵌看板
        private SettingsWindow? _settingsWindow; // 独立设置界面

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 初始化系统托盘图标
            _notifyIcon = new NotifyIcon();
            // 使用 Windows 系统默认的应用图标，避免因找不到图标路径而崩溃 [COMMON]
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = "2.5 学习养成计划";
            _notifyIcon.Visible = true;

            // 绑定双击托盘图标打开设置界面的事件 [COMMON]
            _notifyIcon.DoubleClick += (s, args) => ShowSettingsWindow();

            // 创建托盘右键菜单 [COMMON]
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("打开设置中心", null, (s, args) => ShowSettingsWindow());
            contextMenu.Items.Add("退出程序", null, (s, args) => ExitApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;

            // 2. 启动并显示桌面主看板 [COMMON]
            _mainWindow = new MainWindow();
            _mainWindow.Show();
        }

        public void ShowSettingsWindow()
        {
            // 如果设置窗口未实例化或已被关闭，重新创建它 [COMMON]
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow();
            }

            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }
            _settingsWindow.Show();
            _settingsWindow.Activate(); // 唤醒至最前端 [COMMON]
        }

        public void ExitApplication()
        {
            // 释放托盘图标，防止程序退出后右下角残留图标死尸 [COMMON]
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            System.Windows.Application.Current.Shutdown(); // 显式退出程序 [COMMON]
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
