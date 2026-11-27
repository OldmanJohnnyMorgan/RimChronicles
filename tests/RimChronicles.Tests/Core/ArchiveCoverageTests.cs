using System.Reflection;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

/// <summary>
/// Every saved Core type keeps every public scalar and string-list field and property through a save and load (rule 5),
/// uses each label once, and loads from an empty node. A new field someone forgets to add to <c>Expose</c> fails here,
/// not in a player's save. Members of other types (lists of objects, nested records) are covered by each type's own
/// round-trip tests.
/// </summary>
public class ArchiveCoverageTests
{
    private static readonly List<Type> Archivable = typeof(IArchivable).Assembly.GetTypes()
        .Where(t => typeof(IArchivable).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false } && t.GetConstructor(Type.EmptyTypes) is not null)
        .OrderBy(t => t.FullName)
        .ToList();

    public static TheoryData<string> Types() => new(Archivable.Select(t => t.FullName!));

    [Fact]
    public void Finds_the_saved_types() => Assert.True(Archivable.Count >= 30, $"Only {Archivable.Count} IArchivable types found");

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_member_survives_a_round_trip(string typeName)
    {
        var type = Archivable.Single(t => t.FullName == typeName);
        var saved = (IArchivable)Activator.CreateInstance(type)!;
        var members = SetEveryMember(saved);

        var archive = new DictionaryArchive(ArchiveMode.Saving);
        saved.Expose(archive);
        var loaded = (IArchivable)Activator.CreateInstance(type)!;
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, archive.Data));

        var lost = members.Where(m => !Same(Get(m, saved), Get(m, loaded))).Select(m => m.Name).ToList();
        Assert.True(lost.Count == 0, $"{type.Name} loses {string.Join(", ", lost)} on save and load");
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Labels_are_unique(string typeName)
    {
        var labels = new LabelRecorder();
        ((IArchivable)Activator.CreateInstance(Archivable.Single(t => t.FullName == typeName))!).Expose(labels);
        Assert.True(labels.Repeated.Count == 0, $"{typeName} saves these labels twice: {string.Join(", ", labels.Repeated)}");
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Loads_from_an_empty_node(string typeName)
    {
        var value = (IArchivable)Activator.CreateInstance(Archivable.Single(t => t.FullName == typeName))!;
        value.Expose(new DictionaryArchive(ArchiveMode.Loading, []));
    }

    private static List<MemberInfo> SetEveryMember(object target)
    {
        var set = new List<MemberInfo>();
        var type = target.GetType();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => !f.IsInitOnly))
        {
            if (Different(field.FieldType, field.GetValue(target)) is { } value)
            {
                field.SetValue(target, value);
                set.Add(field);
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0))
        {
            if (Different(property.PropertyType, property.GetValue(target)) is { } value)
            {
                property.SetValue(target, value);
                set.Add(property);
            }
        }

        return set;
    }

    /// <summary>A value unlike the default, or null for member types this test doesn't cover.</summary>
    private static object? Different(Type type, object? current) => type switch
    {
        _ when type == typeof(int) => 7,
        _ when type == typeof(long) => 7L,
        _ when type == typeof(float) => 0.7f,
        _ when type == typeof(double) => 0.7,
        _ when type == typeof(string) => "zz",
        _ when type == typeof(bool) => !(bool)(current ?? false),
        _ when type == typeof(List<string>) => new List<string> { "zz" },
        { IsEnum: true } when Enum.GetValues(type) is { Length: > 1 } values => values.GetValue(values.Length - 1),
        _ => null,
    };

    private static object? Get(MemberInfo member, object target) =>
        member is FieldInfo field ? field.GetValue(target) : ((PropertyInfo)member).GetValue(target);

    private static bool Same(object? a, object? b) =>
        a is List<string> left ? b is List<string> right && left.SequenceEqual(right) : Equals(a, b);

    private sealed class LabelRecorder : IArchive
    {
        private readonly HashSet<string> _seen = [];

        public List<string> Repeated { get; } = [];

        public ArchiveMode Mode => ArchiveMode.Saving;

        public void Value<T>(ref T value, string label, T defaultValue = default!) => See(label);
        public void Strings(ref List<string> list, string label) => See(label);
        public void Deep<T>(ref T value, string label) where T : class, IArchivable, new() => See(label);
        public void DeepList<T>(ref List<T> list, string label) where T : class, IArchivable, new() => See(label);

        private void See(string label)
        {
            if (!_seen.Add(label))
            {
                Repeated.Add(label);
            }
        }
    }
}
