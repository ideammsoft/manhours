using System.Text.Json;

namespace manHours;

public class AppSettings
{
    private static readonly string _path =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "manHours", "settings.json");

    public string Theme    { get; set; } = "dark";
    public float  FontSize { get; set; } = 9.5f;

    private static AppSettings? _instance;
    public static AppSettings Instance => _instance ??= Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
