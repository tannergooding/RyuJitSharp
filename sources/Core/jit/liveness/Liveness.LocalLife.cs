// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    internal unsafe GenTreeLclVarCommon? ComputeLifeCall(nint[] life, nint[] keepAliveVars, GenTreeCall call)
    {
        assert(call is not null);
        if (call.IsTailCallViaJitHelper && _compiler.compMethodRequiresPInvokeFrame)
        {
            assert(!_compiler.opts.ShouldUsePInvokeHelpers || (_compiler.info.compLvFrameListRoot == BAD_VAR_NUM));
            if (!_compiler.opts.ShouldUsePInvokeHelpers)
            {
                ref var frame = ref _compiler.lvaGetDesc(_compiler.info.compLvFrameListRoot);
                if (frame.lvTracked)
                {
                    VarSetOps.AddElemD(_compiler, life, frame._varIndex);
                }
            }
        }

        if (call.IsUnmanaged && _compiler.compMethodRequiresPInvokeFrame)
        {
            assert(!_compiler.opts.ShouldUsePInvokeHelpers || (_compiler.info.compLvFrameListRoot == BAD_VAR_NUM));
            if (!_compiler.opts.ShouldUsePInvokeHelpers && !call.IsSuppressGCTransition)
            {
                ref var frame = ref _compiler.lvaGetDesc(_compiler.info.compLvFrameListRoot);
                if (frame.lvTracked)
                {
                    var index = frame._varIndex;
                    noway_assert(index < _compiler.lvaTrackedCount);
                    if (VarSetOps.IsMember(_compiler, life, index))
                    {
                        call._callMoreFlags &= ~GTF_CALL_M_FRAME_VAR_DEATH;
                    }
                    else
                    {
                        VarSetOps.AddElemD(_compiler, life, index);
                        call._callMoreFlags |= GTF_CALL_M_FRAME_VAR_DEATH;
                    }
                }
            }
        }

        GenTreeLclVarCommon? partialDef = null;
        _ = call.VisitPhysicalLocalDefNodes(_compiler, definition => {
            if ((definition.Flags & GTF_VAR_USEASG) != 0)
            {
                assert(partialDef is null);
                partialDef = definition.AsLclVarCommon();
            }
            _ = ComputeLifeLocal(life, keepAliveVars, definition);
            return GenTree.VisitResult.Continue;
        });

        return partialDef;
    }

    internal bool ComputeLifeLocal(nint[] life, ReadOnlySpan<nint> keepAliveVars, GenTree node)
    {
        var local = node.AsLclVarCommon();
        assert(local.LclNum < _compiler.lvaCount);
        ref var descriptor = ref _compiler.lvaTable[local.LclNum];
        if (descriptor.lvTracked)
        {
            if ((node.Flags & GTF_VAR_DEF) != 0)
            {
                return ComputeLifeTrackedLocalDef(life, keepAliveVars, in descriptor, local);
            }
            ComputeLifeTrackedLocalUse(life, in descriptor, local);
        }
        else
        {
            return ComputeLifeUntrackedLocal(life, keepAliveVars, in descriptor, local);
        }

        return false;
    }

    private void ComputeLifeTrackedLocalUse(nint[] life, in LclVarDsc descriptor, GenTreeLclVarCommon node)
    {
        assert(node is not null);
        assert((node.Flags & GTF_VAR_DEF) == 0);
        assert(descriptor.lvTracked);

        var index = descriptor._varIndex;
        if (VarSetOps.IsMember(_compiler, life, index))
        {
            node.Flags &= ~GTF_VAR_DEATH;
            return;
        }

        node.Flags |= GTF_VAR_DEATH;
        VarSetOps.AddElemD(_compiler, life, index);
    }

    private bool ComputeLifeTrackedLocalDef(nint[] life, ReadOnlySpan<nint> keepAliveVars,
        in LclVarDsc descriptor, GenTreeLclVarCommon node)
    {
        assert(node is not null);
        assert((node.Flags & GTF_VAR_DEF) != 0);
        assert(descriptor.lvTracked);

        var index = descriptor._varIndex;
        if (VarSetOps.IsMember(_compiler, life, index))
        {
            node.Flags &= ~GTF_VAR_DEATH;
            if (((node.Flags & GTF_VAR_USEASG) == 0) && !VarSetOps.IsMember(_compiler, keepAliveVars, index))
            {
                VarSetOps.RemoveElemD(_compiler, life, index);
            }
        }
        else
        {
            node.Flags |= GTF_VAR_DEATH;
            if (TLiveness.EliminateDeadCode)
            {
                noway_assert(!VarSetOps.IsMember(_compiler, keepAliveVars, index));
                return !descriptor.IsAddressExposed &&
                    !(descriptor.lvIsStructField && _compiler.lvaTable[descriptor.lvParentLcl].IsAddressExposed);
            }
        }

        return false;
    }

    private bool ComputeLifeUntrackedLocal(nint[] life, ReadOnlySpan<nint> keepAliveVars,
        in LclVarDsc descriptor, GenTreeLclVarCommon node)
    {
        assert(node is not null);
        var isDef = (node.Flags & GTF_VAR_DEF) != 0;
        // Late liveness has accurate reference counts, including untracked stores.
        if (TLiveness.EliminateDeadCode && TLiveness.IsLIR && isDef &&
            (descriptor.lvRefCnt() == 1) && !descriptor.lvPinned)
        {
            if (descriptor.lvIsStructField)
            {
                if ((_compiler.lvaGetDesc(descriptor.lvParentLcl).lvRefCnt() == 1) &&
                    (_compiler.lvaGetParentPromotionType(in descriptor) == Compiler.PROMOTION_TYPE_DEPENDENT))
                {
                    return true;
                }
            }
            else if (varTypeIsPromotable(descriptor.Type))
            {
                if (_compiler.lvaGetPromotionType(in descriptor) != Compiler.PROMOTION_TYPE_INDEPENDENT)
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        if (!varTypeIsPromotable(descriptor.Type) ||
            (_compiler.lvaGetPromotionType(in descriptor) == Compiler.PROMOTION_TYPE_NONE))
        {
            return false;
        }

        assert(descriptor.lvFieldCnt <= 4);
        node.Flags &= ~GTF_VAR_DEATH_MASK;
        var anyFieldLive = false;
        for (var local = descriptor.lvFieldLclStart; local < descriptor.lvFieldLclStart + descriptor.lvFieldCnt; local++)
        {
            ref var field = ref _compiler.lvaGetDesc(local);
#if !TARGET_64BIT
            if (!varTypeIsLong(field.Type) || !field.lvPromoted)
#endif
            {
                noway_assert(field.lvIsStructField);
            }
            if (field.lvTracked)
            {
                var index = field._varIndex;
                var fieldLive = VarSetOps.IsMember(_compiler, life, index);
                anyFieldLive |= fieldLive;
                if (!fieldLive)
                {
                    node.SetLastUse(local - descriptor.lvFieldLclStart, true);
                }
                if (isDef)
                {
                    if (((node.Flags & GTF_VAR_USEASG) == 0) && !VarSetOps.IsMember(_compiler, keepAliveVars, index))
                    {
                        VarSetOps.RemoveElemD(_compiler, life, index);
                    }
                }
                else
                {
                    VarSetOps.AddElemD(_compiler, life, index);
                }
            }
            else
            {
                anyFieldLive = true;
            }
        }

        if (TLiveness.EliminateDeadCode && isDef && !anyFieldLive)
        {
            return !descriptor.IsAddressExposed;
        }

        return false;
    }
}
