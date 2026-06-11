using _Microsoft.Android.Resource.Designer;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using HabitTracker.Data;

namespace HabitTracker;

[BroadcastReceiver(Label = "@string/widget_label", Exported = true)]
[IntentFilter([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/habit_widget_info")]
public class HabitWidgetProvider : AppWidgetProvider
{
    private const int MaxListedHabits = 5;
    private static Database? _widgetDatabase;

    // The widget or reminder receiver may run before MainActivity ever creates SharedDatabase
    internal static Database GetDatabase() =>
        MainActivity.SharedDatabase ??
        (_widgetDatabase ??= new Database(Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "habits.db")));

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null || appWidgetIds.Length == 0)
            return;
        var pendingResult = GoAsync();
        _ = UpdateWidgetsAsync(context, appWidgetManager, appWidgetIds, pendingResult);
    }

    private static async Task UpdateWidgetsAsync(Context context, AppWidgetManager appWidgetManager,
        int[] appWidgetIds, PendingResult? pendingResult)
    {
        try
        {
            var database = GetDatabase();
            var completions = await database.GetHabitCompletionsForDateAsync(DateTime.Today);
            var habits = await database.GetAllHabitsAsync();

            var localized = GetLocalizedContext(context);
            var views = BuildViews(localized, habits, completions);
            foreach (var id in appWidgetIds)
                appWidgetManager.UpdateAppWidget(id, views);
        }
        catch
        {
            // A failed refresh keeps the previous widget content
        }
        finally
        {
            pendingResult?.Finish();
        }
    }

    private static RemoteViews BuildViews(Context context, List<Habit> habits, List<HabitCompletion> completions)
    {
        var views = new RemoteViews(context.PackageName, ResourceConstant.Layout.widget_habits);

        var paired = completions
            .Select(c => new { Completion = c, Habit = habits.FirstOrDefault(h => h.Id == c.HabitId) })
            .Where(p => p.Habit != null)
            .OrderBy(p => p.Completion.CompletedDate.HasValue)
            .ToList();
        var total = paired.Count;
        var done = paired.Count(p => p.Completion.CompletedDate.HasValue);

        if (total == 0)
        {
            views.SetTextViewText(ResourceConstant.Id.widget_progress_text,
                context.GetString(ResourceConstant.String.widget_empty));
            views.SetViewVisibility(ResourceConstant.Id.widget_progress_bar, Android.Views.ViewStates.Gone);
            views.SetViewVisibility(ResourceConstant.Id.widget_habit_list, Android.Views.ViewStates.Gone);
        }
        else
        {
            views.SetTextViewText(ResourceConstant.Id.widget_progress_text,
                string.Format(context.GetString(ResourceConstant.String.widget_progress), done, total));
            views.SetViewVisibility(ResourceConstant.Id.widget_progress_bar, Android.Views.ViewStates.Visible);
            views.SetProgressBar(ResourceConstant.Id.widget_progress_bar, total, done, false);

            var lines = paired.Take(MaxListedHabits)
                .Select(p => (p.Completion.CompletedDate.HasValue ? "✓ " : "○ ") + p.Habit!.Name);
            var listText = string.Join("\n", lines);
            if (total > MaxListedHabits)
                listText += $"\n+{total - MaxListedHabits}";
            views.SetTextViewText(ResourceConstant.Id.widget_habit_list, listText);
            views.SetViewVisibility(ResourceConstant.Id.widget_habit_list, Android.Views.ViewStates.Visible);
        }

        var launchIntent = new Intent(context, typeof(MainActivity));
        launchIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(context, 0, launchIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        views.SetOnClickPendingIntent(ResourceConstant.Id.widget_root, pendingIntent);

        return views;
    }

    // Widget and notification strings should follow the in-app language preference, not the system locale
    internal static Context GetLocalizedContext(Context context)
    {
        var prefs = context.GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
        var lang = prefs?.GetString("app_language", null);
        if (lang == null) return context;
        var locale = Java.Util.Locale.ForLanguageTag(lang)!;
        var config = new Android.Content.Res.Configuration(context.Resources!.Configuration);
        config.SetLocale(locale);
        return context.CreateConfigurationContext(config) ?? context;
    }

    // Asks all placed widgets to refresh; no-op when none are on the home screen
    public static void RequestUpdate(Context context)
    {
        try
        {
            var manager = AppWidgetManager.GetInstance(context);
            var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(HabitWidgetProvider)));
            var ids = manager?.GetAppWidgetIds(component);
            if (ids == null || ids.Length == 0) return;
            var intent = new Intent(context, typeof(HabitWidgetProvider));
            intent.SetAction(AppWidgetManager.ActionAppwidgetUpdate);
            intent.PutExtra(AppWidgetManager.ExtraAppwidgetIds, ids);
            context.SendBroadcast(intent);
        }
        catch
        {
            // Widget refresh must never break the app flow
        }
    }
}
