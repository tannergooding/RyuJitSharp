// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public enum ScevOper
{
    Constant,
    Local,
    ZeroExtend,
    SignExtend,
    Add,
    Mul,
    Lsh,
    AddRec,
}

public enum ScevVisit
{
    Abort,
    Continue,
}

public enum RelopEvaluationResult
{
    Unknown,
    True,
    False,
}

public readonly struct SimplificationAssumptions
{
    public readonly Scev[]? BackEdgeTakenBound;

    public SimplificationAssumptions(Scev[] backEdgeTakenBound)
    {
        BackEdgeTakenBound = backEdgeTakenBound;
    }
}

[DebuggerDisplay("[{Oper}, {Type}]")]
public class Scev
{
    public ScevOper Oper { get; }

    public var_types Type { get; }

    protected Scev(ScevOper oper, var_types type)
    {
        Oper = oper;
        Type = type;
    }

    public bool OperIs(params ReadOnlySpan<ScevOper> opers) => opers.Contains(Oper);

    public bool TypeIs(var_types type) => Type == type;

    public bool GetConstantValue(Compiler comp, out long value)
    {
        if (this is ScevConstant constant)
        {
            value = constant.Value;
            return true;
        }

        if (this is ScevLocal local)
        {
            return local.GetConstantValue(comp, out value);
        }

        value = 0;
        return false;
    }

    public ScevVisit Visit(Func<Scev, ScevVisit> visitor)
    {
        if (visitor(this) is ScevVisit.Abort)
        {
            return ScevVisit.Abort;
        }

        switch (this)
        {
            case ScevConstant:
            case ScevLocal:
            {
                return ScevVisit.Continue;
            }

            case ScevBinop binop:
            {
                return (binop.Op1.Visit(visitor) is ScevVisit.Abort) ? ScevVisit.Abort : binop.Op2.Visit(visitor);
            }

            case ScevUnop unop:
            {
                return unop.Op1.Visit(visitor);
            }

            case ScevAddRec addRec:
            {
                return (addRec.Start.Visit(visitor) is ScevVisit.Abort) ? ScevVisit.Abort : addRec.Step.Visit(visitor);
            }

            default:
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    "RJS-SCE-003",
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? $"Unexpected scalar evolution operation {Oper}."
                        : null);
                return ScevVisit.Abort;
            }
        }
    }

    public bool IsInvariant() => Visit(node => node.Oper is ScevOper.AddRec ? ScevVisit.Abort : ScevVisit.Continue)
        is ScevVisit.Continue;

    public Scev PeelAdditions(out long offset)
    {
        offset = 0;
        var node = this;
        while (node is ScevBinop { Oper: ScevOper.Add } binop)
        {
            if (binop.Op1 is ScevConstant first)
            {
                offset = unchecked(offset + first.Value);
                node = binop.Op2;
            }
            else if (binop.Op2 is ScevConstant second)
            {
                offset = unchecked(offset + second.Value);
                node = binop.Op1;
            }
            else
            {
                break;
            }
        }

        return node;
    }

    public static bool Equals(Scev left, Scev right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if ((left.Oper != right.Oper) || (left.Type != right.Type))
        {
            return false;
        }

        switch (left, right)
        {
            case (ScevConstant l, ScevConstant r):
            {
                return l.Value == r.Value;
            }

            case (ScevLocal l, ScevLocal r):
            {
                return (l.LclNum == r.LclNum) && (l.SsaNum == r.SsaNum);
            }

            case (ScevBinop l, ScevBinop r):
            {
                return Equals(l.Op1, r.Op1) && Equals(l.Op2, r.Op2);
            }

            case (ScevUnop l, ScevUnop r):
            {
                return Equals(l.Op1, r.Op1);
            }

            case (ScevAddRec l, ScevAddRec r):
            {
                return Equals(l.Start, r.Start) && Equals(l.Step, r.Step);
            }

            default:
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    "RJS-SCE-003",
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? $"Unexpected scalar evolution operation {left.Oper}."
                        : null);
                return false;
            }
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public void Dump(Compiler comp)
    {
        switch (this)
        {
            case ScevConstant constant:
            {
                jitprintf(constant.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            }

            case ScevLocal local:
            {
                jitprintf($"V{local.LclNum:D2}.{local.SsaNum}");
                if (local.GetConstantValue(comp, out var value))
                {
                    jitprintf($" ({value})");
                }
                break;
            }

            case ScevBinop binop:
            {
                jitprintf("(");
                binop.Op1.Dump(comp);
                string operation;
                switch (binop.Oper)
                {
                    case ScevOper.Add:
                    {
                        operation = " + ";
                        break;
                    }

                    case ScevOper.Mul:
                    {
                        operation = " * ";
                        break;
                    }

                    case ScevOper.Lsh:
                    {
                        operation = " << ";
                        break;
                    }

                    default:
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(
                            "RJS-SCE-007",
                            Configuration.DETAILED_FATAL_ERROR_MESSAGES
                                ? $"Unexpected binary operation {binop.Oper}."
                                : null);
                        operation = default!;
                        break;
                    }
                }
                jitprintf(operation);
                binop.Op2.Dump(comp);
                jitprintf(")");
                break;
            }

            case ScevUnop unop:
            {
                jitprintf($"{(Oper is ScevOper.ZeroExtend ? 'z' : 's')}ext<{Type.Size * 8}>(");
                unop.Op1.Dump(comp);
                jitprintf(")");
                break;
            }

            case ScevAddRec addRec:
            {
                jitprintf($"<L{addRec.Loop.Index:D2}, ");
                addRec.Start.Dump(comp);
                jitprintf(", ");
                addRec.Step.Dump(comp);
                jitprintf(">");
                break;
            }

            default:
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    "RJS-SCE-003",
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? $"Unexpected scalar evolution operation {Oper}."
                        : null);
                return;
            }
        }
    }
}

