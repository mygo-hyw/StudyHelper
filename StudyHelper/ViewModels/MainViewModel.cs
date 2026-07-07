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
        private DispatcherTimer _reminderTimer; // 提醒定时器 [COMMON]

        [ObservableProperty] private string _newTaskTitle = string.Empty;
        [ObservableProperty] private string _selectedPriority = "中";
        [ObservableProperty] private bool _needsReview = false;
        [ObservableProperty] private DateTime _selectedDate = DateTime.Today;

        public ObservableCollection<string> Priorities { get; } = new() { "高", "中", "低" };
        public ObservableCollection<LearningTask> Tasks { get; } = new();

        // 绑定到前端 LiveCharts [COMMON] 图表的数据源
        [ObservableProperty] private ISeries[] _series;

        // 分析与打卡统计数据 [COMMON]
        [ObservableProperty] private int _totalAddedCount = 0;
        [ObservableProperty] private double _completionRate = 0;

        public MainViewModel()
        {
            LoadRecommendedTasks();
            InitializeTimer();
            UpdateStatistics();
        }

        private void LoadRecommendedTasks()
        {
            Tasks.Add(new LearningTask { Title = "【系统推荐】复习专业核心算法与错题", Priority = "高", NeedsReview = true });
            Tasks.Add(new LearningTask { Title = "【系统推荐】英语学术论文阅读 2 篇", Priority = "中", NeedsReview = false });
        }

        // 初始化定时器（满足任务提醒功能） [COMMON]
        private void InitializeTimer()
        {
            _reminderTimer = new DispatcherTimer();
            _reminderTimer.Interval = TimeSpan.FromSeconds(30); // 实际项目可以设为 1 分钟，此处设为 30 秒以便快速测试 [COMMON]
            _reminderTimer.Tick += CheckTasksForReminders;
            _reminderTimer.Start();
        }

        // 根据到期时间与优先级触发提醒（弹窗通知） [COMMON]
        private void CheckTasksForReminders(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            foreach (var task in Tasks.Where(t => !t.IsCompleted))
            {
                if (task.TargetTime.Date == now.Date)
                {
                    string alertMsg = $"【学习任务提醒】\n任务：{task.Title}\n优先级：{task.Priority}";
                    if (task.Priority == "高")
                    {
                        MessageBox.Show(alertMsg, "高优先级紧急提醒", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else if (task.Priority == "中" && now.Minute % 30 == 0) // 中优先级每隔 30 分钟弹出一次 [COMMON]
                    {
                        MessageBox.Show(alertMsg, "普通学习提醒", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        // 按钮绑定命令：添加自定义任务 [COMMON]
        [RelayCommand]
        private void AddTask()
        {
            if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;

            var task = new LearningTask
            {
                Title = NewTaskTitle,
                Priority = SelectedPriority,
                NeedsReview = NeedsReview,
                TargetTime = SelectedDate
            };
            Tasks.Add(task);
            NewTaskTitle = string.Empty; // 添加成功后清空文本框 [COMMON]
            UpdateStatistics();
        }

        // 按钮绑定命令：点击打卡 [COMMON]
        [RelayCommand]
        private void CompleteTask(LearningTask task)
        {
            if (task == null) return;
            task.IsCompleted = true;
            task.CompletedDate = DateTime.Today;

            // 重新刷新局部 UI 列表 [COMMON]
            int index = Tasks.IndexOf(task);
            Tasks[index] = task;

            UpdateStatistics();
        }

        // 更新统计率并动态刷新柱状图数据 [COMMON]
        private void UpdateStatistics()
        {
            TotalAddedCount = Tasks.Count;
            int completed = Tasks.Count(t => t.IsCompleted);
            CompletionRate = TotalAddedCount == 0 ? 0 : (double)completed / TotalAddedCount * 100;

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
    }
}
