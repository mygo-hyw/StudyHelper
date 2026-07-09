using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

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
    }
}