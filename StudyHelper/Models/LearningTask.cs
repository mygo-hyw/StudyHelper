using System;

namespace StudyHelper.Models
{
    public class LearningTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty; // 学习任务主题
        public DateTime TargetTime { get; set; } = DateTime.Today; // 设定的完成时间
        public string Priority { get; set; } = "中"; // 任务优先级（高、中、低）
        public bool NeedsReview { get; set; } = false; // 是否需要复习提醒
        public bool IsCompleted { get; set; } = false; // 是否已打卡完成
        public DateTime? CompletedDate { get; set; } // 实际打卡记录的日期
    }
}