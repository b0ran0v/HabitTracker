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
        // Required by the FragmentManager when restoring state (e.g. after Activity.Recreate)
        public TrackerFragment() : this(MainActivity.SharedDatabase!) { }

        private RecyclerView? _recyclerView;
        private View? _emptyState;
        private Button? _addButton;
        private Button? _copyWeekButton;
        private TextView? _dateText;
        private TextView? _relativeDateText;
        private Button? _pickDateButton;
        private LinearLayout? _weekStrip;
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
            _relativeDateText = view?.FindViewById<TextView>(ResourceConstant.Id.relative_date_text);
            _pickDateButton = view?.FindViewById<Button>(ResourceConstant.Id.pick_date_button);
            _weekStrip = view?.FindViewById<LinearLayout>(ResourceConstant.Id.week_strip);

            if (_recyclerView != null)
            {
                _recyclerView.SetLayoutManager(new LinearLayoutManager(Activity));
                _adapter = new TrackerAdapter(_habits, _completions,
                    position => UiSafe.Run(Context, () => OnItemClick(position)));
                _recyclerView.SetAdapter(_adapter);

                var callback = new TrackerSwipeCallback(position => UiSafe.Run(Context, async () =>
                    {
                        var completion = _adapter?.GetCompletionAt(position);
                        if (completion == null) return;
                        await _database.DeleteHabitCompletionAsync(completion);
                        Activity?.RunOnUiThread(() =>
                        {
                            if (Activity == null) return;
                            LoadData();
                        });
                    }), position => UiSafe.Run(Context, () => OnItemClick(position)),
                    position => _adapter?.GetCompletionAt(position)?.CompletedDate.HasValue == true,
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
                _copyWeekButton.Click += (_, _) => UiSafe.Run(Context, CopyHabitsForWeek);
            }

            // Both the compact card and its button open the picker, giving a larger tap target
            var dateContainer = view?.FindViewById<View>(ResourceConstant.Id.date_display_container);
            if (_pickDateButton != null)
                _pickDateButton.Click += (_, _) => ShowDatePicker();
            if (dateContainer != null)
                dateContainer.Click += (_, _) => ShowDatePicker();

            UpdateDateText();
            LoadData();

            return view;
        }

        // Tabs are hidden/shown rather than recreated, so refresh whenever this tab is
        // revealed — habits may have been edited, archived, or deleted on other tabs
        public override void OnHiddenChanged(bool hidden)
        {
            base.OnHiddenChanged(hidden);
            if (!hidden) LoadData();
        }

        private CultureInfo GetCurrentCulture()
        {
            var prefs = Activity?.GetSharedPreferences("HabitTrackerPrefs", FileCreationMode.Private);
            var lang = prefs?.GetString("app_language", null);
            try { return lang != null ? new CultureInfo(lang) : CultureInfo.CurrentCulture; }
            catch { return CultureInfo.CurrentCulture; }
        }

        private void ShowDatePicker()
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
        }

        private void UpdateDateText()
        {
            var culture = GetCurrentCulture();
            _dateText?.Text = _selectedDate.ToString("MMMM dd, yyyy", culture);

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

        private void LoadData() => UiSafe.Run(Context, LoadDataAsync);

        private async Task LoadDataAsync()
        {
            if (Activity == null || _adapter == null) return;

            var completions = await _database.GetHabitCompletionsForDateAsync(_selectedDate);
            var activeHabits = await _database.GetHabitsAsync();
            var archivedHabits = await _database.GetArchivedHabitsAsync();
            var allHabits = activeHabits.Concat(archivedHabits).ToList();
            var allCompletions = await _database.GetHabitCompletionsAsync();

            var pairedData = completions
                .Select(c => new { Completion = c, Habit = allHabits.FirstOrDefault(h => h.Id == c.HabitId) })
                .Where(p => p.Habit != null)
                .OrderBy(p => p.Completion.CompletedDate.HasValue)
                .ToList();

            Activity?.RunOnUiThread(() =>
            {
                if (Activity != null)
                {
                    _adapter?.UpdateData(
                        pairedData.Select(p => p.Habit!).ToList(),
                        pairedData.Select(p => p.Completion).ToList());
                    _emptyState?.Visibility = pairedData.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
                    UpdateWeekStrip(allCompletions, allHabits);
                }
            });

            // Every data mutation funnels through LoadData, so this keeps home screen widgets fresh
            if (Context != null) HabitWidgetProvider.RequestUpdate(Context);
        }

        // Renders the calendar week (Monday-start, matching CopyHabitsForWeek) around the
        // selected date: day name, day number, and a dot per habit completed on that day.
        private void UpdateWeekStrip(List<HabitCompletion> allCompletions, List<Habit> allHabits)
        {
            if (_weekStrip == null || Context == null) return;
            _weekStrip.RemoveAllViews();

            var culture = GetCurrentCulture();
            var colorByHabitId = allHabits.ToDictionary(h => h.Id, h => h.ColorHex);
            var currentDayOfWeek = (int)_selectedDate.DayOfWeek;
            var daysToSubtract = currentDayOfWeek == 0 ? 6 : currentDayOfWeek - 1;
            var startOfWeek = _selectedDate.AddDays(-daysToSubtract).Date;

            var metrics = Context.Resources!.DisplayMetrics;
            int Dp(float v) => (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, v, metrics);
            Android.Graphics.Color Res(int id) =>
                new(AndroidX.Core.Content.ContextCompat.GetColor(Context, id));

            for (var i = 0; i < 7; i++)
            {
                var date = startOfWeek.AddDays(i);
                var isSelected = date == _selectedDate.Date;
                var isToday = date == DateTime.Today;

                var column = new LinearLayout(Context)
                {
                    Orientation = Orientation.Vertical,
                    LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
                };
                column.SetGravity(GravityFlags.CenterHorizontal);
                column.SetPadding(0, Dp(6), 0, Dp(6));
                if (isSelected)
                {
                    var pill = new GradientDrawable();
                    pill.SetColor(Res(ResourceConstant.Color.colorPrimary));
                    pill.SetCornerRadius(Dp(12));
                    column.Background = pill;
                }

                // Default LinearLayout params make these full-width, so the parent's
                // CenterHorizontal gravity is a no-op — center the text itself instead
                var dayName = new TextView(Context)
                {
                    Text = culture.DateTimeFormat.AbbreviatedDayNames[(int)date.DayOfWeek]
                        .TrimEnd('.').ToUpper(culture),
                    TextSize = 10,
                    Gravity = GravityFlags.Center
                };
                dayName.SetTextColor(isSelected ? Android.Graphics.Color.White
                    : Res(ResourceConstant.Color.textColorSecondary));
                column.AddView(dayName);

                var dayNumber = new TextView(Context)
                {
                    Text = date.Day.ToString(),
                    TextSize = 14,
                    Gravity = GravityFlags.Center
                };
                dayNumber.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
                dayNumber.SetTextColor(isSelected ? Android.Graphics.Color.White
                    : isToday ? Res(ResourceConstant.Color.colorPrimary)
                    : Res(ResourceConstant.Color.textColorPrimary));
                column.AddView(dayNumber);

                var dots = new LinearLayout(Context)
                {
                    Orientation = Orientation.Horizontal,
                    LayoutParameters = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.WrapContent, Dp(9)) { TopMargin = Dp(3) }
                };
                dots.SetGravity(GravityFlags.Center);

                var completedColors = allCompletions
                    .Where(c => c.DueDate.Date == date && c.CompletedDate.HasValue)
                    .Select(c => colorByHabitId.GetValueOrDefault(c.HabitId))
                    .Where(hex => !string.IsNullOrEmpty(hex))
                    .Take(4)
                    .ToList();
                foreach (var hex in completedColors)
                {
                    Android.Graphics.Color dotColor;
                    try { dotColor = Android.Graphics.Color.ParseColor(hex!); }
                    catch { dotColor = Res(ResourceConstant.Color.colorPrimary); }
                    if (isSelected) dotColor = Android.Graphics.Color.White;

                    var dot = new View(Context)
                    {
                        LayoutParameters = new LinearLayout.LayoutParams(Dp(6), Dp(6))
                            { LeftMargin = Dp(1), RightMargin = Dp(1) }
                    };
                    var circle = new GradientDrawable();
                    circle.SetShape(ShapeType.Oval);
                    circle.SetColor(dotColor);
                    dot.Background = circle;
                    dots.AddView(dot);
                }
                column.AddView(dots);

                var tappedDate = date;
                column.Click += (_, _) =>
                {
                    _selectedDate = tappedDate;
                    UpdateDateText();
                    LoadData();
                };

                _weekStrip.AddView(column);
            }
        }

        private async Task OnItemClick(int position)
        {
            var completion = _adapter?.GetCompletionAt(position);
            if (completion == null) return;

            var isCompleting = !completion.CompletedDate.HasValue;

            if (isCompleting)
            {
                await ShowNotesDialogAndComplete(completion);
            }
            else
            {
                var updated = new HabitCompletion
                {
                    Id = completion.Id,
                    HabitId = completion.HabitId,
                    CreatedDate = completion.CreatedDate,
                    DueDate = completion.DueDate,
                    CompletedDate = null,
                    Notes = completion.Notes
                };
                await _database.UpdateHabitCompletionAsync(updated);
                LoadData();
            }
        }

        private async Task ShowNotesDialogAndComplete(HabitCompletion completion)
        {
            if (Activity == null) return;

            var input = new EditText(Activity)
            {
                Hint = GetString(ResourceConstant.String.note_hint),
                Text = completion.Notes,
                InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagCapSentences
            };
            input.SetMaxLines(2);
            input.SetPadding(48, 24, 48, 24);

            var tcs = new TaskCompletionSource<string?>();
            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(GetString(ResourceConstant.String.add_note));
            builder.SetView(input);
            builder.SetPositiveButton(GetString(ResourceConstant.String.complete), (_, _) =>
                tcs.TrySetResult(input.Text));
            builder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) =>
                tcs.TrySetResult(null));
            builder.SetCancelable(false);
            builder.Show();

            var notes = await tcs.Task;
            if (notes == null) return;

            var updated = new HabitCompletion
            {
                Id = completion.Id,
                HabitId = completion.HabitId,
                CreatedDate = completion.CreatedDate,
                DueDate = completion.DueDate,
                CompletedDate = DateTime.Now,
                Notes = notes
            };
            await _database.UpdateHabitCompletionAsync(updated);
            LoadData();
        }

        private void ShowAddTrackedHabitDialog() => UiSafe.Run(Context, ShowAddTrackedHabitDialogAsync);

        private async Task ShowAddTrackedHabitDialogAsync()
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
            builder.SetItems(habitNames, (_, e) => UiSafe.Run(Context, async () =>
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
            }));
            builder.SetNeutralButton(GetString(ResourceConstant.String.add_all_activities), (_, _) => UiSafe.Run(Context, async () =>
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
            }));
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
        public const int ViewTypeHeader = 0;
        private const int ViewTypeItem = 1;

        // A row is either a category header (Header != null) or a habit/completion pair
        private class Row
        {
            public string? Header;
            public Habit? Habit;
            public HabitCompletion? Completion;
        }

        private readonly List<Habit> _habits;
        private readonly List<HabitCompletion> _completions;
        private List<Row> _rows = [];
        private readonly Action<int> _onItemClick;

        public TrackerAdapter(List<Habit> habits, List<HabitCompletion> completions, Action<int> onItemClick)
        {
            _habits = habits;
            _completions = completions;
            _onItemClick = onItemClick;
        }

        // Uncategorized habits come first without a header, then each category
        // alphabetically under its own header. Within a group the input order is kept.
        private static List<Row> BuildRows(List<Habit> habits, List<HabitCompletion> completions)
        {
            var rows = new List<Row>();
            foreach (var group in habits
                         .Select((h, i) => new Row { Habit = h, Completion = completions[i] })
                         .GroupBy(r => r.Habit!.Category.Trim())
                         .OrderBy(g => g.Key.Length == 0 ? 0 : 1)
                         .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                if (group.Key.Length > 0)
                    rows.Add(new Row { Header = group.Key });
                rows.AddRange(group);
            }
            return rows;
        }

        public void UpdateData(List<Habit> newHabits, List<HabitCompletion> newCompletions)
        {
            var newRows = BuildRows(newHabits, newCompletions);
            var diff = DiffUtil.CalculateDiff(new TrackerDiffCallback(_rows, newRows));
            _rows = newRows;
            _habits.Clear();
            _habits.AddRange(newHabits);
            _completions.Clear();
            _completions.AddRange(newCompletions);
            diff.DispatchUpdatesTo(this);
        }

        public HabitCompletion? GetCompletionAt(int position) =>
            position >= 0 && position < _rows.Count ? _rows[position].Completion : null;

        private class TrackerDiffCallback(List<Row> oldRows, List<Row> newRows) : DiffUtil.Callback
        {
            public override int OldListSize => oldRows.Count;
            public override int NewListSize => newRows.Count;
            public override bool AreItemsTheSame(int oldPos, int newPos)
            {
                var o = oldRows[oldPos];
                var n = newRows[newPos];
                if (o.Header != null || n.Header != null) return o.Header == n.Header;
                return o.Completion!.Id == n.Completion!.Id;
            }
            public override bool AreContentsTheSame(int oldPos, int newPos)
            {
                var o = oldRows[oldPos];
                var n = newRows[newPos];
                if (o.Header != null) return true; // header text equality is checked in AreItemsTheSame
                return o.Habit!.Name == n.Habit!.Name &&
                       o.Habit.ColorHex == n.Habit.ColorHex &&
                       o.Completion!.CompletedDate.HasValue == n.Completion!.CompletedDate.HasValue &&
                       o.Completion.Notes == n.Completion.Notes;
            }
        }

        public override int ItemCount => _rows.Count;

        public override int GetItemViewType(int position) =>
            _rows[position].Header != null ? ViewTypeHeader : ViewTypeItem;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var row = _rows[position];
            if (holder is HeaderViewHolder headerHolder)
            {
                headerHolder.HeaderText.Text = row.Header;
                return;
            }

            var trackerHolder = (TrackerViewHolder)holder;
            var habit = row.Habit!;
            var isCompleted = row.Completion!.CompletedDate.HasValue;

            trackerHolder.HabitName.Text = habit.Name;
            trackerHolder.Checkbox.Checked = isCompleted;

            var notes = row.Completion.Notes;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                trackerHolder.HabitNotes.Text = notes;
                trackerHolder.HabitNotes.Visibility = ViewStates.Visible;
            }
            else
            {
                trackerHolder.HabitNotes.Visibility = ViewStates.Gone;
            }

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
            if (viewType == ViewTypeHeader)
            {
                var headerView = LayoutInflater.From(parent.Context)
                    ?.Inflate(ResourceConstant.Layout.item_category_header, parent, false);
                return new HeaderViewHolder(headerView!);
            }

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

        private class HeaderViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
        {
            public TextView HeaderText { get; } =
                itemView.FindViewById<TextView>(ResourceConstant.Id.category_header_text)!;
        }

        private class TrackerViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
        {
            public TextView HabitName { get; } =
                itemView.FindViewById<TextView>(ResourceConstant.Id.tracker_habit_name)!;

            public TextView HabitNotes { get; } =
                itemView.FindViewById<TextView>(ResourceConstant.Id.tracker_habit_notes)!;

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

        // Category headers are not swipeable
        public override int GetSwipeDirs(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder) =>
            viewHolder.ItemViewType == TrackerAdapter.ViewTypeHeader ? 0 : base.GetSwipeDirs(recyclerView, viewHolder);

        public override float GetSwipeThreshold(RecyclerView.ViewHolder viewHolder) => 0.2f;

        public override void OnSwiped(RecyclerView.ViewHolder viewHolder, int direction)
        {
            if (direction == ItemTouchHelper.Left)
            {
                _onDeleted(viewHolder.BindingAdapterPosition);
            }
            else if (direction == ItemTouchHelper.Right)
            {
                var position = viewHolder.BindingAdapterPosition;
                // Complete/undo keeps the row, so rebind it to clear the swipe offset —
                // otherwise cancelling the notes dialog leaves the row stuck open
                viewHolder.BindingAdapter?.NotifyItemChanged(position);
                _onCompleted(position);
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