using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.OS;
using Android.Views;
using AndroidX.AppCompat.App;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using Google.Android.Material.Button;
using AlertDialog = Android.App.AlertDialog;
using Fragment = AndroidX.Fragment.App.Fragment;
using System.Text.Json;
using HabitTracker.Data;

namespace HabitTracker
{
    public class SettingsFragment : Fragment
    {
        private const string PrefsName = "HabitTrackerPrefs";
        private const string ThemeKey = "app_theme";
        private const string ThemeDark = "dark";
        private const string ThemeLight = "light";
        private const int ImportRequestCode = 1001;
        private const string ReminderEnabledKey = "reminder_enabled";
        private const string ReminderHourKey = "reminder_hour";
        private const string ReminderMinuteKey = "reminder_minute";
        private const int NotificationPermissionRequestCode = 1002;

        private MaterialButton? _toggleThemeButton;
        private MaterialButton? _reminderButton;
        private Database? _database;

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            var view = inflater.Inflate(ResourceConstant.Layout.fragment_settings, container, false);

            _database = MainActivity.SharedDatabase;

            var toolbar = view?.FindViewById<Toolbar>(ResourceConstant.Id.settings_toolbar);
            if (toolbar != null)
            {
                toolbar.InflateMenu(ResourceConstant.Menu.settings_toolbar_menu);
                UpdateThemeIcon(toolbar);
                toolbar.MenuItemClick += (_, e) =>
                {
                    if (e.Item?.ItemId == ResourceConstant.Id.action_toggle_theme)
                        ToggleTheme(toolbar);
                };
            }

            _toggleThemeButton = view?.FindViewById<MaterialButton>(ResourceConstant.Id.toggle_theme_button);
            if (_toggleThemeButton != null)
            {
                UpdateThemeButtonText();
                _toggleThemeButton.Click += (_, _) => ToggleTheme(toolbar);
            }

            var changeLanguageButton = view?.FindViewById<MaterialButton>(ResourceConstant.Id.change_language_button);
            if (changeLanguageButton != null)
                changeLanguageButton.Click += (_, _) => ShowLanguageDialog();

            _reminderButton = view?.FindViewById<MaterialButton>(ResourceConstant.Id.daily_reminder_button);
            if (_reminderButton != null)
            {
                UpdateReminderButtonText();
                _reminderButton.Click += (_, _) => OnReminderButtonClick();
            }

            var exportButton = view?.FindViewById<MaterialButton>(ResourceConstant.Id.export_data_button);
            if (exportButton != null)
                exportButton.Click += async (_, _) => await ExportDataAsync();

            var importButton = view?.FindViewById<MaterialButton>(ResourceConstant.Id.import_data_button);
            if (importButton != null)
                importButton.Click += (_, _) => LaunchImportPicker();

            return view;
        }

        private bool IsDarkModeActive()
        {
            if (Activity == null) return false;
            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            var savedTheme = prefs?.GetString(ThemeKey, null);
            if (savedTheme != null) return savedTheme == ThemeDark;
            var nightModeFlags = Resources?.Configuration?.UiMode & Android.Content.Res.UiMode.NightMask;
            return nightModeFlags == Android.Content.Res.UiMode.NightYes;
        }

        private void UpdateThemeButtonText()
        {
            if (_toggleThemeButton == null) return;
            _toggleThemeButton.Text = IsDarkModeActive()
                ? GetString(ResourceConstant.String.switch_to_light_mode)
                : GetString(ResourceConstant.String.switch_to_dark_mode);
        }

        private void UpdateThemeIcon(Toolbar? toolbar)
        {
            var menuItem = toolbar?.Menu?.FindItem(ResourceConstant.Id.action_toggle_theme);
            if (menuItem == null) return;
            menuItem.SetIcon(IsDarkModeActive() ? ResourceConstant.Drawable.ic_sun : ResourceConstant.Drawable.ic_moon);
        }

        private void ToggleTheme(Toolbar? toolbar)
        {
            if (Activity == null) return;
            var isDark = IsDarkModeActive();
            var newTheme = isDark ? ThemeLight : ThemeDark;
            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            prefs?.Edit()?.PutString(ThemeKey, newTheme)?.Commit();
            AppCompatDelegate.DefaultNightMode = newTheme == ThemeDark
                ? AppCompatDelegate.ModeNightYes : AppCompatDelegate.ModeNightNo;
            Activity.Recreate();
        }

        private void ShowLanguageDialog()
        {
            if (Activity == null) return;
            var languages = new[] { "Қазақша", "English", "Русский" };
            var languageCodes = new[] { "kk", "en", "ru" };
            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(GetString(ResourceConstant.String.change_language));
            builder.SetItems(languages, (_, e) => SetLocale(languageCodes[e.Which]));
            builder.Show();
        }

        private void SetLocale(string langCode)
        {
            if (Activity == null) return;
            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            prefs?.Edit()?.PutString("app_language", langCode)?.Commit();
            Activity.Recreate();
        }

        private void UpdateReminderButtonText()
        {
            if (_reminderButton == null || Activity == null) return;
            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            if (prefs?.GetBoolean(ReminderEnabledKey, false) == true)
            {
                var time = $"{prefs.GetInt(ReminderHourKey, 20):D2}:{prefs.GetInt(ReminderMinuteKey, 0):D2}";
                _reminderButton.Text = string.Format(GetString(ResourceConstant.String.reminder_at), time);
            }
            else
            {
                _reminderButton.Text = GetString(ResourceConstant.String.reminder_off);
            }
        }

