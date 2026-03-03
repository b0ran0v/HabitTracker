using Android.Views;
using Google.Android.Material.DatePicker;
using AndroidX.RecyclerView.Widget;
using HabitTracker.Data;

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
        private List<Habit> _habits = new();
        private List<HabitCompletion> _completions = new();
        private TrackerAdapter? _adapter;
        private Database? _database;
        private DateTime _selectedDate = DateTime.Today;

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_tracker, container, false);

            _recyclerView = view?.FindViewById<RecyclerView>(Resource.Id.tracker_list);
            _addButton = view?.FindViewById<Button>(Resource.Id.add_tracked_habit_button);
            _dateText = view?.FindViewById<TextView>(Resource.Id.selected_date_text);
            _dayOfWeekText = view?.FindViewById<TextView>(Resource.Id.selected_day_of_week);
            _relativeDateText = view?.FindViewById<TextView>(Resource.Id.relative_date_text);
            _pickDateButton = view?.FindViewById<Button>(Resource.Id.pick_date_button);
            _yesterdayButton = view?.FindViewById<Button>(Resource.Id.yesterday_button);
            _todayButton = view?.FindViewById<Button>(Resource.Id.today_button);
            _tomorrowButton = view?.FindViewById<Button>(Resource.Id.tomorrow_button);

            var dbPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "habits.db");
            var dbDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }
            _database = new Database(dbPath);

            if (_recyclerView != null)
            {
                _recyclerView.SetLayoutManager(new LinearLayoutManager(Activity));
                _adapter = new TrackerAdapter(_habits, _completions, async (position) =>
                {
                    await OnItemClick(position);
                }, () => _selectedDate);
                _recyclerView.SetAdapter(_adapter);

                var callback = new TrackerSwipeCallback(async (position) =>
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
                }, async (position) =>
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
                _addButton.Click += (sender, e) =>
                {
                    ShowAddTrackedHabitDialog();
                };
            }

            if (_pickDateButton != null)
            {
                _pickDateButton.Click += (sender, e) =>
                {
                    var builder = MaterialDatePicker.Builder.DatePicker();
                    builder.SetTitleText("Select Date");
                    
                    // Convert DateTime to milliseconds for MaterialDatePicker
                    var selection = (long)(_selectedDate.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                    builder.SetSelection(Java.Lang.Long.ValueOf(selection));

                    var picker = (MaterialDatePicker)builder.Build();
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
                _yesterdayButton.Click += (sender, e) =>
                {
                    _selectedDate = DateTime.Today.AddDays(-1);
                    UpdateDateText();
                    LoadData();
                };
            }

            if (_todayButton != null)
            {
                _todayButton.Click += (sender, e) =>
                {
                    _selectedDate = DateTime.Today;
                    UpdateDateText();
                    LoadData();
                };
            }

            if (_tomorrowButton != null)
            {
                _tomorrowButton.Click += (sender, e) =>
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

        public void OnDateSet(Android.Widget.DatePicker? view, int year, int month, int dayOfMonth)
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
            
            // Exclude habits already tracked on selected date
            var alreadyTrackedIds = _completions.Select(c => c.HabitId).ToList();
            var availableHabits = allHabits.Where(h => !alreadyTrackedIds.Contains(h.Id)).ToList();

            if (!availableHabits.Any())
            {
                Android.Widget.Toast.MakeText(Activity, "All habits are already being tracked for this date.", ToastLength.Short)?.Show();
                return;
            }

            var habitNames = availableHabits.Select(h => h.Name).ToArray();
            var builder = new Android.App.AlertDialog.Builder(Activity);
            builder.SetTitle("Track a Habit");
            builder.SetItems(habitNames, async (_, e) =>
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

        public TrackerAdapter(List<Habit> habits, List<HabitCompletion> completions, Action<int> onItemClick, Func<DateTime> getDate)
        {
            _habits = habits;
            _completions = completions;
            _onItemClick = onItemClick;
            _getDate = getDate;
        }

        public override int ItemCount => _habits.Count;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var trackerHolder = (TrackerViewHolder)holder;
            var habit = _habits[position];
            var date = _getDate().Date;
            var isCompleted = _completions.Any(c => c.HabitId == habit.Id && c.CompletedDate.HasValue && c.CompletedDate.Value.Date == date);

            trackerHolder.TextView.Text = habit.Name;
            if (isCompleted)
            {
                trackerHolder.TextView.PaintFlags |= Android.Graphics.PaintFlags.StrikeThruText;
            }
            else
            {
                trackerHolder.TextView.PaintFlags &= ~Android.Graphics.PaintFlags.StrikeThruText;
            }

            // Apply habit color to card background
            if (!string.IsNullOrEmpty(habit.ColorHex))
            {
                try
                {
                    var color = Android.Graphics.Color.ParseColor(habit.ColorHex);
                    trackerHolder.Card.SetBackgroundColor(color);
                }
                catch
                {
                    trackerHolder.Card.SetBackgroundResource(Resource.Drawable.card_background);
                }
            }
            else
            {
                trackerHolder.Card.SetBackgroundResource(Resource.Drawable.card_background);
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var container = new FrameLayout(parent.Context)
            {
                LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            };
            container.SetPadding(24, 12, 24, 12);

            var card = new LinearLayout(parent.Context)
            {
                LayoutParameters = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent),
                Orientation = Orientation.Horizontal,
                Elevation = 4f
            };
            card.SetBackgroundResource(Resource.Drawable.card_background);
            card.SetPadding(48, 48, 48, 48);

            var textView = new TextView(parent.Context)
            {
                LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent),
                TextSize = 18,
                Typeface = Android.Graphics.Typeface.Create("sans-serif-medium", Android.Graphics.TypefaceStyle.Normal)
            };
            textView.SetTextColor(new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(parent.Context, Resource.Color.textColorPrimary)));

            card.AddView(textView);
            container.AddView(card);

            var holder = new TrackerViewHolder(container, textView, card);
            container.Click += (sender, e) =>
            {
                if (holder.AdapterPosition != RecyclerView.NoPosition)
                {
                    _onItemClick(holder.AdapterPosition);
                }
            };

            return holder;
        }

        public class TrackerViewHolder : RecyclerView.ViewHolder
        {
            public TextView TextView { get; }
            public LinearLayout Card { get; }

            public TrackerViewHolder(View itemView, TextView textView, LinearLayout card) : base(itemView)
            {
                TextView = textView;
                Card = card;
            }
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
            _deleteBackgroundPaint = new Android.Graphics.Paint { Color = new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(Android.App.Application.Context, Resource.Color.colorDelete)), AntiAlias = true };
            _completeBackgroundPaint = new Android.Graphics.Paint { Color = new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(Android.App.Application.Context, Resource.Color.colorComplete)), AntiAlias = true };
            _undoBackgroundPaint = new Android.Graphics.Paint { Color = new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(Android.App.Application.Context, Resource.Color.colorUndo)), AntiAlias = true };
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
                _onDeleted(viewHolder.AdapterPosition);
            }
            else if (direction == ItemTouchHelper.Right)
            {
                _onCompleted(viewHolder.AdapterPosition);
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
                    var textX = left + Math.Abs(currentDx) / 2f - 12;
                    var textY = itemView.Top + (itemView.Height + textBounds.Height()) / 2f;
                    c.DrawText(text, textX, textY, _textPaint);

                    base.OnChildDraw(c, recyclerView, viewHolder, currentDx, dY, actionState, isCurrentlyActive);
                }
                else if (dX > 0) // Swipe Right (Complete/Undo)
                {
                    maxDisplacement = itemView.Width * 0.2f;
                    currentDx = Math.Min(dX, maxDisplacement);
                    
                    if (_isCompleted(viewHolder.AdapterPosition))
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
                    var textX = itemView.Left + currentDx / 2f + 12;
                    var textY = itemView.Top + (itemView.Height + textBounds.Height()) / 2f;
                    c.DrawText(text, textX, textY, _textPaint);

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
