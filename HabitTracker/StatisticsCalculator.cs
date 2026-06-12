using HabitTracker.Data;

namespace HabitTracker;

public record HabitStreak(Habit Habit, int CurrentStreak);
public record DayColumn(DateTime Date, List<string> CompletedColors);
public record HabitRate(Habit Habit, double Rate, int TrackedDays);

// All statistics are derived in memory from the full habit/completion lists.
// No Android dependencies, so the logic stays testable without an emulator.
public class StatisticsCalculator
{
    private readonly List<Habit> _habits;
    private readonly List<HabitCompletion> _completions;
    private readonly DateTime _today;

    public StatisticsCalculator(List<Habit> habits, List<HabitCompletion> completions, DateTime? today = null)
    {
        _habits = habits;
        _completions = completions;
        _today = (today ?? DateTime.Today).Date;
    }

    public int TotalHabits => _habits.Count;

    public int TrackedToday => _completions.Count(c => c.DueDate.Date == _today);

    public int CompletedToday =>
        _completions.Count(c => c.DueDate.Date == _today && c.CompletedDate.HasValue);

    public List<HabitStreak> HabitStreaks => _habits
        .Select(h => new HabitStreak(h, ComputeStreak(h.Id)))
        .OrderByDescending(s => s.CurrentStreak)
        .ThenBy(s => s.Habit.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    private int ComputeStreak(int habitId)
    {
        var completedDays = _completions
            .Where(c => c.HabitId == habitId && c.CompletedDate.HasValue)
            .Select(c => c.DueDate.Date)
            .ToHashSet();

        // A completed today extends the streak, but an incomplete today does not break it
        var day = completedDays.Contains(_today) ? _today : _today.AddDays(-1);
        var streak = 0;
        while (completedDays.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }
        return streak;
    }

    // The last 7 days (oldest first), each with the colors of habits completed that day
    public List<DayColumn> DayColumns
    {
        get
        {
            var colorById = _habits.ToDictionary(h => h.Id, h => h.ColorHex);
            var columns = new List<DayColumn>();
            for (var i = 6; i >= 0; i--)
            {
                var date = _today.AddDays(-i);
                var colors = _completions
                    .Where(c => c.DueDate.Date == date && c.CompletedDate.HasValue)
                    .Select(c => colorById.GetValueOrDefault(c.HabitId))
                    .Where(hex => !string.IsNullOrEmpty(hex))
                    .Select(hex => hex!)
                    .ToList();
                columns.Add(new DayColumn(date, colors));
            }
            return columns;
        }
    }

    // Rate = completed days / tracked days within the last 30 days; habits never
    // tracked in the window are omitted
    public List<HabitRate> HabitRates
    {
        get
        {
            var windowStart = _today.AddDays(-29);
            var rates = new List<HabitRate>();
            foreach (var habit in _habits)
            {
                var inWindow = _completions
                    .Where(c => c.HabitId == habit.Id &&
                                c.DueDate.Date >= windowStart && c.DueDate.Date <= _today)
                    .ToList();
                var trackedDays = inWindow.Select(c => c.DueDate.Date).Distinct().Count();
                if (trackedDays == 0) continue;
                var completedDays = inWindow
                    .Where(c => c.CompletedDate.HasValue)
                    .Select(c => c.DueDate.Date).Distinct().Count();
                rates.Add(new HabitRate(habit, (double)completedDays / trackedDays, trackedDays));
            }
            return rates
                .OrderByDescending(r => r.Rate)
                .ThenBy(r => r.Habit.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public HabitRate? BestHabit => HabitRates.FirstOrDefault();

    // The 5-day floor prevents a recently added habit from appearing as the worst
    public HabitRate? WorstHabit => HabitRates
        .Where(r => r.TrackedDays >= 5)
        .OrderBy(r => r.Rate)
        .FirstOrDefault();
}
