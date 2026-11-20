using System.Text.Json;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Llm.Prompts;

/// <summary>
/// Loads versioned prompt folders:
/// <code>
/// prompts/
///   CURRENT.json              { "director": "v1", ... }
///   director/v1/meta.json     task, version, maxOutputTokens, schema file, tier overrides
///   director/v1/system.md
///   director/v1/user.md       (optional user.&lt;Tier&gt;.md variants)
///   director/v1/schema.json   (optional)
/// </code>
/// </summary>
public sealed class PromptLibrary
{
    private readonly Dictionary<string, PromptTemplate> _current;

    private PromptLibrary(string root, Dictionary<string, PromptTemplate> current)
    {
        Root = root;
        _current = current;
    }

    public string Root { get; }

    /// <summary>Task names with a current version.</summary>
    public IReadOnlyCollection<string> Tasks => _current.Keys;

    /// <summary>Loads every task's current version, as listed in CURRENT.json.</summary>
    public static PromptLibrary Load(string root)
    {
        var currentPath = Path.Combine(root, "CURRENT.json");
        var current = JsonSerializer.Deserialize(File.ReadAllText(currentPath), PromptJsonContext.Default.DictionaryStringString)
                      ?? throw new InvalidDataException($"{currentPath} is empty.");

        var templates = current.ToDictionary(kv => kv.Key, kv => LoadVersion(root, kv.Key, kv.Value), StringComparer.Ordinal);
        return new PromptLibrary(root, templates);
    }

    /// <summary>The current version of a task's prompt.</summary>
    public PromptTemplate Get(string task) =>
        _current.TryGetValue(task, out var template)
            ? template
            : throw new KeyNotFoundException($"No current prompt for task '{task}' in {Root}.");

    /// <summary>Loads one specific version folder, e.g. for comparing versions in the harness.</summary>
    public static PromptTemplate LoadVersion(string root, string task, string version)
    {
        var dir = Path.Combine(root, task, version);
        var metaPath = Path.Combine(dir, "meta.json");
        var meta = JsonSerializer.Deserialize(File.ReadAllText(metaPath), PromptJsonContext.Default.PromptMeta)
                   ?? throw new InvalidDataException($"{metaPath} is empty.");

        if (meta.Task != task || meta.Version != version)
        {
            throw new InvalidDataException(
                $"{metaPath} says {meta.Task}/{meta.Version} but lives in {task}/{version}.");
        }

        var userByTier = new Dictionary<ModelTier, string>();
        foreach (var (tierName, over) in meta.Tiers)
        {
            if (!Enum.TryParse<ModelTier>(tierName, out var tier))
            {
                throw new InvalidDataException($"{metaPath}: unknown tier '{tierName}'.");
            }

            if (over.UserTemplate is { } file)
            {
                userByTier[tier] = File.ReadAllText(Path.Combine(dir, file));
            }
        }

        JsonSchemaSpec? schema = meta.Schema is { } schemaFile
            ? JsonSchemaSpec.Parse(SchemaName(task), File.ReadAllText(Path.Combine(dir, schemaFile)))
            : null;

        return new PromptTemplate(
            meta,
            File.ReadAllText(Path.Combine(dir, "system.md")),
            File.ReadAllText(Path.Combine(dir, "user.md")),
            userByTier,
            schema);
    }

    private static string SchemaName(string task) => task.Replace('-', '_');
}
