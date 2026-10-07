// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainCheckCast(GenTreeCast node)
    {
#if TARGET_XARCH
        var castOp = node.CastOp;
        var castToType = node.CastType;
        var srcType = node.IsUnsigned ? varTypeToUnsigned(castOp.Type) : castOp.Type;

        if (!node.HasOverflowCheck)
        {
            var srcIsContainable = false;
            if (varTypeIsFloating(castToType) || varTypeIsFloating(srcType))
            {
                if (castOp.IsCnsNonZeroFltOrDbl)
                {
                    MakeSrcContained(node, castOp);
                }
                else
                {
                    // The SSE2 ulong-to-float fallback needs its source in a register.
                    srcIsContainable = !varTypeIsSmall(srcType) &&
                        ((srcType is not TYP_ULONG) || CompilerInstance.canUseEvexEncoding());
                }
            }
            else if (CompilerInstance.opts.Tier0OptimizationEnabled &&
                varTypeIsIntegral(castOp.Type) && varTypeIsIntegral(castToType))
            {
                // A contained load must perform the same sign or zero extension as the cast.
                srcIsContainable = !varTypeIsSmall(castOp.Type) ||
                    (varTypeIsUnsigned(castOp.Type) == node.IsZeroExtending);
            }

            if (srcIsContainable)
            {
                TryMakeSrcContainedOrRegOptional(node, castOp);
            }
        }

#if !TARGET_64BIT
        if (varTypeIsLong(srcType))
        {
            noway_assert(castOp.Oper is GT_LONG);
            castOp.IsContained = true;
        }
#endif
#elif TARGET_ARMARCH
        var castOp = node.CastOp;
        var castToType = node.CastType;
        if (CompilerInstance.opts.OptimizationEnabled && !node.HasOverflowCheck &&
            varTypeIsIntegral(castOp.Type) && varTypeIsIntegral(castToType))
        {
            if (!varTypeIsSmall(castOp.Type) || (varTypeIsUnsigned(castOp.Type) == node.IsZeroExtending))
            {
                var srcIsContainable = false;
                if (castOp is GenTreeIndir indir && (castOp.Oper is GT_IND))
                {
                    if (!indir.IsVolatile && !indir.IsUnaligned)
                    {
                        var address = indir.Addr;
                        if (!address.IsContained)
                        {
                            srcIsContainable = true;
                        }
                        else if ((address is GenTreeAddrMode mode) && !mode.HasIndex)
                        {
                            var loadType = varTypeIsSmall(castToType) ? castToType : castOp.Type;
                            srcIsContainable = Emitter.emitIns_valid_imm_for_ldst_offset(
                                mode.Offset, (emitAttr)loadType.Size);
                        }
                    }
                }
                else
                {
                    assert(castOp.Oper.IsLocalRead || !IsContainableMemoryOp(castOp));
                    srcIsContainable = true;
                }

                if (srcIsContainable)
                {
                    if (IsContainableMemoryOp(castOp) && IsSafeToContainMem(node, castOp))
                    {
                        MakeSrcContained(node, castOp);
                    }
                    else if (IsSafeToMarkRegOptional(node, castOp))
                    {
                        castOp.IsRegOptional = true;
                    }
                }
            }
        }
#if TARGET_ARM
        if (varTypeIsLong(castOp.Type))
        {
            assert(castOp.Oper is GT_LONG);
            MakeSrcContained(node, castOp);
        }
#endif
#elif TARGET_RISCV64
        // Native RISC-V casts have no contained operands.
#elif TARGET_LOONGARCH64
        // Native LoongArch64 casts have no contained operands.
#elif TARGET_WASM
        // Wasm cast containment remains an optimization opportunity.
#else
        throw new System.NotImplementedException("Cast containment is not ported for this target.");
#endif
    }
}
