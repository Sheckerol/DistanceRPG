using System.Reflection;
using System.Reflection.Emit;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The standing constraint, as claims about code (PHASE4-145): <c>Logic/</c> is
/// engine-free and deterministic — no OpenTK or engine type anywhere in its
/// surface, and no <c>Random.Shared</c> outside the one injected delegate's
/// documented fallback. Read off the whole namespace by reflection and IL, so a
/// type a later step adds — the visit, the boss's loot, the run's result — is
/// covered the moment it exists, with no list for anyone to keep up to date.
/// </summary>
public class LogicPurityTests
{
    private const string LogicNamespace = "GameEngine.DistanceRPG.Logic";

    private static readonly Assembly Game = typeof(Floor).Assembly;

    /// <summary>Every type in the Logic namespace, compiler-generated nested ones included (a nested type reports its outer type's namespace).</summary>
    private static Type[] LogicTypes() => Types(Game).Where(t => t.Namespace == LogicNamespace).ToArray();

    /// <summary>Whether <paramref name="type"/> comes from the engine: OpenTK, or the engine's own core.</summary>
    private static bool IsEngine(Type type)
    {
        string name = type.Assembly.GetName().Name ?? "";
        return name.StartsWith("OpenTK", StringComparison.Ordinal) || name == "GameEngine.Core";
    }

    /// <summary>A type and every type it is built from — an array's element, a generic's arguments — so a <c>List&lt;Vector3&gt;</c> is caught as surely as a <c>Vector3</c>.</summary>
    private static IEnumerable<Type> Constituents(Type type)
    {
        yield return type;
        if (type.HasElementType)
            foreach (var inner in Constituents(type.GetElementType()!))
                yield return inner;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                foreach (var inner in Constituents(argument))
                    yield return inner;
    }

