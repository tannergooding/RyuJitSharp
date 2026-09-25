// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildSimple(GenTree tree)
    {
        var sourceCount = 0;
        if (!tree.Oper.IsLeaf)
        {
            assert(tree.Oper.IsSimple);
            sourceCount = buildBinaryUses(tree.AsUnOp());
        }

        if (tree.IsValue)
        {
            _ = buildDef(tree, SRBM_NONE);
        }
        return sourceCount;
    }

    private SingleTypeRegSet internalFloatRegCandidates()
    {
        _needNonIntegerRegisters = true;

        // An internal-only floating temporary must not introduce callee-save
        // usage unless the compiler already knows floating registers are used.
        return _compiler.compFloatingPointUsed ? _availableFloatRegs : _rbmFltCalleeTrash;
    }

#if TARGET_XARCH
    private SingleTypeRegSet lowSIMDRegs()
    {
#if TARGET_AMD64
        return _availableFloatRegs & SRBM_LOWFLOAT;
#else
        return _availableFloatRegs;
#endif
    }

    private SingleTypeRegSet buildEvexIncompatibleMask(GenTree tree)
    {
#if TARGET_AMD64
        assert(!varTypeIsMask(tree.Type));
        if (!varTypeIsFloating(tree.Type) && !varTypeIsSimd(tree.Type))
        {
            return SRBM_NONE;
        }

        if (tree.IsContained &&
            (tree.Oper.IsIndir || tree.Oper is GT_LEA ||
             (tree.Oper.IsHWIntrinsic && tree.AsHWIntrinsic().IsMemoryLoad())))
        {
            return SRBM_NONE;
        }
        return lowSIMDRegs();
#else
        return SRBM_NONE;
#endif
    }

    private static bool doesThisUseGPR(GenTree tree)
    {
        if (varTypeUsesIntReg(tree.Type) || tree.IsContainedIndir)
        {
            return true;
        }

#if FEATURE_HW_INTRINSICS
        if (tree.IsContained && tree.Oper.IsHWIntrinsic)
        {
            var intrinsic = tree.AsHWIntrinsic();
            return intrinsic.IsBroadcastScalar ||
                (intrinsic.IsMemoryLoad() && doesThisUseGPR(intrinsic.GetOp(1)));
        }
#endif
        return false;
    }

    private SingleTypeRegSet forceLowGprForApx(
        GenTree tree, SingleTypeRegSet candidates = SRBM_NONE, bool forceLowGpr = false)
    {
#if TARGET_AMD64
        if (_apxIsSupported && (forceLowGpr || doesThisUseGPR(tree)))
        {
            return candidates == SRBM_NONE ? _lowGprRegs : candidates & _lowGprRegs;
        }
#endif
        return candidates;
    }

    private SingleTypeRegSet forceLowGprForApxIfNeeded(
        GenTree tree, SingleTypeRegSet candidates, bool useApxRegs)
    {
        return useApxRegs ? candidates : forceLowGprForApx(tree, candidates);
    }

    private void setContainsAVXFlags(uint sizeOfSIMDVector = 0)
    {
        if (!_compiler.canUseVexEncoding())
        {
            return;
        }

        assert(_compiler.codeGen is not null);
        var emitter = _compiler.codeGen.Emitter;
        emitter.ContainsAvxInstruction = true;
        if (sizeOfSIMDVector >= 32)
        {
#if DEBUG
            assert((sizeOfSIMDVector == 32) ||
                ((sizeOfSIMDVector == 64) && _compiler.canUseEvexEncodingDebugOnly()));
#endif
            emitter.Contains256BitOrMoreAvxInstruction = true;
        }
    }
#endif
}
