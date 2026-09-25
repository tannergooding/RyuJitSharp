// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTreeAddrMode Offset(GenTree baseAddress, uint offset)
    {
        var type = (baseAddress.Type is TYP_REF) ? TYP_BYREF : baseAddress.Type;
        return new GenTreeAddrMode(type, baseAddress, null, 0, unchecked((int)offset));
    }

    private GenTreeAddrMode OffsetByIndexWithScale(GenTree baseAddress, GenTree index, byte scale)
    {
        var type = (baseAddress.Type is TYP_REF) ? TYP_BYREF : baseAddress.Type;
        return new GenTreeAddrMode(type, baseAddress, index, scale, 0);
    }

    private unsafe GenTreeIndir LowerDelegateInvoke(GenTreeCall call)
    {
        noway_assert(call._callType is CT_USER_FUNC);

        var compiler = CompilerInstance;
        assert((compiler.info.compCompHnd->getMethodAttribs(call._callMethHnd) &
            (CORINFO_FLG_DELEGATE_INVOKE | CORINFO_FLG_FINAL)) ==
            (CORINFO_FLG_DELEGATE_INVOKE | CORINFO_FLG_FINAL));

        var thisArg = call.IsTailCallViaJitHelper ? call.Args.GetArgByIndex(0) : call.Args.ThisArg;
        assert(thisArg is not null);
        var thisArgNode = thisArg.Node;

#if HAS_FIXED_REGISTER_SET
        assert(thisArgNode.Oper is GT_PUTARG_REG);
        var thisExpr = thisArgNode.AsUnOp().Op1;
        var thisExprUse = new LIR.Use(BlockRange(), ref thisArgNode.AsUnOp().Op1Ref, thisArgNode);
#else
        var thisExpr = thisArgNode;
        var thisExprUse = new LIR.Use(BlockRange(), ref thisArg.NodeRef, call);
#endif

        GenTree baseAddress;
        if (thisExpr.Oper is GT_LCL_VAR)
        {
            baseAddress = compiler.gtNewLclvNode(thisExpr.Type, thisExpr.AsLclVar().LclNum);
        }
        else if (thisExpr.Oper is GT_LCL_FLD)
        {
            var local = thisExpr.AsLclFld();
            baseAddress = compiler.gtNewLclFldNode(thisExpr.Type, local.LclNum, local.LclOffs, local.Layout);
        }
        else
        {
            var delegateTemp = compiler.lvaGrabTemp(true, "delegate invoke call");
            baseAddress = compiler.gtNewLclvNode(thisExpr.Type, delegateTemp);
            _ = ReplaceWithLclVar(thisExprUse, delegateTemp);
            thisExpr = thisExprUse.Def();
        }

        var instanceOffset = compiler.eeGetEEInfo().offsetOfDelegateInstance;
        var newThisAddr = new GenTreeAddrMode(TYP_BYREF, thisExpr, null, 0, unchecked((int)instanceOffset));
        var newThis = compiler.gtNewIndir(TYP_REF, newThisAddr);

#if HAS_FIXED_REGISTER_SET
        thisArgNode.AsUnOp().Op1 = newThis;
        BlockRange().Remove(thisArgNode);
        BlockRange().InsertBefore(call, newThisAddr, newThis, thisArgNode);
#else
        thisExprUse.ReplaceWith(newThis);
        BlockRange().Remove(thisExpr);
        BlockRange().InsertBefore(call, thisExpr, newThisAddr, newThis);
#endif

        ContainCheckIndir(newThis);

        var targetOffset = compiler.eeGetEEInfo().offsetOfDelegateFirstTarget;
        var targetAddress = new GenTreeAddrMode(TYP_REF, baseAddress, null, 0, unchecked((int)targetOffset));
        return Ind(targetAddress);
    }

    private unsafe GenTree LowerVirtualVtableCall(GenTreeCall call)
    {
        noway_assert(call._callType is CT_USER_FUNC);

        var thisArg = call.IsTailCallViaJitHelper ? call.Args.GetArgByIndex(0) : call.Args.ThisArg;
        assert(thisArg is not null);
        var thisArgNode = thisArg.Node;
        assert(thisArgNode.Oper is GT_PUTARG_REG);
        var thisPtr = thisArgNode.AsUnOp().Op1;

        int localNumber;
        if (thisPtr.Oper is GT_LCL_VAR or GT_LCL_FLD)
        {
            localNumber = thisPtr.AsLclVarCommon().LclNum;
        }
        else
        {
            if (_vtableCallTemp == BAD_VAR_NUM)
            {
                _vtableCallTemp = CompilerInstance.lvaGrabTemp(true, "virtual vtable call");
            }

            var thisPtrUse = new LIR.Use(BlockRange(), ref thisArgNode.AsUnOp().Op1Ref, thisArgNode);
            _ = ReplaceWithLclVar(thisPtrUse, _vtableCallTemp);
            localNumber = _vtableCallTemp;
        }

        int offsetOfIndirection;
        int offsetAfterIndirection;
        bool isRelative;
        var compiler = CompilerInstance;
        compiler.info.compCompHnd->getMethodVTableOffset(call._callMethHnd,
            &offsetOfIndirection, &offsetAfterIndirection, &isRelative);

        GenTree local;
        if (thisPtr.Oper is GT_LCL_FLD)
        {
            local = new GenTreeLclFld(GT_LCL_FLD, thisPtr.Type, localNumber, thisPtr.AsLclFld().LclOffs);
        }
        else
        {
            local = new GenTreeLclVar(thisPtr.Type, localNumber);
        }

        GenTree result = Ind(Offset(local, VPTR_OFFS));
        if (offsetOfIndirection != CORINFO_VIRTUALCALL_NO_CHUNK)
        {
            if (isRelative)
            {
                var firstTemp = compiler.lvaGrabTemp(true, "lclNumTmp");
                var secondTemp = compiler.lvaGrabTemp(true, "lclNumTmp2");

                var store = compiler.gtNewTempStore(firstTemp, result);
                var relativeOffset = Ind(Offset(compiler.gtNewLclvNode(result.Type, firstTemp),
                    unchecked((uint)offsetOfIndirection)));
                var offset = compiler.gtNewIconNode(TYP_INT, unchecked(offsetOfIndirection + offsetAfterIndirection));
                result = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL,
                    compiler.gtNewLclvNode(result.Type, firstTemp), offset);

                var baseAddress = OffsetByIndexWithScale(result, relativeOffset, 1);
                var secondStore = compiler.gtNewTempStore(secondTemp, baseAddress);

                var firstRange = LIR.SeqTree(compiler, store);
                JITDUMP("result of obtaining pointer to virtual table:\n");
                DISPRANGE(firstRange);
                BlockRange().InsertBefore(call, firstRange);

                var secondRange = LIR.SeqTree(compiler, secondStore);
                ContainCheckIndir(relativeOffset.AsIndir());
                JITDUMP("result of obtaining pointer to virtual table 2nd level indirection:\n");
                DISPRANGE(secondRange);
                BlockRange().InsertAfter(store, secondRange);

                result = Ind(compiler.gtNewLclvNode(result.Type, secondTemp));
                result = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, result,
                    compiler.gtNewLclvNode(result.Type, secondTemp));
            }
            else
            {
                result = Ind(Offset(result, unchecked((uint)offsetOfIndirection)));
            }
        }
        else
        {
            assert(!isRelative);
        }

        if (!isRelative)
        {
            result = Ind(Offset(result, unchecked((uint)offsetAfterIndirection)));
        }

        return result;
    }

    private unsafe GenTree? LowerVirtualStubCall(GenTreeCall call)
    {
        assert(call.IsVirtualStub);

        var compiler = CompilerInstance;
        if (compiler.opts.ShouldUseDispatchHelpers || compiler.opts.IsCFGEnabled)
        {
            if (call._callType is CT_INDIRECT)
            {
                assert(call._controlExpr is not null);
                call._controlExpr.IsUnusedValue = true;
                call._controlExpr = null;
            }

            var helperLookup = compiler.compGetHelperFtn(CORINFO_HELP_INTERFACEDISPATCH_FOR_SLOT);
            assert(helperLookup.accessType is IAT_VALUE);
            call._callType = CT_USER_FUNC;
            call._callMethHnd = null;
            call._directCallAddress = helperLookup.addr;
            call.Flags &= ~GTF_CALL_VIRT_STUB;
            call._callMoreFlags &= ~GTF_CALL_M_VIRTSTUB_REL_INDIRECT;

            return null;
        }

        GenTree? result = null;
        if (call._callType is CT_INDIRECT)
        {
            assert(call._controlExpr is not null);
            var indirection = compiler.gtNewIndir(TYP_I_IMPL, call._controlExpr, GTF_IND_NONFAULTING);
            BlockRange().InsertAfter(call._controlExpr, indirection);
            call._controlExpr = indirection;
            indirection.Flags |= GTF_IND_REQ_ADDR_IN_REG;
            ContainCheckIndir(indirection);
        }
        else
        {
            var stubAddress = call.StubCallStubAddr;
            noway_assert(stubAddress is not null);
            noway_assert(call.IsVirtualStubRelativeIndir);

            var address = AddrGen((nint)stubAddress);
            if (call.IsTailCallViaJitHelper)
            {
                result = address;
            }
            else
            {
#if TARGET_ARMARCH || TARGET_AMD64 || TARGET_LOONGARCH64
                // Codegen uses the call target already computed in the virtual-stub argument register.
#else
                result = compiler.gtNewIndir(TYP_I_IMPL, address, GTF_IND_NONFAULTING);
#endif
            }
        }

        return result;
    }
}
