using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using StudyHelper.ViewModels;
using Microsoft.Toolkit.Uwp.Notifications; // 👈 1. 引入系统通知命名空间

namespace StudyHelper
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;
        private SettingsWindow? _settingsWindow;

        // 注意：这里已经初始化了全局唯一的 MainViewModel
        public MainViewModel MainViewModel { get; } = new MainViewModel();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 👈 2. 监听系统原生通知的点击事件（必须在最开始初始化）
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                // 解析通知参数
                ToastArguments args = ToastArguments.Parse(toastArgs.Argument);

                // 获取任务 ID 和动作
                if (args.TryGetValue("taskId", out string taskId) && args.TryGetValue("action", out string action))
                {
                    // ⚠️ 注意：此回调发生在操作系统的后台线程，操作 UI 或 ViewModel 必须通过 Dispatcher 切换回主线程
                    Current.Dispatcher.Invoke(() =>
                    {
                        HandleNotificationAction(action, taskId);
                    });
                }
            };

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

        // 👈 3. 新增：处理通知点击后的具体业务逻辑
        private void HandleNotificationAction(string action, string taskId)
        {
            // 👈 核心修改：将 string 类型的 taskId 转换为 Guid 类型
            if (!Guid.TryParse(taskId, out Guid taskGuid))
            {
                System.Diagnostics.Debug.WriteLine($"[通知反馈] 转换 Guid 失败，无效的 ID 格式: {taskId}");
                return;
            }

            if (action == "ack")
            {
                MainViewModel.DismissForDay(taskGuid);
            }
            else if (action == "ignore")
            {
                // 👈 这里传入转换后的 taskGuid (Guid 类型)，即可完美解决 CS1503 报错
                MainViewModel.IgnoreTaskReminder(taskGuid);
                System.Diagnostics.Debug.WriteLine($"[通知反馈] 用户点击了“过会儿提醒”，任务ID: {taskGuid}");
            }
            else if (action == "clickTask")
            {
                // 用户直接点击了整个通知卡片：唤醒/激活看板主窗口
                if (_mainWindow != null)
                {
                    if (_mainWindow.WindowState == WindowState.Minimized)
                    {
                        _mainWindow.WindowState = WindowState.Normal;
                    }
                    _mainWindow.Show();
                    _mainWindow.Activate();
                }
            }
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

            // 👈 4. 退出程序时，卸载并清理系统通知关联
            ToastNotificationManagerCompat.Uninstall();

            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            // 👈 5. 退出程序时，卸载并清理系统通知关联
            ToastNotificationManagerCompat.Uninstall();

            base.OnExit(e);
        }
    }
}