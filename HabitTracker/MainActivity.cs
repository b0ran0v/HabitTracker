using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Views;
using AndroidX.AppCompat.App;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using HabitTracker.Data;
using Java.Util;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace HabitTracker;

[Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = false)]
public class MainActivity : AppCompatActivity, NavigationBarView.IOnItemSelectedListener
{
    private BottomNavigationView? _navigation;
    private Database? _database;

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

    protected override void AttachBaseContext(Context? @base)
    {
        if (@base == null)
        {
            base.AttachBaseContext(@base);
            return;
        }
        var prefs = @base.GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
        var lang = prefs?.GetString("app_language", null);
        if (lang == null)
        {
            base.AttachBaseContext(@base);
            return;
        }
        var locale = Locale.ForLanguageTag(lang)!;
        Locale.Default = locale;
        var config = new Android.Content.Res.Configuration(@base.Resources!.Configuration);
        config.SetLocale(locale);
        base.AttachBaseContext(@base.CreateConfigurationContext(config));
    }

    public override void ApplyOverrideConfiguration(Android.Content.Res.Configuration? overrideConfiguration)
    {
        if (overrideConfiguration != null)
        {
            var prefs = GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
            var lang = prefs?.GetString("app_language", null);
            if (lang != null)
                overrideConfiguration.SetLocale(Locale.ForLanguageTag(lang));
        }
        base.ApplyOverrideConfiguration(overrideConfiguration);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        ApplyPersistedNightMode();
        base.OnCreate(savedInstanceState);
        SetContentView(ResourceConstant.Layout.activity_main);

        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "habits.db");
        _database = new Database(dbPath);

        _navigation = FindViewById<BottomNavigationView>(ResourceConstant.Id.bottom_navigation);
        if (_navigation == null) return;
        _navigation.SetOnItemSelectedListener(this);

        if (savedInstanceState != null) return;
        LoadFragment(new TrackerFragment(_database));
        _navigation.SelectedItemId = ResourceConstant.Id.navigation_tracker;
    }

    public bool OnNavigationItemSelected(IMenuItem item)
    {
        Fragment? fragment = item.ItemId switch
        {
            ResourceConstant.Id.navigation_habits => new HabitsFragment(_database!),
            ResourceConstant.Id.navigation_tracker => new TrackerFragment(_database!),
            ResourceConstant.Id.navigation_settings => new SettingsFragment(),
            _ => null
        };

        return LoadFragment(fragment);
    }

    private bool LoadFragment(Fragment? fragment)
    {
        if (fragment == null) return false;

        var transaction = SupportFragmentManager.BeginTransaction();
        transaction.Replace(ResourceConstant.Id.fragment_container, fragment);
        transaction.Commit();
        return true;
    }
}