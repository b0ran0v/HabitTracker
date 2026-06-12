using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using HabitTracker.Data;
using System.Globalization;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace HabitTracker
{
    public class StatisticsFragment(Database database) : Fragment
    {
        // Required by the FragmentManager when restoring state (e.g. after Activity.Recreate)
        public StatisticsFragment() : this(MainActivity.SharedDatabase!) { }

        private readonly Database _database = database;
        private TextView? _totalValue;
        private TextView? _trackedValue;
        private TextView? _completedValue;
        private TextView? _bestHabitText;
        private TextView? _worstHabitText;
        private LinearLayout? _weekGrid;
        private View? _scroll;
        private View? _emptyState;
        private StreakAdapter? _streakAdapter;
        private RateAdapter? _rateAdapter;

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            var view = inflater.Inflate(ResourceConstant.Layout.fragment_statistics, container, false);

            _totalValue = view?.FindViewById<TextView>(ResourceConstant.Id.stat_total_value);
            _trackedValue = view?.FindViewById<TextView>(ResourceConstant.Id.stat_tracked_value);
            _completedValue = view?.FindViewById<TextView>(ResourceConstant.Id.stat_completed_value);
            _bestHabitText = view?.FindViewById<TextView>(ResourceConstant.Id.best_habit_text);
            _worstHabitText = view?.FindViewById<TextView>(ResourceConstant.Id.worst_habit_text);
            _weekGrid = view?.FindViewById<LinearLayout>(ResourceConstant.Id.week_grid);
            _scroll = view?.FindViewById<View>(ResourceConstant.Id.stats_scroll);
            _emptyState = view?.FindViewById<View>(ResourceConstant.Id.stats_empty);

            var streakList = view?.FindViewById<RecyclerView>(ResourceConstant.Id.streak_list);
            if (streakList != null)
            {
                streakList.SetLayoutManager(new LinearLayoutManager(Activity));
                _streakAdapter = new StreakAdapter();
                streakList.SetAdapter(_streakAdapter);
            }

            var rateList = view?.FindViewById<RecyclerView>(ResourceConstant.Id.rate_list);
            if (rateList != null)
            {
                rateList.SetLayoutManager(new LinearLayoutManager(Activity));
                _rateAdapter = new RateAdapter();
                rateList.SetAdapter(_rateAdapter);
            }

            LoadData();
            return view;
        }

        // Tabs are hidden/shown rather than recreated, so refresh whenever this tab is revealed
        public override void OnHiddenChanged(bool hidden)
        {
            base.OnHiddenChanged(hidden);
            if (!hidden) LoadData();
        }

        private void LoadData() => UiSafe.Run(Context, LoadDataAsync);

        private async Task LoadDataAsync()
        {
            if (Activity == null) return;
            var habits = await _database.GetHabitsAsync();
            var completions = await _database.GetHabitCompletionsAsync();
            var calculator = new StatisticsCalculator(habits, completions);

            Activity?.RunOnUiThread(() =>
            {
                if (Activity == null) return;
                Bind(calculator);
            });
        }

        private void Bind(StatisticsCalculator calculator)
        {
            var hasData = calculator.TotalHabits > 0;
            _scroll?.Visibility = hasData ? ViewStates.Visible : ViewStates.Gone;
            _emptyState?.Visibility = hasData ? ViewStates.Gone : ViewStates.Visible;
            if (!hasData) return;

            _totalValue?.Text = calculator.TotalHabits.ToString();
            _trackedValue?.Text = calculator.TrackedToday.ToString();
            _completedValue?.Text = calculator.CompletedToday.ToString();

            _streakAdapter?.Update(calculator.HabitStreaks);
            _rateAdapter?.Update(calculator.HabitRates);
            BuildWeekGrid(calculator.DayColumns);

            var best = calculator.BestHabit;
            var worst = calculator.WorstHabit;
            _bestHabitText?.Text = best != null
                ? $"{GetString(ResourceConstant.String.most_consistent)}: {best.Habit.Name} · {(int)(best.Rate * 100)}%"
                : string.Empty;
            _bestHabitText?.Visibility = best != null ? ViewStates.Visible : ViewStates.Gone;
            _worstHabitText?.Text = worst != null
                ? $"{GetString(ResourceConstant.String.needs_attention)}: {worst.Habit.Name} · {(int)(worst.Rate * 100)}%"
                : string.Empty;
            _worstHabitText?.Visibility = worst != null ? ViewStates.Visible : ViewStates.Gone;
        }

        private CultureInfo GetCurrentCulture()
        {
            var prefs = Activity?.GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
            var lang = prefs?.GetString("app_language", null);
            try { return lang != null ? new CultureInfo(lang) : CultureInfo.CurrentCulture; }
            catch { return CultureInfo.CurrentCulture; }
        }

        private void BuildWeekGrid(List<DayColumn> columns)
        {
            if (_weekGrid == null || Context == null) return;
            _weekGrid.RemoveAllViews();

            var culture = GetCurrentCulture();
            var metrics = Context.Resources!.DisplayMetrics;
            int Dp(float v) => (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, v, metrics);
            Android.Graphics.Color Res(int id) =>
                new(AndroidX.Core.Content.ContextCompat.GetColor(Context, id));

            foreach (var column in columns)
            {
                var columnLayout = new LinearLayout(Context)
                {
                    Orientation = Orientation.Vertical,
                    LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
                };
                columnLayout.SetGravity(GravityFlags.CenterHorizontal | GravityFlags.Bottom);

                var colors = column.CompletedColors.Take(4).ToList();
                if (colors.Count == 0)
                {
                    AddDot(columnLayout, Res(ResourceConstant.Color.dividerColor), Dp);
                }
                else
                {
                    foreach (var hex in colors)
                    {
                        Android.Graphics.Color dotColor;
                        try { dotColor = Android.Graphics.Color.ParseColor(hex); }
                        catch { dotColor = Res(ResourceConstant.Color.colorPrimary); }
                        AddDot(columnLayout, dotColor, Dp);
                    }
                }

                var isToday = column.Date == DateTime.Today;

                // Center the text within each TextView so the name and number line
                // up regardless of the view's measured width
                var dayName = new TextView(Context)
                {
                    Text = culture.DateTimeFormat.AbbreviatedDayNames[(int)column.Date.DayOfWeek]
                        .TrimEnd('.').ToUpper(culture),
                    TextSize = 10,
                    Gravity = GravityFlags.Center
                };
                dayName.SetTextColor(Res(ResourceConstant.Color.textColorSecondary));
                var nameParams = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(6) };
                dayName.LayoutParameters = nameParams;
                columnLayout.AddView(dayName);

                var dayNumber = new TextView(Context)
                {
                    Text = column.Date.Day.ToString(),
                    TextSize = 12,
                    Gravity = GravityFlags.Center
                };
                dayNumber.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                dayNumber.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
                dayNumber.SetTextColor(isToday
                    ? Res(ResourceConstant.Color.colorPrimary)
                    : Res(ResourceConstant.Color.textColorPrimary));
                columnLayout.AddView(dayNumber);

                _weekGrid.AddView(columnLayout);
            }
        }

        private void AddDot(LinearLayout column, Android.Graphics.Color color, Func<float, int> dp)
        {
            var dot = new View(Context)
            {
                LayoutParameters = new LinearLayout.LayoutParams(dp(10), dp(10)) { TopMargin = dp(3) }
            };
            var circle = new GradientDrawable();
            circle.SetShape(ShapeType.Oval);
            circle.SetColor(color);
            dot.Background = circle;
            column.AddView(dot);
        }
    }

    public class StreakAdapter : RecyclerView.Adapter
    {
        private List<HabitStreak> _items = [];

        public void Update(List<HabitStreak> items)
        {
            _items = items;
            NotifyDataSetChanged();
        }

        public override int ItemCount => _items.Count;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var item = _items[position];
            var streakHolder = (StreakViewHolder)holder;
            streakHolder.HabitName.Text = item.Habit.Name;
            // GetQuantityString picks the locale's plural category; the {0} pattern
            // is filled via string.Format, matching the convention used app-wide
            var pattern = holder.ItemView.Context!.Resources!
                .GetQuantityString(ResourceConstant.Plurals.streak_days, item.CurrentStreak);
            streakHolder.Badge.Text = string.Format(pattern!, item.CurrentStreak);
            try
            {
                var color = Android.Graphics.Color.ParseColor(item.Habit.ColorHex);
                (streakHolder.ColorIndicator.Background as GradientDrawable)?.SetColor(color);
            }
            catch { }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)
                ?.Inflate(ResourceConstant.Layout.item_stat_streak, parent, false);
            return new StreakViewHolder(view!);
        }

        private class StreakViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
        {
            public TextView HabitName { get; } =
                itemView.FindViewById<TextView>(ResourceConstant.Id.streak_habit_name)!;
            public TextView Badge { get; } =
                itemView.FindViewById<TextView>(ResourceConstant.Id.streak_badge)!;
            public View ColorIndicator { get; } =
                itemView.FindViewById<View>(ResourceConstant.Id.streak_color_indicator)!;
        }
    }

    public class RateAdapter : RecyclerView.Adapter
    {
        private List<HabitRate> _items = [];

        public void Update(List<HabitRate> items)
        {
            _items = items;
            NotifyDataSetChanged();
        }

        public override int ItemCount => _items.Count;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var item = _items[position];
            var rateHolder = (RateViewHolder)holder;
            var percent = (int)(item.Rate * 100);
            rateHolder.HabitName.Text = item.Habit.Name;
            rateHolder.Percent.Text = $"{percent}%";
            rateHolder.Progress.Progress = percent;
            try
            {
                var color = Android.Graphics.Color.ParseColor(item.Habit.ColorHex);
                rateHolder.Progress.ProgressTintList = Android.Content.Res.ColorStateList.ValueOf(color);
            }
            catch { }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)
                ?.Inflate(ResourceConstant.Layout.item_stat_rate, parent, false);
            return new RateViewHolder(view!);
        }

        private class RateViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
        {
            public TextView HabitName { get; } =
                itemView.FindViewById<TextView>(ResourceConstant.Id.rate_habit_name)!;
            public TextView Percent { get; } =
                itemView.FindViewById<TextView>(ResourceConstant.Id.rate_percent)!;
            public ProgressBar Progress { get; } =
                itemView.FindViewById<ProgressBar>(ResourceConstant.Id.rate_progress)!;
        }
    }
}
