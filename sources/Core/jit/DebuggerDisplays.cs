// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if DEBUG
using System.Globalization;
#endif
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

#if DEBUG
[DebuggerDisplay("[{info.compFullName} ({DebuggerMethodHash})]")]
public partial class Compiler
{
    private string DebuggerMethodHash => unchecked((uint)info.compMethodHash()).ToString("x8", CultureInfo.InvariantCulture);
}

[DebuggerTypeProxy(typeof(StatementDebuggerProxy))]
public sealed partial class Statement
{
}

internal sealed class StatementDebuggerProxy
{
    private readonly Statement _statement;

    public StatementDebuggerProxy(Statement statement)
    {
        _statement = statement;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public GenTree[] Trees
    {
        get
        {
            var trees = new List<GenTree>();
            foreach (var tree in _statement.TreeList)
            {
                trees.Add(tree);
            }

            return [.. trees];
        }
    }
}
#endif

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public partial class BasicBlock
{
    private string DebuggerDisplayText => Kind switch
    {
        BBJ_ALWAYS or BBJ_LEAVE or BBJ_EHCATCHRET or BBJ_CALLFINALLY or BBJ_CALLFINALLYRET or BBJ_EHFILTERRET
            when _anonymous1 is FlowEdge targetEdge =>
                $"BB{bbNum}->BB{targetEdge.DestinationBlock.bbNum}; {Kind}",
        BBJ_COND when _anonymous1 is FlowEdge trueEdge && bbFalseEdge is FlowEdge falseEdge =>
            $"BB{bbNum}-> (BB{trueEdge.DestinationBlock.bbNum}(T),BB{falseEdge.DestinationBlock.bbNum}(F)) ; {Kind}",
        BBJ_SWITCH when _anonymous1 is BBswtDesc switchTargets =>
            $"BB{bbNum}; {Kind}; {switchTargets.Cases.Length} cases",
        BBJ_EHFINALLYRET when bbEhfTargets is not null =>
            $"BB{bbNum}; {Kind}; {bbEhfTargets.Succs.Length} succs",
        _ => $"BB{bbNum}; {Kind}",
    };
}

#if DEBUG
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed partial class FlowEdge
{
    private string DebuggerDisplayText
    {
        get
        {
            var display = $"BB{_sourceBlock.bbNum}->BB{_destBlock.bbNum} ({_likelihood:g})";
            return (_dupCount is not 1) ? $"{display} (dup {_dupCount})" : display;
        }
    }
}

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
[DebuggerTypeProxy(typeof(FlowGraphNaturalLoopDebuggerProxy))]
public sealed partial class FlowGraphNaturalLoop
{
    private string DebuggerDisplayText
    {
        get
        {
            var top = GetLexicallyTopMostBlock();
            var bottom = GetLexicallyBottomMostBlock();
            var preheader = GetPreheader();
            var range = $"[BB{top.bbNum}..BB{bottom.bbNum}]";
            var header = $"h:BB{Header.bbNum}";
            var preheaderText = preheader is null ? "" : $" pre-h:BB{preheader.bbNum}";

            return $"{range}{preheaderText} {header} entries={EntryEdges.Length}";
        }
    }
}

[DebuggerTypeProxy(typeof(FlowGraphNaturalLoopsDebuggerProxy))]
public sealed partial class FlowGraphNaturalLoops
{
}

internal sealed class FlowGraphNaturalLoopDebuggerProxy
{
    private readonly FlowGraphNaturalLoop _loop;

    public FlowGraphNaturalLoopDebuggerProxy(FlowGraphNaturalLoop loop)
    {
        _loop = loop;
    }

    public BasicBlock Header => _loop.Header;

    public FlowGraphNaturalLoop? Parent => _loop.Parent;

    public FlowGraphNaturalLoop? Child => _loop.Child;

    public FlowGraphNaturalLoop? Sibling => _loop.Sibling;

    public FlowEdge[] BackEdges => [.. _loop.BackEdges];

    public FlowEdge[] EntryEdges => [.. _loop.EntryEdges];

    public FlowEdge[] ExitEdges => [.. _loop.ExitEdges];

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public BasicBlock[] Blocks
    {
        get
        {
            var blocks = new BasicBlock[_loop.NumLoopBlocks()];
            var index = 0;
            _ = _loop.VisitLoopBlocks(block =>
            {
                blocks[index++] = block;
                return BasicBlockVisit.Continue;
            });
            return blocks;
        }
    }
}

internal sealed class FlowGraphNaturalLoopsDebuggerProxy
{
    private readonly FlowGraphNaturalLoops _loops;

