// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed class StackLevelSetter : Phase
{
    private Dictionary<GenTreePutArgStk, uint>? _putArgNumSlots;
    private readonly bool _throwHelperBlocksUsed;

    private bool _framePointerRequired;

    private uint _currentStackLevel;
    private uint _maxStackLevel;

    public StackLevelSetter(Compiler compiler)
        : base(compiler, PHASE_STACK_LEVEL_SETTER)
    {
        _throwHelperBlocksUsed = compiler.fgUseThrowHelperBlocks();
        var codeGen = compiler.codeGen;
        assert(codeGen is not null);
        if (!Configuration.FEATURE_FIXED_OUT_ARGS)
        {
            _framePointerRequired = codeGen.IsFramePointerRequired;
        }
        codeGen.ResetWritePhaseForFramePointerRequired();
    }

    protected override PhaseStatus DoPhase()
    {
        var compiler = CompilerInstance;
        ProcessBlocks();

        if (!Configuration.FEATURE_FIXED_OUT_ARGS && _framePointerRequired)
        {
            var codeGen = compiler.codeGen;
            assert(codeGen is not null);
            codeGen.IsFramePointerRequired = true;
        }

        CheckAdditionalArgs();
        CheckArgCnt();

        var madeChanges = false;
        compiler.compUsesThrowHelper = false;

        if (compiler.fgHasAddCodeDscMap)
        {
            if (compiler.opts.OptimizationEnabled)
            {
                foreach (var add in new List<Compiler.AddCodeDsc>(compiler.fgGetAddCodeDscMap().Values))
                {
                    if (add.acdUsed)
                    {
                        compiler.fgCreateThrowHelperBlockCode(add);
                        compiler.compUsesThrowHelper = true;
                    }
                    else
                    {
                        var block = add.acdDstBlk;
                        assert(block is not null);
                        assert(block.IsEmpty);
                        JITDUMP($"Throw help block {FMT_BB(block.bbNum)} is unused\n");
                        block.RemoveFlags(BBF_DONT_REMOVE);
                        _ = compiler.fgRemoveBlock(block, unreachable: true);
                    }
                    madeChanges = true;
                }
            }
            else
            {
                foreach (var add in compiler.fgGetAddCodeDscMap().Values)
                {
                    compiler.compUsesThrowHelper = true;
                    add.acdUsed = true;
                    compiler.fgCreateThrowHelperBlockCode(add);
                    madeChanges = true;
                }
            }
        }

        compiler.fgRngChkThrowAdded = true;
        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private void ProcessBlocks()
    {
        if (!Configuration.TARGET_X86 && !_throwHelperBlocksUsed)
        {
            return;
        }

        var compiler = CompilerInstance;
        for (var block = compiler.fgFirstBB; block is not null; block = block.Next)
        {
            ProcessBlock(block);
        }
    }

    private void ProcessBlock(BasicBlock block)
    {
        assert(_currentStackLevel == 0);

        for (var node = block.LastLIRNode; node is not null; node = node.Prev)
        {
            if (Configuration.TARGET_X86)
            {
                if (node.Oper.IsPutArgStk)
                {
                    var putArg = node.AsPutArgStk();
                    var putArgNumSlots = _putArgNumSlots;
                    assert(putArgNumSlots is not null);
                    var found = putArgNumSlots.Remove(putArg, out var numSlots);
                    assert(found);
                    SubStackLevel(numSlots);
                }

                if (node.Oper.IsCall)
                {
                    var call = node.AsCall();
                    var usedStackSlotsCount = PopArgumentsFromCall(call);
                    if (Configuration.UNIX_X86_ABI)
                    {
                        call.Args.SetStkSizeBytes(unchecked(usedStackSlotsCount * (uint)Configuration.TARGET_POINTER_SIZE));
                    }
                }
            }

            if (!_throwHelperBlocksUsed)
            {
                continue;
            }

            var checkForHelpers = true;
            if (!Configuration.FEATURE_FIXED_OUT_ARGS)
            {
                checkForHelpers |= !_framePointerRequired;
            }

            if (checkForHelpers && MayUseThrowHelperBlock(node))
            {
                SetThrowHelperBlocks(node, block);
            }
        }

        assert(_currentStackLevel == 0);
    }

    private bool MayUseThrowHelperBlock(GenTree node)
    {
        if (((node.Flags & GTF_EXCEPT) != 0) && node.MayThrow(CompilerInstance))
        {
            return true;
        }

        if (Configuration.TARGET_WASM)
        {
            switch (node.Oper)
            {
                case GT_NULLCHECK:
                case GT_IND:
                case GT_STORE_BLK:
                case GT_STOREIND:
                {
                    return (node.Flags & GTF_IND_NONFAULTING) == 0;
                }

                case GT_CALL:
                {
                    return node.AsCall().NeedsNullCheck;
                }
            }
        }

        return false;
    }

    private void SetThrowHelperBlocks(GenTree node, BasicBlock block)
    {
        assert(MayUseThrowHelperBlock(node));

        switch (node.Oper)
        {
            case GT_BOUNDS_CHECK:
            {
                SetThrowHelperBlock(node.AsBoundsChk().ThrowKind, block);
                break;
            }

            case GT_HWINTRINSIC:
            {
                if (Configuration.FEATURE_HW_INTRINSICS)
                {
                    if (Configuration.TARGET_XARCH &&
                        (node.AsHWIntrinsic().HWIntrinsicId is NI_Vector_op_Division))
                    {
                        SetThrowHelperBlock(SCK_DIV_BY_ZERO, block);
                        SetThrowHelperBlock(SCK_OVERFLOW, block);
                    }
                    else if (Configuration.TARGET_WASM)
                    {
                        var category = HWIntrinsicInfo.lookupCategory(node.AsHWIntrinsic().HWIntrinsicId);
                        if (category is HW_Category_MemoryLoad or HW_Category_MemoryStore)
                        {
                            SetThrowHelperBlock(SCK_NULL_CHECK, block);
                        }
                    }
                }
                break;
            }
            case GT_INDEX_ADDR:
            {
                if (node.AsIndexAddr().IsBoundsChecked)
                {
                    SetThrowHelperBlock(SCK_RNGCHK_FAIL, block);
                }
                break;
            }

            case GT_CKFINITE:
            {
                SetThrowHelperBlock(SCK_ARITH_EXCPN, block);
                break;
            }

            case GT_DIV:
            case GT_UDIV:
            case GT_MOD:
            case GT_UMOD:
            {
                var supportsDivideExceptions =
                    Configuration.TARGET_ARM64 ||
                    Configuration.TARGET_ARM ||
                    Configuration.TARGET_LOONGARCH64 ||
                    Configuration.TARGET_RISCV64 ||
                    Configuration.TARGET_WASM;
                var supportsModuloExceptions =
                    Configuration.TARGET_LOONGARCH64 ||
                    Configuration.TARGET_RISCV64 ||
                    Configuration.TARGET_WASM;
                if (supportsDivideExceptions &&
                    ((node.Oper is GT_DIV or GT_UDIV) || supportsModuloExceptions))
                {
                    var exceptionFlags = node.Exceptions(CompilerInstance);
                    if ((exceptionFlags & ExceptionSetFlags.DivideByZeroException) != ExceptionSetFlags.None)
                    {
                        SetThrowHelperBlock(SCK_DIV_BY_ZERO, block);
                    }
                    else
                    {
                        node.Flags |= GTF_DIV_MOD_NO_BY_ZERO;
                    }

                    if ((exceptionFlags & ExceptionSetFlags.ArithmeticException) != ExceptionSetFlags.None)
                    {
                        SetThrowHelperBlock(SCK_ARITH_EXCPN, block);
                    }
                    else
                    {
                        node.Flags |= GTF_DIV_MOD_NO_OVERFLOW;
                    }
                }
                break;
            }

            case GT_NULLCHECK:
            {
                if (Configuration.TARGET_WASM)
                {
                    SetThrowHelperBlock(SCK_NULL_CHECK, block);
                }
                break;
            }

            case GT_IND:
            case GT_STORE_BLK:
            case GT_STOREIND:
            {
                if (Configuration.TARGET_WASM && ((node.Flags & GTF_IND_NONFAULTING) == 0))
                {
                    SetThrowHelperBlock(SCK_NULL_CHECK, block);
                }
                break;
            }

            case GT_CALL:
            {
                if (Configuration.TARGET_WASM && node.AsCall().NeedsNullCheck)
                {
                    SetThrowHelperBlock(SCK_NULL_CHECK, block);
                }
                break;
            }
        }

        if (node.HasOverflowCheckEx)
        {
            SetThrowHelperBlock(SCK_OVERFLOW, block);
        }
    }

    private void SetThrowHelperBlock(SpecialCodeKind kind, BasicBlock block)
    {
        var compiler = CompilerInstance;
        var add = compiler.fgGetExcptnTarget(kind, block, createIfNeeded: true);
        add.acdUsed = true;

        if (!Configuration.FEATURE_FIXED_OUT_ARGS)
        {
            if (add.acdStkLvlInit)
            {
                if (Configuration.UNIX_X86_ABI)
                {
                    _framePointerRequired = true;
                }
                else if (unchecked((uint)add.acdStkLvl) != _currentStackLevel)
                {
                    _framePointerRequired = true;
                }
            }
            else
            {
                add.acdStkLvlInit = true;
                if (unchecked((uint)add.acdStkLvl) != _currentStackLevel)
                {
                    var mismatchedDestination = add.acdDstBlk;
                    assert(mismatchedDestination is not null);
                    JITDUMP($"Wrong stack level was set for {FMT_BB(mismatchedDestination.bbNum)}\n");
                }
#if DEBUG
                var destination = add.acdDstBlk;
                assert(destination is not null);
                destination.bbTgtStkDepth = unchecked((int)_currentStackLevel);
#endif
                add.acdStkLvl = unchecked((int)_currentStackLevel);
            }
        }
    }

    private uint PopArgumentsFromCall(GenTreeCall call)
    {
        var usedStackSlotsCount = 0u;
        if (call.Args.HasStackArgs)
        {
            foreach (var arg in call.Args.Args)
            {
                var stackBytesConsumed = 0u;
                foreach (ref readonly var segment in arg.AbiInfo.Segments)
                {
                    if (segment.IsPassedOnStack)
                    {
                        stackBytesConsumed = unchecked(stackBytesConsumed + (uint)segment.StackSize);
                    }
                }

                var pointerSize = (uint)Configuration.TARGET_POINTER_SIZE;
                var slotCount = unchecked((stackBytesConsumed + pointerSize - 1) / pointerSize);
                if (slotCount != 0)
                {
                    var node = arg.Node;
                    assert(node.Oper.IsPutArgStk);
                    var putArg = node.AsPutArgStk();
                    if (!Configuration.FEATURE_FIXED_OUT_ARGS)
                    {
                        assert(unchecked(slotCount * (uint)Configuration.TARGET_POINTER_SIZE) == (uint)putArg.StackByteSize);
                    }
                    var putArgNumSlots = _putArgNumSlots ??= [];
                    putArgNumSlots[putArg] = slotCount;
                    usedStackSlotsCount = unchecked(usedStackSlotsCount + slotCount);
                    AddStackLevel(slotCount);
                }
            }
        }

        return usedStackSlotsCount;
    }

    private void AddStackLevel(uint value)
    {
        _currentStackLevel = unchecked(_currentStackLevel + value);
        if (_currentStackLevel > _maxStackLevel)
        {
            _maxStackLevel = _currentStackLevel;
        }
    }

    private void SubStackLevel(uint value)
    {
        assert(_currentStackLevel >= value);
        _currentStackLevel = unchecked(_currentStackLevel - value);
    }

    private void CheckArgCnt()
    {
        if (Configuration.JIT32_GCENCODER)
        {
            // The x86 GC unwind table represents argument slots with a 1024-bit map.
            const uint maxPtrArgOffset = 1024;
            if (_maxStackLevel >= maxPtrArgOffset)
            {
                var codeGen = CompilerInstance.codeGen;
                assert(codeGen is not null);
                codeGen.Interruptible = false;
            }

            if (_maxStackLevel >= sizeof(uint))
            {
                var codeGen = CompilerInstance.codeGen;
                assert(codeGen is not null);
                codeGen.IsFramePointerRequired = true;
            }
        }
    }

    private void CheckAdditionalArgs()
    {
        if (Configuration.TARGET_X86)
        {
            var compiler = CompilerInstance;
            if (compiler.compIsProfilerHookNeeded && (_maxStackLevel == 0))
            {
                JITDUMP($"Upping fgPtrArgCntMax from {_maxStackLevel} to 1\n");
                _maxStackLevel = 1;
            }
        }
    }
}
