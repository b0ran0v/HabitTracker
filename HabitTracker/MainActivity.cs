using AndroidX.AppCompat.App;
using Google.Android.Material.BottomNavigation;

namespace HabitTracker;

[Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
[Obsolete("Obsolete")]
public class MainActivity : AppCompatActivity, BottomNavigationView.IOnNavigationItemSelectedListener
{
    private BottomNavigationView? _navigation;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        _navigation = FindViewById<BottomNavigationView>(Resource.Id.bottom_navigation);
        if (_navigation != null)
        {
            _navigation.SetOnNavigationItemSelectedListener(this);
            
            if (savedInstanceState == null)
            {
                LoadFragment(new TrackerFragment());
                _navigation.SelectedItemId = Resource.Id.navigation_tracker;
            }
        }
    }

    public bool OnNavigationItemSelected(Android.Views.IMenuItem item)
    {
        if (item.ItemId == (_navigation?.SelectedItemId ?? -1))
        {
            return true;
        }

        AndroidX.Fragment.App.Fragment? fragment = item.ItemId switch
        {
            Resource.Id.navigation_habits => new HabitsFragment(),
            Resource.Id.navigation_tracker => new TrackerFragment(),
            _ => null
        };

        return LoadFragment(fragment);
    }

    private bool LoadFragment(AndroidX.Fragment.App.Fragment? fragment)
    {
        if (fragment == null) return false;
        var transaction = SupportFragmentManager.BeginTransaction();
        transaction.Replace(Resource.Id.fragment_container, fragment);
        transaction.Commit();
        return true;
    }
}