    public FlowGraphNaturalLoopsDebuggerProxy(FlowGraphNaturalLoops loops)
    {
        _loops = loops;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public FlowGraphNaturalLoop[] Loops => [.. _loops.InReversePostOrder()];
}
#endif

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public partial class GenTree
{
    private string DebuggerDisplayText
    {
        get
        {
            var valueNumbers = $"VNP=[L: {_vnPair.Liberal:x}, C: {_vnPair.Conservative:x}]";

#if DEBUG
            var prefix = $"{TreeId}: ";
#else
            var prefix = "";
#endif

            if (Oper is GT_CNS_STR)
            {
                return $"CNS_STR, {valueNumbers}";
            }

#if FEATURE_SIMD
            if (Oper is GT_CNS_VEC)
            {
                return $"CNS_VEC, {valueNumbers}";
            }
#endif

            if (this is GenTreeIntConCommon integerConstant)
            {
                if (Oper is GT_CNS_LNG)
                {
                    return $"{prefix}[LngCon={integerConstant.IntegralValue}], {valueNumbers}";
                }

                return $"{prefix}[IntCon={integerConstant.IconValue}], {valueNumbers}";
            }

            if (this is GenTreeDblCon doubleConstant)
            {
                return $"{prefix}[DblCon={doubleConstant.DconVal:g}], {valueNumbers}";
            }

            if (this is GenTreeLclVarCommon local)
            {
                if (this is GenTreeLclFld localField)
                {
                    return $"{prefix}[{Oper.Name}, {Type.Name} V{local.LclNum}[+{localField.LclOffs}]], {valueNumbers}";
                }

                return $"{prefix}[{Oper.Name}, {Type.Name} V{local.LclNum}], {valueNumbers}";
            }

            if (this is GenTreeCast cast)
            {
                return $"{prefix}[{cast.CastType.Name} <- {cast.CastOp.Type.Name}], {valueNumbers}";
            }

#if FEATURE_HW_INTRINSICS
            if (this is GenTreeHWIntrinsic intrinsic)
            {
                return $"{prefix}[{intrinsic.HWIntrinsicId}, {Type.Name}], {valueNumbers}";
            }
#endif

            return $"{prefix}[{Oper.Name}, {Type.Name}], {valueNumbers}";
        }
    }
}

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public partial struct LclVarDsc
{
#if DEBUG
    private readonly string DebuggerDisplayText => string.IsNullOrEmpty(lvReason)
        ? $"[V{lvSlotNum}: {Type.Name}]"
        : $"[V{lvSlotNum}: {Type.Name}-{lvReason}]";
#else
    private readonly string DebuggerDisplayText => $"[V{lvSlotNum}: {Type.Name}]";
#endif
}

#if DEBUG
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed partial class Interval
{
    private string DebuggerDisplayText
    {
        get
        {
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
            if (isUpperVector)
            {
                return $"[U{relatedInterval?.varNum ?? varNum}, #{intervalIndex}, reg={physReg}]";
            }
#endif

            if (isLocalVar)
            {
                return $"[V{varNum}, #{intervalIndex}, reg={physReg}]";
            }

            if (isConstant)
            {
                return $"[C{intervalIndex}, reg={physReg}]";
            }

            return $"[I{intervalIndex}, reg={physReg}]";
        }
    }
}

[DebuggerDisplay("[#{rpNum} - {refType}]")]
[DebuggerTypeProxy(typeof(RefPositionDebuggerProxy))]
public sealed partial class RefPosition
{
}

public partial class Emitter
{
    [DebuggerTypeProxy(typeof(DebuggerProxy))]
    public abstract partial class instrDesc
    {
        [DebuggerDisplay("{Display,nq}")]
        public sealed class DebuggerProxy
        {
            private readonly instrDesc _descriptor;

            public DebuggerProxy(instrDesc descriptor)
            {
                _descriptor = descriptor;
            }

            private string Display
            {
                get
                {
#if TARGET_XARCH
                    var instruction = _descriptor.idIns();
                    var register = _descriptor.idReg1();
                    var format = _descriptor.idInsFmt();

                    if (format is IF_RRD or IF_RWR or IF_RRW)
                    {
                        return $"{instruction} {register}";
                    }

                    if (format is IF_RRD_CNS or IF_RWR_CNS or IF_RRW_CNS)
                    {
                        return $"{instruction} {register}, {GetConstant()}";
                    }

                    if (format is IF_RRW_SHF)
                    {
                        return $"{instruction} {register}, {GetConstant()}";
                    }
#endif
                    return _descriptor.idIns().ToString();
                }
            }

            private nint GetConstant()
            {
#if TARGET_ARM
                return _descriptor.idIsLargeCns()
                    ? ((instrDescCns)_descriptor).idcCnsVal
                    : _descriptor.idSmallCns();
#else
                return emitGetInsSC(_descriptor);
#endif
            }
        }
    }
}
#endif

#if DEBUG
[DebuggerDisplay("LinearScan")]
[DebuggerTypeProxy(typeof(LinearScan.DebuggerProxy))]
public sealed partial class LinearScan
{
    internal sealed class DebuggerProxy
    {
        private readonly LinearScan _linearScan;

        public DebuggerProxy(LinearScan linearScan)
        {
            _linearScan = linearScan;
        }

        public DebuggerVarMapBlock[] InVarToRegMaps => GetVarToRegMaps(_linearScan._inVarToRegMaps);

        public DebuggerVarMapBlock[] OutVarToRegMaps => GetVarToRegMaps(_linearScan._outVarToRegMaps);

