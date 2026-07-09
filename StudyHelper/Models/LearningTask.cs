using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace StudyHelper.Models
{
    public partial class LearningTask : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty] private string _title = string.Empty;

        [ObservableProperty] private DateTime _targetTime = DateTime.Today;

        [ObservableProperty] private string _priority = "中";

        [ObservableProperty] private bool _needsReview = false;

        [ObservableProperty] private bool _isCompleted = false;

        [ObservableProperty] private DateTime? _completedDate;

        // 一对多关系：一个大任务包含多个子任务 [COMMON]
        public ObservableCollection<SubTask> SubTasks { get; set; } = new();

        // ============ DDL 与剩余天数相关计算属性 ============

        /// <summary>
        /// 格式化的截止日期显示，例如 "06/15"
        /// </summary>
        public string DueDateDisplay => TargetTime.ToString("MM/dd");

        /// <summary>
        /// 剩余天数（正数 = 剩余，0 = 今天，负数 = 已过期）
        /// </summary>
        public int RemainingDays => (TargetTime.Date - DateTime.Today).Days;

        /// <summary>
        /// 剩余天数的友好显示文本
        /// </summary>
        public string RemainingDaysDisplay
        {
            get
            {
                if (RemainingDays < 0) return "已过期";
                if (RemainingDays == 0) return "今天截止";
                if (RemainingDays == 1) return "剩余1天";
                return $"剩余{RemainingDays}天";
            }
        }

        /// <summary>
        /// 优先级与 DDL 合并显示，例如 "优先级: 高 | 截止: 06/15"
        /// </summary>
        public string PriorityDueDateDisplay => $"优先级: {Priority} | 截止: {DueDateDisplay}";

        // 当 TargetTime 变化时，通知所有相关计算属性更新
        partial void OnTargetTimeChanged(DateTime value)
        {
            OnPropertyChanged(nameof(DueDateDisplay));
            OnPropertyChanged(nameof(RemainingDays));
            OnPropertyChanged(nameof(RemainingDaysDisplay));
            OnPropertyChanged(nameof(PriorityDueDateDisplay));
        }

        // 当 Priority 变化时，通知合并显示属性更新
        partial void OnPriorityChanged(string value)
        {
            OnPropertyChanged(nameof(PriorityDueDateDisplay));
        }

        /// <summary>
        /// 公开方法：用于每日零点刷新所有天数相关的属性
        /// </summary>
        public void RefreshDayProperties()
        {
            OnPropertyChanged(nameof(DueDateDisplay));
            OnPropertyChanged(nameof(RemainingDays));
            OnPropertyChanged(nameof(RemainingDaysDisplay));
            OnPropertyChanged(nameof(PriorityDueDateDisplay));
        }
    }
}