using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Widget;

namespace HabitTracker;

// Runs fire-and-forget async work from UI event handlers. An exception thrown
// after the first await in a raw `async void` handler propagates to the
// synchronization context and crashes the process; routing handlers through
// here catches it instead, logging and showing a generic error toast.
public static class UiSafe
{
    private const string LogTag = "HabitTracker";

    public static async void Run(Context? context, Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(LogTag, ex.ToString());
            try
            {
                if (context != null)
                    Toast.MakeText(context, ResourceConstant.String.generic_error, ToastLength.Short)?.Show();
            }
            catch
            {
                // The toast is best-effort; never let error reporting throw
            }
        }
    }
}
