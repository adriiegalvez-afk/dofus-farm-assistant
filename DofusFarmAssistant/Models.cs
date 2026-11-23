using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace DofusFarmAssistant;

public sealed class Settings
{
    public string ProcessName { get; set; } = "Dofus";
    public string ClientType { get; set; } = "Dofus Unity";
    public int PollSeconds { get; set; } = 3;
    public int ScreenshotMinutes { get; set; }
    public int KeepDays { get; set; } = 30;
    public int MaxStorageMb { get; set; } = 1024;
    public bool AutoStartMonitoring { get; set; }
    public bool AutoStartWindows { get; set; }
    public bool RemoteEnabled { get; set; }
    public string RemotePasswordHash { get; set; } = "";
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPasswordProtected { get; set; } = "";
    public string MailTo { get; set; } = "";
    public bool EmailEnabled { get; set; }
    public bool NotifyClose { get; set; } = true;
    public bool NotifyHang { get; set; } = true;
    public bool NotifyPotentialDisconnect { get; set; } = true;
    public string? LastState { get; set; }
}

public sealed record EventRecord(DateTime Timestamp, string Type, string Details);
public sealed record Snapshot(string State, DateTime? StartedAt, long? RuntimeSeconds, DateTime? LastEvent, int EventCount, string? LastScreenshot, bool Monitoring);

public static class Store
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DofusFarmAssistant");
    public static readonly string Shots = Path.Combine(Root, "captures");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string DbPath => Path.Combine(Root,"assistant.db");
    private static string ConnectionString => new SqliteConnectionStringBuilder { DataSource=DbPath, Mode=SqliteOpenMode.ReadWriteCreate, Cache=SqliteCacheMode.Shared, DefaultTimeout=5 }.ToString();
    static Store()
    {
        Directory.CreateDirectory(Root); Directory.CreateDirectory(Shots);
        using var db=new SqliteConnection(ConnectionString); db.Open(); using var cmd=db.CreateCommand(); cmd.CommandText="PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS events (id INTEGER PRIMARY KEY AUTOINCREMENT, timestamp TEXT NOT NULL, type TEXT NOT NULL, details TEXT NOT NULL);"; cmd.ExecuteNonQuery();
    }
    public static Settings LoadSettings() => Read<Settings>(Path.Combine(Root, "settings.json")) ?? new Settings();
    public static void SaveSettings(Settings s) => File.WriteAllText(Path.Combine(Root, "settings.json"), JsonSerializer.Serialize(s, JsonOptions));
    public static List<EventRecord> LoadEvents()
    {
        var result=new List<EventRecord>(); using var db=new SqliteConnection(ConnectionString); db.Open(); using var cmd=db.CreateCommand(); cmd.CommandText="SELECT timestamp,type,details FROM events ORDER BY id"; using var reader=cmd.ExecuteReader(); while(reader.Read()) result.Add(new(DateTime.Parse(reader.GetString(0)),reader.GetString(1),reader.GetString(2))); return result;
    }
    public static void AddEvent(string type, string details)
    {
        using var db=new SqliteConnection(ConnectionString); db.Open(); using var tx=db.BeginTransaction(); using(var cmd=db.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="INSERT INTO events(timestamp,type,details) VALUES($time,$type,$details)";cmd.Parameters.AddWithValue("$time",DateTime.Now.ToString("O"));cmd.Parameters.AddWithValue("$type",type);cmd.Parameters.AddWithValue("$details",details);cmd.ExecuteNonQuery();} using(var prune=db.CreateCommand()){prune.Transaction=tx;prune.CommandText="DELETE FROM events WHERE id NOT IN (SELECT id FROM events ORDER BY id DESC LIMIT 5000)";prune.ExecuteNonQuery();} tx.Commit();
    }
    private static T? Read<T>(string path) => File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default;
}
