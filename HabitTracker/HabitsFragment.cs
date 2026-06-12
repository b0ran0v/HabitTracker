using _Microsoft.Android.Resource.Designer;
using Android.Views;
using HabitTracker.Data;
using AndroidX.RecyclerView.Widget;
using Android.Content;
using Android.Util;
using Google.Android.Material.TextField;
using AlertDialog = Android.App.AlertDialog;
using Android.Graphics.Drawables;

namespace HabitTracker
{
    public class HabitsFragment(Database database) : AndroidX.Fragment.App.Fragment
    {
        // Required by the FragmentManager when restoring state (e.g. after Activity.Recreate)
        public HabitsFragment() : this(MainActivity.SharedDatabase!) { }

        private RecyclerView? _recyclerView;
        private View? _emptyState;
        private Button? _addButton;
        private Button? _manageCategoriesButton;
        private Button? _viewArchivedButton;
        private List<Habit> _habits = [];
        private HabitAdapter? _adapter;
        private ItemTouchHelper? _itemTouchHelper;
        private readonly Database _database = database;

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            return inflater.Inflate(ResourceConstant.Layout.fragment_habits, container, false);
        }

        public override void OnViewCreated(View view, Bundle? savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            _recyclerView = view.FindViewById<RecyclerView>(ResourceConstant.Id.habits_list);
            _emptyState = view.FindViewById<View>(ResourceConstant.Id.empty_state_habits);
            _addButton = view.FindViewById<Button>(ResourceConstant.Id.add_habit_button);
            _manageCategoriesButton = view.FindViewById<Button>(ResourceConstant.Id.manage_categories_button);
            _viewArchivedButton = view.FindViewById<Button>(ResourceConstant.Id.view_archived_button);

            if (_recyclerView != null)
            {
                _recyclerView.SetLayoutManager(new LinearLayoutManager(Activity));
                _adapter = new HabitAdapter(_habits, holder =>
                    _itemTouchHelper?.StartDrag(holder));
                _recyclerView.SetAdapter(_adapter);

                var callback = new HabitSwipeCallback(
                    onArchive: position => UiSafe.Run(Context, async () =>
                    {
                        var habit = _adapter?.GetHabitAt(position);
                        if (habit == null) return;
                        await _database.ArchiveHabitAsync(habit);
                        Activity?.RunOnUiThread(() =>
                        {
                            if (Activity == null) return;
                            _habits.Remove(habit);
                            _adapter?.UpdateHabits(_habits);
                            _emptyState?.Visibility = _habits.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
                        });
                    }),
                    onEdit: (position) =>
                    {
                        var habit = _adapter?.GetHabitAt(position);
                        if (habit != null) ShowEditHabitDialog(habit);
                    },
                    onMove: (fromPos, toPos) => _adapter?.MoveItem(fromPos, toPos) ?? false,
                    onDragEnd: () => UiSafe.Run(Context, async () =>
                    {
                        if (_adapter == null) return;
                        await _database.UpdateHabitSortOrdersAsync(_adapter.GetHabitsInDisplayOrder());
                    }),
                    context: Context);

                _itemTouchHelper = new ItemTouchHelper(callback);
                _itemTouchHelper.AttachToRecyclerView(_recyclerView);
            }

            if (_addButton != null)
                _addButton.Click += (_, _) => ShowAddHabitDialog();

            if (_manageCategoriesButton != null)
                _manageCategoriesButton.Click += (_, _) => ShowCategoriesDialog();

            if (_viewArchivedButton != null)
                _viewArchivedButton.Click += (_, _) => ShowArchivedHabitsDialog();

            LoadHabits();
        }

        // Tabs are hidden/shown rather than recreated, so refresh whenever this tab is
        // revealed — e.g. a data import on the Settings tab replaces every habit
        public override void OnHiddenChanged(bool hidden)
        {
            base.OnHiddenChanged(hidden);
            if (!hidden) LoadHabits();
        }