        private void OnReminderButtonClick()
        {
            if (Activity == null) return;
            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            if (prefs?.GetBoolean(ReminderEnabledKey, false) != true)
            {
                ShowReminderTimePicker();
                return;
            }
            var options = new[]
            {
                GetString(ResourceConstant.String.reminder_change_time),
                GetString(ResourceConstant.String.reminder_disable)
            };
            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(GetString(ResourceConstant.String.daily_reminder));
            builder.SetItems(options, (_, e) =>
            {
                if (e.Which == 0) ShowReminderTimePicker();
                else DisableReminder();
            });
            builder.Show();
        }

        private void ShowReminderTimePicker()
        {
            if (Activity == null) return;
            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            var hour = prefs?.GetInt(ReminderHourKey, 20) ?? 20;
            var minute = prefs?.GetInt(ReminderMinuteKey, 0) ?? 0;
            new TimePickerDialog(Activity, (_, e) => EnableReminder(e.HourOfDay, e.Minute),
                hour, minute, Android.Text.Format.DateFormat.Is24HourFormat(Activity)).Show();
        }

        private void EnableReminder(int hour, int minute)
        {
            if (Activity == null) return;
            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            prefs?.Edit()?
                .PutBoolean(ReminderEnabledKey, true)?
                .PutInt(ReminderHourKey, hour)?
                .PutInt(ReminderMinuteKey, minute)?
                .Apply();
            ReminderReceiver.Schedule(Activity, hour, minute);
            UpdateReminderButtonText();

#pragma warning disable CA1416, CS0618
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
                Activity.CheckSelfPermission(Android.Manifest.Permission.PostNotifications)
                    != Android.Content.PM.Permission.Granted)
            {
                RequestPermissions([Android.Manifest.Permission.PostNotifications], NotificationPermissionRequestCode);
            }
#pragma warning restore CA1416, CS0618
        }

        private void DisableReminder()
        {
            if (Activity == null) return;
            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            prefs?.Edit()?.PutBoolean(ReminderEnabledKey, false)?.Apply();
            ReminderReceiver.Cancel(Activity);
            UpdateReminderButtonText();
        }

        private async Task ExportDataAsync()
        {
            if (Activity == null || _database == null) return;
            try
            {
                var habits = await _database.GetAllHabitsAsync();
                var completions = await _database.GetHabitCompletionsAsync();
                var export = new ExportData { Habits = habits, Completions = completions };
                var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });

                var downloadsDir = Android.OS.Environment.GetExternalStoragePublicDirectory(
                    Android.OS.Environment.DirectoryDownloads)!.AbsolutePath;
                var filePath = Path.Combine(downloadsDir, "HabitTracker.json");
                await File.WriteAllTextAsync(filePath, json);

                Activity.RunOnUiThread(() =>
                    Toast.MakeText(Activity, GetString(ResourceConstant.String.export_success), ToastLength.Long)?.Show());
            }
            catch
            {
                Activity.RunOnUiThread(() =>
                    Toast.MakeText(Activity, GetString(ResourceConstant.String.export_error), ToastLength.Short)?.Show());
            }
        }

        private void LaunchImportPicker()
        {
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/json");
#pragma warning disable CS0618
            StartActivityForResult(intent, ImportRequestCode);
#pragma warning restore CS0618
        }

#pragma warning disable CS0618, CS0672
        public override async void OnActivityResult(int requestCode, int resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (requestCode != ImportRequestCode || resultCode != (int)Android.App.Result.Ok || data?.Data == null) return;

            var confirmBuilder = new AlertDialog.Builder(Activity);
            confirmBuilder.SetTitle(GetString(ResourceConstant.String.import_confirm_title));
            confirmBuilder.SetMessage(GetString(ResourceConstant.String.import_confirm_message));
            confirmBuilder.SetPositiveButton(GetString(ResourceConstant.String.ok), async (_, _) =>
            {
                await ImportDataAsync(data.Data);
            });
            confirmBuilder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) => { });
            confirmBuilder.Show();
        }
#pragma warning restore CS0618, CS0672

        private async Task ImportDataAsync(Android.Net.Uri uri)
        {
            if (Activity == null || _database == null) return;
            try
            {
                var stream = Activity.ContentResolver?.OpenInputStream(uri);
                if (stream == null) throw new InvalidOperationException("Could not open file stream.");
                string json;
                using (stream)
                using (var reader = new System.IO.StreamReader(stream))
                    json = await reader.ReadToEndAsync();

                var import = JsonSerializer.Deserialize<ExportData>(json);
                if (import?.Habits == null) throw new InvalidDataException();

                await _database.ClearTablesAsync();
                foreach (var habit in import.Habits)
                    await _database.SaveHabitAsync(habit);
                foreach (var completion in import.Completions ?? [])
                    await _database.SaveHabitCompletionAsync(completion);

                if (Context != null) HabitWidgetProvider.RequestUpdate(Context);
                Activity.RunOnUiThread(() =>
                    Toast.MakeText(Activity, GetString(ResourceConstant.String.import_success), ToastLength.Short)?.Show());
            }
            catch
            {
                Activity?.RunOnUiThread(() =>
                    Toast.MakeText(Activity, GetString(ResourceConstant.String.import_error), ToastLength.Short)?.Show());
            }
        }
    }

    public class ExportData
    {
        public List<Habit> Habits { get; set; } = [];
        public List<HabitCompletion> Completions { get; set; } = [];
    }
}
