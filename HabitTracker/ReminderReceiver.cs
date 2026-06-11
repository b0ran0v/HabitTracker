using _Microsoft.Android.Resource.Designer;
using Android.Content;
using AndroidX.Core.App;

namespace HabitTracker;

[BroadcastReceiver(Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    private const string ChannelId = "daily_reminder";
    private const int NotificationId = 1;
    private const string PrefsName = "HabitTrackerPrefs";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        // The alarm is one-shot (repeating alarms get an 18h delivery window), so arm tomorrow's now
        ScheduleFromPrefs(context);
        var pendingResult = GoAsync();
        _ = ShowReminderAsync(context, pendingResult);
    }

    private static async Task ShowReminderAsync(Context context, PendingResult? pendingResult)
    {
        try
        {
            var database = HabitWidgetProvider.GetDatabase();
            var completions = await database.GetHabitCompletionsForDateAsync(DateTime.Today);
            var pending = completions.Count(c => !c.CompletedDate.HasValue);
            // No nagging when everything tracked today is already done
            if (completions.Count > 0 && pending == 0) return;

            var localized = HabitWidgetProvider.GetLocalizedContext(context);
            var text = pending > 0
                ? string.Format(localized.GetString(ResourceConstant.String.reminder_text_pending), pending)
                : localized.GetString(ResourceConstant.String.reminder_text_generic);

            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            var channel = new NotificationChannel(ChannelId,
                localized.GetString(ResourceConstant.String.reminder_channel_name),
                NotificationImportance.Default);
            manager?.CreateNotificationChannel(channel);

            var launchIntent = new Intent(context, typeof(MainActivity));
            launchIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            var contentIntent = PendingIntent.GetActivity(context, 0, launchIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var notification = new NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(ResourceConstant.Drawable.ic_habit)
                .SetContentTitle(localized.GetString(ResourceConstant.String.app_name))
                .SetContentText(text)
                .SetContentIntent(contentIntent)
                .SetAutoCancel(true)
                .Build();
            NotificationManagerCompat.From(context).Notify(NotificationId, notification);
        }
        catch
        {
            // A denied POST_NOTIFICATIONS permission must not crash the receiver
        }
        finally
        {
            pendingResult?.Finish();
        }
    }

    public static void Schedule(Context context, int hour, int minute)
    {
        var manager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (manager == null) return;
        var now = DateTime.Now;
        var next = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
        if (next <= now) next = next.AddDays(1);
        var triggerAt = new DateTimeOffset(next).ToUnixTimeMilliseconds();
        // A 10-minute window keeps delivery near the chosen time without needing
        // the SCHEDULE_EXACT_ALARM permission (it is also the OS minimum window)
        manager.SetWindow(AlarmType.RtcWakeup, triggerAt, 10 * 60 * 1000, GetPendingIntent(context));
    }

    public static void Cancel(Context context)
    {
        var manager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        manager?.Cancel(GetPendingIntent(context));
    }

    // Alarms do not survive reboots or reinstalls, so this re-arms from saved prefs
    public static void ScheduleFromPrefs(Context context)
    {
        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        if (prefs?.GetBoolean("reminder_enabled", false) != true) return;
        Schedule(context, prefs.GetInt("reminder_hour", 20), prefs.GetInt("reminder_minute", 0));
    }

    private static PendingIntent GetPendingIntent(Context context) =>
        PendingIntent.GetBroadcast(context, 0, new Intent(context, typeof(ReminderReceiver)),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
}

[BroadcastReceiver(Exported = true)]
[IntentFilter([Intent.ActionBootCompleted])]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent?.Action != Intent.ActionBootCompleted) return;
        ReminderReceiver.ScheduleFromPrefs(context);
    }
}
