using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace StudyHelper.Models
{
    public partial class LearningSubTask : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private bool _isCompleted = false;
    }
}
