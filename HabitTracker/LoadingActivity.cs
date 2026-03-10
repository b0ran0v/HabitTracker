using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.AppCompat.App;
using Java.Util;

namespace HabitTracker
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, NoHistory = true)]
    public class LoadingActivity : AppCompatActivity
    {
        protected override void AttachBaseContext(Context @base)
        {
            var prefs = @base.GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
            var lang = prefs?.GetString("app_language", null);
            if (lang == null)
            {
                base.AttachBaseContext(@base);
                return;
            }
            var locale = new Locale(lang);
            Locale.Default = locale;
            var config = new Android.Content.Res.Configuration(@base.Resources!.Configuration);
            config.SetLocale(locale);
            base.AttachBaseContext(@base.CreateConfigurationContext(config));
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(ResourceConstant.Layout.activity_loading);

            // Simulate loading or perform initialization
            new Handler(Looper.MainLooper!).PostDelayed(() =>
            {
                StartActivity(new Intent(this, typeof(MainActivity)));
                Finish();
            }, 2000); // 2-second delay
        }
    }
}