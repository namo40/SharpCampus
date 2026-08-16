using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpCampus.Client.Common;

// Survives process exit so one-shot invocations and the REPL share the same signed-in user.
// A guest has no address yet, which is the only thing that separates the two kinds of session.
public sealed record ClientSession(string AccessToken, string? Email)
{
    // Derived from the address, so the stored file keeps the two fields it always had.
    [JsonIgnore]
    public bool IsGuest => Email is null;

    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SharpCampus",
        "session.json");

    public static ClientSession? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        // A truncated or hand-edited file is easier to treat as "signed out" than to repair.
        try
        {
            return JsonSerializer.Deserialize<ClientSession>(File.ReadAllText(_filePath), JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        // Production note: this token grants full access to the account, so a real client would use the OS keychain.
        File.WriteAllText(_filePath, JsonSerializer.Serialize(this, JsonSerializerOptions.Web));
    }

    public static bool Delete()
    {
        if (!File.Exists(_filePath))
        {
            return false;
        }

        File.Delete(_filePath);
        return true;
    }
}
