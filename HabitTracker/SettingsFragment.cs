using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Views;
using Google.Android.Material.Button;
using AlertDialog = Android.App.AlertDialog;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace HabitTracker
{
    public class SettingsFragment : Fragment
    {
        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            var view = inflater.Inflate(ResourceConstant.Layout.fragment_settings, container, false);

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

            var prefs = Activity.GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
            prefs?.Edit()?.PutString("app_language", langCode)?.Commit();

            Activity.Recreate();
        }
    }
}
