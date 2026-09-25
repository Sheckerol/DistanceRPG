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
