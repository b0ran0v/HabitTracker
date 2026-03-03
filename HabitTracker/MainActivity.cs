using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Views;
using AndroidX.AppCompat.App;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace HabitTracker;

[Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = false)]
public class MainActivity : AppCompatActivity, NavigationBarView.IOnItemSelectedListener
{
    private BottomNavigationView? _navigation;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(ResourceConstant.Layout.activity_main);

        _navigation = FindViewById<BottomNavigationView>(ResourceConstant.Id.bottom_navigation);
        if (_navigation == null) return;
        _navigation.SetOnItemSelectedListener(this);

        if (savedInstanceState != null) return;
        LoadFragment(new TrackerFragment());
        _navigation.SelectedItemId = ResourceConstant.Id.navigation_tracker;
    }

    public bool OnNavigationItemSelected(IMenuItem item)
    {
        Fragment? fragment = item.ItemId switch
        {
            ResourceConstant.Id.navigation_habits => new HabitsFragment(),
            ResourceConstant.Id.navigation_tracker => new TrackerFragment(),
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