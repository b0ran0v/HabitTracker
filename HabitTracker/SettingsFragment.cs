using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Views;
using Google.Android.Material.Button;
using Java.Util;
using AlertDialog = Android.App.AlertDialog;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace HabitTracker
{
    public class SettingsFragment : Fragment
    {
        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            var view = inflater?.Inflate(ResourceConstant.Layout.fragment_settings, container, false);

            var changeLanguageButton = view?.FindViewById<MaterialButton>(ResourceConstant.Id.change_language_button);
            if (changeLanguageButton != null)
            {
                changeLanguageButton.Click += (_, _) => ShowLanguageDialog();
            }

            return view;
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

            // Save preference
            var prefs = Activity.GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
            var editor = prefs?.Edit();
            editor?.PutString("app_language", langCode);
            editor?.Apply();

            // Apply to current context
            var locale = new Locale(langCode);
            Locale.Default = locale;
            var config = new Android.Content.Res.Configuration();
            config.SetLocale(locale);
            Activity.Resources?.UpdateConfiguration(config, Activity.Resources.DisplayMetrics);

            // Restart activity
            var intent = new Intent(Activity, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
            StartActivity(intent);
        }
    }
}
