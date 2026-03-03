using _Microsoft.Android.Resource.Designer;
using Android.Views;
using Google.Android.Material.DatePicker;
using AndroidX.RecyclerView.Widget;
using HabitTracker.Data;
using Android.Graphics.Drawables;

namespace HabitTracker
{
    public class TrackerFragment : AndroidX.Fragment.App.Fragment
    {
        private RecyclerView? _recyclerView;
        private Button? _addButton;
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
        private Database? _database;
        private DateTime _selectedDate = DateTime.Today;

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            var view = inflater.Inflate(ResourceConstant.Layout.fragment_tracker, container, false);

            _recyclerView = view?.FindViewById<RecyclerView>(ResourceConstant.Id.tracker_list);
            _addButton = view?.FindViewById<Button>(ResourceConstant.Id.add_tracked_habit_button);
            _dateText = view?.FindViewById<TextView>(ResourceConstant.Id.selected_date_text);
            _dayOfWeekText = view?.FindViewById<TextView>(ResourceConstant.Id.selected_day_of_week);
            _relativeDateText = view?.FindViewById<TextView>(ResourceConstant.Id.relative_date_text);
            _pickDateButton = view?.FindViewById<Button>(ResourceConstant.Id.pick_date_button);
            _yesterdayButton = view?.FindViewById<Button>(ResourceConstant.Id.yesterday_button);
            _todayButton = view?.FindViewById<Button>(ResourceConstant.Id.today_button);
            _tomorrowButton = view?.FindViewById<Button>(ResourceConstant.Id.tomorrow_button);

            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "habits.db");
            var dbDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }
            _database = new Database(dbPath);

            if (_recyclerView != null)
            {
                _recyclerView.SetLayoutManager(new LinearLayoutManager(Activity));
                _adapter = new TrackerAdapter(_habits, _completions, async void (position) =>
                {
                    await OnItemClick(position);
                }, () => _selectedDate);
                _recyclerView.SetAdapter(_adapter);

                var callback = new TrackerSwipeCallback(async void (position) =>
                {
                    if (_database != null && position < _completions.Count)
                    {
                        var completion = _completions[position];
                        await _database.DeleteHabitCompletionAsync(completion);
                        Activity?.RunOnUiThread(() =>
                        {
                            if (Activity != null && position < _completions.Count)
                            {
                                _habits.RemoveAt(position);
                                _completions.RemoveAt(position);
                                _adapter?.NotifyItemRemoved(position);
                            }
                        });
                    }
                }, async void (position) =>
                {
                    await OnItemClick(position);
                }, (position) => 
                {
                    return position < _completions.Count && _completions[position].CompletedDate.HasValue;
                });

                var itemTouchHelper = new ItemTouchHelper(callback);
                itemTouchHelper.AttachToRecyclerView(_recyclerView);
            }

            if (_addButton != null)
            {
                _addButton.Click += (_, _) =>
                {
                    ShowAddTrackedHabitDialog();
                };
            }

            if (_pickDateButton != null)
            {
                _pickDateButton.Click += (_, _) =>
                {
                    var builder = MaterialDatePicker.Builder.DatePicker();
                    builder.SetTitleText("Select Date");
                    
                    // Convert DateTime to milliseconds for MaterialDatePicker
                    var selection = (long)(_selectedDate.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
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

        public void OnDateSet(DatePicker? view, int year, int month, int dayOfMonth)
        {
            // This method is now unused, as we are using MaterialDatePicker
        }

        private void UpdateDateText()
        {
            if (_dateText != null)
            {
                _dateText.Text = _selectedDate.ToString("MMMM dd, yyyy");
            }
            if (_dayOfWeekText != null)
            {
                _dayOfWeekText.Text = _selectedDate.DayOfWeek.ToString();
            }
            if (_relativeDateText != null)
            {
                if (_selectedDate.Date == DateTime.Today)
                    _relativeDateText.Text = "Today";
                else if (_selectedDate.Date == DateTime.Today.AddDays(-1))
                    _relativeDateText.Text = "Yesterday";
                else if (_selectedDate.Date == DateTime.Today.AddDays(1))
                    _relativeDateText.Text = "Tomorrow";
                else
                    _relativeDateText.Text = _selectedDate.ToString("dddd");
            }
            if (_addButton != null)
            {
                _addButton.Text = _selectedDate.Date == DateTime.Today ? "Add to Tracker Today" : $"Add to Tracker for {_selectedDate:MM-dd}";
            }
        }

        private async void LoadData()
        {
            if (Activity == null || _database == null || _adapter == null) return;

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
                    _habits.Clear();
                    _habits.AddRange(pairedData.Select(p => p.Habit!));
                    _completions.Clear();
                    _completions.AddRange(pairedData.Select(p => p.Completion));
                    _adapter?.NotifyDataSetChanged();
                }
            });
        }

        private async Task OnItemClick(int position)
        {
            if (_database == null || position >= _completions.Count) return;

            var completion = _completions[position];
            if (completion.CompletedDate.HasValue)
            {
                completion.CompletedDate = null;
            }
            else
            {
                completion.CompletedDate = DateTime.Now;
            }
            
            await _database.UpdateHabitCompletionAsync(completion);
            LoadData();
        }

        private async void ShowAddTrackedHabitDialog()
        {
            if (Activity == null || _database == null) return;

            var allHabits = await _database.GetHabitsAsync();
            
            // Exclude habits already tracked on the selected date
            var alreadyTrackedIds = _completions.Select(c => c.HabitId).ToList();
            var availableHabits = allHabits.Where(h => !alreadyTrackedIds.Contains(h.Id)).ToList();

            if (!availableHabits.Any())
            {
                Toast.MakeText(Activity, "All habits are already being tracked for this date.", ToastLength.Short)?.Show();
                return;
            }

            var habitNames = availableHabits.Select(h => h.Name).ToArray();
            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle("Track a Habit");
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
            builder.SetNegativeButton("Cancel", (_, _) => { });
            builder.Show();
        }

        private class DatePickerPositiveListener(Action<long> onSelection)
            : Java.Lang.Object, IMaterialPickerOnPositiveButtonClickListener
        {
            public void OnPositiveButtonClick(Java.Lang.Object? selection)
            {
                if (selection is Java.Lang.Long longSelection)
                {
                    onSelection(longSelection.LongValue());
                }
            }
        }
    }

    public class TrackerAdapter(
        List<Habit> habits,
        List<HabitCompletion> completions,
        Action<int> onItemClick,
        Func<DateTime> getDate)
        : RecyclerView.Adapter
    {
        public override int ItemCount => habits.Count;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var trackerHolder = (TrackerViewHolder)holder;
            var habit = habits[position];
            var date = getDate().Date;
            var isCompleted = completions.Any(c => c.HabitId == habit.Id && c.CompletedDate.HasValue && c.CompletedDate.Value.Date == date);

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
            var view = LayoutInflater.From(parent.Context)?.Inflate(ResourceConstant.Layout.item_tracker_habit, parent, false);
            var holder = new TrackerViewHolder(view!);
            
            view!.Click += (_, _) =>
            {
                if (holder.BindingAdapterPosition != RecyclerView.NoPosition)
                {
                    onItemClick(holder.BindingAdapterPosition);
                }
            };
            
            // Also handle checkbox clicks
            holder.Checkbox.Click += (_, _) =>
            {
                if (holder.BindingAdapterPosition != RecyclerView.NoPosition)
                {
                    onItemClick(holder.BindingAdapterPosition);
                }
            };

            return holder;
        }

        private class TrackerViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
        {
            public TextView HabitName { get; } = itemView.FindViewById<TextView>(ResourceConstant.Id.tracker_habit_name)!;
            public CheckBox Checkbox { get; } = itemView.FindViewById<CheckBox>(ResourceConstant.Id.tracker_checkbox)!;
            public View ColorIndicator { get; } = itemView.FindViewById<View>(ResourceConstant.Id.tracker_color_indicator)!;
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

        public TrackerSwipeCallback(Action<int> onDeleted, Action<int> onCompleted, Func<int, bool> isCompleted) 
            : base(0, ItemTouchHelper.Left | ItemTouchHelper.Right)
        {
            _onDeleted = onDeleted;
            _onCompleted = onCompleted;
            _isCompleted = isCompleted;
            _deleteBackgroundPaint = new Android.Graphics.Paint { Color = new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(Application.Context, ResourceConstant.Color.colorDelete)), AntiAlias = true };
            _completeBackgroundPaint = new Android.Graphics.Paint { Color = new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(Application.Context, ResourceConstant.Color.colorComplete)), AntiAlias = true };
            _undoBackgroundPaint = new Android.Graphics.Paint { Color = new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(Application.Context, ResourceConstant.Color.colorUndo)), AntiAlias = true };
            _textPaint = new Android.Graphics.Paint
            {
                Color = Android.Graphics.Color.White,
                TextSize = 32,
                TextAlign = Android.Graphics.Paint.Align.Center,
                AntiAlias = true
            };
            _textPaint.SetTypeface(Android.Graphics.Typeface.Create("sans-serif-medium", Android.Graphics.TypefaceStyle.Normal));
        }

        public override bool OnMove(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder, RecyclerView.ViewHolder target) => false;

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

        public override void OnChildDraw(Android.Graphics.Canvas c, RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder, float dX, float dY, int actionState, bool isCurrentlyActive)
        {
            if (actionState == ItemTouchHelper.ActionStateSwipe)
            {
                var itemView = viewHolder.ItemView;
                float maxDisplacement;
                float currentDx;
                Android.Graphics.Paint backgroundPaint;
                string text;
                var cornerRadius = 24f;

                if (dX < 0) // Swipe Left (Delete)
                {
                    maxDisplacement = -itemView.Width * 0.2f;
                    currentDx = Math.Max(dX, maxDisplacement);
                    backgroundPaint = _deleteBackgroundPaint;
                    text = "Delete";

                    var left = itemView.Right + currentDx;
                    var background = new Android.Graphics.RectF(left - cornerRadius, itemView.Top + 12, itemView.Right - 24, itemView.Bottom - 12);
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
                }
                else if (dX > 0) // Swipe Right (Complete/Undo)
                {
                    maxDisplacement = itemView.Width * 0.2f;
                    currentDx = Math.Min(dX, maxDisplacement);
                    
                    if (_isCompleted(viewHolder.BindingAdapterPosition))
                    {
                        backgroundPaint = _undoBackgroundPaint;
                        text = "Undo";
                    }
                    else
                    {
                        backgroundPaint = _completeBackgroundPaint;
                        text = "Complete";
                    }

                    var right = itemView.Left + currentDx;
                    var background = new Android.Graphics.RectF(itemView.Left + 24, itemView.Top + 12, right + cornerRadius, itemView.Bottom - 12);
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
                }
                else
                {
                    base.OnChildDraw(c, recyclerView, viewHolder, dX, dY, actionState, isCurrentlyActive);
                }
            }
            else
            {
                base.OnChildDraw(c, recyclerView, viewHolder, dX, dY, actionState, isCurrentlyActive);
            }
        }
    }
}