public sealed class ScevConstant : Scev
{
    public long Value { get; }

    public ScevConstant(var_types type, long value) : base(ScevOper.Constant, type)
    {
        Value = type.Size == 4 ? unchecked((int)value) : value;
    }
}

public sealed class ScevLocal : Scev
{
    public int LclNum { get; }

    public int SsaNum { get; }

    public ScevLocal(var_types type, int lclNum, int ssaNum) : base(ScevOper.Local, type)
    {
        LclNum = lclNum;
        SsaNum = ssaNum;
    }

    public new bool GetConstantValue(Compiler comp, out long value)
    {
        ref readonly var definition = ref comp.lvaGetDesc(LclNum).GetPerSsaData(SsaNum);
        var defNode = definition.DefNode;
        if ((defNode is not null) && defNode.Data.Oper is GT_CNS_INT or GT_CNS_LNG)
        {
            value = defNode.Data.AsIntConCommon().IntegralValue;
            return true;
        }

        value = 0;
        return false;
    }
}

public class ScevUnop : Scev
{
    public Scev Op1 { get; }

    public ScevUnop(ScevOper oper, var_types type, Scev op1) : base(oper, type)
    {
        Op1 = op1;
    }
}

public sealed class ScevBinop : ScevUnop
{
    public Scev Op2 { get; }

    public ScevBinop(ScevOper oper, var_types type, Scev op1, Scev op2) : base(oper, type, op1)
    {
        Op2 = op2;
    }
}

public sealed class ScevAddRec : Scev
{
    private sealed class DebugData
    {
        internal FlowGraphNaturalLoop? Loop;
    }

    private static class DebugDataStorage
    {
        internal static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScevAddRec, DebugData> Table = [];
    }

    public Scev Start { get; }

    public Scev Step { get; }

    public FlowGraphNaturalLoop Loop
    {
        get
        {
            var loop = GetDebugData().Loop;
            if (loop is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(
                    "RJS-SCE-011",
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? "Scalar-evolution loop data is unavailable."
                        : null);
            }

            return loop;
        }
    }

    public ScevAddRec(var_types type, Scev start, Scev step, FlowGraphNaturalLoop loop)
        : base(ScevOper.AddRec, type)
    {
        Start = start;
        Step = step;
        if (Configuration.DEBUG)
        {
            DebugDataStorage.Table.GetValue(this, static _ => new DebugData()).Loop = loop;
        }
    }

    private DebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(
                "RJS-SCE-012",
                Configuration.DETAILED_FATAL_ERROR_MESSAGES
                    ? "Scalar-evolution debug data is only available in Debug builds."
                    : null);
        }

        return DebugDataStorage.Table.GetValue(this, static _ => new DebugData());
    }
}
