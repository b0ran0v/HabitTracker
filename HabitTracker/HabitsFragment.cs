using Android.Views;
using HabitTracker.Data;
using AndroidX.RecyclerView.Widget;
using Android.Content;
using Google.Android.Material.TextField;
using AlertDialog = Android.App.AlertDialog;

namespace HabitTracker
{
    public class HabitsFragment : AndroidX.Fragment.App.Fragment
    {
        private RecyclerView? _recyclerView;
        private Button? _addButton;
        private List<Habit> _habits = new();
        private HabitAdapter? _adapter;
        private Database? _database;

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            return inflater.Inflate(Resource.Layout.fragment_habits, container, false);
        }

        public override void OnViewCreated(View view, Bundle? savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            _recyclerView = view.FindViewById<RecyclerView>(Resource.Id.habits_list);
            _addButton = view.FindViewById<Button>(Resource.Id.add_habit_button);

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
                _adapter = new HabitAdapter(_habits);
                _recyclerView.SetAdapter(_adapter);

                var callback = new SwipeToDeleteCallback(async (position) =>
                {
                    if (_database != null && position < _habits.Count)
                    {
                        var habit = _habits[position];
                        await _database.DeleteHabitAsync(habit);
                        Activity?.RunOnUiThread(() =>
                        {
                            if (Activity != null && position < _habits.Count)
                            {
                                _habits.RemoveAt(position);
                                _adapter?.NotifyItemRemoved(position);
                            }
                        });
                    }
                });

                var itemTouchHelper = new ItemTouchHelper(callback);
                itemTouchHelper.AttachToRecyclerView(_recyclerView);
            }

            if (_addButton != null)
            {
                _addButton.Click += (sender, e) =>
                {
                    ShowAddHabitDialog();
                };
            }

