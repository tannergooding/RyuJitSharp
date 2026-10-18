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

    private SingleTypeRegSet lowSIMDRegs()
    {
        if (Configuration.TARGET_AMD64)
        {
            return _availableFloatRegs & SRBM_LOWFLOAT;
        }

        return _availableFloatRegs;
    }

    private SingleTypeRegSet buildEvexIncompatibleMask(GenTree tree)
    {
        if (Configuration.TARGET_AMD64)
        {
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
        }

        return SRBM_NONE;
    }

    private static bool doesThisUseGPR(GenTree tree)
    {
        if (varTypeUsesIntReg(tree.Type) || tree.IsContainedIndir)
        {
            return true;
        }

        if (Configuration.FEATURE_HW_INTRINSICS && tree.IsContained && tree.Oper.IsHWIntrinsic)
        {
            var intrinsic = tree.AsHWIntrinsic();
            return intrinsic.IsBroadcastScalar ||
                (intrinsic.IsMemoryLoad() && doesThisUseGPR(intrinsic.GetOp(1)));
        }
        return false;
    }

    private SingleTypeRegSet forceLowGprForApx(
        GenTree tree, SingleTypeRegSet candidates = SRBM_NONE, bool forceLowGpr = false)
    {
        if (Configuration.TARGET_AMD64 && _apxIsSupported && (forceLowGpr || doesThisUseGPR(tree)))
        {
            return candidates == SRBM_NONE ? _lowGprRegs : candidates & _lowGprRegs;
        }
        return candidates;
    }

    private SingleTypeRegSet forceLowGprForApxIfNeeded(
        GenTree tree, SingleTypeRegSet candidates, bool useApxRegs)
    {
        return useApxRegs ? candidates : forceLowGprForApx(tree, candidates);
    }

    private void setContainsAVXFlags(uint sizeOfSIMDVector = 0)
    {
        if (!Configuration.TARGET_XARCH || !_compiler.canUseVexEncoding())
        {
            return;
        }

        assert(_compiler.codeGen is not null);
        var emitter = _compiler.codeGen.Emitter;
        emitter.ContainsAvxInstruction = true;
        if (sizeOfSIMDVector >= 32)
        {
            if (Configuration.DEBUG)
            {
                assert((sizeOfSIMDVector == 32) ||
                    ((sizeOfSIMDVector == 64) && _compiler.canUseEvexEncodingDebugOnly()));
            }
            emitter.Contains256BitOrMoreAvxInstruction = true;
        }
    }
}
