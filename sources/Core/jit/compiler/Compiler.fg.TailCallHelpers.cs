// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgValidateIRForTailCall(GenTreeCall call)
    {
#if DEBUG
        // Struct-field forwarding makes valid return patterns hard to track.
        if (call.Type is TYP_STRUCT)
        {
            return;
        }

        var visitor = new TailCallIRValidatorVisitor(this, call);
        for (var statement = compCurStmt; statement is not null; statement = statement.NextStmt)
        {
            _ = visitor.WalkTree(ref statement.RootNodeRef, null);
        }

        var block = compCurBB;
        assert(block is not null);
        while (block.Kind is not BBJ_RETURN)
        {
            block = block.UniqueSucc;
            assert(block is not null, "Expected straight flow after tailcall");
            foreach (var statement in block.Statements)
            {
                _ = visitor.WalkTree(ref statement.RootNodeRef, null);
            }
        }
#endif
    }

    public bool fgCanTailCallViaJitHelper(GenTreeCall call)
    {
#if !TARGET_X86 || UNIX_X86_ABI
        // Only Windows x86 has the faster JIT-helper mechanism.
        return false;
#else
        // R2R must use the portable path so the EE can request a runtime JIT.
        if (IsAot)
        {
            return false;
        }

        if (compLocallocUsed)
        {
            return false;
        }

        // Delegate calls can use VSD stubs that inspect the call site.
        if (call.IsDelegateInvoke)
        {
            return false;
        }

        return true;
#endif
    }

    public unsafe GenTree fgCreateCallDispatcherAndGetResult(GenTreeCall originalCall,
        CORINFO_METHOD_HANDLE callTargetStub, CORINFO_METHOD_HANDLE dispatcher)
    {
        assert(fgMorphStmt is not null);
        var dispatcherCall = gtNewUserCallNode(TYP_VOID, dispatcher, fgMorphStmt.DebugInfo);
        GenTree returnValueArgument;
        GenTree? returnValue = null;

        if (originalCall.Args.HasRetBuffer)
        {
            JITDUMP("Transferring retbuf\n");
            var retBufferArgument = originalCall.Args.RetBufferArg;
            assert(retBufferArgument is not null);
            var retBuffer = retBufferArgument.Node;
            assert(info.compRetBuffArg != BAD_VAR_NUM);
            assert(retBuffer.Oper.IsLocal);
            assert(retBuffer.AsLclVarCommon().LclNum == info.compRetBuffArg);
            returnValueArgument = retBuffer;

            if (originalCall.Type is not TYP_VOID)
            {
                returnValue = gtClone(retBuffer);
            }
        }
        else if (originalCall.Type is not TYP_VOID)
        {
            JITDUMP("Creating a new temp for the return value\n");
            var returnLocal = lvaGrabTemp(false, "Return value for tail call dispatcher");
            if (varTypeIsStruct(originalCall.Type))
            {
                lvaSetStruct(returnLocal, originalCall._retClsHnd, false);
            }
            else
            {
                // Use the real return type so loading the dispatcher-written
                // value performs the required small-type normalization.
                lvaTable[returnLocal].Type = originalCall._returnType;
            }

            lvaSetVarAddrExposed(returnLocal, AddressExposedReason.DISPATCH_RET_BUF);
            if (varTypeIsStruct(originalCall.Type) && compMethodReturnsMultiRegRetType)
            {
                lvaGetDesc(returnLocal).lvIsMultiRegRet = true;
            }

            returnValueArgument = gtNewLclVarAddrNode(TYP_BYREF, returnLocal);
            returnValue = gtNewLclvNode(lvaTable[returnLocal].Type.ActualType, returnLocal);
        }
        else
        {
            JITDUMP("No return value so using null pointer as arg\n");
            returnValueArgument = gtNewZeroConNode(TYP_I_IMPL);
        }

        var target = new GenTreeFptrVal(TYP_I_IMPL, callTargetStub);
        if (lvaRetAddrVar == BAD_VAR_NUM)
        {
            lvaRetAddrVar = lvaGrabTemp(false, "Return address");
            lvaTable[lvaRetAddrVar].Type = TYP_I_IMPL;
            lvaSetVarAddrExposed(lvaRetAddrVar, AddressExposedReason.DISPATCH_RET_BUF);
        }

        // DispatchTailCalls(void** callersRetAddrSlot, void* target, ref byte retValue)
        _ = dispatcherCall.Args.PushBack(NewCallArg.CreateForPrimitive(gtNewLclVarAddrNode(TYP_BYREF, lvaRetAddrVar)));
        _ = dispatcherCall.Args.PushBack(NewCallArg.CreateForPrimitive(target));
        _ = dispatcherCall.Args.PushBack(NewCallArg.CreateForPrimitive(returnValueArgument));

        if (originalCall.Type is TYP_VOID)
        {
            return dispatcherCall;
        }

        assert(returnValue is not null);
        var comma = gtNewBinaryNode(GT_COMMA, originalCall.Type, dispatcherCall, returnValue);
        if (originalCall.HasMultiRegRetVal)
        {
            // CSE of this comma would disrupt the multi-register return value.
            comma.Flags |= GTF_DONT_CSE;
        }

        return comma;
    }

    public void fgMorphTailCallViaJitHelper(GenTreeCall call)
    {
        JITDUMP("fgMorphTailCallViaJitHelper (before):\n");
        DISPTREE(call);
        assert(!call.IsUnmanaged);
        assert(!call.IsHelperCall());
        assert(!call.IsImplicitTailCall);

        // Moving 'this' into normal arguments disables implicit null checks.
        // x86 evaluates the eventual target argument first, so materialize
        // complex delegate/vtable receivers before lowering introduces uses.
        var thisArgument = call.Args.ThisArg;
        if (thisArgument is not null)
        {
            GenTree? thisPointer = null;
            var objectPointer = thisArgument.Node;
            if ((call.IsDelegateInvoke || call.IsVirtualVtable) && (objectPointer.Oper is not GT_LCL_VAR))
            {
                var local = lvaGrabTemp(true, "tail call thisptr");
                var store = gtNewTempStore(local, objectPointer);
                var type = objectPointer.Type;
                thisPointer = gtNewBinaryNode(GT_COMMA, type, store, gtNewLclvNode(type, local));
                objectPointer = thisPointer;
            }

            if (call.NeedsNullCheck)
            {
                if ((thisPointer is null) && ((objectPointer.Flags & GTF_SIDE_EFFECT) == 0))
                {
                    thisPointer = gtClone(objectPointer, true);
                }

                var type = objectPointer.Type;
                if (thisPointer is null)
                {
                    var local = lvaGrabTemp(true, "tail call thisptr");
                    var store = gtNewTempStore(local, objectPointer);
                    var nullCheck = gtNewNullCheck(gtNewLclvNode(type, local));
                    store = gtNewBinaryNode(GT_COMMA, TYP_VOID, store, nullCheck);
                    thisPointer = gtNewBinaryNode(GT_COMMA, type, store, gtNewLclvNode(type, local));
                }
                else
                {
                    var nullCheck = gtNewNullCheck(thisPointer);
                    var receiver = gtClone(objectPointer, true);
                    assert(receiver is not null);
                    thisPointer = gtNewBinaryNode(GT_COMMA, type, nullCheck, receiver);
                }

                call.Flags &= ~GTF_CALL_NULLCHECK;
            }
            else
            {
                thisPointer = objectPointer;
            }

            // Retain virtual-stub flags until LowerVirtualStubCall handles the target.
            assert(thisPointer is not null);
            _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(thisPointer, thisArgument.SignatureType));
            call.Args.Remove(thisArgument);
        }

        // The x86 helper's stack suffix is old words, new words, flags, target.
        // Lowering replaces the 9/8/7 placeholders with the final values.
        // Flags select callee-save restoration (bit 0) and VSD dispatch (bit 1);
        // the helper requires saved registers in EDI, ESI, EBX order below EBP.
        var oldStackWords = lvaParameterStackSize / REGSIZE_BYTES;
        var oldCount = call.Args.PushBack(NewCallArg.CreateForPrimitive(gtNewIconNode(TYP_I_IMPL, oldStackWords))
            .WithWellKnownArg(WellKnownArg.X86TailCallSpecialArg));
        var newCount = call.Args.InsertAfter(oldCount, NewCallArg.CreateForPrimitive(gtNewIconNode(TYP_I_IMPL, 9))
            .WithWellKnownArg(WellKnownArg.X86TailCallSpecialArg));
        var flags = call.Args.InsertAfter(newCount, NewCallArg.CreateForPrimitive(gtNewIconNode(TYP_I_IMPL, 8))
            .WithWellKnownArg(WellKnownArg.X86TailCallSpecialArg));
        _ = call.Args.InsertAfter(flags, NewCallArg.CreateForPrimitive(gtNewIconNode(TYP_I_IMPL, 7))
            .WithWellKnownArg(WellKnownArg.X86TailCallSpecialArg));
        call.Args.IsVarArgs = true;
        call.Flags &= ~GTF_CALL_POP_ARGS;
        assert(!call.NeedsNullCheck);
        JITDUMP("fgMorphTailCallViaJitHelper (after):\n");
        DISPTREE(call);
    }
}
