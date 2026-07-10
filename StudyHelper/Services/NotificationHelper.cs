using Microsoft.Toolkit.Uwp.Notifications;

public static class NotificationHelper
{
    public static void ShowTaskNotification(string taskId, string taskTitle, string priority)
    {
        new ToastContentBuilder()
            // 1. 添加通知参数（当点击通知主体时，可以用这些参数识别是哪个任务）
            .AddArgument("action", "clickTask")
            .AddArgument("taskId", taskId)

            // 2. 标题和内容
            .AddText("🔔 学习提醒")
            .AddText($"任务: {taskTitle}")
            .AddText($"优先级: {priority}")

            // 3. 添加交互按钮（这些按钮会在通知上直接显示）
            .AddButton(new ToastButton()
                .SetContent("知道了")
                .AddArgument("action", "ack")
                .AddArgument("taskId", taskId)
                .SetBackgroundActivation()) // 点击后只在后台触发代码，不强行把程序弹到前台

            .AddButton(new ToastButton()
                .SetContent("过会儿提醒")
                .AddArgument("action", "ignore")
                .AddArgument("taskId", taskId)
                .SetBackgroundActivation())

            // 4. 发送通知
            .Show();
    }
}