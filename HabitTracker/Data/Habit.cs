using SQLite;

namespace HabitTracker.Data;

[Table("Habits")]
public class Habit
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#5C6BC0";
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public int CategoryId { get; set; }

    // Display name resolved from the Categories table; also kept in JSON exports
    // so files stay readable and pre-CategoryId backups can still be imported
    [Ignore]
    public string Category { get; set; } = string.Empty;
}