        private void LoadHabits() => UiSafe.Run(Context, LoadHabitsAsync);

        private async Task LoadHabitsAsync()
        {
            if (Activity == null || _adapter == null) return;
            var habits = await _database.GetHabitsAsync();
            Activity?.RunOnUiThread(() =>
            {
                _habits = habits;
                _adapter?.UpdateHabits(_habits);
                _emptyState?.Visibility = _habits.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
            });
        }

        private void ShowArchivedHabitsDialog() => UiSafe.Run(Context, ShowArchivedHabitsDialogAsync);

        private async Task ShowArchivedHabitsDialogAsync()
        {
            if (Activity == null) return;
            var archived = await _database.GetArchivedHabitsAsync();
            if (archived.Count == 0)
            {
                Toast.MakeText(Activity, GetString(ResourceConstant.String.no_archived_habits), ToastLength.Short)?.Show();
                return;
            }

            var names = archived.Select(h => h.Name).ToArray();
            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(GetString(ResourceConstant.String.archived_habits));
            builder.SetItems(names, (_, e) =>
            {
                var selected = archived[e.Which];
                var actionBuilder = new AlertDialog.Builder(Activity);
                actionBuilder.SetTitle(selected.Name);
                actionBuilder.SetPositiveButton(GetString(ResourceConstant.String.restore), (_, _) => UiSafe.Run(Context, async () =>
                {
                    await _database.UnarchiveHabitAsync(selected);
                    LoadHabits();
                }));
                actionBuilder.SetNegativeButton(GetString(ResourceConstant.String.delete_permanently), (_, _) => UiSafe.Run(Context, async () =>
                {
                    await _database.DeleteHabitAsync(selected);
                    await _database.DeleteHabitCompletionsForHabitAsync(selected.Id);
                }));
                actionBuilder.SetNeutralButton(GetString(ResourceConstant.String.cancel), (_, _) => { });
                actionBuilder.Show();
            });
            builder.SetNeutralButton(GetString(ResourceConstant.String.cancel), (_, _) => { });
            builder.Show();
        }

        private void ShowAddHabitDialog() => UiSafe.Run(Context, ShowAddHabitDialogAsync);

