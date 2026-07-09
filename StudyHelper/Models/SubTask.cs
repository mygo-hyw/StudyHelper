using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StudyHelper.Models
{
    public partial class SubTask : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty]
        private string _title = string.Empty; // 子任务名称

        [ObservableProperty]
        private bool _isCompleted = false; // 是否完成打卡

        [ObservableProperty]
        private DateTime? _completedDate; // 实际打卡时间

        public Guid LearningTaskId { get; set; } // 外键，指向大任务
    }
}
