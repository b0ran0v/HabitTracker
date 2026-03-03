using _Microsoft.Android.Resource.Designer;
using Android.Views;
using AndroidX.AppCompat.App;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;

namespace HabitTracker;

[Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
public class MainActivity : AppCompatActivity, NavigationBarView.IOnItemSelectedListener
{
    private BottomNavigationView? _navigation;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(ResourceConstant.Layout.activity_main);

        _navigation = FindViewById<BottomNavigationView>(ResourceConstant.Id.bottom_navigation);
        if (_navigation != null)
        {
            _navigation.SetOnItemSelectedListener(this);
            
            if (savedInstanceState == null)
            {
                LoadFragment(new TrackerFragment());
                _navigation.SelectedItemId = ResourceConstant.Id.navigation_tracker;
            }
        }
    }

    public bool OnNavigationItemSelected(IMenuItem item)
    {
        AndroidX.Fragment.App.Fragment? fragment = null;
        
        if (item.ItemId == ResourceConstant.Id.navigation_habits)
        {
            fragment = new HabitsFragment();
        }
        else if (item.ItemId == ResourceConstant.Id.navigation_tracker)
        {
            fragment = new TrackerFragment();
        }

        return LoadFragment(fragment);
    }

    private bool LoadFragment(AndroidX.Fragment.App.Fragment? fragment)
    {
        if (fragment == null) return false;

        var transaction = SupportFragmentManager.BeginTransaction();
        transaction.Replace(ResourceConstant.Id.fragment_container, fragment);
        transaction.Commit();
        return true;
    }
}