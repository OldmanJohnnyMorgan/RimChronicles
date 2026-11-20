using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RimChronicles.Core.Snapshots;

/// <summary>Reads and writes snapshot JSON (camelCase, unknown fields rejected so typos fail loudly).</summary>
/// <remarks>
/// Uses the reflection-based serializer on purpose: the source generator assigns default(T) to init-only
/// properties missing from the JSON, which would turn absent lists into null instead of empty.
/// </remarks>
public static class SnapshotJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static WorldSnapshot Parse(string json) =>
        JsonSerializer.Deserialize<WorldSnapshot>(json, Options) ?? throw new JsonException("Snapshot JSON is null.");

    public static WorldSnapshot Load(string path) => Parse(File.ReadAllText(path));

    public static string Serialize(WorldSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);
}
