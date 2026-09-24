// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class Rationalizer
{
    private readonly record struct ParameterUse(GenTreeLclVarCommon? Node, BasicBlock Block);

    private sealed class ParameterUses
    {
        public readonly List<ParameterUse> Uses = [];
        public bool HasKills;
        public bool HasReads;
    }

    private bool ShouldRecordParameterUse(GenTree node)
    {
        assert(node.Oper is GT_LCL_FLD or GT_STORE_LCL_VAR or GT_STORE_LCL_FLD or GT_LCL_ADDR);
        var localNumber = node.AsLclVarCommon().LclNum;
        if (localNumber >= CompilerInstance.info.compArgsCount)
        {
            return false;
        }

        ref var parameter = ref CompilerInstance.lvaGetDesc(localNumber);
        if (parameter.lvPromoted || ((parameter.Type is not TYP_STRUCT) && !parameter.lvDoNotEnregister) ||
            !CompilerInstance.lvaGetParameterAbiInfo(localNumber).HasAnyRegisterSegment)
        {
            return false;
        }

        return (node.Oper is not GT_LCL_FLD) || (node.Type is not TYP_STRUCT);
    }

    private void RecordParameterUse(GenTree node)
    {
        assert(_parameterUses is not null);
        if (!ShouldRecordParameterUse(node))
        {
            return;
        }

        assert(_block is not null);
        var local = node.AsLclVarCommon();
        var uses = _parameterUses[local.LclNum] ??= new ParameterUses();
        uses.Uses.Add(new(local, _block));
        uses.HasKills |= node.Oper is not GT_LCL_FLD;
        uses.HasReads |= node.Oper is GT_LCL_FLD;
    }

    private void ForgetParameterUses(LIR.ReadOnlyRange range)
    {
        if (_parameterUses is null)
        {
            return;
        }

        foreach (var node in range)
        {
            if ((node.Oper is not GT_LCL_FLD and not GT_LCL_ADDR) || !ShouldRecordParameterUse(node))
            {
                continue;
            }

            var uses = _parameterUses[node.AsLclVarCommon().LclNum];
            assert(uses is not null);
            var found = false;
            for (var index = uses.Uses.Count - 1; index >= 0; index--)
            {
                var use = uses.Uses[index];
                if (use.Node == node)
                {
                    uses.Uses[index] = use with { Node = null };
                    found = true;
                    break;
                }
            }
            assert(found);
        }
    }

    private void RewriteParameterUses()
    {
        assert(_parameterUses is not null);
        var traits = new BitVecTraits(CompilerInstance, CompilerInstance.fgBBNumMax + 1);
        var killedOnEntry = BitVecOps.UninitVal();
        var haveKilledSet = false;
        var worklist = new Stack<BasicBlock>();
        foreach (var uses in _parameterUses)
        {
            if ((uses is null) || !uses.HasReads)
            {
                continue;
            }

            if (uses.HasKills)
            {
                if (!haveKilledSet)
                {
                    killedOnEntry = BitVecOps.MakeEmpty(traits);
                    haveKilledSet = true;
                }
                else
                {
                    BitVecOps.ClearD(traits, killedOnEntry);
                }

                BasicBlockVisit QueueSuccessor(BasicBlock successor)
                {
                    if (BitVecOps.TryAddElemD(traits, killedOnEntry, successor.bbNum))
                    {
                        worklist.Push(successor);
                    }
                    return BasicBlockVisit.Continue;
                }

                // Include exceptional edges and backedges: a kill on any
                // reaching path invalidates the incoming register value.
                BasicBlock? lastKillBlock = null;
                foreach (var use in uses.Uses)
                {
                    if ((use.Node is not null) && (use.Node.Oper is not GT_LCL_FLD) && (use.Block != lastKillBlock))
                    {
                        foreach (var successor in CompilerInstance.fgGetAllSuccessors(use.Block))
                        {
                            _ = QueueSuccessor(successor);
                        }
                        lastKillBlock = use.Block;
                    }
                }
                while (worklist.TryPop(out var block))
                {
                    foreach (var successor in CompilerInstance.fgGetAllSuccessors(block))
                    {
                        _ = QueueSuccessor(successor);
                    }
                }
            }

            BasicBlock? currentBlock = null;
            var killed = false;
            foreach (var use in uses.Uses)
            {
                if (use.Node is null)
                {
                    continue;
                }
                if (use.Block != currentBlock)
                {
                    currentBlock = use.Block;
                    killed = uses.HasKills && BitVecOps.IsMember(traits, killedOnEntry, currentBlock.bbNum);
                }
                if (use.Node.Oper is not GT_LCL_FLD)
                {
                    killed = true;
                }
                else if (!killed)
                {
                    assert(currentBlock is not null);
                    RewriteParameterField(currentBlock, use.Node.AsLclFld());
                }
            }
        }
    }

    private void RewriteParameterField(BasicBlock block, GenTreeLclFld field)
    {
        CompilerInstance.compCurBB = block;
        var abiInfo = CompilerInstance.lvaGetParameterAbiInfo(field.LclNum);
        AbiPassingSegment? registerSegment = null;
        foreach (var segment in abiInfo.Segments)
        {
            if (!segment.IsPassedInRegister)
            {
                continue;
            }
            assert(field.LclOffs <= CompilerInstance.lvaLclExactSize(field.LclNum));
            var accessedSize = Math.Min(field.Type.Size, CompilerInstance.lvaLclExactSize(field.LclNum) - field.LclOffs);
            if ((field.LclOffs < segment.Offset) || (field.LclOffs + accessedSize > segment.Offset + segment.Size))
            {
                continue;
            }
            if (genIsValidFloatReg(segment.Register) && (!varTypeUsesFloatReg(field.Type) || (field.LclOffs != segment.Offset)))
            {
                continue;
            }
            registerSegment = segment;
            break;
        }
        if (registerSegment is not AbiPassingSegment selected)
        {
            return;
        }

#if DEBUG
        JITDUMP($"LCL_FLD use [{field.TreeId:D6}] in {FMT_BB(block.bbNum)} of parameter V{field.LclNum:D2} is contained in ");
        if (CompilerInstance.verbose)
        {
            selected.Dump();
        }
        JITDUMP("\n");
#endif
        if (!block.TryGetUse(field, out var use))
        {
            JITDUMP("  ..but no use was found\n");
            return;
        }

        CompilerInstance._paramRegLocalMappings ??= [];
        var mapping = CompilerInstance.FindParameterRegisterLocalMappingByRegister(selected.Register);
        int remappedLocal;
        if (mapping is not ParameterRegisterLocalMapping existing)
        {
            if (!CompilerInstance.lvaGetDesc(field.LclNum).lvDoNotEnregister)
            {
                CompilerInstance.lvaSetVarDoNotEnregister(field.LclNum, DoNotEnregisterReason.LocalField);
            }
            remappedLocal = CompilerInstance.lvaGrabTemp(false, $"V{field.LclNum:D2}.{selected.Register.Name}");
#if TARGET_WASM
            var fullWidthType = selected.GetRegisterType().ActualType;
#else
            var fullWidthType = TYP_I_IMPL;
#endif
            var registerType = genIsValidIntReg(selected.Register) ? fullWidthType : selected.GetRegisterType();
            if ((registerType is TYP_I_IMPL) && varTypeIsGC(field.Type))
            {
                registerType = field.Type;
            }
            ref var descriptor = ref CompilerInstance.lvaGetDesc(remappedLocal);
            descriptor.Type = registerType.ActualType;
            JITDUMP($"Created new local V{remappedLocal:D2} for the mapping\n");
            CompilerInstance._paramRegLocalMappings.Add(new(selected, remappedLocal, 0));
            descriptor.lvIsParamRegTarget = true;
            JITDUMP("New mapping: ");
#if DEBUG
            if (CompilerInstance.verbose)
            {
                selected.Dump();
            }
#endif
            JITDUMP($" -> V{remappedLocal:D2}\n");
        }
        else
        {
            remappedLocal = existing.LclNum;
        }

        GenTree value = CompilerInstance.gtNewLclvNode(CompilerInstance.lvaGetDesc(remappedLocal).Type, remappedLocal);
#if TARGET_WASM
        if (varTypeIsSimd(value.Type) && !varTypeIsSimd(field.Type))
        {
            var offset = field.LclOffs - selected.Offset;
            assert((offset % field.Type.Size) == 0);
            value = CompilerInstance.gtNewSimdGetElementNode(field.Type, value,
                CompilerInstance.gtNewIconNode(TYP_INT, offset / field.Type.Size), field.Type, value.Type.Size);
        }
        else
#endif
        if (varTypeUsesFloatReg(value.Type))
        {
            assert(field.LclOffs == selected.Offset);
            value.Type = field.Type;
#if FEATURE_SIMD
            if (value.Type is TYP_SIMD12)
            {
                value.Type = TYP_SIMD16;
            }
#endif
        }
        else
        {
            var registerType = value.Type;
            if (field.LclOffs > selected.Offset)
            {
                assert(value.Type is TYP_INT or TYP_LONG);
                var amount = CompilerInstance.gtNewIconNode(TYP_INT, (field.LclOffs - selected.Offset) * 8);
                value = CompilerInstance.gtNewBinaryNode(varTypeIsSmall(field.Type) && varTypeIsSigned(field.Type) ? GT_RSH : GT_RSZ,
                    value.Type, value, amount);
            }
            if (varTypeIsSmall(field.Type) && (selected.Offset + field.Type.Size != registerType.Size))
            {
                value = CompilerInstance.gtNewCastNode(TYP_INT, value, false, field.Type);
            }
            if (value.Type.Size != field.Type.ActualType.Size)
            {
                assert((value.Type.Size == 8) && (field.Type.ActualType.Size == 4));
                if (value.Oper.IsScalarLocal)
                {
                    value.Type = TYP_INT;
                }
                else
                {
                    value = CompilerInstance.gtNewCastNode(TYP_INT, value, false, TYP_INT);
                }
            }
            if (value.Type != field.Type.ActualType)
            {
                value = CompilerInstance.gtNewBitCastNode(field.Type.ActualType, value);
            }
        }

        CompilerInstance.gtSetEvalOrder(value);
        block.InsertAfter(field, new LIR.Range(CompilerInstance.fgSetTreeSeq(value, isLIR: true), value));
        use.ReplaceWith(value);
        JITDUMP("New user tree range:\n");
        DISPTREERANGE(block, use.User());
        block.Remove(field);
    }
}
