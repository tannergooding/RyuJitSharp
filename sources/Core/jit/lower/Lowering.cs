// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering : Phase
{
    private readonly IRegAlloc _regAlloc;
    private SideEffectSet _scratchSideEffects;
    private BasicBlock? _block;
    private int _vtableCallTemp = BAD_VAR_NUM;
    private uint _outgoingArgSpaceSize;

    public Lowering(Compiler compiler, IRegAlloc regAlloc)
        : base(compiler, PHASE_LOWERING)
    {
        _regAlloc = regAlloc;
        assert(_regAlloc is not null);
    }

    private BasicBlock BlockRange()
    {
        assert(_block is not null);
        assert(_block.IsLIR);
        return _block;
    }

    public void FinalizeOutgoingArgSpace()
    {
#if FEATURE_FIXED_OUT_ARGS
        var compiler = CompilerInstance;
        if (_outgoingArgSpaceSize < (uint)MIN_ARG_AREA_FOR_CALL)
        {
            if (compiler.opts.compDbgCode || compiler.compUsesThrowHelper || compiler.compIsProfilerHookNeeded ||
                (compiler.compMethodRequiresPInvokeFrame && !compiler.opts.ShouldUsePInvokeHelpers) ||
                compiler.NeedsGSSecurityCookie)
            {
                _outgoingArgSpaceSize = (uint)MIN_ARG_AREA_FOR_CALL;
                JITDUMP($"Bumping outgoing arg space size to {_outgoingArgSpaceSize} for possible helper or profile hook call");
            }
        }

        if (compiler.compLocallocUsed)
        {
            // Localloc moves the outgoing area; aligning its size avoids holes when that area is moved.
            _outgoingArgSpaceSize = roundUp(_outgoingArgSpaceSize, (uint)STACK_ALIGN);
            JITDUMP($"Bumping outgoing arg space size to {_outgoingArgSpaceSize} for localloc");
        }

        assert((_outgoingArgSpaceSize % TARGET_POINTER_SIZE) == 0);
        compiler.lvaOutgoingArgSpaceSize.Value = unchecked((int)_outgoingArgSpaceSize);
        compiler.lvaOutgoingArgSpaceSize.MarkAsReadOnly();
        compiler.lvaGetDesc(compiler.lvaOutgoingArgSpaceVar)
            .GrowBlockLayout(compiler.typGetBlkLayout(_outgoingArgSpaceSize));

        SetFramePointerFromArgSpaceSize();
#endif
    }

    private void RequireOutgoingArgSpace(GenTree node, int size)
    {
#if FEATURE_FIXED_OUT_ARGS
        var unsignedSize = unchecked((uint)size);
        if (unsignedSize <= _outgoingArgSpaceSize)
        {
            return;
        }

#if DEBUG
        JITDUMP($"Bumping outgoing arg space size from {_outgoingArgSpaceSize} to {unsignedSize} for [{node.TreeId:D6}]\n");
#endif
        _outgoingArgSpaceSize = unsignedSize;
#endif
    }

    private void SetFramePointerFromArgSpaceSize()
    {
        var compiler = CompilerInstance;
        var stackLevelSpace = _outgoingArgSpaceSize;
        if (compiler.compTailCallUsed)
        {
            for (var block = compiler.fgFirstBB; block is not null; block = block.Next)
            {
                if (block.EndsWithTailCall(compiler, fastTailCallsOnly: true,
                    tailCallsConvertibleToLoopOnly: false, out var tailCall))
                {
                    assert(tailCall is not null);
                    stackLevelSpace = uint.Max(stackLevelSpace, unchecked((uint)tailCall.Args.OutgoingArgsStackSize));
                }
            }
        }

        var stackLevel = (uint.Max(stackLevelSpace, (uint)MIN_ARG_AREA_FOR_CALL) - (uint)MIN_ARG_AREA_FOR_CALL) /
            TARGET_POINTER_SIZE;
        // Four or more argument slots beyond the mandatory call area require a frame pointer.
        if (stackLevel >= 4)
        {
            var codeGen = compiler.codeGen;
            assert(codeGen is not null);
            codeGen.IsFramePointerRequired = true;
        }
    }

    protected override PhaseStatus DoPhase()
    {
        var compiler = CompilerInstance;
        // Insert the one-time prolog even if all P/Invoke calls were eliminated.
        // Epilogs are inserted at the appropriate sites during block lowering.
        if (compiler.compMethodRequiresPInvokeFrame)
        {
            InsertPInvokeMethodProlog();
        }

#if LOWER_DECOMPOSE_LONGS
        var decomp = new DecomposeLongs(compiler, this);
        if (compiler.compLongUsed)
        {
            decomp.PrepareForDecomposition();
        }
#endif

        // Containment needs to know which locals cannot be enregistered.
        if (!compiler.compEnregLocals)
        {
            compiler.lvSetMinOptsDoNotEnreg();
        }
        else
        {
            compiler.lvSetVarsDoNotEnreg();
        }

        if (compiler.opts.OptimizationEnabled && !compiler.opts.IsOSR)
        {
            MapParameterRegisterLocals();
        }

        foreach (var block in compiler.Blocks)
        {
            compiler.compCurBB = block;
#if LOWER_DECOMPOSE_LONGS
            if (compiler.compLongUsed)
            {
                decomp.DecomposeBlock(block);
            }
#endif
            LowerBlock(block);
        }

        AfterLowerBlocks();
        compiler._dfsTree ??= compiler.fgComputeDfs();
        _ = compiler.fgRemoveBlocksOutsideDfsTree();

        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    private void AfterLowerBlocks()
    {
#if TARGET_WASM
        AfterLowerBlocksWasm();
#endif
    }
}
