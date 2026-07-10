using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using StudyHelper.Models;
using StudyHelper.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace StudyHelper.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AppSettingsStorage _settingsStorage = new();
        private DispatcherTimer _reminderTimer;
        private bool _isLoadingSettings;
        private Dictionary<Guid, DateTime> _ignoredTasks = new(); // 记录被忽略的任务及其过期时间
        private int _activeNotificationCount = 0; // 当前活跃的通知窗口数量
        private DateTime _lastRefreshDate = DateTime.Today; // 用于每日零点刷新剩余天数

        // 任务表单临时属性 [COMMON]
        [ObservableProperty] private string _newTaskTitle = string.Empty;
        [ObservableProperty] private string _selectedPriority = "中";
        [ObservableProperty] private bool _needsReview = false;
        [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
        [ObservableProperty] private string _selectedHour = DateTime.Now.Hour.ToString("00");
        [ObservableProperty] private string _selectedMinute = DateTime.Now.Minute.ToString("00");

        // 绑定前台添加子任务的输入框 [COMMON]
        [ObservableProperty] private string _newSubTaskInput = string.Empty;
        public ObservableCollection<string> TempSubTaskTitles { get; } = new();

        public ObservableCollection<string> Priorities { get; } = new() { "高", "中", "低" };
        public ObservableCollection<string> Themes { get; } = new() { "经典浅色", "赛博", "森林绿", "极夜黑" };
        public ObservableCollection<LearningTask> Tasks { get; } = new();
        public ObservableCollection<SubTask> DraftSubTasks { get; } = new();
        public ObservableCollection<DateTime> HistoryCheckInDates { get; } = new();

        // 动态界面换肤绑定属性（解决界面风格更改需求） [COMMON]
        [ObservableProperty] private string _widgetBackground = "#E6FFFFFF"; // 默认白
        [ObservableProperty] private string _widgetTextColor = "#FF333333";
        [ObservableProperty] private string _widgetBorderBrush = "#20000000";

        // 风格设置 [COMMON]
        [ObservableProperty] private string _selectedTheme = "经典浅色";
        [ObservableProperty] private double _windowOpacity = 0.90;
        [ObservableProperty] private bool _isDesktopEmbedded = true;

        // 可视化分析属性 [COMMON]
        [ObservableProperty] private ISeries[] _series;
        [ObservableProperty] private int _totalAddedCount = 0;
        [ObservableProperty] private double _completionRate = 0;
        [ObservableProperty] private int _streakDays = 0;

        public MainViewModel()
        {
            LoadUiSettings();
            InitializeDatabaseAndLoadTasks();
            InitializeTimer();
            UpdateStatistics();
        }

        partial void OnSelectedThemeChanged(string value)
        {
            ApplyTheme(value);
            if (!_isLoadingSettings)
            {
                SaveUiSettings();
            }
        }

        partial void OnWindowOpacityChanged(double value)
        {
            if (!_isLoadingSettings)
            {
                SaveUiSettings();
            }
        }

        partial void OnIsDesktopEmbeddedChanged(bool value)
        {
            if (!_isLoadingSettings)
            {
                SaveUiSettings();
            }
        }

        private void InitializeDatabaseAndLoadTasks()
        {
            using (var db = new StudyHelperDbContext())
            {
                db.Database.EnsureCreated(); // 自动生成本地数据库 [COMMON]

                // 补丁：针对旧版数据库缺失 SubTasks 表的情况进行手动修复 [COMMON]
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ""SubTasks"" (
                        ""Id"" TEXT NOT NULL CONSTRAINT ""PK_SubTasks"" PRIMARY KEY,
                        ""Title"" TEXT NOT NULL,
                        ""IsCompleted"" INTEGER NOT NULL,
                        ""CompletedDate"" TEXT,
                        ""LearningTaskId"" TEXT NOT NULL,
                        CONSTRAINT ""FK_SubTasks_Tasks_LearningTaskId"" FOREIGN KEY (""LearningTaskId"") REFERENCES ""Tasks"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_SubTasks_LearningTaskId"" ON ""SubTasks"" (""LearningTaskId"");
                ");

                // 如果为空，载入含有子任务的推荐模板 [COMMON]
                if (!db.Tasks.Any())
                {
                    var recTask = new LearningTask { Title = "【系统推荐】复习专业核心算法", Priority = "高", NeedsReview = true };
                    recTask.SubTasks.Add(new SubTask { Title = "完成错题集前 5 题" });
                    recTask.SubTasks.Add(new SubTask { Title = "背诵快速排序算法伪代码" });
                    db.Tasks.Add(recTask);
                    db.SaveChanges();
                }

                // 显式级联加载子任务 [COMMON]
                var localTasks = db.Tasks.Include(t => t.SubTasks).ToList();
                foreach (var task in localTasks)
                {
                    Tasks.Add(task);
                }
            }
        }

        // 添加子任务到当前大任务临时列表 [COMMON]
        [RelayCommand]
        private void AddTempSubTask()
        {
            if (string.IsNullOrWhiteSpace(NewSubTaskInput)) return;
            TempSubTaskTitles.Add(NewSubTaskInput);
            NewSubTaskInput = string.Empty;
        }

        // 创建大任务并保存进 SQLite [COMMON]
        [RelayCommand]
        private void AddTask()
        {
            if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;

            var task = new LearningTask
            {
                Title = NewTaskTitle.Trim(),
                Priority = SelectedPriority,
                NeedsReview = NeedsReview,
                TargetTime = BuildSelectedTargetTime(),
                SubTasks = new ObservableCollection<SubTask>(
                    DraftSubTasks.Select(subTask => new SubTask
                    {
                        Title = subTask.Title,
                        IsCompleted = subTask.IsCompleted
                    }))
            };

            // 导入临时装载的子任务 [COMMON]
            foreach (var subTitle in TempSubTaskTitles)
            {
                task.SubTasks.Add(new SubTask { Title = subTitle });
            }

            using (var db = new StudyHelperDbContext())
            {
                db.Tasks.Add(task);
                db.SaveChanges();
            }

            Tasks.Add(task);
            NewTaskTitle = string.Empty;
            TempSubTaskTitles.Clear();
            UpdateStatistics();
        }

        // 子任务打卡联动指令（核心业务：所有子任务完成则大任务自动打卡） [COMMON]
        [RelayCommand]
        private void CompleteSubTask(SubTask subTask)
        {
            if (subTask == null) return;

            // 1. 切换状态 [COMMON]
            subTask.IsCompleted = !subTask.IsCompleted;
            subTask.CompletedDate = subTask.IsCompleted ? DateTime.Now : null;

            using (var db = new StudyHelperDbContext())
            {
                var dbSubTask = db.SubTasks.Find(subTask.Id);
                if (dbSubTask != null)
                {
                    dbSubTask.IsCompleted = subTask.IsCompleted;
                    dbSubTask.CompletedDate = subTask.CompletedDate;
                    db.SaveChanges();
                }

                // 2. 联动更新大任务 [COMMON]
                var parentTask = db.Tasks.Include(t => t.SubTasks).FirstOrDefault(t => t.Id == subTask.LearningTaskId);
                if (parentTask != null)
                {
                    bool allDone = parentTask.SubTasks.Count > 0 && parentTask.SubTasks.All(s => s.IsCompleted);

                    var inMemParent = Tasks.FirstOrDefault(t => t.Id == subTask.LearningTaskId);
                    if (inMemParent != null)
                    {
                        inMemParent.IsCompleted = allDone;
                        inMemParent.CompletedDate = allDone ? DateTime.Today : null;
                    }

                    parentTask.IsCompleted = allDone;
                    parentTask.CompletedDate = allDone ? DateTime.Today : null;
                    db.SaveChanges();
                }
            }
            UpdateStatistics();
        }

        // 删除大任务 [COMMON]
        [RelayCommand]
        private void DeleteTask(LearningTask task)
        {
            if (task == null) return;
            using (var db = new StudyHelperDbContext())
            {
                db.Tasks.Remove(task);
                db.SaveChanges();
            }
            Tasks.Remove(task);
            UpdateStatistics();
        }

        private void LoadUiSettings()
        {
            var snapshot = _settingsStorage.Load();
            _isLoadingSettings = true;
            SelectedTheme = NormalizeThemeName(snapshot.SelectedTheme);
            WindowOpacity = ClampOpacity(snapshot.WindowOpacity);
            IsDesktopEmbedded = snapshot.IsDesktopEmbedded;
            _isLoadingSettings = false;
            ApplyTheme(SelectedTheme);
        }

        private void SaveUiSettings()
        {
            _settingsStorage.Save(new AppSettingsSnapshot
            {
                SelectedTheme = SelectedTheme,
                WindowOpacity = WindowOpacity,
                IsDesktopEmbedded = IsDesktopEmbedded
            });
        }

        private static double ClampOpacity(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0.90;
            }

            return Math.Clamp(value, 0.55, 1.0);
        }

        private static string NormalizeThemeName(string? themeName)
        {
            return themeName switch
            {
                "Cyberpunk" or "赛博风" => "赛博风",
                "Forest" or "森林绿" => "森林绿",
                "Midnight" or "极夜黑" => "极夜黑",
                _ => "经典浅色"
            };
        }

        private void ApplyTheme(string themeName)
        {
            switch (NormalizeThemeName(themeName))
            {
                case "赛博风":
                    WidgetBackground = "#E60F1026";
                    WidgetTextColor = "#FF00FFFF";
                    WidgetBorderBrush = "#FFFF00FF";
                    break;
                case "森林绿":
                    WidgetBackground = "#CCD6E4C3";
                    WidgetTextColor = "#FF2C402E";
                    WidgetBorderBrush = "#FF8DA27A";
                    break;
                case "极夜黑":
                    WidgetBackground = "#CC121214";
                    WidgetTextColor = "#FFFFFFFF";
                    WidgetBorderBrush = "#FF2D2D30";
                    break;
                default:
                    WidgetBackground = "#E6FFFFFF";
                    WidgetTextColor = "#FF333333";
                    WidgetBorderBrush = "#20000000";
                    break;
            }
        }

        // 改变桌面卡片配色风格 [COMMON]
        [RelayCommand]
        private void ChangeStyle(string styleName)
        {
            SelectedTheme = NormalizeThemeName(styleName);
        }

        private void InitializeTimer()
        {
            _reminderTimer = new DispatcherTimer();
            _reminderTimer.Interval = TimeSpan.FromSeconds(30);
            _reminderTimer.Tick += CheckTasksForReminders;
            _reminderTimer.Start();
        }

        private void CheckTasksForReminders(object? sender, EventArgs e)
        {
            // 每日零点刷新所有任务的剩余天数（每隔 30s 检查一次日期变更）
            if (DateTime.Today > _lastRefreshDate)
            {
                _lastRefreshDate = DateTime.Today;
                foreach (var task in Tasks)
                {
                    task.RefreshDayProperties();
                }
            }

            var now = DateTime.Now;
            foreach (var task in Tasks.Where(t => !t.IsCompleted))
            {
                // 检查任务是否在忽略列表中
                if (IsTaskIgnored(task.Id))
                {
                    continue;
                }

                if (task.TargetTime.Date == now.Date)
                {
                    bool shouldRemind = false;
                    if (task.Priority == "高")
                    {
                        shouldRemind = true;
                    }
                    else if (task.Priority == "中" && now.Minute % 30 == 0)
                    {
                        shouldRemind = true;
                    }

                    if (shouldRemind)
                    {
                        ShowToastNotification(task);
                    }
                }
            }
        }

        private void ShowToastNotification(LearningTask task)
        {
            try
            {
                var notificationWindow = new ToastNotificationWindow(task, this);
                notificationWindow.Show();
            }
            catch
            {
                // 如果弹窗显示失败，回退到消息框
                string alertMsg = $"【学习到期提醒】\n任务：{task.Title}\n优先级：{task.Priority}";
                System.Windows.MessageBox.Show(alertMsg, "学习提醒", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        private void UpdateStatistics()
        {
            var checkable = Tasks.Where(t => t.SubTasks.Count > 0).ToList();
            TotalAddedCount = checkable.Count;
            int completed = checkable.Count(t => t.IsCompleted);
            CompletionRate = TotalAddedCount == 0 ? 0 : (double)completed / TotalAddedCount * 100;
            UpdateStreakDays();

            Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = new double[] { TotalAddedCount, completed },
                    Name = "任务总数 vs 已完成"
                }
            };
        }

        private DateTime BuildSelectedTargetTime()
        {
            var date = SelectedDate.Date;
            var hour = int.TryParse(SelectedHour, out var parsedHour) ? parsedHour : 0;
            var minute = int.TryParse(SelectedMinute, out var parsedMinute) ? parsedMinute : 0;
            hour = Math.Clamp(hour, 0, 23);
            minute = Math.Clamp(minute, 0, 59);
            return date.AddHours(hour).AddMinutes(minute);
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

        /// <summary>
        /// 检查任务是否在忽略列表中（1小时内）
        /// </summary>
        public bool IsTaskIgnored(Guid taskId)
        {
            if (_ignoredTasks.TryGetValue(taskId, out var expireTime))
            {
                if (DateTime.Now < expireTime)
                {
                    return true;
                }
                else
                {
                    // 过期的忽略记录，删除它
                    _ignoredTasks.Remove(taskId);
                }
            }
            return false;
        }

        /// <summary>
        /// 忽略任务提醒 1 小时
        /// </summary>
        public void IgnoreTaskReminder(Guid taskId)
        {
            _ignoredTasks[taskId] = DateTime.Now.AddHours(1);
        }

        /// <summary>
        /// 增加活跃通知计数（用于位置计算）
        /// </summary>
        public void IncrementNotificationCount()
        {
            _activeNotificationCount++;
        }

        /// <summary>
        /// 减少活跃通知计数
        /// </summary>
        public void DecrementNotificationCount()
        {
            if (_activeNotificationCount > 0)
            {
                _activeNotificationCount--;
            }
        }

        /// <summary>
        /// 获取当前活跃通知计数
        /// </summary>
        public int GetActiveNotificationCount()
        {
            return _activeNotificationCount;
        }
    }
}
