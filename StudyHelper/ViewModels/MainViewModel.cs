using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Windows.Threading;
using System.Windows;
using StudyHelper.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace StudyHelper.ViewModels
{
    // 必须标记为 partial 关键字，以便自动化框架生成属性绑定代码 [COMMON]
    public partial class MainViewModel : ObservableObject
    {
        private readonly DispatcherTimer _reminderTimer = new(); // 提醒定时器 [COMMON]

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
        private string _newTaskTitle = string.Empty;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddSubTaskCommand))]
        private string _newSubTaskTitle = string.Empty;
        [ObservableProperty] private string _selectedPriority = "中";
        [ObservableProperty] private bool _needsReview = false;
        [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
        [ObservableProperty] private string _selectedHour = DateTime.Now.Hour.ToString("00");
        [ObservableProperty] private string _selectedMinute = DateTime.Now.Minute.ToString("00");

        public ObservableCollection<string> Hours { get; } = new(Enumerable.Range(0, 24).Select(hour => hour.ToString("00")));
        public ObservableCollection<string> Minutes { get; } = new(Enumerable.Range(0, 60).Select(minute => minute.ToString("00")));
        public ObservableCollection<string> Priorities { get; } = new() { "高", "中", "低" };
        public ObservableCollection<LearningTask> Tasks { get; } = new();
        public ObservableCollection<LearningSubTask> DraftSubTasks { get; } = new();
        public ObservableCollection<DateTime> HistoryCheckInDates { get; } = new();

        // 绑定到前端 LiveCharts [COMMON] 图表的数据源
        [ObservableProperty] private ISeries[] _series = Array.Empty<ISeries>();

        // 分析与打卡统计数据 [COMMON]
        [ObservableProperty] private int _totalAddedCount = 0;
        [ObservableProperty] private double _completionRate = 0;
        [ObservableProperty] private int _streakDays = 0;

        public MainViewModel()
        {
            LoadRecommendedTasks();
            InitializeTimer();
            UpdateStatistics();
        }

        private void LoadRecommendedTasks()
        {
            Tasks.Add(new LearningTask { Title = "【系统推荐】复习专业核心算法与错题", Priority = "高", NeedsReview = true, TargetTime = DateTime.Today.AddHours(20) });
            Tasks.Add(new LearningTask { Title = "【系统推荐】英语学术论文阅读 2 篇", Priority = "中", NeedsReview = false, TargetTime = DateTime.Today.AddHours(22) });
        }

        // 初始化定时器（满足任务提醒功能） [COMMON]
        private void InitializeTimer()
        {
            _reminderTimer.Interval = TimeSpan.FromSeconds(30); // 实际项目可以设为 1 分钟，此处设为 30 秒以便快速测试 [COMMON]
            _reminderTimer.Tick += CheckTasksForReminders;
            _reminderTimer.Start();
        }

        // 根据到期时间与优先级触发提醒（弹窗通知） [COMMON]
        // 根据优先级与完成时间对用户进行弹窗提醒 [COMMON]
        // 根据优先级与完成时间对用户进行弹窗提醒 [COMMON]
        private void CheckTasksForReminders(object? sender, EventArgs e)
        {
            UpdateTaskDeadlineStates();

            var now = DateTime.Now;
            foreach (var task in Tasks.Where(t => !t.IsCompleted))
            {
                // 如果任务设定的目标时间已到或即将超时 [COMMON]
                if (task.TargetTime.Date == now.Date)
                {
                    string alertMsg = $"【学习任务提醒】\n任务：{task.Title}\n优先级：{task.Priority}";
                    if (task.Priority == "高")
                    {
                        // 修正：显式指定调用 System.Windows 下的 WPF 弹窗组件 [COMMON]
                        System.Windows.MessageBox.Show(alertMsg, "高优先级红色警报", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                    else if (task.Priority == "中" && now.Minute % 30 == 0) // 中优先级每 30 分钟提醒 [COMMON]
                    {
                        // 修正：显式指定调用 System.Windows 下的 WPF 弹窗组件 [COMMON]
                        System.Windows.MessageBox.Show(alertMsg, "普通任务提醒", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                }
            }
        }

        [RelayCommand]
        private void ExitApplication()
        {
            if (System.Windows.Application.Current is App app)
            {
                app.ExitApplication();
                return;
            }

            System.Windows.Application.Current.Shutdown();
        }

        [RelayCommand(CanExecute = nameof(CanAddSubTask))]
        private void AddSubTask()
        {
            if (string.IsNullOrWhiteSpace(NewSubTaskTitle)) return;

            DraftSubTasks.Add(new LearningSubTask
            {
                Title = NewSubTaskTitle.Trim()
            });
            NewSubTaskTitle = string.Empty;
        }

        private bool CanAddSubTask()
        {
            return !string.IsNullOrWhiteSpace(NewSubTaskTitle);
        }

        [RelayCommand]
        private void RemoveDraftSubTask(LearningSubTask subTask)
        {
            if (subTask == null) return;

            DraftSubTasks.Remove(subTask);
        }

        [RelayCommand(CanExecute = nameof(CanAddTask))]
        private void AddTask()
        {
            if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;

            var task = new LearningTask
            {
                Title = NewTaskTitle.Trim(),
                Priority = SelectedPriority,
                NeedsReview = NeedsReview,
                TargetTime = BuildSelectedTargetTime(),
                SubTasks = new ObservableCollection<LearningSubTask>(
                    DraftSubTasks.Select(subTask => new LearningSubTask
                    {
                        Title = subTask.Title,
                        IsCompleted = subTask.IsCompleted
                    }))
            };
            Tasks.Add(task);
            NewTaskTitle = string.Empty; // 添加成功后清空文本框 [COMMON]
            DraftSubTasks.Clear();
            UpdateTaskDeadlineStates();
            UpdateStatistics();
        }

        private bool CanAddTask()
        {
            return !string.IsNullOrWhiteSpace(NewTaskTitle);
        }

        [RelayCommand]
        private void DeleteTask(LearningTask task)
        {
            if (task == null) return;

            Tasks.Remove(task);
            UpdateTaskDeadlineStates();
            UpdateStatistics();
            CompleteTaskCommand.NotifyCanExecuteChanged();
            CompleteSubTaskCommand.NotifyCanExecuteChanged();
        }

        // 按钮绑定命令：点击打卡 [COMMON]
        [RelayCommand(CanExecute = nameof(CanCompleteTask))]
        private void CompleteTask(LearningTask task)
        {
            if (task == null) return;
            UpdateTaskDeadlineStates();
            if (!CanCompleteTask(task)) return;

            CompleteTaskCore(task);
        }

        private bool CanCompleteTask(LearningTask task)
        {
            if (task == null || task.IsCompleted) return false;
            if (IsTaskOverdue(task)) return false;

            return task.SubTasks.Count == 0 || task.SubTasks.All(subTask => subTask.IsCompleted);
        }

        [RelayCommand(CanExecute = nameof(CanCompleteSubTask))]
        private void CompleteSubTask(LearningSubTask subTask)
        {
            if (subTask == null) return;

            var parentTask = Tasks.FirstOrDefault(task => task.SubTasks.Contains(subTask));
            if (parentTask == null || parentTask.IsCompleted) return;
            UpdateTaskDeadlineStates();
            if (IsTaskOverdue(parentTask)) return;

            subTask.IsCompleted = true;

            if (parentTask.SubTasks.All(item => item.IsCompleted))
            {
                CompleteTaskCore(parentTask);
                return;
            }

            RefreshTask(parentTask);
            CompleteTaskCommand.NotifyCanExecuteChanged();
            CompleteSubTaskCommand.NotifyCanExecuteChanged();
        }

        private bool CanCompleteSubTask(LearningSubTask subTask)
        {
            if (subTask == null || subTask.IsCompleted) return false;

            var parentTask = Tasks.FirstOrDefault(task => task.SubTasks.Contains(subTask));
            return parentTask is { IsCompleted: false } && !IsTaskOverdue(parentTask);
        }

        private void CompleteTaskCore(LearningTask task)
        {
            if (task.IsCompleted) return;

            task.IsCompleted = true;
            task.CompletedDate = DateTime.Today;
            task.IsDueSoon = false;
            task.IsOverdue = false;

            if (!HistoryCheckInDates.Any(date => date.Date == DateTime.Today))
            {
                HistoryCheckInDates.Add(DateTime.Today);
            }

            RefreshTask(task);

            UpdateTaskDeadlineStates();
            UpdateStatistics();
            CompleteTaskCommand.NotifyCanExecuteChanged();
            CompleteSubTaskCommand.NotifyCanExecuteChanged();
        }

        private void RefreshTask(LearningTask task)
        {
            int index = Tasks.IndexOf(task);
            if (index >= 0)
            {
                Tasks[index] = task;
            }
        }

        private DateTime BuildSelectedTargetTime()
        {
            int hour = int.TryParse(SelectedHour, out int parsedHour) ? parsedHour : 0;
            int minute = int.TryParse(SelectedMinute, out int parsedMinute) ? parsedMinute : 0;

            return SelectedDate.Date.AddHours(hour).AddMinutes(minute);
        }

        private static bool IsTaskOverdue(LearningTask task)
        {
            return !task.IsCompleted && task.TargetTime < DateTime.Now;
        }

        private void UpdateTaskDeadlineStates()
        {
            DateTime now = DateTime.Now;

            foreach (LearningTask task in Tasks)
            {
                if (task.IsCompleted)
                {
                    task.IsDueSoon = false;
                    task.IsOverdue = false;
                    continue;
                }

                task.IsOverdue = task.TargetTime < now;
                task.IsDueSoon = !task.IsOverdue && task.TargetTime <= now.AddHours(1);
            }

            CompleteTaskCommand.NotifyCanExecuteChanged();
            CompleteSubTaskCommand.NotifyCanExecuteChanged();
        }

        // 更新统计率并动态刷新柱状图数据 [COMMON]
        private void UpdateStatistics()
        {
            UpdateTaskDeadlineStates();
            TotalAddedCount = Tasks.Count;
            int completed = Tasks.Count(t => t.IsCompleted);
            CompletionRate = TotalAddedCount == 0 ? 0 : (double)completed / TotalAddedCount * 100;
            UpdateStreakDays();

            // 重新填充 LiveCharts [COMMON] 内部绑定数据
            Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = new double[] { TotalAddedCount, completed },
                    Name = "任务总数 vs 已完成"
                }
            };
        }

        private void UpdateStreakDays()
        {
            int streak = 0;
            DateTime cursor = DateTime.Today;

            while (HistoryCheckInDates.Any(date => date.Date == cursor))
            {
                streak++;
                cursor = cursor.AddDays(-1);
            }

            StreakDays = streak;
        }
    }
}
