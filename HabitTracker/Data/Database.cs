using SQLite;

namespace HabitTracker.Data;

public class Database
{
    private readonly SQLiteAsyncConnection _database;
    private readonly Task _initializationTask;

    public Database(string dbPath)
    {
        _database = new SQLiteAsyncConnection(dbPath);
        _initializationTask = InitializeDatabase();
    }

    private async Task InitializeDatabase()
    {
        await _database.CreateTableAsync<Habit>();
        await _database.CreateTableAsync<HabitCompletion>();
        await _database.CreateTableAsync<Category>();
        await MigrateLegacyCategoryColumnAsync();
    }

    // Older versions stored the category as a free-text column on Habits;
    // convert those values into Categories rows and CategoryId references once.
    private async Task MigrateLegacyCategoryColumnAsync()
    {
        try
        {
            var legacy = await _database.QueryAsync<LegacyCategoryRow>(
                "SELECT Id, Category FROM Habits WHERE Category IS NOT NULL AND TRIM(Category) != '' AND CategoryId = 0");
            if (legacy.Count == 0) return;
            foreach (var group in legacy.GroupBy(r => r.Category.Trim(), StringComparer.CurrentCultureIgnoreCase))
            {
                var category = await FindOrCreateCategoryAsync(group.Key);
                foreach (var row in group)
                    await _database.ExecuteAsync("UPDATE Habits SET CategoryId = ? WHERE Id = ?", category.Id, row.Id);
            }
            await _database.ExecuteAsync("UPDATE Habits SET Category = ''");
        }
        catch
        {
            // Fresh installs have no legacy Category column to migrate
        }
    }

    private class LegacyCategoryRow
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    private async Task<Category> FindOrCreateCategoryAsync(string name)
    {
        var trimmed = name.Trim();
        var all = await _database.Table<Category>().ToListAsync();
        var existing = all.FirstOrDefault(c =>
            string.Equals(c.Name, trimmed, StringComparison.CurrentCultureIgnoreCase));
        if (existing != null) return existing;
        var category = new Category { Name = trimmed };
        await _database.InsertAsync(category);
        return category;
    }

    private async Task<List<Habit>> PopulateCategoryNamesAsync(List<Habit> habits)
    {
        var nameById = (await _database.Table<Category>().ToListAsync())
            .ToDictionary(c => c.Id, c => c.Name);
        foreach (var habit in habits)
            habit.Category = nameById.GetValueOrDefault(habit.CategoryId, string.Empty);
        return habits;
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        await _initializationTask;
        var categories = await _database.Table<Category>().ToListAsync();
        return categories.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<Category> GetOrCreateCategoryAsync(string name)
    {
        await _initializationTask;
        return await FindOrCreateCategoryAsync(name);
    }

    public async Task<int> SaveCategoryAsync(Category category)
    {
        await _initializationTask;
        return await _database.InsertAsync(category);
    }

    public async Task<List<Habit>> GetHabitsAsync()
    {
        await _initializationTask;
        var habits = await _database.Table<Habit>()
            .Where(h => !h.IsArchived)
            .OrderBy(h => h.SortOrder)
            .ToListAsync();
        return await PopulateCategoryNamesAsync(habits);
    }

    public async Task<List<Habit>> GetArchivedHabitsAsync()
    {
        await _initializationTask;
        var habits = await _database.Table<Habit>()
            .Where(h => h.IsArchived)
            .OrderBy(h => h.Name)
            .ToListAsync();
        return await PopulateCategoryNamesAsync(habits);
    }

    public async Task<List<Habit>> GetAllHabitsAsync()
    {
        await _initializationTask;
        var habits = await _database.Table<Habit>().ToListAsync();
        return await PopulateCategoryNamesAsync(habits);
    }

    public async Task UpdateHabitSortOrdersAsync(List<Habit> habits)
    {
        await _initializationTask;
        for (var i = 0; i < habits.Count; i++)
        {
            habits[i].SortOrder = i;
            await _database.UpdateAsync(habits[i]);
        }
    }

    public async Task ArchiveHabitAsync(Habit habit)
    {
        await _initializationTask;
        habit.IsArchived = true;
        await _database.UpdateAsync(habit);
    }

    public async Task UnarchiveHabitAsync(Habit habit)
    {
        await _initializationTask;
        habit.IsArchived = false;
        await _database.UpdateAsync(habit);
    }

    public async Task<int> SaveHabitAsync(Habit habit)
    {
        await _initializationTask;
        return await _database.InsertAsync(habit);
    }

    public async Task<List<HabitCompletion>> GetHabitCompletionsAsync()
    {
        await _initializationTask;
        return await _database.Table<HabitCompletion>().ToListAsync();
    }

    public async Task<int> SaveHabitCompletionAsync(HabitCompletion completion)
    {
        await _initializationTask;
        return await _database.InsertAsync(completion);
    }

    public async Task<int> UpdateHabitCompletionAsync(HabitCompletion completion)
    {
        await _initializationTask;
        return await _database.UpdateAsync(completion);
    }

    public async Task<int> DeleteHabitCompletionAsync(HabitCompletion completion)
    {
        await _initializationTask;
        return await _database.DeleteAsync(completion);
    }

    public async Task<List<HabitCompletion>> GetHabitCompletionsForDateAsync(DateTime date)
    {
        await _initializationTask;
        var day = date.Date;
        var nextDay = day.AddDays(1);
        return await _database.Table<HabitCompletion>()
            .Where(c => c.DueDate >= day && c.DueDate < nextDay)
            .ToListAsync();
    }

    public async Task<int> UpdateHabitAsync(Habit habit)
    {
        await _initializationTask;
        return await _database.UpdateAsync(habit);
    }

    public async Task<int> DeleteHabitAsync(Habit habit)
    {
        await _initializationTask;
        return await _database.DeleteAsync(habit);
    }

    public async Task DeleteHabitCompletionsForHabitAsync(int habitId)
    {
        await _initializationTask;
        await _database.Table<HabitCompletion>()
            .Where(c => c.HabitId == habitId)
            .DeleteAsync();
    }

    public async Task ClearTablesAsync()
    {
        await _initializationTask;
        await _database.DeleteAllAsync<Habit>();
        await _database.DeleteAllAsync<HabitCompletion>();
        await _database.DeleteAllAsync<Category>();
    }
}