        public DebuggerRegisterSet[] AvailableRegs
        {
            get
            {
                var sets = new DebuggerRegisterSet[(int)TYP_COUNT];
                for (var index = 0; index < sets.Length; index++)
                {
                    var type = (var_types)index;
                    var registerMask = DebuggerDisplayHelpers.GetRegisterMask(
                        _linearScan._availableRegs[index],
                        type);
                    sets[index] = new DebuggerRegisterSet(
                        $"AvailableRegs[{type}]",
                        DebuggerDisplayHelpers.GetRegisters(registerMask));
                }

                return sets;
            }
        }

        public DebuggerRegisterSet RegistersWithConstants => new(
            "RegistersWithConstants",
            DebuggerDisplayHelpers.GetRegisters(_linearScan._registersWithConstants));

        private DebuggerVarMapBlock[] GetVarToRegMaps(regNumber[]?[]? maps)
        {
            if (maps is null)
            {
                return [];
            }

            var blocks = new List<DebuggerVarMapBlock>();
            for (var block = _linearScan._compiler.fgFirstBB; block is not null; block = block.Next)
            {
                var blockNumber = checked((int)block.bbNum);
                if ((uint)blockNumber >= (uint)maps.Length || maps[blockNumber] is not regNumber[] map)
                {
                    continue;
                }

                var variables = new List<DebuggerVarReg>();
                for (var index = 0; index < map.Length; index++)
                {
                    if (!VarSetOps.IsMember(_linearScan._compiler, block.bbLiveIn, index))
                    {
                        continue;
                    }

                    var interval = _linearScan.localVarIntervals is Interval?[] intervals &&
                        (uint)index < (uint)intervals.Length
                            ? intervals[index]
                            : null;
                    var variableNumber = interval is Interval liveInterval
                        ? liveInterval.varNum
                        : checked((uint)index);
                    variables.Add(new DebuggerVarReg(variableNumber, map[index]));
                }

                blocks.Add(new DebuggerVarMapBlock(blockNumber, [.. variables]));
            }

            return [.. blocks];
        }
    }
}

[DebuggerDisplay("{Name,nq}")]
internal sealed class DebuggerRegisterSet
{
    public DebuggerRegisterSet(string name, regNumber[] registers)
    {
        Name = name;
        Registers = registers;
    }

    public string Name { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public regNumber[] Registers { get; }
}

[DebuggerDisplay("---BB{BlockNumber,2}---")]
internal sealed class DebuggerVarMapBlock
{
    public DebuggerVarMapBlock(int blockNumber, DebuggerVarReg[] variables)
    {
        BlockNumber = blockNumber;
        Variables = variables;
    }

    public int BlockNumber { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public DebuggerVarReg[] Variables { get; }
}

[DebuggerDisplay("V{VariableNumber,2}: {Register}")]
internal readonly struct DebuggerVarReg
{
    public DebuggerVarReg(uint variableNumber, regNumber register)
    {
        VariableNumber = variableNumber;
        Register = register;
    }

    public uint VariableNumber { get; }

    public regNumber Register { get; }
}

internal static class DebuggerDisplayHelpers
{
    public static regMaskTP GetRegisterMask(SingleTypeRegSet registers, var_types type)
    {
        var mask = default(regMaskTP);
        regMaskTP.AddRegsetForType(ref mask, registers, type);
        return mask;
    }

    public static regNumber[] GetRegisters(regMaskTP mask)
    {
        var registers = new List<regNumber>();
        for (var index = 0; index < (int)REG_COUNT; index++)
        {
            var register = (regNumber)index;
            if (mask.IsSet(register))
            {
                registers.Add(register);
            }
        }

        return [.. registers];
    }
}

internal sealed class RefPositionDebuggerProxy
{
    private readonly RefPosition _reference;

    public RefPositionDebuggerProxy(RefPosition reference)
    {
        _reference = reference;
    }

    public Referenceable? Referent => _reference.referent;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public regNumber[] Registers
    {
        get
        {
            if (_reference.registerAssignment is SRBM_NONE)
            {
                return [];
            }

            var referent = _reference.referent
                ?? throw new InvalidOperationException("A non-empty register assignment requires a referent.");

            return DebuggerDisplayHelpers.GetRegisters(
                DebuggerDisplayHelpers.GetRegisterMask(_reference.registerAssignment, referent.registerType));
        }
    }
}

internal sealed class RegRecordDebuggerProxy
{
    private readonly RegRecord _record;

    public RegRecordDebuggerProxy(RegRecord record)
    {
        _record = record;
    }

    public Interval? Assigned => _record.assignedInterval;

    public Interval? Previous => _record.previousInterval;
}
#endif

#if DEBUG
[DebuggerTypeProxy(typeof(RegRecordDebuggerProxy))]
public sealed partial class RegRecord
{
}
#endif

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed partial class insGroup
{
    private string DebuggerDisplayText
    {
        get
        {
            var display = $"IG{GetDisplayId()}, size={igSize}, offset={igOffs}";
            return ((igFlags & InsGroupFlags.Extend) != 0) ? $"{display} [extend]" : display;
        }
    }
}
