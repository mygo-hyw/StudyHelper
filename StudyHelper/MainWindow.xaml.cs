using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StudyHelper.ViewModels;

namespace StudyHelper
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = ((App)System.Windows.Application.Current).MainViewModel;
            DataContext = _viewModel;

            Loaded += (_, _) => ApplyWindowSettings();
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.WindowOpacity) or nameof(MainViewModel.IsDesktopEmbedded))
            {
                Dispatcher.Invoke(ApplyWindowSettings);
            }
        }

        private void ApplyWindowSettings()
        {
            Opacity = _viewModel.WindowOpacity;
            ShowInTaskbar = !_viewModel.IsDesktopEmbedded;

            if (_viewModel.IsDesktopEmbedded)
            {
                SendToBottom();
            }
            else
            {
                Activate();
            }
        }

        private void SendToBottom()
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hWnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 修正：显式指定调用 System.Windows 下的 WPF 主程序实例 [COMMON]
            ((App)System.Windows.Application.Current).ExitApplication();
        }
    }
}
