using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StudyHelper.ViewModels;
using M = System.Windows.Media;

namespace StudyHelper
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = ((App)System.Windows.Application.Current).MainViewModel;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ColorCalendarDays();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }
            DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CheckInCalendar_Loaded(object sender, RoutedEventArgs e)
        {
            ColorCalendarDays();
        }

        private void CheckInCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            ColorCalendarDays();
        }

        private void CheckInCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            ColorCalendarDays();
        }

        private void CheckInTab_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                ColorCalendarDays();
        }

        private void ColorCalendarDays()
        {
            if (CheckInCalendar == null) return;
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                HighlightDays(vm);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        public void RefreshCalendarColors()
        {
            ColorCalendarDays();
        }

        private void HighlightDays(MainViewModel vm, int retry = 0)
        {
            var calendar = CheckInCalendar;
            if (calendar == null) return;

            var calendarItems = calendar.Template?.FindName("CalendarItem", calendar) as FrameworkElement;
            if (calendarItems == null)
            {
                if (retry < 5)
                    Dispatcher.BeginInvoke(new Action(() => HighlightDays(vm, retry + 1)), System.Windows.Threading.DispatcherPriority.ContextIdle);
                return;
            }

            calendar.UpdateLayout();
            calendarItems.UpdateLayout();

            var monthControl = FindVisualChild<ItemsControl>(calendarItems);
            if (monthControl == null) return;

            foreach (var item in monthControl.Items)
            {
                var container = monthControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container == null) continue;

                var dayButton = FindVisualChild<System.Windows.Controls.Primitives.CalendarDayButton>(container);
                if (dayButton == null) continue;

                if (dayButton.DataContext is DateTime date)
                {
                    int status = vm.GetCompletionPercent(date);
                    if (status >= 100)
                        dayButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 34, 197, 94));
                    else if (status >= 50)
                        dayButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 37, 99, 235));
                    else
                        dayButton.Background = System.Windows.Media.Brushes.Transparent;

                    // 构建自定义 ToolTip
                    var dayTasks = (DataContext as MainViewModel)?.Tasks.Where(t => t.TargetTime.Date == date.Date).ToList();
                    var tipPanel = new StackPanel { Margin = new Thickness(4) };
                    tipPanel.Children.Add(new TextBlock
                    {
                        Text = date.ToString("yyyy-MM-dd"),
                        FontWeight = FontWeights.Bold,
                        FontSize = 13,
                        Foreground = M.Brushes.Black,
                        Margin = new Thickness(0, 0, 0, 6)
                    });

                    if (dayTasks == null || dayTasks.Count == 0)
                    {
                        tipPanel.Children.Add(new TextBlock
                        {
                            Text = "该日无任务",
                            FontSize = 12,
                            Foreground = System.Windows.Media.Brushes.Gray
                        });
                    }
                    else
                    {
                        foreach (var task in dayTasks)
                        {
                            int done = task.SubTasks.Count(s => s.IsCompleted);
                            int total = task.SubTasks.Count;
                            string emoji = task.IsCompleted ? "✅ " : (done > 0 ? "🔄 " : "⬜ ");
                            var taskLine = new TextBlock
                            {
                                Text = $"{emoji}{task.Title}",
                                FontSize = 12,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = M.Brushes.Black,
                                Margin = new Thickness(0, 0, 0, 2)
                            };
                            tipPanel.Children.Add(taskLine);

                            var progressLine = new TextBlock
                            {
                                Text = $"    完成进度: {done}/{total} ({(total > 0 ? done * 100 / total : 0)}%)",
                                FontSize = 11,
                                Foreground = M.Brushes.DimGray,
                                Margin = new Thickness(0, 0, 0, 4)
                            };
                            tipPanel.Children.Add(progressLine);
                        }
                    }

                    dayButton.ToolTip = new System.Windows.Controls.ToolTip
                    {
                        Content = tipPanel,
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(245, 255, 255, 255)),
                        BorderBrush = System.Windows.Media.Brushes.LightGray,
                        BorderThickness = new Thickness(1),
                        FontSize = 12,
                        MaxWidth = 300
                    };
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found) return found;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
