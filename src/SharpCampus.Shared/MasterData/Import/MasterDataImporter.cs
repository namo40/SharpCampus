using System.Text.Json;
using System.Text.Json.Serialization;
using MasterMemory.Validation;

namespace SharpCampus.Shared.MasterData.Import;

/// <summary>
/// Reads the master data source files into the in-memory database. The servers import the sources
/// directly during development; the packaged binary is the same bytes, built ahead of time.
/// </summary>
public static class MasterDataImporter
{
    /// <summary>Name of the file holding the single <see cref="GameConfig"/> row.</summary>
    public const string GameConfigFile = "game_config.json";

    /// <summary>Name of the file holding the <see cref="GravityCurve"/> rows.</summary>
    public const string GravityCurveFile = "gravity_curve.json";

    /// <summary>Name of the file holding the <see cref="AttackTable"/> rows.</summary>
    public const string AttackTableFile = "attack_table.json";

    /// <summary>Name of the file holding the <see cref="ComboTable"/> rows.</summary>
    public const string ComboTableFile = "combo_table.json";

    /// <summary>Name of the file holding the <see cref="Skin"/> rows.</summary>
    public const string SkinsFile = "skins.json";

    /// <summary>Name of the file holding the <see cref="Mission"/> rows.</summary>
    public const string MissionsFile = "missions.json";

    /// <summary>Name of the file holding the single <see cref="Economy"/> row.</summary>
    public const string EconomyFile = "economy.json";

    // Comments and trailing commas stay rejected: a source file the tool accepts has to be the same
    // file every JSON reader accepts, including the ones a future editor or pipeline brings along.
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Reads the source file of every table in a directory and serializes the result.</summary>
    /// <param name="inputDirectory">Directory holding one source file per table.</param>
    /// <returns>The serialized database, ready for <see cref="MemoryDatabase"/>.</returns>
    /// <exception cref="FileNotFoundException">The directory misses a table's source file.</exception>
    /// <exception cref="JsonException">A source file is malformed or holds no rows.</exception>
    public static byte[] Build(string inputDirectory)
    {
        var builder = new DatabaseBuilder();
        builder.Append([ReadRow<GameConfig>(inputDirectory, GameConfigFile)]);
        builder.Append(ReadRows<GravityCurve>(inputDirectory, GravityCurveFile));
        builder.Append(ReadRows<AttackTable>(inputDirectory, AttackTableFile));
        builder.Append(ReadRows<ComboTable>(inputDirectory, ComboTableFile));
        builder.Append(ReadRows<Skin>(inputDirectory, SkinsFile));
        builder.Append(ReadRows<Mission>(inputDirectory, MissionsFile));
        builder.Append([ReadRow<Economy>(inputDirectory, EconomyFile)]);
        return builder.Build();
    }

    /// <summary>Runs the schema validators over a built database.</summary>
    /// <param name="binary">Bytes produced by <see cref="Build"/>.</param>
    /// <returns>The validation result, which reports the failures it collected.</returns>
    public static ValidateResult Validate(byte[] binary) => new MemoryDatabase(binary).Validate();

    private static T ReadRow<T>(string inputDirectory, string fileName)
        where T : class
    {
        var path = Locate(inputDirectory, fileName);
        return Deserialize<T>(path) ?? throw new JsonException($"'{path}' holds no row.");
    }

    // An empty file reaches the validators as a table that simply has no data, which reads as a
    // schema failure rather than as the emptied file it is.
    private static List<T> ReadRows<T>(string inputDirectory, string fileName)
    {
        var path = Locate(inputDirectory, fileName);
        return Deserialize<List<T>>(path) is { Count: > 0 } rows
            ? rows
            : throw new JsonException($"'{path}' holds no rows.");
    }

    private static string Locate(string inputDirectory, string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(inputDirectory, fileName));
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException($"The master data source file was not found at '{path}'.", path);
    }

    private static T? Deserialize<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), _serializerOptions);
        }
        catch (JsonException e)
        {
            // The original message pins down the token by path and line but not by file, and every
            // table has one of its own now.
            throw new JsonException($"{Path.GetFileName(path)}: {e.Message}", e);
        }
    }
}
