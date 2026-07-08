using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace StudyHelper.Models
{
    public partial class LearningTask : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty] private string _title = string.Empty; // 学习任务主题
        [ObservableProperty] private DateTime _targetTime = DateTime.Today; // 设定的完成时间
        [ObservableProperty] private string _priority = "中"; // 任务优先级（高、中、低）
        [ObservableProperty] private bool _needsReview = false; // 是否需要复习提醒
        [ObservableProperty] private bool _isCompleted = false; // 是否已打卡完成
        [ObservableProperty] private DateTime? _completedDate; // 实际打卡记录的日期
        [ObservableProperty] private bool _isDueSoon = false; // 是否 1 小时内到期
        [ObservableProperty] private bool _isOverdue = false; // 是否已逾期

        public ObservableCollection<LearningSubTask> SubTasks { get; set; } = new();
    }
}
