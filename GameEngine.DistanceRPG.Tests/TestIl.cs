using System.Reflection;
using System.Reflection.Emit;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Reading IL, for the guards that are claims about code rather than about state:
/// that no test builds a party member outside <see cref="TestPools"/>
/// (<c>ProgressionStateTests</c>), and that nothing in <c>Logic/</c> reaches for
/// <c>Random.Shared</c> (<c>LogicPurityTests</c>). Lifted out of
/// <c>ProgressionStateTests</c> unchanged so a second guard reads the same walker
/// rather than a copy of it.
/// </summary>
internal static class TestIl
{
    /// <summary>The outermost type a method belongs to, so a lambda's closure or an iterator's state machine is charged to the class that wrote it.</summary>
    internal static Type? Root(Type? type)
    {
        while (type?.DeclaringType is { } outer)
            type = outer;
        return type;
    }

    internal static string Describe((MethodBase From, MethodBase Target, OpCode Op) call)
        => $"{call.From.DeclaringType?.FullName}.{call.From.Name}";

    /// <summary>Every call, callvirt and newobj in <paramref name="assembly"/>, with the method it sits in.</summary>
    internal static IEnumerable<(MethodBase From, MethodBase Target, OpCode Op)> Calls(Assembly assembly)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var type in Types(assembly))
        {
            var methods = type.GetMethods(Flags).Cast<MethodBase>().Concat(type.GetConstructors(Flags));
            foreach (var method in methods)
            {
                if (Body(method) is not { } il) continue;
                foreach (var (op, token) in Instructions(il))
                {
                    if (op != OpCodes.Newobj && op != OpCodes.Call && op != OpCodes.Callvirt) continue;
                    if (Resolve(method, token) is { } target)
                        yield return (method, target, op);
                }
            }
        }
    }

    /// <summary>
    /// Every method-typed operand in <paramref name="assembly"/> with the method it
    /// sits in: <see cref="Calls"/>' call, callvirt and newobj, and beside them the
    /// <c>ldftn</c> and <c>ldvirtftn</c> that make a delegate of a method — a
    /// method handed on to run later, which is not a call.
    /// </summary>
    internal static IEnumerable<(MethodBase From, MethodBase Target, OpCode Op)> Edges(Assembly assembly)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var type in Types(assembly))
        {
            var methods = type.GetMethods(Flags).Cast<MethodBase>().Concat(type.GetConstructors(Flags));
            if (type.TypeInitializer is { } initializer)
                methods = methods.Append(initializer);
            foreach (var method in methods.Distinct())
            {
                if (Body(method) is not { } il) continue;
                foreach (var (op, token) in Instructions(il))
                    if (IsCall(op) || IsDelegate(op))
                        if (Resolve(method, token) is { } target)
                            yield return (method, target, op);
            }
        }
    }

    /// <summary>A call, callvirt or newobj: an instruction that runs the method it names, there and then.</summary>
    internal static bool IsCall(OpCode op) => op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj;

    /// <summary>An <c>ldftn</c> or <c>ldvirtftn</c>: an instruction that makes a delegate of the method it names, to be run by whoever is handed it.</summary>
    internal static bool IsDelegate(OpCode op) => op == OpCodes.Ldftn || op == OpCodes.Ldvirtftn;

    /// <summary>Whether <paramref name="method"/> is a type initializer (<c>.cctor</c>) — which <see cref="MethodBase.IsConstructor"/> does not count as a constructor.</summary>
    internal static bool IsTypeInitializer(MethodBase method) => method is ConstructorInfo { IsStatic: true };

    /// <summary>The methods <paramref name="method"/>'s body calls (<see cref="IsCall"/>), each as named, in order.</summary>
    internal static IEnumerable<MethodBase> CallsIn(MethodBase method)
    {
        if (Body(method) is not { } il) yield break;
        foreach (var (op, token) in Instructions(il))
            if (IsCall(op) && Resolve(method, token) is { } target)
                yield return target;
    }

    /// <summary>
    /// Every method of <paramref name="roots"/>' own assembly that running them can
    /// reach, the roots included, each as its definition: through every call,
    /// callvirt and newobj; through every delegate a method makes (<c>ldftn</c>) —
    /// a lambda handed to a query runs there and then — except one a type
    /// initializer makes, which it stores to run later (a table of behaviours, a
    /// value made lazily); and to the type initializer of every type a reached
    /// method calls into or reads or writes a static field of, which the runtime
    /// runs first. A virtual call is followed to the method named, not to its
    /// overrides; a method outside the assembly is not followed at all.
    /// </summary>
    internal static IReadOnlyCollection<MethodBase> Reachable(params MethodBase[] roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var module = roots[0].Module;
        var reached = new Dictionary<int, MethodBase>();
        var queue = new Queue<MethodBase>(roots.Select(Definition));
        while (queue.Count > 0)
        {
            var method = queue.Dequeue();
            if (!reached.TryAdd(method.MetadataToken, method))
                continue;
            if (Body(method) is not { } il)
                continue;
            bool initializer = IsTypeInitializer(method);
            foreach (var (op, token) in MemberOperands(il))
            {
                if (op.OperandType == OperandType.InlineMethod)
                {
                    if ((IsCall(op) || (IsDelegate(op) && !initializer)) && Resolve(method, token) is { } target && target.Module == module)
                    {
                        queue.Enqueue(Definition(target));
                        Initialize(target.DeclaringType);
                    }
                }
                else if ((op == OpCodes.Ldsfld || op == OpCodes.Ldsflda || op == OpCodes.Stsfld)
                    && ResolveField(method, token) is { } field && field.Module == module)
                {
                    Initialize(field.DeclaringType);
                }
            }
        }
        return reached.Values;

        void Initialize(Type? type)
        {
            if (type?.TypeInitializer is { } cctor && cctor.Module == module)
                queue.Enqueue(cctor);
        }

        MethodBase Definition(MethodBase target) => target.Module.ResolveMethod(target.MetadataToken)!;
    }

    /// <summary>Every instruction in <paramref name="il"/> whose operand names a method or a field, with the token.</summary>
    private static IEnumerable<(OpCode Op, int Token)> MemberOperands(byte[] il)
    {
        foreach (var (op, _, at) in InstructionsAt(il))
            if (op.OperandType is OperandType.InlineMethod or OperandType.InlineField)
                yield return (op, BitConverter.ToInt32(il, at));
    }

    private static FieldInfo? ResolveField(MethodBase from, int token)
    {
        try
        {
            var typeArgs = from.DeclaringType is { IsGenericType: true } t ? t.GetGenericArguments() : null;
            var methodArgs = from is MethodInfo { IsGenericMethodDefinition: true } m ? m.GetGenericArguments() : null;
            return from.Module.ResolveField(token, typeArgs, methodArgs);
        }
        catch (Exception)
        {
            return null;   // a token that is not a field this assembly can name
        }
    }

    internal static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t is not null)!;
        }
    }

    internal static byte[]? Body(MethodBase method)
    {
        try
        {
            return method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception)
        {
            return null;   // no managed body to read: an extern, a runtime intrinsic
        }
    }

    internal static MethodBase? Resolve(MethodBase from, int token)
    {
        try
        {
            var typeArgs = from.DeclaringType is { IsGenericType: true } t ? t.GetGenericArguments() : null;
            var methodArgs = from is MethodInfo { IsGenericMethodDefinition: true } m ? m.GetGenericArguments() : null;
            return from.Module.ResolveMethod(token, typeArgs, methodArgs);
        }
        catch (Exception)
        {
            return null;   // a token that is not a method this assembly can name
        }
    }

    /// <summary>
    /// Walk a method body one instruction at a time. A byte-wise search for the
    /// opcode would match the same byte inside an operand, so every instruction's
    /// operand is measured and skipped.
    /// </summary>
    internal static IEnumerable<(OpCode Op, int Token)> Instructions(byte[] il)
        => InstructionsAt(il).Select(instruction => (instruction.Op, instruction.Token));

    /// <summary>
    /// <see cref="Instructions"/>, with where each instruction's operand starts in
    /// <paramref name="il"/> — for a guard that reads an operand the walker does
    /// not decode, such as the float an <c>ldc.r4</c> loads.
    /// </summary>
    internal static IEnumerable<(OpCode Op, int Token, int At)> InstructionsAt(byte[] il)
    {
        int i = 0;
        while (i < il.Length)
        {
            short code = il[i++];
            if (code == 0xFE && i < il.Length)
                code = (short)((0xFE << 8) | il[i++]);
            if (!OpCodeTable.TryGetValue(code, out var op))
                yield break;   // not something this walker knows: stop rather than guess a length

            int operand = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, i)),
                _ => 4,
            };
            if (i + operand > il.Length) yield break;

            int token = op.OperandType == OperandType.InlineMethod ? BitConverter.ToInt32(il, i) : 0;
            yield return (op, token, i);
            i += operand;
        }
    }

    private static readonly Dictionary<short, OpCode> OpCodeTable = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode))
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(op => op.Value);
}
