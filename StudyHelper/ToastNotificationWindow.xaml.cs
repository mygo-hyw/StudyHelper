using System;
using System.Windows;
using System.Windows.Threading;
using StudyHelper.Models;
using StudyHelper.ViewModels;

namespace StudyHelper
{
    public partial class ToastNotificationWindow : Window
    {
        private readonly LearningTask _task;
        private readonly MainViewModel _viewModel;
        private DispatcherTimer _autoCloseTimer;
        private const int AUTO_CLOSE_SECONDS = 5;

        public ToastNotificationWindow(LearningTask task, MainViewModel viewModel)
        {
            InitializeComponent();
            _task = task;
            _viewModel = viewModel;

            TaskTitleTextBlock.Text = task.Title;
            PriorityTextBlock.Text = task.Priority;

            Loaded += (s, e) => PositionWindow();

            InitializeAutoCloseTimer();
        }

        private void PositionWindow()
        {
            // 强制更新布局以获取实际尺寸
            this.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            this.Arrange(new Rect(new System.Windows.Point(0, 0), this.DesiredSize));
            this.UpdateLayout();

            _viewModel.IncrementNotificationCount();

            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null) return;

            double screenWidth = screen.WorkingArea.Width;
            double screenHeight = screen.WorkingArea.Height;

            int notificationIndex = _viewModel.GetActiveNotificationCount();
            const double MARGIN = 15;
            const double SPACING = 10;

            double windowWidth = this.ActualWidth;
            double windowHeight = this.ActualHeight;

            double left = screenWidth - windowWidth - MARGIN;
            double top = screenHeight - (windowHeight + MARGIN) - (notificationIndex - 1) * (windowHeight + SPACING);

            this.Left = left;
            this.Top = top;
        }

        private void InitializeAutoCloseTimer()
        {
            _autoCloseTimer = new DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(AUTO_CLOSE_SECONDS);
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer.Stop();
                CloseNotification();
            };
            _autoCloseTimer.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseNotification();
        }

        private void AckButton_Click(object sender, RoutedEventArgs e)
        {
            CloseNotification();
        }

        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.IgnoreTaskReminder(_task.Id);
            CloseNotification();
        }

        private void CloseNotification()
        {
            _autoCloseTimer?.Stop();
            _viewModel.DecrementNotificationCount();
            this.Close();
        }
    }
}