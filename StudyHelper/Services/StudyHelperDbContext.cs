using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using StudyHelper.Models;

namespace StudyHelper.Services
{
    public class StudyHelperDbContext : DbContext
    {
        public DbSet<LearningTask> Tasks { get; set; } = null!;
        public DbSet<SubTask> SubTasks { get; set; } = null!; // 新增子任务表 [COMMON]

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFolder = Path.Combine(appDataPath, "StudyHelper");
            if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);
            string dbPath = Path.Combine(dbFolder, "studyhelper.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}