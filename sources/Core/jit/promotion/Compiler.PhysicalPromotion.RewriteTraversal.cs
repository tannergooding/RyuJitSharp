// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotion.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed unsafe partial class PhysicalPromotionReplaceVisitor
    {
    private struct PhysicalPromotionTreeWalker(
        PhysicalPromotionReplaceVisitor replacer, bool prepass) : IGenTreeVisitor<PhysicalPromotionTreeWalker>
    {
        private readonly GenTreeStack _ancestors = [];

        public static bool DoPreOrder => true;
        public static bool DoPostOrder => true;
        public static bool UseExecutionOrder => true;
        public static bool ComputeStack => true;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<PhysicalPromotionTreeWalker>.WalkTree(ref this, ref use, user, _ancestors);

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (!prepass)
            {
                return WALK_CONTINUE;
            }

            if ((use.Flags & GTF_CALL) == 0)
            {
                return WALK_SKIP_SUBTREES;
            }

            if (use is GenTreeCall call)
            {
                foreach (var argument in call.Args.Args)
                {
                    var value = argument.Node.EffectiveVal;
                    if ((value.Type is not TYP_STRUCT) || !value.Oper.IsLocalRead)
                    {
                        continue;
                    }

                    var local = value.AsLclVarCommon();
                    if (replacer._aggregates.Lookup(local.LclNum) is null)
                    {
                        continue;
                    }

                    if (!call.Args.IsAbiInformationDetermined)
                    {
                        call.Args.DetermineAbiInfo(replacer._compiler, call);
                    }

                    if (!argument.AbiInfo.HasAnyStackSegment && !argument.AbiInfo.IsPassedByReference)
                    {
                        continue;
                    }

                    var __jitExceptionReceiver596 = replacer;
                    var __jitExceptionArgument597 = local.LclNum;
                    var __jitExceptionArgument598 = local.LclOffs;
                    int __jitExceptionArgument599;
                    int __jitExceptionOperand600;
                    uint __jitExceptionOperand601;
                    global::RyuJitSharp.ClassLayout __jitExceptionOperand602;
                    var __jitExceptionCoalesceLeft603 = local.GetLayout(replacer._compiler);
                    if (__jitExceptionCoalesceLeft603 != null)
                    {
                        __jitExceptionOperand602 = __jitExceptionCoalesceLeft603;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.StructArgumentNeedsALayout);
                        __jitExceptionOperand602 = default;
                    }
                    __jitExceptionOperand601 = __jitExceptionOperand602.Size;
                    __jitExceptionOperand600 = (int)__jitExceptionOperand601;
                    __jitExceptionArgument599 = checked(__jitExceptionOperand600);
                    __jitExceptionReceiver596.WriteBackBeforeCurrentStatement(__jitExceptionArgument597, __jitExceptionArgument598, __jitExceptionArgument599);
                }
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            if (prepass)
            {
                return WALK_CONTINUE;
            }

            var readBacks = replacer.InsertMidTreeReadBacks(use);
            var readBackType = (readBacks is not null) && use.IsValue ? use.Type : TYP_VOID;
            if (use.Oper.IsStore)
            {
                if (use.Type is TYP_STRUCT)
                {
                    replacer.HandleStructStore(ref use);
                }
                else if (use.Oper.IsLocalStore)
                {
                    replacer.ReplaceLocal(ref use, user, EffectiveUser(user));
                }
            }
            else if (use is GenTreeCall call)
            {
                replacer.ReadBackAfterCall(call);
            }
            else if (use.Oper is GT_LCL_VAR or GT_LCL_FLD)
            {
                replacer.ReplaceLocal(ref use, user, EffectiveUser(user));
            }

            if (readBacks is not null)
            {
                use = readBacks.PrefixTo(use, replacer._compiler, readBackType);
            }

            return WALK_CONTINUE;
        }

        private readonly GenTree? EffectiveUser(GenTree? user)
        {
            if (user?.Oper is not GT_COMMA)
            {
                return user;
            }

            var ancestors = _ancestors.ToArray();
            for (var index = 1; index < ancestors.Length; index++)
            {
                var parent = ancestors[index];
                var child = ancestors[index - 1];
                if (parent.Oper is not GT_COMMA)
                {
                    return parent;
                }

                if (ReferenceEquals(parent.AsOp().Op1, child))
                {
                    return null;
                }
            }

            return null;
        }
    }

        public void WalkTree(ref GenTree tree)
        {
            var walker = new PhysicalPromotionTreeWalker(this, prepass: false);
            _ = walker.WalkTree(ref tree, null);
        }

        private PhysicalPromotionDecompositionStatementList? InsertMidTreeReadBacks(GenTree use)
        {
            if ((_pendingReadBacks == 0) || !_compiler.ehBlockHasExnFlowDsc(CurrentBlock) ||
                ((use.Flags & (GTF_EXCEPT | GTF_CALL)) == 0) || !use.MayThrow(_compiler))
            {
                return null;
            }

            JITDUMP("Reading back pending replacements before tree with possible exception side effect " +
                "inside block in try region\n");
            var readBacks = new PhysicalPromotionDecompositionStatementList();
            foreach (var aggregate in _aggregates.Aggregates)
            {
                foreach (var replacement in aggregate.Replacements)
                {
                    if (replacement.NeedsReadBack)
                    {
                        JITDUMP($"  V{aggregate.LclNum:D2}.[{replacement.Offset:D3}.." +
                            $"{replacement.Offset + replacement.AccessType.Size:D3}) -> " +
                            $"V{replacement.LclNum:D2}\n");
                        ClearNeedsReadBack(replacement);
                        var read = _compiler.PhysicalPromotionCreateReadBack(aggregate.LclNum, replacement);
                        readBacks.AddStatement(read);
                        _madeChanges = true;
                    }
                }
            }

            assert(_pendingReadBacks == 0);
            return readBacks.Count == 0 ? null : readBacks;
        }

        private void ReadBackAfterCall(GenTreeCall call)
        {
            if (!call.IsOptimizingRetBufAsLocal)
            {
                return;
            }

            global::RyuJitSharp.CallArg __jitExceptionResult135;
            var __jitExceptionCoalesceLeft136 = call.Args.RetBufferArg;
            if (__jitExceptionCoalesceLeft136 != null)
            {
                __jitExceptionResult135 = __jitExceptionCoalesceLeft136;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.OptimizedRetbufferCallNeedsARetbufferArgument);
                __jitExceptionResult135 = default;
            }
            var argument = __jitExceptionResult135;
            var retBuffer = argument.Node.AsLclVarCommon();
            var size = checked((int)_compiler.typGetObjLayout(call.RetClsHnd).Size);
            MarkForReadBack(retBuffer, size, "used as retbuf");
        }

        private GenTreeFieldList? CreateFieldListForStructLocal(GenTreeLclVarCommon local)
        {
            var aggregate = _aggregates.Lookup(local.LclNum);
            if (aggregate is null)
            {
                return null;
            }

            int __jitExceptionResult604;
            int __jitExceptionOperand605;
            uint __jitExceptionOperand606;
            global::RyuJitSharp.ClassLayout __jitExceptionOperand607;
            var __jitExceptionCoalesceLeft608 = local.GetLayout(_compiler);
            if (__jitExceptionCoalesceLeft608 != null)
            {
                __jitExceptionOperand607 = __jitExceptionCoalesceLeft608;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.StructLocalNeedsALayout);
                __jitExceptionOperand607 = default;
            }
            __jitExceptionOperand606 = __jitExceptionOperand607.Size;
            __jitExceptionOperand605 = (int)__jitExceptionOperand606;
            __jitExceptionResult604 = checked(__jitExceptionOperand605);
            var size = __jitExceptionResult604;
            var start = local.LclOffs;
            if (aggregate.Unpromoted.Intersects(new SegmentList.Segment(start, start + size)) ||
                !aggregate.OverlappingReplacements(start, size, out var first, out var end))
            {
                return null;
            }

            for (var index = first; index < end; index++)
            {
                var replacement = aggregate.Replacements[index];
                if ((replacement.Offset < start) || (replacement.Offset + replacement.AccessType.Size > start + size))
                {
                    return null;
                }
            }

            var deaths = _liveness.GetDeathsForStructLocal(local);
            var fields = new GenTreeFieldList();
            for (var index = first; index < end; index++)
            {
                var replacement = aggregate.Replacements[index];
                GenTree value;
                if (replacement.NeedsReadBack)
                {
                    value = _compiler.gtNewLclFldNode(replacement.AccessType, local.LclNum,
                        checked((ushort)replacement.Offset));
                    if (!_compiler.lvaGetDesc(local.LclNum).lvDoNotEnregister)
                    {
                        _compiler.lvaSetVarDoNotEnregister(local.LclNum, DoNotEnregisterReason.LocalField);
                    }
                }
                else
                {
                    value = _compiler.gtNewLclvNode(replacement.AccessType, replacement.LclNum);
                    if (deaths.IsReplacementDying(index))
                    {
                        value.Flags |= GTF_VAR_DEATH;
                        CheckForwardSubForLastUse(replacement.LclNum);
                    }
                }

                fields.AddField(_compiler, value, checked((ushort)(replacement.Offset - start)),
                    replacement.AccessType);
            }

            return fields;
        }

        private bool ReplaceStructLocal(ref GenTree use, GenTree user, GenTreeLclVarCommon local)
        {
            if (user is GenTreeCall call)
            {
                var argument = call.Args.FindByNode(local);
                if (argument is null)
                {
                    return false;
                }

                if (!call.Args.IsAbiInformationDetermined)
                {
                    call.Args.DetermineAbiInfo(_compiler, call);
                }

                if (argument.AbiInfo.HasAnyStackSegment || argument.AbiInfo.IsPassedByReference)
                {
                    return false;
                }
            }
            else
            {
                assert(user.Oper is GT_RETURN or GT_SWIFT_ERROR_RET);
                if (_compiler.genReturnLocal != BAD_VAR_NUM)
                {
                    JITDUMP("Replacing merged return by store to merged return local\n");
                    GenTree? effects = null;
                    _compiler.gtExtractSideEffList(user, ref effects, GTF_SIDE_EFFECT, ignoreRoot: true);
                    CurrentStatement.RootNode = effects ?? _compiler.gtNewNothingNode();
                    DISPSTMT(CurrentStatement);
                    _madeChanges = true;

                    var store = _compiler.gtNewStoreLclVarNode(_compiler.genReturnLocal, local);
                    var storeStatement = _compiler.fgNewStmtFromTree(store);
                    _compiler.fgInsertStmtAfter(CurrentBlock, CurrentStatement, storeStatement);
                    DISPSTMT(storeStatement);
                    user.AsUnOp().Op1Ref = _compiler.gtNewLclVarNode(TYP_STRUCT, _compiler.genReturnLocal);
                    var returnStatement = _compiler.fgNewStmtFromTree(user);
                    _compiler.fgInsertStmtAfter(CurrentBlock, storeStatement, returnStatement);
                    DISPSTMT(returnStatement);
                    return true;
                }
            }

            var fields = CreateFieldListForStructLocal(local);
            if (fields is null)
            {
                return false;
            }

            use = fields;
            _madeChanges = true;
            return true;
        }

        private void ReplaceLocal(ref GenTree use, GenTree? user, GenTree? effectiveUser)
        {
            var local = use.AsLclVarCommon();
            var aggregate = _aggregates.Lookup(local.LclNum);
            if (aggregate is null)
            {
                return;
            }

            if (local.Type is TYP_STRUCT)
            {
                assert(local.Oper.IsLocalRead);
                if ((effectiveUser is null) || effectiveUser.Oper.IsStore)
                {
                    return;
                }

                if (Configuration.DEBUG)
                {
                    string __jitExceptionArgument609;
                    var __jitExceptionOperand610 = $"Processing struct use [{local.TreeId:D6}] of V{local.LclNum:D2}.";
                    string __jitExceptionOperand611;
                    var __jitExceptionOperand612 = local.LclOffs;
                    uint __jitExceptionOperand613;
                    var __jitExceptionOperand614 = local.LclOffs;
                    uint __jitExceptionOperand615;
                    global::RyuJitSharp.ClassLayout __jitExceptionOperand616;
                    var __jitExceptionCoalesceLeft617 = local.GetLayout(_compiler);
                    if (__jitExceptionCoalesceLeft617 != null)
                    {
                        __jitExceptionOperand616 = __jitExceptionCoalesceLeft617;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.StructUseNeedsALayout);
                        __jitExceptionOperand616 = default;
                    }

                    __jitExceptionOperand615 = __jitExceptionOperand616.Size;
                    __jitExceptionOperand613 = __jitExceptionOperand614 + __jitExceptionOperand615;
                    __jitExceptionOperand611 = $"[{__jitExceptionOperand612:D3}..{__jitExceptionOperand613:D3})\n";
                    __jitExceptionArgument609 = __jitExceptionOperand610 + __jitExceptionOperand611;
                    JITDUMP(__jitExceptionArgument609);
                }
                if (!ReplaceStructLocal(ref use, effectiveUser, local))
                {
                    int __jitExceptionResult618;
                    int __jitExceptionOperand619;
                    uint __jitExceptionOperand620;
                    global::RyuJitSharp.ClassLayout __jitExceptionOperand621;
                    var __jitExceptionCoalesceLeft622 = local.GetLayout(_compiler);
                    if (__jitExceptionCoalesceLeft622 != null)
                    {
                        __jitExceptionOperand621 = __jitExceptionCoalesceLeft622;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.StructUseNeedsALayout);
                        __jitExceptionOperand621 = default;
                    }
                    __jitExceptionOperand620 = __jitExceptionOperand621.Size;
                    __jitExceptionOperand619 = (int)__jitExceptionOperand620;
                    __jitExceptionResult618 = checked(__jitExceptionOperand619);
                    var size = __jitExceptionResult618;
                    WriteBackBeforeUse(ref use, local.LclNum, local.LclOffs, size);
                    if (IsPromotedStructLocalDying(local))
                    {
                        local.Flags |= GTF_VAR_DEATH;
                        CheckForwardSubForLastUse(local.LclNum);
                        foreach (var replacement in aggregate.Replacements)
                        {
                            SetNeedsWriteBack(replacement);
                        }
                    }
                }

                return;
            }

            var index = LowerBound(aggregate.Replacements, local.LclOffs, static rep => rep.Offset);
            JITDUMP($"Processing primitive use [{local.TreeId:D6}] of V{local.LclNum:D2}." +
                $"[{local.LclOffs:D3}..{local.LclOffs + local.Type.Size:D3})\n");
            if ((index >= aggregate.Replacements.Count) ||
                (aggregate.Replacements[index].Offset != local.LclOffs))
            {
                return;
            }

            var replacementLocal = aggregate.Replacements[index];
            assert(local.Type == replacementLocal.AccessType);
            var isDef = local.Oper.IsLocalStore;
            use = isDef
                ? _compiler.gtNewStoreLclVarNode(replacementLocal.LclNum, local.Data)
                : _compiler.gtNewLclvNode(local.Type, replacementLocal.LclNum);
            if ((local.Flags & GTF_VAR_DEATH) != 0)
            {
                use.Flags |= GTF_VAR_DEATH;
                CheckForwardSubForLastUse(replacementLocal.LclNum);
            }

            if (isDef)
            {
                ClearNeedsReadBack(replacementLocal);
                SetNeedsWriteBack(replacementLocal);
            }
            else if (replacementLocal.NeedsReadBack)
            {
                JITDUMP("  ..needs a read back\n");
                use = _compiler.gtNewCommaNode(use.Type,
                    _compiler.PhysicalPromotionCreateReadBack(local.LclNum, replacementLocal), use);
                ClearNeedsReadBack(replacementLocal);
                _compiler.lvaGetDesc(replacementLocal.LclNum).lvRedefinedInEmbeddedStatement = true;
            }

            JITDUMP($"  ..replaced with V{replacementLocal.LclNum:D2}\n");
            _madeChanges = true;
        }

        private bool IsPromotedStructLocalDying(GenTreeLclVarCommon local)
        {
            if (!_liveness.GetDeathsForStructLocal(local).IsRemainderDying())
            {
                return false;
            }

            global::RyuJitSharp.Compiler.PhysicalPromotionAggregateInfo __jitExceptionResult137;
            var __jitExceptionCoalesceLeft138 = _aggregates.Lookup(local.LclNum);
            if (__jitExceptionCoalesceLeft138 != null)
            {
                __jitExceptionResult137 = __jitExceptionCoalesceLeft138;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.DyingPromotedLocalNeedsAggregateInformation);
                __jitExceptionResult137 = default;
            }
            var aggregate = __jitExceptionResult137;
            foreach (var replacement in aggregate.Replacements)
            {
                if (replacement.NeedsReadBack)
                {
                    return false;
                }
            }

            for (var next = local.Next; next is not null; next = next.Next)
            {
                if ((next.Type is TYP_STRUCT) && next.Oper.IsLocal &&
                    (next.AsLclVarCommon().LclNum == local.LclNum))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
