using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StudyHelper.Models
{
    public partial class SubTask : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isCompleted = false;

        [ObservableProperty]
        private bool _isDailyCheckIn = true;

        [ObservableProperty]
        private DateTime? _completedDate;

        public Guid LearningTaskId { get; set; }
    }
}
