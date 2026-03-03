using _Microsoft.Android.Resource.Designer;
using Android.Views;
using AndroidX.Fragment.App;
using HabitTracker.Data;
using AndroidX.RecyclerView.Widget;
using Android.Content;
using Google.Android.Material.TextField;
using AlertDialog = Android.App.AlertDialog;
using Android.Graphics.Drawables;

namespace HabitTracker
{
    public class HabitsFragment : AndroidX.Fragment.App.Fragment
    {
        private RecyclerView? _recyclerView;
        private Button? _addButton;
        private List<Habit> _habits = [];
        private HabitAdapter? _adapter;
        private Database? _database;

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            return inflater.Inflate(ResourceConstant.Layout.fragment_habits, container, false);
        }

        public override void OnViewCreated(View view, Bundle? savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            _recyclerView = view.FindViewById<RecyclerView>(ResourceConstant.Id.habits_list);
            _addButton = view.FindViewById<Button>(ResourceConstant.Id.add_habit_button);

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
                _adapter = new HabitAdapter(_habits);
                _recyclerView.SetAdapter(_adapter);

                var callback = new SwipeToDeleteCallback(async void (position) =>
                {
                    if (_database == null || position >= _habits.Count) return;
                    var habit = _habits[position];
                    await _database.DeleteHabitAsync(habit);
                    Activity?.RunOnUiThread(() =>
                    {
                        if (Activity == null || position >= _habits.Count) return;
                        _habits.RemoveAt(position);
                        _adapter?.NotifyItemRemoved(position);
                    });
                });

                var itemTouchHelper = new ItemTouchHelper(callback);
                itemTouchHelper.AttachToRecyclerView(_recyclerView);
            }

            if (_addButton != null)
            {
                _addButton.Click += (_, _) => { ShowAddHabitDialog(); };
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
            Activity?.RunOnUiThread(() =>
            {
                _habits = habits;
                _adapter?.UpdateHabits(_habits);
            });
        }

        private void ShowAddHabitDialog()
        {
            if (Activity == null)
            {
                return;
            }

            var dialogView = LayoutInflater.From(Activity)?.Inflate(ResourceConstant.Layout.dialog_add_habit, null);
            var input = dialogView?.FindViewById<TextInputEditText>(ResourceConstant.Id.habit_name_input);
            var inputLayout = dialogView?.FindViewById<TextInputLayout>(ResourceConstant.Id.habit_name_layout);

            var selectedColorHex = "#5C6BC0"; // Default
            var colorOptions = new[]
            {
                new { Id = ResourceConstant.Id.color_option_1, Hex = "#5C6BC0" },
                new { Id = ResourceConstant.Id.color_option_2, Hex = "#66BB6A" },
                new { Id = ResourceConstant.Id.color_option_3, Hex = "#FFA726" },
                new { Id = ResourceConstant.Id.color_option_4, Hex = "#FF5252" },
                new { Id = ResourceConstant.Id.color_option_5, Hex = "#26C6DA" },
                new { Id = ResourceConstant.Id.color_option_6, Hex = "#AB47BC" }
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

                    v.Click += (_, _) =>
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

            dialog.GetButton((int)DialogButtonType.Positive)?.Click += async (_, _) =>
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

    public class HabitAdapter(List<Habit> habits) : RecyclerView.Adapter
    {
        private List<Habit> _habits = habits;

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
            habitHolder.HabitName.Text = habit.Name;

            // Apply habit color to indicator
            if (!string.IsNullOrEmpty(habit.ColorHex))
            {
                try
                {
                    var color = Android.Graphics.Color.ParseColor(habit.ColorHex);
                    var background = habitHolder.ColorIndicator.Background as GradientDrawable;
                    background?.SetColor(color);
                }
                catch
                {
                    // Fallback to the default color if parsing fails
                }
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)?.Inflate(ResourceConstant.Layout.item_habit, parent, false);
            return new HabitViewHolder(view!);
        }

        private class HabitViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
        {
            public TextView HabitName { get; } = itemView.FindViewById<TextView>(ResourceConstant.Id.habit_name)!;

            public View ColorIndicator { get; } =
                itemView.FindViewById<View>(ResourceConstant.Id.habit_color_indicator)!;
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
            _backgroundPaint = new Android.Graphics.Paint
            {
                Color = new Android.Graphics.Color(
                    AndroidX.Core.Content.ContextCompat.GetColor(Android.App.Application.Context,
                        ResourceConstant.Color.colorDelete)),
                AntiAlias = true
            };
            _textPaint = new Android.Graphics.Paint
            {
                Color = Android.Graphics.Color.White,
                TextSize = 32,
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
            _onSwiped(viewHolder.BindingAdapterPosition);
        }

        public override void OnChildDraw(Android.Graphics.Canvas c, RecyclerView recyclerView,
            RecyclerView.ViewHolder viewHolder, float dX, float dY, int actionState, bool isCurrentlyActive)
        {
            if (actionState == ItemTouchHelper.ActionStateSwipe && dX < 0)
            {
                var itemView = viewHolder.ItemView;
                var maxDisplacement = -itemView.Width * 0.2f;
                var currentDx = Math.Max(dX, maxDisplacement);

                var backgroundWidth = Math.Abs(currentDx);
                var left = itemView.Right + currentDx;

                // Draw a rounded rectangle background
                var cornerRadius = 24f;
                var background = new Android.Graphics.RectF(left - cornerRadius, itemView.Top + 12, itemView.Right - 24,
                    itemView.Bottom - 12);
                c.DrawRoundRect(background, cornerRadius, cornerRadius, _backgroundPaint);

                var text = "Delete";
                var textBounds = new Android.Graphics.Rect();
                _textPaint.GetTextBounds(text, 0, text.Length, textBounds);

                // Clip the text to the background bounds
                c.Save();
                c.ClipRect(background);

                var textX = left + backgroundWidth / 2f - 12;
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
    }
}