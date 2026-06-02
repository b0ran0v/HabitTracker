using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Util;
using Android.Views;
using Google.Android.Material.DatePicker;
using AndroidX.RecyclerView.Widget;
using HabitTracker.Data;
using Android.Graphics.Drawables;
using System.Globalization;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace HabitTracker
{
    public class TrackerFragment(Database database) : Fragment
    {
        private RecyclerView? _recyclerView;
        private View? _emptyState;
        private Button? _addButton;
        private Button? _copyWeekButton;
        private TextView? _dateText;
        private TextView? _dayOfWeekText;
        private TextView? _relativeDateText;
        private Button? _pickDateButton;
        private Button? _yesterdayButton;
        private Button? _todayButton;
        private Button? _tomorrowButton;
        private readonly List<Habit> _habits = [];
        private readonly List<HabitCompletion> _completions = [];
        private TrackerAdapter? _adapter;
        private readonly Database _database = database;
        private DateTime _selectedDate = DateTime.Today;

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            var view = inflater.Inflate(ResourceConstant.Layout.fragment_tracker, container, false);

            _recyclerView = view?.FindViewById<RecyclerView>(ResourceConstant.Id.tracker_list);
            _emptyState = view?.FindViewById<View>(ResourceConstant.Id.empty_state_tracker);
            _addButton = view?.FindViewById<Button>(ResourceConstant.Id.add_tracked_habit_button);
            _copyWeekButton = view?.FindViewById<Button>(ResourceConstant.Id.copy_week_button);
            _dateText = view?.FindViewById<TextView>(ResourceConstant.Id.selected_date_text);
            _dayOfWeekText = view?.FindViewById<TextView>(ResourceConstant.Id.selected_day_of_week);
            _relativeDateText = view?.FindViewById<TextView>(ResourceConstant.Id.relative_date_text);
            _pickDateButton = view?.FindViewById<Button>(ResourceConstant.Id.pick_date_button);
            _yesterdayButton = view?.FindViewById<Button>(ResourceConstant.Id.yesterday_button);
            _todayButton = view?.FindViewById<Button>(ResourceConstant.Id.today_button);
            _tomorrowButton = view?.FindViewById<Button>(ResourceConstant.Id.tomorrow_button);

            if (_recyclerView != null)
            {
                _recyclerView.SetLayoutManager(new LinearLayoutManager(Activity));
                _adapter = new TrackerAdapter(_habits, _completions,
                    async void (position) => { await OnItemClick(position); }, () => _selectedDate);
                _recyclerView.SetAdapter(_adapter);

                var callback = new TrackerSwipeCallback(async void (position) =>
                    {
                        if (_database == null || position >= _completions.Count) return;
                        var completion = _completions[position];
                        await _database.DeleteHabitCompletionAsync(completion);
                        Activity?.RunOnUiThread(() =>
                        {
                            if (Activity == null) return;
                            var idx = _completions.IndexOf(completion);
                            if (idx < 0) return;
                            _habits.RemoveAt(idx);
                            _completions.RemoveAt(idx);
                            _adapter?.NotifyItemRemoved(idx);
                        });
                    }, async void (position) => { await OnItemClick(position); },
                    position => position < _completions.Count && _completions[position].CompletedDate.HasValue,
                    Context);

                var itemTouchHelper = new ItemTouchHelper(callback);
                itemTouchHelper.AttachToRecyclerView(_recyclerView);
            }

            if (_addButton != null)
            {
                _addButton.Click += (_, _) => { ShowAddTrackedHabitDialog(); };
            }

            if (_copyWeekButton != null)
            {
                _copyWeekButton.Click += async (_, _) => { await CopyHabitsForWeek(); };
            }

            if (_pickDateButton != null)
            {
                _pickDateButton.Click += (_, _) =>
                {
                    var builder = MaterialDatePicker.Builder.DatePicker();
                    builder.SetTitleText(GetString(ResourceConstant.String.select_date));

                    // Convert DateTime to milliseconds for MaterialDatePicker
                    var selection =
                        (long)(_selectedDate.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                        .TotalMilliseconds;
                    builder.SetSelection(Java.Lang.Long.ValueOf(selection));

                    var picker = builder.Build();
                    picker.AddOnPositiveButtonClickListener(new DatePickerPositiveListener(selectionMs =>
                    {
                        var date = DateTimeOffset.FromUnixTimeMilliseconds(selectionMs).DateTime.ToLocalTime();
                        _selectedDate = date;
                        UpdateDateText();
                        LoadData();
                    }));
                    picker.Show(ChildFragmentManager, "DATE_PICKER");
                };
            }

            if (_yesterdayButton != null)
            {
                _yesterdayButton.Click += (_, _) =>
                {
                    _selectedDate = DateTime.Today.AddDays(-1);
                    UpdateDateText();
                    LoadData();
                };
            }

            if (_todayButton != null)
            {
                _todayButton.Click += (_, _) =>
                {
                    _selectedDate = DateTime.Today;
                    UpdateDateText();
                    LoadData();
                };
            }

            if (_tomorrowButton != null)
            {
                _tomorrowButton.Click += (_, _) =>
                {
                    _selectedDate = DateTime.Today.AddDays(1);
                    UpdateDateText();
                    LoadData();
                };
            }

            UpdateDateText();
            LoadData();

            return view;
        }

        private CultureInfo GetCurrentCulture()
        {
            var prefs = Activity?.GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
            var lang = prefs?.GetString("app_language", null);
            try { return lang != null ? new CultureInfo(lang) : CultureInfo.CurrentCulture; }
            catch { return CultureInfo.CurrentCulture; }
        }

        private void UpdateDateText()
        {
            var culture = GetCurrentCulture();
            _dateText?.Text = _selectedDate.ToString("MMMM dd, yyyy", culture);
            _dayOfWeekText?.Text = _selectedDate.ToString("dddd", culture).ToUpper(culture);

            if (_relativeDateText != null)
            {
                if (_selectedDate.Date == DateTime.Today)
                    _relativeDateText.Text = GetString(ResourceConstant.String.today);
                else if (_selectedDate.Date == DateTime.Today.AddDays(-1))
                    _relativeDateText.Text = GetString(ResourceConstant.String.yesterday);
                else if (_selectedDate.Date == DateTime.Today.AddDays(1))
                    _relativeDateText.Text = GetString(ResourceConstant.String.tomorrow);
                else
                    _relativeDateText.Text = _selectedDate.ToString("dddd", culture);
            }

            _addButton?.Text = _selectedDate.Date == DateTime.Today
                ? GetString(ResourceConstant.String.add_to_tracker_today)
                : string.Format(GetString(ResourceConstant.String.add_to_tracker_for), _selectedDate.ToString("MM-dd"));
        }

        private async void LoadData()
        {
            if (Activity == null || _adapter == null) return;

            var completions = await _database.GetHabitCompletionsForDateAsync(_selectedDate);
            var allHabits = await _database.GetHabitsAsync();

            // Map completions back to habits and sort by completion status
            var pairedData = completions
                .Select(c => new { Completion = c, Habit = allHabits.FirstOrDefault(h => h.Id == c.HabitId) })
                .Where(p => p.Habit != null)
                .OrderBy(p => p.Completion.CompletedDate.HasValue) // false (not completed) comes first
                .ToList();

            Activity?.RunOnUiThread(() =>
            {
                if (Activity != null)
                {
                    _adapter?.UpdateData(
                        pairedData.Select(p => p.Habit!).ToList(),
                        pairedData.Select(p => p.Completion).ToList());
                    _emptyState?.Visibility = pairedData.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
                }
            });
        }

        private async Task OnItemClick(int position)
        {
            if (position >= _completions.Count) return;

            var completion = _completions[position];
            var updated = new HabitCompletion
            {
                Id = completion.Id,
                HabitId = completion.HabitId,
                CreatedDate = completion.CreatedDate,
                DueDate = completion.DueDate,
                CompletedDate = completion.CompletedDate.HasValue ? null : DateTime.Now
            };

            await _database.UpdateHabitCompletionAsync(updated);
            LoadData();
        }

        private async void ShowAddTrackedHabitDialog()
        {
            if (Activity == null) return;

            var allHabits = await _database.GetHabitsAsync();

            // Exclude habits already tracked on the selected date
            var alreadyTrackedIds = _completions.Select(c => c.HabitId).ToList();
            var availableHabits = allHabits.Where(h => !alreadyTrackedIds.Contains(h.Id)).ToList();

            if (availableHabits.Count == 0)
            {
                Toast.MakeText(Activity, GetString(ResourceConstant.String.all_habits_tracked_error), ToastLength.Short)
                    ?.Show();
                return;
            }

            var habitNames = availableHabits.Select(h => h.Name).ToArray();
            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(GetString(ResourceConstant.String.track_a_habit));
            builder.SetItems(habitNames, async void (_, e) =>
            {
                var selectedHabit = availableHabits[e.Which];
                var newCompletion = new HabitCompletion
                {
                    HabitId = selectedHabit.Id,
                    CreatedDate = DateTime.Now,
                    DueDate = _selectedDate.Date,
                    CompletedDate = null
                };
                await _database.SaveHabitCompletionAsync(newCompletion);
                LoadData();
            });
            builder.SetNeutralButton(GetString(ResourceConstant.String.add_all_activities), async void (_, _) =>
            {
                foreach (var habit in availableHabits)
                {
                    var newCompletion = new HabitCompletion
                    {
                        HabitId = habit.Id,
                        CreatedDate = DateTime.Now,
                        DueDate = _selectedDate.Date,
                        CompletedDate = null
                    };
                    await _database.SaveHabitCompletionAsync(newCompletion);
                }

                LoadData();
            });
            builder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) => { });
            builder.Show();
        }

        private async Task CopyHabitsForWeek()
        {
            if (Activity == null) return;

            var currentCompletions = await _database.GetHabitCompletionsForDateAsync(_selectedDate);
            if (currentCompletions.Count == 0)
            {
                Toast.MakeText(Activity, GetString(ResourceConstant.String.no_habits_to_copy), ToastLength.Short)?.Show();
                return;
            }

            var tcs = new TaskCompletionSource<bool>();
            var confirmBuilder = new AlertDialog.Builder(Activity);
            confirmBuilder.SetTitle(GetString(ResourceConstant.String.copy_week_confirm_title));
            confirmBuilder.SetMessage(GetString(ResourceConstant.String.copy_week_confirm_message));
            confirmBuilder.SetPositiveButton(GetString(ResourceConstant.String.ok), (_, _) => tcs.TrySetResult(true));
            confirmBuilder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) => tcs.TrySetResult(false));
            confirmBuilder.SetCancelable(false);
            confirmBuilder.Show();

            if (!await tcs.Task) return;

            var currentDayOfWeek = (int)_selectedDate.DayOfWeek;
            var daysToSubtract = currentDayOfWeek == 0 ? 6 : currentDayOfWeek - 1;
            var startOfWeek = _selectedDate.AddDays(-daysToSubtract).Date;

            var daysAdded = 0;
            for (var i = 0; i < 7; i++)
            {
                var targetDate = startOfWeek.AddDays(i);
                if (targetDate == _selectedDate.Date) continue;

                var existingCompletions = await _database.GetHabitCompletionsForDateAsync(targetDate);
                var existingHabitIds = existingCompletions.Select(c => c.HabitId).ToList();

                var toAdd = currentCompletions.Where(c => !existingHabitIds.Contains(c.HabitId)).ToList();
                foreach (var completion in toAdd)
                {
                    await _database.SaveHabitCompletionAsync(new HabitCompletion
                    {
                        HabitId = completion.HabitId,
                        CreatedDate = DateTime.Now,
                        DueDate = targetDate,
                        CompletedDate = null
                    });
                }
                if (toAdd.Count > 0) daysAdded++;
            }

            var successBuilder = new AlertDialog.Builder(Activity);
            successBuilder.SetMessage(string.Format(GetString(ResourceConstant.String.copy_week_success),
                currentCompletions.Count, daysAdded));
            successBuilder.SetPositiveButton(GetString(ResourceConstant.String.ok), (_, _) => { });
            successBuilder.Show();
        }

        private class DatePickerPositiveListener : Java.Lang.Object, IMaterialPickerOnPositiveButtonClickListener
        {
            private readonly Action<long> _onSelection;

            public DatePickerPositiveListener(Action<long> onSelection)
            {
                _onSelection = onSelection;
            }

            public void OnPositiveButtonClick(Java.Lang.Object? selection)
            {
                if (selection is Java.Lang.Long longSelection)
                {
                    _onSelection(longSelection.LongValue());
                }
            }
        }
    }

    public class TrackerAdapter : RecyclerView.Adapter
    {
        private readonly List<Habit> _habits;
        private readonly List<HabitCompletion> _completions;
        private readonly Action<int> _onItemClick;
        private readonly Func<DateTime> _getDate;

        public TrackerAdapter(List<Habit> habits, List<HabitCompletion> completions, Action<int> onItemClick,
            Func<DateTime> getDate)
        {
            _habits = habits;
            _completions = completions;
            _onItemClick = onItemClick;
            _getDate = getDate;
        }

        public void UpdateData(List<Habit> newHabits, List<HabitCompletion> newCompletions)
        {
            var oldHabits = new List<Habit>(_habits);
            var oldCompletions = new List<HabitCompletion>(_completions);
            var diff = DiffUtil.CalculateDiff(new TrackerDiffCallback(oldHabits, oldCompletions, newHabits, newCompletions));
            _habits.Clear();
            _habits.AddRange(newHabits);
            _completions.Clear();
            _completions.AddRange(newCompletions);
            diff.DispatchUpdatesTo(this);
        }

        private class TrackerDiffCallback(
            List<Habit> oldHabits, List<HabitCompletion> oldCompletions,
            List<Habit> newHabits, List<HabitCompletion> newCompletions) : DiffUtil.Callback
        {
            public override int OldListSize => oldHabits.Count;
            public override int NewListSize => newHabits.Count;
            public override bool AreItemsTheSame(int oldPos, int newPos) =>
                oldCompletions[oldPos].Id == newCompletions[newPos].Id;
            public override bool AreContentsTheSame(int oldPos, int newPos) =>
                oldHabits[oldPos].Name == newHabits[newPos].Name &&
                oldHabits[oldPos].ColorHex == newHabits[newPos].ColorHex &&
                oldCompletions[oldPos].CompletedDate.HasValue == newCompletions[newPos].CompletedDate.HasValue;
        }

        public override int ItemCount => _habits.Count;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var trackerHolder = (TrackerViewHolder)holder;
            var habit = _habits[position];
            var date = _getDate().Date;
            var isCompleted = _completions.Any(c =>
                c.HabitId == habit.Id && c.CompletedDate.HasValue && c.CompletedDate.Value.Date == date);

            trackerHolder.HabitName.Text = habit.Name;
            trackerHolder.Checkbox.Checked = isCompleted;

            if (isCompleted)
            {
                trackerHolder.HabitName.PaintFlags |= Android.Graphics.PaintFlags.StrikeThruText;
                trackerHolder.HabitName.Alpha = 0.6f;
            }
            else
            {
                trackerHolder.HabitName.PaintFlags &= ~Android.Graphics.PaintFlags.StrikeThruText;
                trackerHolder.HabitName.Alpha = 1.0f;
            }

            // Apply habit color to indicator
            if (!string.IsNullOrEmpty(habit.ColorHex))
            {
                try
                {
                    var color = Android.Graphics.Color.ParseColor(habit.ColorHex);
                    var background = trackerHolder.ColorIndicator.Background as GradientDrawable;
                    background?.SetColor(color);

                    // Also tint the checkbox
                    trackerHolder.Checkbox.ButtonTintList = Android.Content.Res.ColorStateList.ValueOf(color);
                }
                catch
                {
                    // Fallback
                }
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)
                ?.Inflate(ResourceConstant.Layout.item_tracker_habit, parent, false);
            var holder = new TrackerViewHolder(view!);

            view!.Click += (_, _) =>
            {
                if (holder.BindingAdapterPosition != RecyclerView.NoPosition)
                {
                    _onItemClick(holder.BindingAdapterPosition);
                }
            };

            // Also handle checkbox clicks
            holder.Checkbox.Click += (_, _) =>
            {
                if (holder.BindingAdapterPosition != RecyclerView.NoPosition)
                {
                    _onItemClick(holder.BindingAdapterPosition);
                }
            };

            return holder;
        }

        private class TrackerViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
        {
            public TextView HabitName { get; } =
                itemView.FindViewById<TextView>(ResourceConstant.Id.tracker_habit_name)!;

            public CheckBox Checkbox { get; } = itemView.FindViewById<CheckBox>(ResourceConstant.Id.tracker_checkbox)!;

            public View ColorIndicator { get; } =
                itemView.FindViewById<View>(ResourceConstant.Id.tracker_color_indicator)!;
        }
    }

    public class TrackerSwipeCallback : ItemTouchHelper.SimpleCallback
    {
        private readonly Action<int> _onDeleted;
        private readonly Action<int> _onCompleted;
        private readonly Func<int, bool> _isCompleted;
        private readonly Android.Graphics.Paint _deleteBackgroundPaint;
        private readonly Android.Graphics.Paint _completeBackgroundPaint;
        private readonly Android.Graphics.Paint _undoBackgroundPaint;
        private readonly Android.Graphics.Paint _textPaint;
        private readonly Context? _context;

        public TrackerSwipeCallback(Action<int> onDeleted, Action<int> onCompleted, Func<int, bool> isCompleted,
            Context? context)
            : base(0, ItemTouchHelper.Left | ItemTouchHelper.Right)
        {
            _onDeleted = onDeleted;
            _onCompleted = onCompleted;
            _isCompleted = isCompleted;
            _context = context;
            _deleteBackgroundPaint = new Android.Graphics.Paint
            {
                Color = new Android.Graphics.Color(
                    AndroidX.Core.Content.ContextCompat.GetColor(Application.Context,
                        ResourceConstant.Color.colorDelete)),
                AntiAlias = true
            };
            _completeBackgroundPaint = new Android.Graphics.Paint
            {
                Color = new Android.Graphics.Color(
                    AndroidX.Core.Content.ContextCompat.GetColor(Application.Context,
                        ResourceConstant.Color.colorComplete)),
                AntiAlias = true
            };
            _undoBackgroundPaint = new Android.Graphics.Paint
            {
                Color = new Android.Graphics.Color(
                    AndroidX.Core.Content.ContextCompat.GetColor(Application.Context,
                        ResourceConstant.Color.colorUndo)),
                AntiAlias = true
            };
            var textSizePx = TypedValue.ApplyDimension(ComplexUnitType.Dip, 14,
                context?.Resources?.DisplayMetrics ?? Application.Context.Resources!.DisplayMetrics);
            _textPaint = new Android.Graphics.Paint
            {
                Color = Android.Graphics.Color.White,
                TextSize = textSizePx,
                TextAlign = Android.Graphics.Paint.Align.Center,
                AntiAlias = true
            };
            _textPaint.SetTypeface(Android.Graphics.Typeface.Create("sans-serif-medium",
                Android.Graphics.TypefaceStyle.Normal));
        }

        public override bool OnMove(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder,
            RecyclerView.ViewHolder target) => false;

        public override float GetSwipeThreshold(RecyclerView.ViewHolder viewHolder) => 0.2f;

        public override void OnSwiped(RecyclerView.ViewHolder viewHolder, int direction)
        {
            if (direction == ItemTouchHelper.Left)
            {
                _onDeleted(viewHolder.BindingAdapterPosition);
            }
            else if (direction == ItemTouchHelper.Right)
            {
                _onCompleted(viewHolder.BindingAdapterPosition);
            }
        }

        public override void OnChildDraw(Android.Graphics.Canvas c, RecyclerView recyclerView,
            RecyclerView.ViewHolder viewHolder, float dX, float dY, int actionState, bool isCurrentlyActive)
        {
            if (actionState == ItemTouchHelper.ActionStateSwipe)
            {
                var itemView = viewHolder.ItemView;
                float maxDisplacement;
                float currentDx;
                Android.Graphics.Paint backgroundPaint;
                string text;
                const float cornerRadius = 24f;

                switch (dX)
                {
                    // Swipe Left (Delete)
                    case < 0:
                    {
                        maxDisplacement = -itemView.Width * 0.2f;
                        currentDx = Math.Max(dX, maxDisplacement);
                        backgroundPaint = _deleteBackgroundPaint;
                        text = _context?.GetString(ResourceConstant.String.delete) ?? "Delete";

                        var left = itemView.Right + currentDx;
                        var background = new Android.Graphics.RectF(left - cornerRadius, itemView.Top + 12,
                            itemView.Right - 24, itemView.Bottom - 12);
                        c.DrawRoundRect(background, cornerRadius, cornerRadius, backgroundPaint);

                        var textBounds = new Android.Graphics.Rect();
                        _textPaint.GetTextBounds(text, 0, text.Length, textBounds);

                        // Clip text
                        c.Save();
                        c.ClipRect(background);

                        var textX = left + Math.Abs(currentDx) / 2f - 12;
                        var textY = itemView.Top + (itemView.Height + textBounds.Height()) / 2f;
                        c.DrawText(text, textX, textY, _textPaint);

                        c.Restore();

                        base.OnChildDraw(c, recyclerView, viewHolder, currentDx, dY, actionState, isCurrentlyActive);
                        break;
                    }
                    // Swipe Right (Complete/Undo)
                    case > 0:
                    {
                        maxDisplacement = itemView.Width * 0.2f;
                        currentDx = Math.Min(dX, maxDisplacement);

                        if (_isCompleted(viewHolder.BindingAdapterPosition))
                        {
                            backgroundPaint = _undoBackgroundPaint;
                            text = _context?.GetString(ResourceConstant.String.undo) ?? "Undo";
                        }
                        else
                        {
                            backgroundPaint = _completeBackgroundPaint;
                            text = _context?.GetString(ResourceConstant.String.complete) ?? "Complete";
                        }

                        var right = itemView.Left + currentDx;
                        var background = new Android.Graphics.RectF(itemView.Left + 24, itemView.Top + 12,
                            right + cornerRadius, itemView.Bottom - 12);
                        c.DrawRoundRect(background, cornerRadius, cornerRadius, backgroundPaint);

                        var textBounds = new Android.Graphics.Rect();
                        _textPaint.GetTextBounds(text, 0, text.Length, textBounds);

                        // Clip text
                        c.Save();
                        c.ClipRect(background);

                        var textX = itemView.Left + currentDx / 2f + 12;
                        var textY = itemView.Top + (itemView.Height + textBounds.Height()) / 2f;
                        c.DrawText(text, textX, textY, _textPaint);

                        c.Restore();

                        base.OnChildDraw(c, recyclerView, viewHolder, currentDx, dY, actionState, isCurrentlyActive);
                        break;
                    }
                    default:
                        base.OnChildDraw(c, recyclerView, viewHolder, dX, dY, actionState, isCurrentlyActive);
                        break;
                }
            }
            else
            {
                base.OnChildDraw(c, recyclerView, viewHolder, dX, dY, actionState, isCurrentlyActive);
            }
        }
    }
}