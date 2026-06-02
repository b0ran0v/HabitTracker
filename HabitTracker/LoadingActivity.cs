using Android.Content;
using AndroidX.AppCompat.App;

namespace HabitTracker
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, NoHistory = true)]
    public class LoadingActivity : AppCompatActivity
    {
        private void ApplyPersistedNightMode()
        {
            var prefs = GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
            var savedTheme = prefs?.GetString("app_theme", null);
            var nightMode = savedTheme switch
            {
                "dark" => AppCompatDelegate.ModeNightYes,
                "light" => AppCompatDelegate.ModeNightNo,
                _ => AppCompatDelegate.ModeNightFollowSystem
            };
            AppCompatDelegate.DefaultNightMode = nightMode;
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ApplyPersistedNightMode();
            base.OnCreate(savedInstanceState);
            StartActivity(new Intent(this, typeof(MainActivity)));
            Finish();
        }
    }
}