// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ObjectAllocator : Phase
{
    private bool _isObjectStackAllocationEnabled;
    private readonly bool _isR2R;

    private enum ObjectAllocationType
    {
        OAT_NONE,
        OAT_NEWOBJ,
        OAT_NEWOBJ_HEAP,
        OAT_NEWARR,
        OAT_NEWARR_R2R,
    }

    public unsafe ObjectAllocator(Compiler compiler)
        : base(compiler, PHASE_ALLOCATE_OBJECTS)
    {
        _isR2R = compiler.IsReadyToRun;
        _bitVecTraits = new BitVecTraits(compiler, compiler.lvaCount);
        _trackFields = JitConfig.JitObjectStackAllocationTrackFields > 0;
        _initialMaxBlockID = compiler.compBasicBlockID;
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

        if (Configuration.DEBUG && enabled)
        {
            Compiler.DebugData.MethodRangeData.s_jitObjectStackAllocationRange.EnsureInit(JitConfig.JitObjectStackAllocationRange);
            enabled &= Compiler.DebugData.MethodRangeData.s_jitObjectStackAllocationRange.Contains(compiler.info.compMethodHash());
            disableReason = ": range config";
        }
        if (enabled)
        {
            JITDUMP("enabled, analyzing...\n");
            DoAnalysis();

            CloneAndSpecialize();
        }
        else
        {
            JITDUMP($"disabled{(_isObjectStackAllocationEnabled ? disableReason : "")}, punting\n");
            _isObjectStackAllocationEnabled = false;
        }

        var didStackAllocate = MorphAllocObjNodes();
        if (didStackAllocate)
        {
            assert(enabled);
            ComputeStackObjectPointers(_bitVecTraits);
            RewriteUses();
        }

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
            var isValueClass = compiler.eeIsValueClass(clsHnd);
            var canBeOnStack = isValueClass || compiler.info.compCompHnd->canAllocateOnStack(clsHnd);
            allocType = canBeOnStack ? ObjectAllocationType.OAT_NEWOBJ : ObjectAllocationType.OAT_NEWOBJ_HEAP;
            JITDUMP(canBeOnStack
                ? $"Checking if we can stack allocate object [{TreeIdForDump(tree):D6}] ({compiler.eeGetClassName(clsHnd)})\n"
                : $"Object [{TreeIdForDump(tree):D6}] must be allocated on the heap ({compiler.eeGetClassName(clsHnd)})\n");
        }
        else if ((tree.Oper is GT_CALL) && tree.AsCall().IsHelperCall())
        {
            var call = tree.AsCall();

            switch (call.HelperNum)
            {
                case CORINFO_HELP_NEWARR_1_VC:
                case CORINFO_HELP_NEWARR_1_PTR:
                case CORINFO_HELP_NEWARR_1_DIRECT:
                case CORINFO_HELP_NEWARR_1_ALIGN8:
                {
                    if ((call.Args.CountUserArgs() == 2) &&
                        (call.Args.GetUserArgByIndex(1)?.Node.Oper.IsCnsIntOrI == true))
                    {
                        allocType = ObjectAllocationType.OAT_NEWARR;
                        JITDUMP($"Checking if we can stack allocate array [{TreeIdForDump(call):D6}]\n");
                    }
                    else
                    {
                        JITDUMP($"Can't handle unknown length newarr [{TreeIdForDump(call):D6}]\n");
                    }
                    break;
                }

                case CORINFO_HELP_READYTORUN_NEWARR_1:
                {
                    assert(_isR2R);
                    if ((call.Args.CountUserArgs() == 1) &&
                        (call.Args.GetUserArgByIndex(0)?.Node.Oper.IsCnsIntOrI == true))
                    {
                        allocType = ObjectAllocationType.OAT_NEWARR_R2R;
                        JITDUMP($"Checking if we can stack allocate array [{TreeIdForDump(call):D6}]\n");
                    }
                    else
                    {
                        JITDUMP($"Can't handle unknown length newarr [{TreeIdForDump(call):D6}]\n");
                    }
                    break;
                }
            }
        }

        return allocType;
    }

    internal unsafe GenTreeCall MorphAllocObjNodeIntoHelperCall(GenTreeAllocObj allocObj)
    {
        var compiler = CompilerInstance;
        var arg = allocObj.Op1;
        var helper = allocObj.NewHelper;
        var helperHasSideEffects = allocObj.NewHelperHasSideEffects;

        var entryPoint = default(CORINFO_CONST_LOOKUP);
        if (Configuration.FEATURE_READYTORUN)
        {
            entryPoint = allocObj.EntryPoint;

            if (helper is CORINFO_HELP_READYTORUN_NEW)
            {
                arg = null;
            }
        }

        // Native fgMorphIntoHelperCall uses ChangeOper here. Replace the CLR
        // node instead, leaving its owner responsible for installing this call.
        var helperCall = arg is null
            ? compiler.gtNewHelperCallNode(allocObj, helper)
            : compiler.gtNewHelperCallNode(allocObj, helper, arg);

        if (helperHasSideEffects)
        {
            helperCall._callMoreFlags |= GTF_CALL_M_ALLOC_SIDE_EFFECTS;
            helperCall.Flags |= GTF_GLOB_REF;
        }

        if (Configuration.FEATURE_READYTORUN)
        {
            if (entryPoint.addr is not null)
            {
                assert(compiler.IsAot);
                helperCall._entryPoint = entryPoint;
            }
            else
            {
                assert(helper is not CORINFO_HELP_READYTORUN_NEW);
            }
        }
        return helperCall;
    }
}
