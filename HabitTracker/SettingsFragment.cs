using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Views;
using AndroidX.AppCompat.App;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using Google.Android.Material.Button;
using AlertDialog = Android.App.AlertDialog;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace HabitTracker
{
    public class SettingsFragment : Fragment
    {
        private const string PrefsName = "HabitTrackerPrefs";
        private const string ThemeKey = "app_theme";
        private const string ThemeDark = "dark";
        private const string ThemeLight = "light";

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            var view = inflater.Inflate(ResourceConstant.Layout.fragment_settings, container, false);

            var toolbar = view?.FindViewById<Toolbar>(ResourceConstant.Id.settings_toolbar);
            if (toolbar != null)
            {
                toolbar.InflateMenu(ResourceConstant.Menu.settings_toolbar_menu);
                UpdateThemeIcon(toolbar);
                toolbar.MenuItemClick += (_, e) =>
                {
                    if (e.Item?.ItemId == ResourceConstant.Id.action_toggle_theme)
                    {
                        ToggleTheme(toolbar);
                    }
                };
            }

            var changeLanguageButton = view?.FindViewById<MaterialButton>(ResourceConstant.Id.change_language_button);
            if (changeLanguageButton != null)
            {
                changeLanguageButton.Click += (_, _) => ShowLanguageDialog();
            }

            return view;
        }

        private bool IsDarkModeActive()
        {
            if (Activity == null) return false;
            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            var savedTheme = prefs?.GetString(ThemeKey, null);
            if (savedTheme != null)
            {
                return savedTheme == ThemeDark;
            }
            // Fall back to current system night mode if no preference is saved
            var nightModeFlags = Resources?.Configuration?.UiMode & Android.Content.Res.UiMode.NightMask;
            return nightModeFlags == Android.Content.Res.UiMode.NightYes;
        }

        private void UpdateThemeIcon(Toolbar toolbar)
        {
            var menuItem = toolbar.Menu?.FindItem(ResourceConstant.Id.action_toggle_theme);
            if (menuItem == null) return;

            // Dark mode is active: show sun icon (tap to switch to light)
            // Light mode is active: show moon icon (tap to switch to dark)
            if (IsDarkModeActive())
            {
                menuItem.SetIcon(ResourceConstant.Drawable.ic_sun);
            }
            else
            {
                menuItem.SetIcon(ResourceConstant.Drawable.ic_moon);
            }
        }

        private void ToggleTheme(Toolbar toolbar)
        {
            if (Activity == null) return;

            var isDark = IsDarkModeActive();
            var newTheme = isDark ? ThemeLight : ThemeDark;

            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            prefs?.Edit()?.PutString(ThemeKey, newTheme)?.Commit();

            var nightMode = newTheme == ThemeDark
                ? AppCompatDelegate.ModeNightYes
                : AppCompatDelegate.ModeNightNo;
            AppCompatDelegate.DefaultNightMode = nightMode;

            Activity.Recreate();
        }

        private void ShowLanguageDialog()
        {
            if (Activity == null) return;

            var languages = new[] { "Қазақша", "English", "Русский" };
            var languageCodes = new[] { "kk", "en", "ru" };

            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(GetString(ResourceConstant.String.change_language));
            builder.SetItems(languages, (_, e) =>
            {
                var selectedCode = languageCodes[e.Which];
                SetLocale(selectedCode);
            });
            builder.Show();
        }

        private void SetLocale(string langCode)
        {
            if (Activity == null) return;

            var prefs = Activity.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            prefs?.Edit()?.PutString("app_language", langCode)?.Commit();

            Activity.Recreate();
        }
    }
}