            LoadHabits();
        }

        private async void LoadHabits()
        {
            if (Activity == null || _database == null || _adapter == null)
            {
                return;
            }
            var habits = await _database.GetHabitsAsync();
            if (Activity != null)
            {
                Activity.RunOnUiThread(() =>
                {
                    _habits = habits;
                    _adapter?.UpdateHabits(_habits);
                });
            }
        }

        private void ShowAddHabitDialog()
        {
            if (Activity == null)
            {
                return;
            }
            
            var dialogView = LayoutInflater.From(Activity).Inflate(Resource.Layout.dialog_add_habit, null);
            var input = dialogView?.FindViewById<TextInputEditText>(Resource.Id.habit_name_input);
            var inputLayout = dialogView?.FindViewById<TextInputLayout>(Resource.Id.habit_name_layout);
            
            var selectedColorHex = "#5C6BC0"; // Default
            var colorOptions = new[]
            {
                new { Id = Resource.Id.color_option_1, Hex = "#5C6BC0" },
                new { Id = Resource.Id.color_option_2, Hex = "#66BB6A" },
                new { Id = Resource.Id.color_option_3, Hex = "#FFA726" },
                new { Id = Resource.Id.color_option_4, Hex = "#FF5252" },
                new { Id = Resource.Id.color_option_5, Hex = "#26C6DA" },
                new { Id = Resource.Id.color_option_6, Hex = "#AB47BC" }
            };

            var views = new List<View>();
            foreach (var option in colorOptions)
            {
                var v = dialogView?.FindViewById<View>(option.Id);
                if (v != null)
                {
                    views.Add(v);
                    // Initial highlight for default
                    if (option.Hex == selectedColorHex)
                    {
                        v.Alpha = 1.0f;
                        v.ScaleX = 1.2f;
                        v.ScaleY = 1.2f;
                    }
                    else
                    {
                        v.Alpha = 0.6f;
                    }

                    v.Click += (s, e) =>
                    {
                        selectedColorHex = option.Hex;
                        foreach (var otherV in views)
                        {
                            otherV.Alpha = 0.6f;
                            otherV.ScaleX = 1.0f;
                            otherV.ScaleY = 1.0f;
                        }
                        v.Alpha = 1.0f;
                        v.ScaleX = 1.2f;
                        v.ScaleY = 1.2f;
                    };
                }
            }

            var builder = new AlertDialog.Builder(Activity);
            builder.SetView(dialogView);

            builder.SetPositiveButton("Add", (IDialogInterfaceOnClickListener?)null); 
            builder.SetNegativeButton("Cancel", (_, _) => { });

            var dialog = builder.Create();
            if (dialog == null) return;
            dialog.Show();

            dialog.GetButton((int)DialogButtonType.Positive)?.Click += async (sender, e) =>
            {
                var habitName = input?.Text;
                if (string.IsNullOrWhiteSpace(habitName))
                {
                    if (inputLayout != null)
                    {
                        inputLayout.Error = "Please enter a habit name";
                    }
                    return;
                }

                if (_database != null)
                {
                    var habit = new Habit { Name = habitName, ColorHex = selectedColorHex };
                    await _database.SaveHabitAsync(habit);
                    LoadHabits();
                }
                dialog.Dismiss();
            };
        }
    }

    public class HabitAdapter : RecyclerView.Adapter
    {
        private List<Habit> _habits;

        public HabitAdapter(List<Habit> habits)
        {
            _habits = habits;
        }

        public void UpdateHabits(List<Habit> habits)
        {
            _habits = habits;
            NotifyDataSetChanged();
        }

        public override int ItemCount => _habits.Count;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var habitHolder = (HabitViewHolder)holder;
            var habit = _habits[position];
            habitHolder.TextView.Text = habit.Name;
            
            // Apply habit color to card background if color is set
            if (!string.IsNullOrEmpty(habit.ColorHex))
            {
                try
                {
                    var color = Android.Graphics.Color.ParseColor(habit.ColorHex);
                    habitHolder.Card.SetBackgroundColor(color);
                }
                catch
                {
                    habitHolder.Card.SetBackgroundResource(Resource.Drawable.card_background);
                }
            }
            else
            {
                habitHolder.Card.SetBackgroundResource(Resource.Drawable.card_background);
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

            return new HabitViewHolder(container, textView, card);
        }

        public class HabitViewHolder : RecyclerView.ViewHolder
        {
            public TextView TextView { get; }
            public LinearLayout Card { get; }

            public HabitViewHolder(View itemView, TextView textView, LinearLayout card) : base(itemView)
            {
                TextView = textView;
                Card = card;
            }
        }
    }

    public class SwipeToDeleteCallback : ItemTouchHelper.SimpleCallback
    {
        private readonly Action<int> _onSwiped;
        private readonly Android.Graphics.Paint _backgroundPaint;
        private readonly Android.Graphics.Paint _textPaint;

        public SwipeToDeleteCallback(Action<int> onSwiped) : base(0, ItemTouchHelper.Left)
        {
            _onSwiped = onSwiped;
            _backgroundPaint = new Android.Graphics.Paint { Color = new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(Android.App.Application.Context, Resource.Color.colorDelete)), AntiAlias = true };
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
            _onSwiped(viewHolder.AdapterPosition);
        }

        public override void OnChildDraw(Android.Graphics.Canvas c, RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder, float dX, float dY, int actionState, bool isCurrentlyActive)
        {
            if (actionState == ItemTouchHelper.ActionStateSwipe && dX < 0)
            {
                var itemView = viewHolder.ItemView;
                var maxDisplacement = -itemView.Width * 0.2f;
                var currentDx = Math.Max(dX, maxDisplacement);

                var backgroundWidth = Math.Abs(currentDx);
                var left = itemView.Right + currentDx;
                
                // Draw rounded rectangle background
                var cornerRadius = 24f;
                var background = new Android.Graphics.RectF(left - cornerRadius, itemView.Top + 12, itemView.Right - 24, itemView.Bottom - 12);
                c.DrawRoundRect(background, cornerRadius, cornerRadius, _backgroundPaint);

                var text = "Delete";
                var textBounds = new Android.Graphics.Rect();
                _textPaint.GetTextBounds(text, 0, text.Length, textBounds);
                
                var textX = left + backgroundWidth / 2f - 12;
                var textY = itemView.Top + (itemView.Height + textBounds.Height()) / 2f;

                c.DrawText(text, textX, textY, _textPaint);

                base.OnChildDraw(c, recyclerView, viewHolder, currentDx, dY, actionState, isCurrentlyActive);
            }
            else
            {
                base.OnChildDraw(c, recyclerView, viewHolder, dX, dY, actionState, isCurrentlyActive);
            }
        }
    }
}
