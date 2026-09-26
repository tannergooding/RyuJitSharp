// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    internal unsafe int MorphNewArrNodeIntoStackAlloc(GenTreeCall newArr, CORINFO_CLASS_HANDLE clsHnd,
        uint length, int blockSize, BasicBlock block, Statement stmt)
    {
        assert(_analysisDone);
        assert(clsHnd != NO_CLASS_HANDLE);
        assert(newArr.IsHelperCall());
        assert(newArr.HelperNum is not CORINFO_HELP_NEWARR_1_MAYBEFROZEN);
        var compiler = CompilerInstance;
        var alignTo8 = newArr.HelperNum is CORINFO_HELP_NEWARR_1_ALIGN8;
        var local = compiler.lvaGrabTemp(shortLifetime: false, "stack allocated array temp");
        ref var descriptor = ref compiler.lvaGetDesc(local);

        if (alignTo8)
        {
            blockSize = (blockSize + 7) & ~7;
        }

        compiler.lvaSetStruct(local, compiler.typGetArrayLayout(clsHnd, checked((int)length)),
            unsafeValueClsCheck: false);
        descriptor.lvStackAllocatedObject = true;

        var inLoop = block.HasFlag(BBF_BACKWARD_JUMP);
        var isReturn = block.Kind is BBJ_RETURN;
        if (compiler.fgVarNeedsExplicitZeroInit(local, inLoop, isReturn))
        {
            var init = compiler.gtNewStoreLclVarNode(local, compiler.gtNewIconNode(TYP_INT, 0));
            compiler.fgInsertStmtBefore(block, stmt, compiler.gtNewStmt(init));
        }
        else
        {
            JITDUMP($"\nSuppressing zero-init for V{local:D2} -- expect to zero in prolog\n");
            descriptor.lvSuppressedZeroInit = true;
            compiler.compSuppressedZeroInit = true;
        }

#if !TARGET_64BIT
        descriptor.lvStructDoubleAlign = alignTo8;
#endif
        var stackAddress = compiler.gtNewLclAddrNode(TYP_BYREF, local, 0);
        _ = newArr.Args.PushBack(NewCallArg.CreateForPrimitive(stackAddress)
            .WithWellKnownArg(WellKnownArg.StackArrayLocal));
        newArr._callMoreFlags |= GTF_CALL_M_STACK_ARRAY;
        newArr.ChangeType(TYP_I_IMPL);
        newArr._returnType = TYP_I_IMPL;
        compiler.MethodHasStackAllocatedArray = true;

        return local;
    }

    internal int MorphAllocObjNodeIntoStackAlloc(GenTreeAllocObj allocObj, ClassLayout layout,
        BasicBlock block, Statement stmt)
    {
        assert(_analysisDone);
        var compiler = CompilerInstance;
#if DEBUG
        var reason = $"stack allocated {layout.ShortClassName}";
#else
        var reason = "stack allocated object";
#endif
        var local = compiler.lvaGrabTemp(shortLifetime: false, reason);
        compiler.lvaSetStruct(local, layout, unsafeValueClsCheck: false);
        ref var descriptor = ref compiler.lvaGetDesc(local);
        descriptor.lvStackAllocatedObject = true;

        var inLoop = block.HasFlag(BBF_BACKWARD_JUMP);
        var isReturn = block.Kind is BBJ_RETURN;
        if (compiler.fgVarNeedsExplicitZeroInit(local, inLoop, isReturn))
        {
            var init = compiler.gtNewStoreLclVarNode(local, compiler.gtNewIconNode(TYP_INT, 0));
            compiler.fgInsertStmtBefore(block, stmt, compiler.gtNewStmt(init));
        }
        else
        {
            JITDUMP($"\nSuppressing zero-init for V{local:D2} -- expect to zero in prolog\n");
            descriptor.lvSuppressedZeroInit = true;
            compiler.compSuppressedZeroInit = true;
        }

        var methodTableStore = compiler.gtNewStoreLclFldNode(
            TYP_I_IMPL, local, 0, allocObj.Op1);
        compiler.fgInsertStmtBefore(block, stmt, compiler.gtNewStmt(methodTableStore));

        if ((allocObj.Flags & GTF_ALLOCOBJ_EMPTY_STATIC) != 0)
        {
            var predecessor = block.GetUniquePred(compiler)
                ?? throw new FatalJitException("An empty-static allocation requires one predecessor.");
            assert(predecessor.Kind is BBJ_COND);
            JITDUMP($"Empty static pattern controlled by {FMT_BB(predecessor.bbNum)}, optimizing to always use stack allocated instance\n");

            var controllingStmt = predecessor.LastStmt
                ?? throw new FatalJitException("An empty-static predecessor requires its branch statement.");
            var branch = controllingStmt.RootNode;
            assert(branch.Oper is GT_JTRUE);
            var trueEdge = predecessor.TrueEdge;
            var falseEdge = predecessor.FalseEdge;
            var kept = trueEdge.DestinationBlock == block ? trueEdge : falseEdge;
            var removed = trueEdge.DestinationBlock == block ? falseEdge : trueEdge;
            assert(kept.DestinationBlock == block);

            var removedBlock = removed.DestinationBlock;
            compiler.fgRemoveRefPred(removed);
            predecessor.SetKindAndTargetEdge(BBJ_ALWAYS, kept);
            compiler.fgRepairProfileCondToUncond(predecessor, kept, removed);
            controllingStmt.RootNode = branch.AsUnOp().Op1;
            assert(removedBlock.bbRefs == 0);
            assert(removedBlock.Kind is BBJ_ALWAYS);
            compiler.fgRemoveBlock(removedBlock, unreachable: true);
        }
        else
        {
#if DEBUG
            JITDUMP($"ALLOCOBJ [{allocObj.TreeId:D6}] is not part of an empty static\n");
#endif
        }

        return local;
    }

    private ClassLayout GetBoxedLayout(ClassLayout layout)
    {
        assert(layout.IsValueClass);
        var compiler = CompilerInstance;
        var builder = new ClassLayoutBuilder(compiler, checked((uint)(TARGET_POINTER_SIZE + layout.Size)));
        builder.CopyPaddingFrom(TARGET_POINTER_SIZE, layout);
        builder.CopyGCInfoFrom(TARGET_POINTER_SIZE, layout);
#if DEBUG
        builder.CopyNameFrom(layout, "[boxed] ");
#endif
        return compiler.typGetCustomLayout(builder);
    }
}
