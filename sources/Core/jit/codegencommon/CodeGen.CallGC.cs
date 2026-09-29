// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.ICorDebugInfo.VarLocType;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genEmitCallWithCurrentGC(ref EmitCallParams parameters)
    {
        parameters.ptrVars = GCInfo.gcVarPtrSetCur;
        parameters.gcrefRegs = GCInfo.gcRegGCrefSetCur;
        parameters.byrefRegs = GCInfo.gcRegByrefSetCur;
        Emitter.emitIns_Call(in parameters);

        var call = parameters.returnValueCall;
        if ((call is null) || !_compiler.opts.compDbgInfo || !_compiler.opts.compScopeInfo ||
            (_compiler.genCallSite2DebugInfoMap is null) || parameters.isJump)
        {
            return;
        }
        if (call._returnType == TYP_VOID)
        {
            return;
        }
        if (!_compiler.genCallSite2DebugInfoMap.TryGetValue(call, out var debugInfo))
        {
            return;
        }

        var info = new EmittedCallReturnInfo
        {
            callILOffset = debugInfo.GetRoot().Location.Offset,
            returnLocation = new emitLocation(Emitter),
        };
        var retBuffer = call.Args.RetBufferArg;
        if (retBuffer is not null)
        {
            var node = retBuffer.Node;
#if HAS_FIXED_REGISTER_SET
            assert(node.Oper.IsPutArg);
            node = node.AsUnOp().Op1.SkipCopyOrReload;
#endif
            if (node.Oper != GT_LCL_ADDR)
            {
                return;
            }

            var local = node.AsLclVarCommon();
            var stackLevelBias = 0;
#if TARGET_X86
            stackLevelBias = unchecked((int)getCurrentStackLevel());
            if (parameters.argSize > 0)
            {
                // The call popped these arguments before the codegen stack level was adjusted.
                stackLevelBias = unchecked(stackLevelBias - (int)parameters.argSize);
            }
#endif

            if (_compiler.lvaIsUnknownSizeLocal(local.LclNum))
            {
                return;
            }
            info.returnValueLoc = getSiVarLoc(in _compiler.lvaGetDesc(local.LclNum), local.LclOffs, stackLevelBias);
        }
        else if (call.HasMultiRegRetVal)
        {
            ref readonly var returnDescriptor = ref call.ReturnTypeDesc;
            var count = returnDescriptor.ReturnRegCount;
            if (count > 2)
            {
                return;
            }
            assert(count == 2);
            var reg1 = returnDescriptor.GetAbiReturnReg(0, call.UnmanagedCallConv);
            var reg2 = returnDescriptor.GetAbiReturnReg(1, call.UnmanagedCallConv);

#if TARGET_ARM64
            if ((!genIsValidIntReg(reg1) || !genIsValidIntReg(reg2)) &&
                (returnDescriptor.GetReturnFieldOffset(1) != TARGET_POINTER_SIZE))
            {
                // The debugger's two-register representation assumes pointer-sized pieces.
                return;
            }
#endif

#if !TARGET_64BIT
            // FP-containing pairs require both 64-bit pieces and debugger FP-register support.
            if (!genIsValidIntReg(reg1) || !genIsValidIntReg(reg2))
            {
                return;
            }
#elif !TARGET_AMD64 && !TARGET_ARM64
            // Only AMD64 and ARM64 include FP registers in their debugger register encodings.
            if (!genIsValidIntReg(reg1) || !genIsValidIntReg(reg2))
            {
                return;
            }
#endif

            info.returnValueLoc.storeVariableInRegisters(reg1, reg2);
        }
        else if (varTypeIsFloating(call.Type))
        {
#if TARGET_X86
            info.returnValueLoc.vlType = VLT_FPSTK;
            info.returnValueLoc.vlFPstk.vlfReg = 0;
#else
            info.returnValueLoc.storeVariableInRegisters(REG_FLOATRET, REG_NA);
#endif
        }
        else if (varTypeUsesFloatReg(call.Type))
        {
            info.returnValueLoc.storeVariableInRegisters(REG_FLOATRET, REG_NA);
        }
        else
        {
            info.returnValueLoc.storeVariableInRegisters(REG_INTRET, REG_NA);
        }

        assert(emittedCallReturnInfo is not null);
        emittedCallReturnInfo.Add(info);
    }
}
