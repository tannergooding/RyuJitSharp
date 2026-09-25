// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genEmitCallWithCurrentGC(ref EmitCallParams parameters)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Call generation with current GC state requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
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
            assert(node.Oper.IsPutArg);
            node = node.AsUnOp().Op1.SkipCopyOrReload;
            if (node.Oper != GT_LCL_ADDR)
            {
                return;
            }

            var local = node.AsLclVarCommon();
            if (_compiler.lvaIsUnknownSizeLocal(local.LclNum))
            {
                return;
            }
            info.returnValueLoc = getSiVarLoc(in _compiler.lvaGetDesc(local.LclNum), local.LclOffs, 0);
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
            info.returnValueLoc.storeVariableInRegisters(reg1, reg2);
        }
        else if (varTypeIsFloating(call.Type))
        {
            info.returnValueLoc.storeVariableInRegisters(REG_FLOATRET, REG_NA);
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
#endif
    }
}
