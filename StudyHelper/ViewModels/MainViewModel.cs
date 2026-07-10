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
using LiveChartsCore.Defaults;

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

        // 打卡日历选择
        [ObservableProperty] private DateTime? _selectedCheckInDate = DateTime.Today;

        // 分析日历选择
        [ObservableProperty] private DateTime? _selectedAnalysisDate = DateTime.Today;

        // 主窗口尺寸
        [ObservableProperty] private double _mainWindowWidth = 380;
        [ObservableProperty] private double _mainWindowHeight = double.NaN;

        // 复习计划
        [ObservableProperty] private string _customReviewDays = "1";
        [ObservableProperty] private string _customReviewDuration = "15";
        [ObservableProperty] private bool _isDailyTask = false;

        // 绑定前台添加子任务的输入框 [COMMON]
        [ObservableProperty] private string _newSubTaskInput = string.Empty;
        [ObservableProperty] private bool _newSubTaskIsDaily = true;
        public ObservableCollection<SubTask> TempSubTaskTitles { get; } = new();

        public ObservableCollection<string> Priorities { get; } = new() { "高", "中", "低" };
        public ObservableCollection<string> Themes { get; } = new() { "经典浅色", "赛博", "森林绿", "极夜黑" };
        public ObservableCollection<string> Hours { get; } = new(Enumerable.Range(0, 24).Select(x => x.ToString("00")));
        public ObservableCollection<string> Minutes { get; } = new(Enumerable.Range(0, 60).Select(x => x.ToString("00")));
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

        // 分析面板属性
        [ObservableProperty] private double _analysisCompletionRate = 0;
        [ObservableProperty] private int _analysisTaskCount = 0;
        [ObservableProperty] private int _analysisCompletedCount = 0;
        [ObservableProperty] private int _analysisIncompleteCount = 0;
        public Axis[] AnalysisXAxes { get; } = new Axis[]
        {
            new Axis { Labels = new[] { "总数", "已完成" } }
        };

        // 计算属性
        public string SelectedTargetTime => BuildSelectedTargetTime().ToString("yyyy-MM-dd HH:mm");
        public IEnumerable<LearningTask> ActiveTasks => MainWindowTasks.Where(t => t.TargetTime >= DateTime.Today);
        public IEnumerable<LearningTask> MainWindowTasks => Tasks.Where(t => !t.ShouldAutoDelete).OrderBy(t => t.TargetTime);
        public IEnumerable<LearningTask> ReviewPendingTasks => Tasks.Where(t => !t.ReviewPlanSet && !t.ReviewDeclined && !t.IsCompleted);
        public IEnumerable<LearningTask> SelectedDateTasks => Tasks.Where(t => t.TargetTime.Date == (SelectedCheckInDate?.Date ?? DateTime.Today));

        partial void OnSelectedDateChanged(DateTime value) => OnPropertyChanged(nameof(SelectedTargetTime));
        partial void OnSelectedHourChanged(string value) => OnPropertyChanged(nameof(SelectedTargetTime));
        partial void OnSelectedMinuteChanged(string value) => OnPropertyChanged(nameof(SelectedTargetTime));

        public MainViewModel()
        {
            LoadUiSettings();
            InitializeDatabaseAndLoadTasks();
            RemindAllTasks();
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

        partial void OnMainWindowWidthChanged(double value)
        {
            if (!_isLoadingSettings) SaveUiSettings();
        }

        partial void OnSelectedCheckInDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(SelectedDateTasks));
        }

        partial void OnSelectedAnalysisDateChanged(DateTime? value)
        {
            UpdateDateAnalysis(value);
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

                // 补丁：新增字段（旧库升级）
                try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Tasks\" ADD COLUMN \"ReviewIntervalDays\" INTEGER NOT NULL DEFAULT 1"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Tasks\" ADD COLUMN \"ReviewPlanSet\" INTEGER NOT NULL DEFAULT 0"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Tasks\" ADD COLUMN \"ReviewDurationDays\" INTEGER NOT NULL DEFAULT 0"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Tasks\" ADD COLUMN \"ReviewStartDate\" TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Tasks\" ADD COLUMN \"ReviewDeclined\" INTEGER NOT NULL DEFAULT 0"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE \"SubTasks\" ADD COLUMN \"IsDailyCheckIn\" INTEGER NOT NULL DEFAULT 1"); } catch { }

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
            TempSubTaskTitles.Add(new SubTask { Title = NewSubTaskInput, IsDailyCheckIn = NewSubTaskIsDaily });
            NewSubTaskInput = string.Empty;
            NewSubTaskIsDaily = true;
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
                SubTasks = new ObservableCollection<SubTask>()
            };

            // 导入临时装载的子任务 [COMMON]
            foreach (var sub in TempSubTaskTitles)
            {
                task.SubTasks.Add(new SubTask { Title = sub.Title, LearningTaskId = task.Id, IsDailyCheckIn = sub.IsDailyCheckIn });
            }

            using (var db = new StudyHelperDbContext())
            {
                db.Tasks.Add(task);
                db.SaveChanges();
            }

            Tasks.Add(task);
            OnPropertyChanged(nameof(MainWindowTasks));
            OnPropertyChanged(nameof(ActiveTasks));
            OnPropertyChanged(nameof(ReviewPendingTasks));
            NewTaskTitle = string.Empty;
            TempSubTaskTitles.Clear();
            UpdateStatistics();
        }

        // 子任务打卡联动指令（核心业务：所有子任务完成则大任务自动打卡） [COMMON]
        [RelayCommand]
        private void CompleteSubTask(SubTask subTask)
        {
            if (subTask == null) return;
            if (subTask.Id == Guid.Empty) return;
            if (!subTask.IsDailyCheckIn) return;

            subTask.IsCompleted = !subTask.IsCompleted;
            subTask.CompletedDate = subTask.IsCompleted ? DateTime.Now : null;

            try
            {
                using (var db = new StudyHelperDbContext())
                {
                    var dbSubTask = db.SubTasks.Find(subTask.Id);
                    if (dbSubTask != null)
                    {
                        dbSubTask.IsCompleted = subTask.IsCompleted;
                        dbSubTask.CompletedDate = subTask.CompletedDate;
                        try { db.SaveChanges(); } catch (DbUpdateConcurrencyException) { }
                    }

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
                        try { db.SaveChanges(); } catch (DbUpdateConcurrencyException) { }
                    }
                }
            }
            catch { }
            OnPropertyChanged(nameof(MainWindowTasks));
            OnPropertyChanged(nameof(ActiveTasks));
            OnPropertyChanged(nameof(ReviewPendingTasks));
            UpdateStatistics();
        }

        // 删除大任务 [COMMON]
        [RelayCommand]
        private void DeleteTask(LearningTask task)
        {
            if (task == null) return;
            try
            {
                using (var db = new StudyHelperDbContext())
                {
                    var dbTask = db.Tasks.Find(task.Id);
                    if (dbTask != null)
                    {
                        db.Tasks.Remove(dbTask);
                        db.SaveChanges();
                    }
                }
            }
            catch { }
            Tasks.Remove(task);
            OnPropertyChanged(nameof(MainWindowTasks));
            OnPropertyChanged(nameof(ActiveTasks));
            OnPropertyChanged(nameof(ReviewPendingTasks));
            UpdateStatistics();
        }

        [RelayCommand]
        private void ApplyReviewPlan(LearningTask task)
        {
            if (task == null) return;
            task.ReviewPlanSet = true;
            task.ReviewStartDate = DateTime.Today;
            if (int.TryParse(CustomReviewDays, out var days) && days > 0)
                task.ReviewIntervalDays = days;
            else
                task.ReviewIntervalDays = 1;
            if (int.TryParse(CustomReviewDuration, out var dur) && dur > 0)
                task.ReviewDurationDays = dur;
            else
                task.ReviewDurationDays = 0;
            OnPropertyChanged(nameof(ReviewPendingTasks));
        }

        [RelayCommand]
        private void DeclineReview(LearningTask task)
        {
            if (task == null) return;
            task.ReviewDeclined = true;
            OnPropertyChanged(nameof(ReviewPendingTasks));
        }

        private void LoadUiSettings()
        {
            var snapshot = _settingsStorage.Load();
            _isLoadingSettings = true;
            SelectedTheme = NormalizeThemeName(snapshot.SelectedTheme);
            WindowOpacity = ClampOpacity(snapshot.WindowOpacity);
            IsDesktopEmbedded = snapshot.IsDesktopEmbedded;
            MainWindowWidth = snapshot.MainWindowWidth;
            _isLoadingSettings = false;
            ApplyTheme(SelectedTheme);
        }

        private void SaveUiSettings()
        {
            _settingsStorage.Save(new AppSettingsSnapshot
            {
                SelectedTheme = SelectedTheme,
                WindowOpacity = WindowOpacity,
                IsDesktopEmbedded = IsDesktopEmbedded,
                MainWindowWidth = MainWindowWidth
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
            var now = DateTime.Now;

            // 每日零点刷新剩余天数 + 清空隔夜忽略列表 + 重新提醒全部今日任务
            if (DateTime.Today > _lastRefreshDate)
            {
                _lastRefreshDate = DateTime.Today;
                _ignoredTasks.Clear();
                foreach (var task in Tasks)
                {
                    task.RefreshDayProperties();
                }
                RemindAllTasks();
            }

            // 每 tick 检查并清理过期任务
            CleanupExpiredTasks();

            foreach (var task in Tasks.Where(t => !t.IsCompleted).ToList())
            {
                if (IsTaskIgnored(task.Id))
                {
                    continue;
                }

                bool dateMatch = task.TargetTime.Date == now.Date;
                bool shouldRemind = false;

                if (task.Priority == "高" && dateMatch && now.Minute == 0 && now.Second < 15)
                {
                    shouldRemind = true;
                }
                else if (task.Priority == "中" && dateMatch && now.Hour % 3 == 0 && now.Minute == 0 && now.Second < 15)
                {
                    shouldRemind = true;
                }

                if (shouldRemind)
                {
                    ShowToastNotification(task);
                }
            }
        }

        private void ShowToastNotification(LearningTask task)
        {
            //  新代码：调用系统原生通知
            NotificationHelper.ShowTaskNotification(task.Id.ToString(), task.Title, task.Priority);
        }

        /// <summary>
        /// 自动清理过期任务：无复习计划的直接删除；有复习计划的待复习天数用完后删除
        /// </summary>
        private void CleanupExpiredTasks()
        {
            var toDelete = Tasks.Where(t => t.ShouldAutoDelete).ToList();
            if (toDelete.Count == 0) return;

            using (var db = new StudyHelperDbContext())
            {
                foreach (var task in toDelete)
                {
                    var dbTask = db.Tasks.Find(task.Id);
                    if (dbTask != null)
                    {
                        db.Tasks.Remove(dbTask);
                    }
                }
                db.SaveChanges();
                db.SaveChanges();
            }

            foreach (var task in toDelete)
            {
                Tasks.Remove(task);
            }
            OnPropertyChanged(nameof(MainWindowTasks));
            OnPropertyChanged(nameof(ActiveTasks));
            OnPropertyChanged(nameof(ReviewPendingTasks));
            UpdateStatistics();
        }

        /// <summary>
        /// 将任务列表中所有未完成且目标时间为今天的任务全部提醒一遍（启动/每日0点）
        /// </summary>
        private void RemindAllTasks()
        {
            var today = DateTime.Today;
            foreach (var task in Tasks.Where(t => !t.IsCompleted && t.TargetTime.Date == today))
            {
                ShowToastNotification(task);
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

        private void UpdateDateAnalysis(DateTime? date)
        {
            if (date == null) return;
            var d = date.Value;
            var dayTasks = Tasks.Where(t => t.TargetTime.Date == d.Date).ToList();
            AnalysisTaskCount = dayTasks.Count;
            AnalysisCompletedCount = dayTasks.Count(t => t.IsCompleted);
            AnalysisIncompleteCount = AnalysisTaskCount - AnalysisCompletedCount;
            AnalysisCompletionRate = AnalysisTaskCount == 0 ? 0 : (double)AnalysisCompletedCount / AnalysisTaskCount * 100;
        }

        public int GetCompletionPercent(DateTime date)
        {
            var dayTasks = Tasks.Where(t => t.TargetTime.Date == date.Date).ToList();
            if (dayTasks.Count == 0) return 0;
            int completed = dayTasks.Count(t => t.IsCompleted);
            if (completed == 0) return 0;
            return completed >= dayTasks.Count ? 100 : 50;
        }

        public string GetDateDetail(DateTime date)
        {
            var dayTasks = Tasks.Where(t => t.TargetTime.Date == date.Date).ToList();
            if (dayTasks.Count == 0) return "该日无任务";

            var lines = new System.Collections.Generic.List<string>();
            foreach (var task in dayTasks)
            {
                int done = task.SubTasks.Count(s => s.IsCompleted);
                int total = task.SubTasks.Count;
                string status = task.IsCompleted ? "✅" : (done > 0 ? "🔄" : "⬜");
                lines.Add($"{status} {task.Title}");
                foreach (var sub in task.SubTasks)
                {
                    string mark = sub.IsCompleted ? "  ✅" : "  ⬜";
                    lines.Add($"{mark} {sub.Title}");
                }
            }
            return string.Join("\n", lines);
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
        /// 过会儿提醒：忽略 10 分钟
        /// </summary>
        public void IgnoreTaskReminder(Guid taskId)
        {
            _ignoredTasks[taskId] = DateTime.Now.AddMinutes(10);
        }

        /// <summary>
        /// 知道了：忽略到次日 0 点
        /// </summary>
        public void DismissForDay(Guid taskId)
        {
            _ignoredTasks[taskId] = DateTime.Today.AddDays(1);
        }

        /// <summary>
        /// 通过 ID 查找任务（供外部激活回调使用）
        /// </summary>
        public LearningTask? GetTaskById(Guid taskId)
        {
            return Tasks.FirstOrDefault(t => t.Id == taskId);
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
