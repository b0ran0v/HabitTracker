using SQLite;

namespace HabitTracker.Data;

[Table("Habits")]
public class Habit
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#5C6BC0"; // Default to primary color
}
