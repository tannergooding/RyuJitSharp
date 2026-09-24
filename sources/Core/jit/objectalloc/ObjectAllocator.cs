// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class ObjectAllocator : Phase
{
    private bool _isObjectStackAllocationEnabled;
    private readonly bool _isR2R;

#if DEBUG
    private static ConfigMethodRange s_jitObjectStackAllocationRange;
#endif

    private enum ObjectAllocationType
    {
        OAT_NONE,
        OAT_NEWOBJ,
        OAT_NEWOBJ_HEAP,
        OAT_NEWARR,
    }

    public unsafe ObjectAllocator(Compiler compiler)
        : base(compiler, PHASE_ALLOCATE_OBJECTS)
    {
        _isR2R = compiler.IsReadyToRun;
    }

    public void EnableObjectStackAllocation()
    {
        _isObjectStackAllocationEnabled = true;
    }

    protected override unsafe PhaseStatus DoPhase()
    {
        var compiler = CompilerInstance;

        if ((compiler.optMethodFlags & (OMF_HAS_NEWOBJ | OMF_HAS_NEWARRAY)) == 0)
        {
            JITDUMP("no newobjs or newarr in this method; punting\n");
            compiler.fgInvalidateDfsTree();
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (compiler.opts.OptimizationDisabled && ((compiler.optMethodFlags & OMF_HAS_NEWOBJ) == 0))
        {
            JITDUMP("optimizations are disabled and there are no newobjs; punting\n");
            compiler.fgInvalidateDfsTree();
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var enabled = _isObjectStackAllocationEnabled;
        var disableReason = ": global config";

#if DEBUG
        if (enabled)
        {
            s_jitObjectStackAllocationRange.EnsureInit(JitConfig.JitObjectStackAllocationRange);
            enabled &= s_jitObjectStackAllocationRange.Contains(compiler.info.compMethodHash());
            disableReason = ": range config";
        }
#endif
        if (enabled)
        {
            IMPL_LIMITATION("object stack allocation analysis and cloning are not yet ported");
        }

        JITDUMP($"disabled{(_isObjectStackAllocationEnabled ? disableReason : "")}, punting\n");
        _isObjectStackAllocationEnabled = false;
        MorphHeapAllocObjNodes();

        compiler.fgInvalidateDfsTree();
        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    private unsafe ObjectAllocationType AllocationKind(GenTree tree)
    {
        var compiler = CompilerInstance;
        var allocType = ObjectAllocationType.OAT_NONE;

        if (tree.Oper is GT_ALLOCOBJ)
        {
            var clsHnd = tree.AsAllocObj().ClsHnd;
            assert(clsHnd != NO_CLASS_HANDLE);
            var isValueClass = compiler.info.compCompHnd->isValueClass(clsHnd);
            var canBeOnStack = isValueClass || compiler.info.compCompHnd->canAllocateOnStack(clsHnd);
            allocType = canBeOnStack ? ObjectAllocationType.OAT_NEWOBJ : ObjectAllocationType.OAT_NEWOBJ_HEAP;
        }
        else if (!_isR2R && (tree.Oper is GT_CALL) && tree.AsCall().IsHelperCall())
        {
            var call = tree.AsCall();

            switch (call.HelperNum)
            {
                case CORINFO_HELP_NEWARR_1_VC:
                case CORINFO_HELP_NEWARR_1_PTR:
                case CORINFO_HELP_NEWARR_1_DIRECT:
                case CORINFO_HELP_NEWARR_1_ALIGN8:
                {
                    if (call.Args.CountUserArgs() == 2)
                    {
                        var lengthArg = call.Args.GetUserArgByIndex(1);
                        assert(lengthArg is not null);

                        if (lengthArg.Node.Oper.IsCnsIntOrI)
                        {
                            allocType = ObjectAllocationType.OAT_NEWARR;
                        }
                    }
                    break;
                }
            }
        }

        return allocType;
    }

    // The stack-allocation-disabled path of MorphAllocObjNodes/MorphAllocObjNode.
    internal void MorphHeapAllocObjNodes()
    {
        assert(!_isObjectStackAllocationEnabled);
        var compiler = CompilerInstance;

        foreach (var block in compiler.Blocks)
        {
            if (!block.HasFlag(BBF_HAS_NEWOBJ) && !block.HasFlag(BBF_HAS_NEWARR))
            {
                continue;
            }

            foreach (var stmt in block.Statements)
            {
                var tree = stmt.RootNode;

                if ((tree.Oper is not GT_STORE_LCL_VAR) || (tree.Type is not TYP_REF))
                {
                    assert(!compiler.gtTreeContainsOper(tree, GT_ALLOCOBJ));
                    continue;
                }

                var store = tree.AsLclVar();
                var allocType = AllocationKind(store.Data);

                if (allocType is ObjectAllocationType.OAT_NONE)
                {
                    continue;
                }

#if DEBUG
                JITDUMP($"Allocating V{store.LclNum:D2} / [{tree.TreeId:D6}] on the heap: [object stack allocation disabled]\n");
#endif

                if (allocType is ObjectAllocationType.OAT_NEWOBJ or ObjectAllocationType.OAT_NEWOBJ_HEAP)
                {
                    var newData = MorphAllocObjNodeIntoHelperCall(store.Data.AsAllocObj());
                    store.DataRef = newData;
                    store.Flags |= newData.Flags & GTF_ALL_EFFECT;
                }

                // Tracking and the connection graph are established by the
                // stack-allocation analysis, which cannot reach this path.
                assert(!compiler.lvaGetDesc(store.LclNum).lvTracked);
            }
        }
    }

    internal unsafe GenTreeCall MorphAllocObjNodeIntoHelperCall(GenTreeAllocObj allocObj)
    {
        var compiler = CompilerInstance;
        var arg = allocObj.Op1;
        var helper = allocObj.NewHelper;
        var helperHasSideEffects = allocObj.NewHelperHasSideEffects;

#if FEATURE_READYTORUN
        var entryPoint = allocObj.EntryPoint;

        if (helper is CORINFO_HELP_READYTORUN_NEW)
        {
            arg = null;
        }
#endif
        // Native fgMorphIntoHelperCall uses ChangeOper here. Replace the CLR
        // node instead, leaving its owner responsible for installing this call.
        var helperCall = arg is null
            ? compiler.gtNewHelperCallNode(allocObj, helper)
            : compiler.gtNewHelperCallNode(allocObj, helper, arg);
        helperCall.SetMorphed(compiler);

        if (helperHasSideEffects)
        {
            helperCall._callMoreFlags |= GTF_CALL_M_ALLOC_SIDE_EFFECTS;
        }

#if FEATURE_READYTORUN
        if (entryPoint.addr is not null)
        {
            assert(compiler.IsAot);
            helperCall._entryPoint = entryPoint;
        }
        else
        {
            assert(helper is not CORINFO_HELP_READYTORUN_NEW);
        }
#endif
        return helperCall;
    }
}