        private async Task ShowAddHabitDialogAsync()
        {
            if (Activity == null)
            {
                return;
            }

            var dialogView = LayoutInflater.From(Activity)?.Inflate(ResourceConstant.Layout.dialog_add_habit, null);
            var input = dialogView?.FindViewById<TextInputEditText>(ResourceConstant.Id.habit_name_input);
            var inputLayout = dialogView?.FindViewById<TextInputLayout>(ResourceConstant.Id.habit_name_layout);
            var categoryInput = dialogView?.FindViewById<AutoCompleteTextView>(ResourceConstant.Id.habit_category_input);
            var getSelectedCategoryId = await SetupCategoryDropdownAsync(categoryInput, 0);

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
                if (v == null) continue;
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

            var builder = new AlertDialog.Builder(Activity);
            builder.SetView(dialogView);
            builder.SetPositiveButton(GetString(ResourceConstant.String.add), (IDialogInterfaceOnClickListener?)null);
            builder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) => { });

            var dialog = builder.Create();
            if (dialog == null) return;
            dialog.Show();

            dialog.GetButton((int)DialogButtonType.Positive)?.Click += (_, _) => UiSafe.Run(Context, async () =>
            {
                var habitName = input?.Text;
                if (string.IsNullOrWhiteSpace(habitName))
                {
                    if (inputLayout != null)
                    {
                        inputLayout.Error = GetString(ResourceConstant.String.enter_habit_name_error);
                    }
                    return;
                }

                var habit = new Habit
                {
                    Name = habitName,
                    ColorHex = selectedColorHex,
                    CategoryId = getSelectedCategoryId()
                };
                await _database.SaveHabitAsync(habit);
                LoadHabits();
                dialog.Dismiss();
            });
        }

        private void ShowEditHabitDialog(Habit habit) =>
            UiSafe.Run(Context, () => ShowEditHabitDialogAsync(habit));

        private async Task ShowEditHabitDialogAsync(Habit habit)
        {
            if (Activity == null)
            {
                return;
            }

            var dialogView = LayoutInflater.From(Activity)?.Inflate(ResourceConstant.Layout.dialog_add_habit, null);
            var titleView = dialogView?.FindViewById<TextView>(ResourceConstant.Id.dialog_title);
            var input = dialogView?.FindViewById<TextInputEditText>(ResourceConstant.Id.habit_name_input);
            var inputLayout = dialogView?.FindViewById<TextInputLayout>(ResourceConstant.Id.habit_name_layout);
            var categoryInput = dialogView?.FindViewById<AutoCompleteTextView>(ResourceConstant.Id.habit_category_input);
            var getSelectedCategoryId = await SetupCategoryDropdownAsync(categoryInput, habit.CategoryId);

            // Pre-fill with existing habit data
            titleView?.Text = GetString(ResourceConstant.String.edit_habit);
            if (input != null)
            {
                input.Text = habit.Name;
                input.SetSelection(habit.Name.Length);
            }

            var selectedColorHex = habit.ColorHex;
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
                if (v == null) continue;
                views.Add(v);
                // Highlight the habit's current color
                if (option.Hex == selectedColorHex)
                {
                    v.Alpha = 1.0f;
                    v.ScaleX = 1.2f;
                    v.ScaleY = 1.2f;
                }
                else
                {
                    v.Alpha = 0.6f;
                    v.ScaleX = 1.0f;
                    v.ScaleY = 1.0f;
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

            var builder = new AlertDialog.Builder(Activity);
            builder.SetView(dialogView);
            builder.SetPositiveButton(GetString(ResourceConstant.String.save), (IDialogInterfaceOnClickListener?)null);
            builder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) => { });

            var dialog = builder.Create();
            if (dialog == null) return;
            dialog.Show();

            dialog.GetButton((int)DialogButtonType.Positive)?.Click += (_, _) => UiSafe.Run(Context, async () =>
            {
                var habitName = input?.Text;
                if (string.IsNullOrWhiteSpace(habitName))
                {
                    inputLayout?.Error = GetString(ResourceConstant.String.enter_habit_name_error);
                    return;
                }

                await _database.UpdateHabitAsync(new Habit
                {
                    Id = habit.Id,
                    Name = habitName,
                    ColorHex = selectedColorHex,
                    SortOrder = habit.SortOrder,
                    IsArchived = habit.IsArchived,
                    CategoryId = getSelectedCategoryId()
                });
                LoadHabits();
                dialog.Dismiss();
            });
        }

        private void ShowCategoriesDialog() => UiSafe.Run(Context, ShowCategoriesDialogAsync);

        private async Task ShowCategoriesDialogAsync()
        {
            if (Activity == null) return;
            var categories = await _database.GetCategoriesAsync();

            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(GetString(ResourceConstant.String.categories));
            if (categories.Count == 0)
                builder.SetMessage(GetString(ResourceConstant.String.no_categories));
            else
                builder.SetItems(categories.Select(c => c.Name).ToArray(),
                    (_, e) => ShowCategoryDetailDialog(categories[e.Which]));
            builder.SetPositiveButton(GetString(ResourceConstant.String.new_category), (_, _) =>
                ShowNewCategoryDialog(name => UiSafe.Run(Context, async () =>
                {
                    await _database.GetOrCreateCategoryAsync(name);
                    ShowCategoriesDialog();
                })));
            builder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) => { });
            builder.Show();
        }

        private void ShowCategoryDetailDialog(Category category) =>
            UiSafe.Run(Context, () => ShowCategoryDetailDialogAsync(category));

        private async Task ShowCategoryDetailDialogAsync(Category category)
        {
            if (Activity == null) return;
            var habitsInCategory = (await _database.GetAllHabitsAsync())
                .Where(h => h.CategoryId == category.Id)
                .OrderBy(h => h.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(category.Name);
            if (habitsInCategory.Count == 0)
                builder.SetMessage(GetString(ResourceConstant.String.no_habits_in_category));
            else
                builder.SetItems(habitsInCategory.Select(h => h.Name).ToArray(), (_, _) => { });
            builder.SetPositiveButton(GetString(ResourceConstant.String.delete), (_, _) =>
                ConfirmDeleteCategory(category, habitsInCategory.Count));
            builder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) => { });
            builder.Show();
        }

        private void ConfirmDeleteCategory(Category category, int habitCount)
        {
            if (Activity == null) return;
            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(GetString(ResourceConstant.String.delete_category_title));
            builder.SetMessage(string.Format(
                GetString(ResourceConstant.String.delete_category_message), category.Name, habitCount));
            builder.SetPositiveButton(GetString(ResourceConstant.String.delete), (_, _) => UiSafe.Run(Context, async () =>
            {
                await _database.DeleteCategoryAsync(category);
                if (Context != null) HabitWidgetProvider.RequestUpdate(Context);
                LoadHabits();
            }));
            builder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) => { });
            builder.Show();
        }

        // Binds the category dropdown: "No category", saved categories, and a final
        // "+ New category…" entry that opens a naming dialog. Returns a getter for
        // the currently selected category id (0 = none).
        private async Task<Func<int>> SetupCategoryDropdownAsync(AutoCompleteTextView? categoryInput, int currentCategoryId)
        {
            if (categoryInput == null || Activity == null) return () => currentCategoryId;

            var selectedId = currentCategoryId;
            var categories = await _database.GetCategoriesAsync();
            var noCategoryText = GetString(ResourceConstant.String.no_category);

            void BindAdapter()
            {
                var items = new List<string> { noCategoryText };
                items.AddRange(categories.Select(c => c.Name));
                items.Add(GetString(ResourceConstant.String.new_category));
                categoryInput.Adapter = new ArrayAdapter(Activity,
                    Android.Resource.Layout.SimpleDropDownItem1Line, items);
            }
            BindAdapter();

            var current = categories.FirstOrDefault(c => c.Id == selectedId);
            categoryInput.SetText(current?.Name ?? noCategoryText, false);

            categoryInput.ItemClick += (_, e) =>
            {
                if (e.Position == 0)
                {
                    selectedId = 0;
                    return;
                }
                if (e.Position == categories.Count + 1)
                {
                    // Revert the field until the new category is actually created
                    var revertName = categories.FirstOrDefault(c => c.Id == selectedId)?.Name ?? noCategoryText;
                    categoryInput.SetText(revertName, false);
                    ShowNewCategoryDialog(name => UiSafe.Run(Context, async () =>
                    {
                        var category = await _database.GetOrCreateCategoryAsync(name);
                        if (categories.All(c => c.Id != category.Id))
                        {
                            categories.Add(category);
                            categories = categories
                                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                            BindAdapter();
                        }
                        selectedId = category.Id;
                        categoryInput.SetText(category.Name, false);
                    }));
                    return;
                }
                selectedId = categories[e.Position - 1].Id;
            };

            return () => selectedId;
        }

        private void ShowNewCategoryDialog(Action<string> onCreated)
        {
            if (Activity == null) return;
            var input = new EditText(Activity)
            {
                Hint = GetString(ResourceConstant.String.category_name_hint),
                InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagCapSentences
            };
            input.SetSingleLine();
            input.SetFilters([new Android.Text.InputFilterLengthFilter(30)]);
            input.SetPadding(48, 24, 48, 24);

            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle(GetString(ResourceConstant.String.new_category_title));
            builder.SetView(input);
            builder.SetPositiveButton(GetString(ResourceConstant.String.add), (_, _) =>
            {
                var name = input.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(name)) onCreated(name);
            });
            builder.SetNegativeButton(GetString(ResourceConstant.String.cancel), (_, _) => { });
            builder.Show();
        }
    }

    public class HabitAdapter : RecyclerView.Adapter
    {
        public const int ViewTypeHeader = 0;
        private const int ViewTypeHabit = 1;

        // A row is either a category header (Header != null) or a habit entry
        private class Row
        {
            public string? Header;
            public Habit? Habit;
        }

        private List<Row> _rows;
        private readonly Action<RecyclerView.ViewHolder> _onStartDrag;

        public HabitAdapter(List<Habit> habits, Action<RecyclerView.ViewHolder> onStartDrag)
        {
            _rows = BuildRows(habits);
            _onStartDrag = onStartDrag;
        }

        // Uncategorized habits come first without a header, then each category
        // alphabetically under its own header. Within a group the input order is kept.
        private static List<Row> BuildRows(List<Habit> habits)
        {
            var rows = new List<Row>();
            foreach (var group in habits
                         .GroupBy(h => h.Category.Trim())
                         .OrderBy(g => g.Key.Length == 0 ? 0 : 1)
                         .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                if (group.Key.Length > 0)
                    rows.Add(new Row { Header = group.Key });
                rows.AddRange(group.Select(h => new Row { Habit = h }));
            }
            return rows;
        }

        public void UpdateHabits(List<Habit> habits)
        {
            var newRows = BuildRows(habits);
            var diff = DiffUtil.CalculateDiff(new RowDiffCallback(_rows, newRows));
            _rows = newRows;
            diff.DispatchUpdatesTo(this);
        }

        public Habit? GetHabitAt(int position) =>
            position >= 0 && position < _rows.Count ? _rows[position].Habit : null;

        public List<Habit> GetHabitsInDisplayOrder() =>
            _rows.Where(r => r.Habit != null).Select(r => r.Habit!).ToList();

        // Drag is only allowed between habit rows of the same category
        public bool MoveItem(int fromPos, int toPos)
        {
            if (fromPos < 0 || toPos < 0 || fromPos >= _rows.Count || toPos >= _rows.Count) return false;
            var from = _rows[fromPos].Habit;
            var to = _rows[toPos].Habit;
            if (from == null || to == null) return false;
            if (from.Category.Trim() != to.Category.Trim()) return false;
            var row = _rows[fromPos];
            _rows.RemoveAt(fromPos);
            _rows.Insert(toPos, row);
            NotifyItemMoved(fromPos, toPos);
            return true;
        }

        private class RowDiffCallback(List<Row> oldRows, List<Row> newRows) : DiffUtil.Callback
        {
            public override int OldListSize => oldRows.Count;
            public override int NewListSize => newRows.Count;
            public override bool AreItemsTheSame(int oldPos, int newPos)
            {
                var o = oldRows[oldPos];
                var n = newRows[newPos];
                if (o.Header != null || n.Header != null) return o.Header == n.Header;
                return o.Habit!.Id == n.Habit!.Id;
            }
            public override bool AreContentsTheSame(int oldPos, int newPos)
            {
                var o = oldRows[oldPos];
                var n = newRows[newPos];
                if (o.Header != null) return true; // header text equality is checked in AreItemsTheSame
                return o.Habit!.Name == n.Habit!.Name &&
                       o.Habit.ColorHex == n.Habit.ColorHex;
            }
        }

        public override int ItemCount => _rows.Count;

        public override int GetItemViewType(int position) =>
            _rows[position].Header != null ? ViewTypeHeader : ViewTypeHabit;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var row = _rows[position];
            if (holder is HeaderViewHolder headerHolder)
            {
                headerHolder.HeaderText.Text = row.Header;
                return;
            }

            var habitHolder = (HabitViewHolder)holder;
            var habit = row.Habit!;
            habitHolder.HabitName.Text = habit.Name;

            if (!string.IsNullOrEmpty(habit.ColorHex))
            {
                try
                {
                    var color = Android.Graphics.Color.ParseColor(habit.ColorHex);
                    var background = habitHolder.ColorIndicator.Background as GradientDrawable;
                    background?.SetColor(color);
                }
                catch { }
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

            var view = LayoutInflater.From(parent.Context)?.Inflate(ResourceConstant.Layout.item_habit, parent, false);
            var holder = new HabitViewHolder(view!);
            holder.DragHandle.SetOnTouchListener(new DragHandleTouchListener(holder, _onStartDrag));
            return holder;
        }

        private class HeaderViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
        {
            public TextView HeaderText { get; } =
                itemView.FindViewById<TextView>(ResourceConstant.Id.category_header_text)!;
        }

        private class DragHandleTouchListener(RecyclerView.ViewHolder holder,
            Action<RecyclerView.ViewHolder> onStartDrag) : Java.Lang.Object, View.IOnTouchListener
        {
            public bool OnTouch(View? v, MotionEvent? e)
            {
                if (e?.Action == MotionEventActions.Down)
                {
                    onStartDrag(holder);
                    return true;
                }
                return false;
            }
        }

        private class HabitViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
        {
            public TextView HabitName { get; } = itemView.FindViewById<TextView>(ResourceConstant.Id.habit_name)!;
            public View ColorIndicator { get; } = itemView.FindViewById<View>(ResourceConstant.Id.habit_color_indicator)!;
            public View DragHandle { get; } = itemView.FindViewById<View>(ResourceConstant.Id.habit_drag_handle)!;
        }
    }

    public class HabitSwipeCallback : ItemTouchHelper.SimpleCallback
    {
        private readonly Action<int> _onArchive;
        private readonly Action<int> _onEdit;
        private readonly Func<int, int, bool> _onMove;
        private readonly Action _onDragEnd;
        private bool _isDragging;
        private readonly Android.Graphics.Paint _archivePaint;
        private readonly Android.Graphics.Paint _editPaint;
        private readonly Android.Graphics.Paint _textPaint;
        private readonly Context? _context;

        public HabitSwipeCallback(Action<int> onArchive, Action<int> onEdit, Func<int, int, bool> onMove,
            Action onDragEnd, Context? context)
            : base(ItemTouchHelper.Up | ItemTouchHelper.Down, ItemTouchHelper.Left | ItemTouchHelper.Right)
        {
            _onArchive = onArchive;
            _onEdit = onEdit;
            _onMove = onMove;
            _onDragEnd = onDragEnd;
            _context = context;

            _archivePaint = new Android.Graphics.Paint
            {
                Color = new Android.Graphics.Color(
                    AndroidX.Core.Content.ContextCompat.GetColor(Application.Context, ResourceConstant.Color.colorDelete)),
                AntiAlias = true
            };
            _editPaint = new Android.Graphics.Paint
            {
                Color = new Android.Graphics.Color(
                    AndroidX.Core.Content.ContextCompat.GetColor(Application.Context, ResourceConstant.Color.colorEdit)),
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
            _textPaint.SetTypeface(Android.Graphics.Typeface.Create("sans-serif-medium", Android.Graphics.TypefaceStyle.Normal));
        }

        public override bool OnMove(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder,
            RecyclerView.ViewHolder target)
        {
            var moved = _onMove(viewHolder.BindingAdapterPosition, target.BindingAdapterPosition);
            if (moved) _isDragging = true;
            return moved;
        }

        // Category headers are neither draggable nor swipeable
        public override int GetDragDirs(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder) =>
            viewHolder.ItemViewType == HabitAdapter.ViewTypeHeader ? 0 : base.GetDragDirs(recyclerView, viewHolder);

        public override int GetSwipeDirs(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder) =>
            viewHolder.ItemViewType == HabitAdapter.ViewTypeHeader ? 0 : base.GetSwipeDirs(recyclerView, viewHolder);

        public override void ClearView(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder)
        {
            base.ClearView(recyclerView, viewHolder);
            if (_isDragging)
            {
                _isDragging = false;
                _onDragEnd();
            }
        }

        public override float GetSwipeThreshold(RecyclerView.ViewHolder viewHolder) => 0.2f;

        public override void OnSwiped(RecyclerView.ViewHolder viewHolder, int direction)
        {
            var position = viewHolder.BindingAdapterPosition;
            if (direction == ItemTouchHelper.Left)
            {
                _onArchive(position);
            }
            else if (direction == ItemTouchHelper.Right)
            {
                // Edit is a non-removing swipe, so the row must be rebound to clear
                // ItemTouchHelper's swipe offset — otherwise it stays held open
                viewHolder.BindingAdapter?.NotifyItemChanged(position);
                _onEdit(position);
            }
        }

        public override void OnChildDraw(Android.Graphics.Canvas c, RecyclerView recyclerView,
            RecyclerView.ViewHolder viewHolder, float dX, float dY, int actionState, bool isCurrentlyActive)
        {
            if (actionState != ItemTouchHelper.ActionStateSwipe)
            {
                base.OnChildDraw(c, recyclerView, viewHolder, dX, dY, actionState, isCurrentlyActive);
                return;
            }

            var itemView = viewHolder.ItemView;
            const float cornerRadius = 24f;
            const float verticalMargin = 12f;
            const float horizontalMargin = 24f;

            if (dX < 0)
            {
                var maxDisplacement = -itemView.Width * 0.2f;
                var currentDx = Math.Max(dX, maxDisplacement);
                var backgroundWidth = Math.Abs(currentDx);
                var left = itemView.Right + currentDx;
                var bg = new Android.Graphics.RectF(left - cornerRadius, itemView.Top + verticalMargin,
                    itemView.Right - horizontalMargin, itemView.Bottom - verticalMargin);
                c.DrawRoundRect(bg, cornerRadius, cornerRadius, _archivePaint);
                DrawLabel(c, _context?.GetString(ResourceConstant.String.archive) ?? "Archive",
                    left + backgroundWidth / 2f - horizontalMargin / 2f, itemView, bg);
                base.OnChildDraw(c, recyclerView, viewHolder, currentDx, dY, actionState, isCurrentlyActive);
            }
            else if (dX > 0)
            {
                var maxDisplacement = itemView.Width * 0.2f;
                var currentDx = Math.Min(dX, maxDisplacement);
                var right = itemView.Left + currentDx;
                var bg = new Android.Graphics.RectF(itemView.Left + horizontalMargin, itemView.Top + verticalMargin,
                    right + cornerRadius, itemView.Bottom - verticalMargin);
                c.DrawRoundRect(bg, cornerRadius, cornerRadius, _editPaint);
                DrawLabel(c, _context?.GetString(ResourceConstant.String.edit) ?? "Edit",
                    itemView.Left + horizontalMargin + currentDx / 2f, itemView, bg);
                base.OnChildDraw(c, recyclerView, viewHolder, currentDx, dY, actionState, isCurrentlyActive);
            }
        }

        private void DrawLabel(Android.Graphics.Canvas c, string text, float centerX,
            View itemView, Android.Graphics.RectF clipRect)
        {
            var textBounds = new Android.Graphics.Rect();
            _textPaint.GetTextBounds(text, 0, text.Length, textBounds);
            var textY = itemView.Top + (itemView.Height + textBounds.Height()) / 2f;
            c.Save();
            c.ClipRect(clipRect);
            c.DrawText(text, centerX, textY, _textPaint);
            c.Restore();
        }
    }
}
