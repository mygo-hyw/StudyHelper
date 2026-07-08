using System;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using StudyHelper.ViewModels;

namespace StudyHelper
{
    public partial class MainWindow : Window
    {
        // 导入 Windows 原生 API 用以控制窗口物理层级 [COMMON]
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1); // 置于最底层 [COMMON]
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();

            // 核心：加载时和失去焦点时，确保自己死死贴在系统桌面最底层 [COMMON]
            this.Loaded += (s, e) => SendToBottom();
            this.Deactivated += (s, e) => SendToBottom();
        }

        private void SendToBottom()
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hWnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 调用全局退出，彻底关闭程序 [COMMON]
            ((App)System.Windows.Application.Current).ExitApplication();
        }
    }
}