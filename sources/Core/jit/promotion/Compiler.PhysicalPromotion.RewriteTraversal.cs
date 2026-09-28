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

                    replacer.WriteBackBeforeCurrentStatement(local.LclNum, local.LclOffs,
                        checked((int)(local.GetLayout(replacer._compiler) ??
                            throw new InvalidOperationException("A struct argument needs a layout.")).Size));
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

            var argument = call.Args.RetBufferArg ??
                throw new InvalidOperationException("An optimized retbuffer call needs a retbuffer argument.");
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

            var size = checked((int)(local.GetLayout(_compiler) ??
                throw new InvalidOperationException("A struct local needs a layout.")).Size);
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

#if DEBUG
                JITDUMP($"Processing struct use [{local.TreeId:D6}] of V{local.LclNum:D2}." +
                    $"[{local.LclOffs:D3}..{local.LclOffs + (local.GetLayout(_compiler) ??
                        throw new InvalidOperationException("A struct use needs a layout.")).Size:D3})\n");
#endif
                if (!ReplaceStructLocal(ref use, effectiveUser, local))
                {
                    var size = checked((int)(local.GetLayout(_compiler) ??
                        throw new InvalidOperationException("A struct use needs a layout.")).Size);
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
#if DEBUG
            JITDUMP($"Processing primitive use [{local.TreeId:D6}] of V{local.LclNum:D2}." +
                $"[{local.LclOffs:D3}..{local.LclOffs + local.Type.Size:D3})\n");
#endif
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

            var aggregate = _aggregates.Lookup(local.LclNum) ??
                throw new InvalidOperationException("A dying promoted local needs aggregate information.");
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
