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
    }

    public async Task<List<Habit>> GetHabitsAsync()
    {
        await _initializationTask;
        return await _database.Table<Habit>().ToListAsync();
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

    public async Task<int> DeleteHabitAsync(Habit habit)
    {
        await _initializationTask;
        return await _database.DeleteAsync(habit);
    }

    public async Task ClearTablesAsync()
    {
        await _initializationTask;
        await _database.DeleteAllAsync<Habit>();
        await _database.DeleteAllAsync<HabitCompletion>();
    }
}