    /// <summary>Everything <paramref name="type"/> declares that names a type: its base and interfaces, fields, properties, events, and every method's and constructor's parameters and return.</summary>
    private static IEnumerable<(string Where, Type Type)> Signature(Type type)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        if (type.BaseType is { } baseType)
            yield return ($"{type.FullName} : {baseType.Name}", baseType);
        foreach (var contract in type.GetInterfaces())
            yield return ($"{type.FullName} : {contract.Name}", contract);
        foreach (var field in type.GetFields(All))
            yield return ($"{type.FullName}.{field.Name}", field.FieldType);
        foreach (var property in type.GetProperties(All))
            yield return ($"{type.FullName}.{property.Name}", property.PropertyType);
        foreach (var e in type.GetEvents(All))
            yield return ($"{type.FullName}.{e.Name}", e.EventHandlerType!);
        foreach (var method in type.GetMethods(All))
        {
            yield return ($"{type.FullName}.{method.Name} returns", method.ReturnType);
            foreach (var parameter in method.GetParameters())
                yield return ($"{type.FullName}.{method.Name}({parameter.Name})", parameter.ParameterType);
        }
        foreach (var constructor in type.GetConstructors(All))
            foreach (var parameter in constructor.GetParameters())
                yield return ($"{type.FullName}..ctor({parameter.Name})", parameter.ParameterType);
    }

    private static bool NamesTheEngine((string Where, Type Type) entry) => Constituents(entry.Type).Any(IsEngine);

    [Fact]
    public void NoLogicSignatureNamesTheEngine()
    {
        // No field, property, event, parameter, return, base or interface of any
        // Logic type comes from OpenTK or the engine — the stairs, the floor and
        // the visit included — so Logic compiles against nothing it draws with.
        var logic = LogicTypes();
        var offenders = logic.SelectMany(Signature).Where(NamesTheEngine)
            .Select(entry => entry.Where).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal([], offenders);

        // Nor does any Logic method call into it, from a signature or not.
        var calls = Calls(Game).Where(c => Root(c.From.DeclaringType)?.Namespace == LogicNamespace).ToArray();
        var engineCalls = calls.Where(c => c.Target.DeclaringType is { } t && IsEngine(t))
            .Select(Describe).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal([], engineCalls);

        // The scan is not reading nothing: the whole namespace, this phase's
        // types among it, and the same predicate does catch the presentation,
        // which is built on the engine.
        Assert.Contains(typeof(Floor), logic);
        Assert.Contains(typeof(DungeonState), logic);
        Assert.Contains(typeof(StairPlacer), logic);
        Assert.Contains(typeof(StairTiles), logic);
        Assert.Contains(typeof(TurnSystem), logic);
        Assert.Contains(logic, t => t.DeclaringType != null && t.Name.StartsWith('<'));   // compiler-generated, nested
        Assert.Contains(Signature(typeof(DungeonScene)), NamesTheEngine);
        Assert.Contains(Calls(Game), c => Root(c.From.DeclaringType) == typeof(DungeonScene) && IsEngine(c.Target.DeclaringType!));
    }

    [Fact]
    public void TheNewStreamsNeverReachForRandomShared()
    {
        // Every stream in Logic is a Mulberry32 derived from the map seed with a
        // salt of its own. The IL of every Logic type, compiler-generated nested
        // types included, calls Random.Shared nowhere but in TurnSystem's one
        // documented fallback — a d20 for a caller that injects none — and builds
        // no System.Random of its own.
        var calls = Calls(Game).Where(c => Root(c.From.DeclaringType)?.Namespace == LogicNamespace).ToArray();
        static bool IsShared(MethodBase target) => target is MethodInfo { Name: "get_Shared" } && target.DeclaringType == typeof(Random);

        var shared = calls.Where(c => IsShared(c.Target)).ToArray();
        var offenders = shared.Where(c => Root(c.From.DeclaringType) != typeof(TurnSystem))
            .Select(Describe).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal([], offenders);
        Assert.Single(shared, c => Root(c.From.DeclaringType) == typeof(TurnSystem));

        var ownRandom = calls.Where(c => c.Op == OpCodes.Newobj && c.Target.DeclaringType == typeof(Random))
            .Select(Describe).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal([], ownRandom);

        // The scan sees this phase's streams being built where they are drawn —
        // the stairs' and the boss's, the floor's map, and the enchanter's
        // improvement roll (Phase 6a.4) — so an assertion above cannot pass by
        // finding nothing.
        bool Builds(Type from) => calls.Any(c => c.Op == OpCodes.Newobj && c.Target.DeclaringType == typeof(Mulberry32)
            && Root(c.From.DeclaringType) == from);
        Assert.True(Builds(typeof(StairPlacer)));
        Assert.True(Builds(typeof(DungeonState)));
        Assert.True(Builds(typeof(Floor)));
        Assert.True(Builds(typeof(EnemyPlacer)));
        Assert.True(Builds(typeof(Enchanter)));
    }

    /// <summary>
    /// What reading or writing the machine looks like from IL: a file or a folder,
    /// the environment's variables and folders, the executable's own folder, a
    /// stream opened on a path — and what the build carries, a manifest resource
    /// read out of an assembly, which is IO like any file.
    /// </summary>
    private static bool TouchesTheDisk(MethodBase target)
    {
        var type = target.DeclaringType;
        if (type is null) return false;
        if (type == typeof(File) || type == typeof(Directory) || type == typeof(FileInfo) || type == typeof(DirectoryInfo) || type == typeof(FileStream))
            return true;
        if (type == typeof(Environment))
            return target.Name is nameof(Environment.GetEnvironmentVariable) or nameof(Environment.GetFolderPath);
        if (type == typeof(AppContext))
            return target.Name == "get_" + nameof(AppContext.BaseDirectory);
        if (typeof(Assembly).IsAssignableFrom(type))
            return target.Name is nameof(Assembly.GetManifestResourceStream) or nameof(Assembly.GetManifestResourceNames) or nameof(Assembly.GetManifestResourceInfo);
        return (type == typeof(StreamReader) || type == typeof(StreamWriter))
            && target is ConstructorInfo constructor
            && constructor.GetParameters() is [{ ParameterType: var first }, ..] && first == typeof(string);
    }

    /// <summary>The game's root namespace: the edge, where the disk and the build are read and the scenes draw.</summary>
    private const string EdgeNamespace = "GameEngine.DistanceRPG";

    /// <summary>Whether <paramref name="target"/> belongs to a type of the game's edge — the root namespace, where <c>ShippedData</c>, <c>DataFiles</c> and every scene live — which <c>Logic/</c> never calls into.</summary>
    private static bool IsTheEdge(MethodBase target)
        => target.Module == Game.ManifestModule && Root(target.DeclaringType)?.Namespace == EdgeNamespace;

    [Fact]
    public void NoLogicTypeTouchesTheDisk()
    {
        // A31, 1e: Logic reads strings and never the disk. The data files are read
        // at the edge (DataFiles) and handed in as text, so every reader is tested
        // on text and no test can touch a real folder through Logic. Nothing in the
        // namespace calls into a file, a folder, the environment's variables or
        // folders, the executable's own folder, or what the build carries — the IL
        // of every Logic type, compiler-generated nested types included.
        var calls = Calls(Game).Where(c => Root(c.From.DeclaringType)?.Namespace == LogicNamespace).ToArray();
        var offenders = calls.Where(c => TouchesTheDisk(c.Target)).Select(Describe).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal([], offenders);

        // Nor does it reach the disk or the build through the edge (the content
        // rework, 1f): no Logic type calls a type of the game's root namespace —
        // ShippedData, DataFiles, a scene — so a built-in copy is only ever handed
        // in, as the content GameContent.Current opens on is (GameContent.OpenOn).
        var intoTheEdge = calls.Where(c => IsTheEdge(c.Target)).Select(c => $"{Describe(c)} -> {c.Target.DeclaringType?.Name}.{c.Target.Name}")
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal([], intoTheEdge);
        Assert.Contains(Calls(Game), c => Root(c.From.DeclaringType) == typeof(DistanceRpgGame) && c.From.Name == "LoadData"
            && IsTheEdge(c.Target) && c.Target.DeclaringType == typeof(ShippedData));   // the predicate catches the edge calling itself
        Assert.Contains(Calls(Game), c => Root(c.From.DeclaringType) == typeof(ShippedData) && TouchesTheDisk(c.Target));   // and the build's reader

        // Both guards read the namespace exactly, so a Logic type declared in a
        // namespace under it (…Logic.Content.Json, say) would slip past both: no
        // type of the game's is declared in one.
        var nested = Types(Game).Where(t => t.Namespace?.StartsWith(LogicNamespace + ".", StringComparison.Ordinal) == true)
            .Select(t => t.FullName ?? t.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal([], nested);

        // The scan is not reading nothing: the new readers are in it, and the same
        // predicate catches the edge, which does read and write the disk.
        var logic = LogicTypes();
        Assert.Contains(typeof(TuningJson), logic);
        Assert.Contains(typeof(ContentFiles), logic);
        Assert.Contains(Calls(Game), c => Root(c.From.DeclaringType) == typeof(DataFiles) && TouchesTheDisk(c.Target));
        Assert.Contains(Calls(Game), c => Root(c.From.DeclaringType) == typeof(DistanceRpgGame) && TouchesTheDisk(c.Target));
    }
}
