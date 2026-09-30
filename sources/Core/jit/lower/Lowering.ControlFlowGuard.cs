// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private enum CFGCallKind
    {
        ValidateAndCall,
        Dispatch,
    }

    private CFGCallKind GetCFGCallKind(GenTreeCall call)
    {
#if TARGET_AMD64
        var mayUseDispatcher = call.IndirectionCellArgKind is WellKnownArg.None;
        var shouldUseDispatcher = true;
#elif TARGET_ARM64
        var mayUseDispatcher = true;
        var shouldUseDispatcher = false;
#else
        var mayUseDispatcher = false;
        var shouldUseDispatcher = false;
#endif
#if DEBUG
        switch (JitConfig.JitCFGUseDispatcher)
        {
            case 0:
            {
                shouldUseDispatcher = false;
                break;
            }

            case 1:
            {
                shouldUseDispatcher = true;
                break;
            }
        }
#endif
        return mayUseDispatcher && shouldUseDispatcher ? CFGCallKind.Dispatch : CFGCallKind.ValidateAndCall;
    }

    private GenTree CloneCFGUse(ref LIR.Use use, bool cloneConsts)
    {
        var definition = use.Def();
        var canClone = cloneConsts && definition.Oper.IsCnsIntOrI;
        if (!canClone && (definition.Oper is GT_LCL_VAR))
        {
            canClone = !CompilerInstance.lvaGetDesc(definition.AsLclVarCommon().LclNum).IsAddressExposed;
        }

        if (canClone)
        {
            var clone = CompilerInstance.gtCloneExpr(definition);
            assert(clone is not null);
            return clone;
        }

        var local = use.ReplaceWithLclVar(CompilerInstance);
        return CompilerInstance.gtNewLclvNode(TYP_I_IMPL, local);
    }

    private unsafe void LowerCFGCall(GenTreeCall call)
    {
        assert(!call.IsHelperCall(CORINFO_HELP_DISPATCH_INDIRECT_CALL));
        if (call.IsHelperCall(CORINFO_HELP_VALIDATE_INDIRECT_CALL))
        {
            return;
        }

        var compiler = CompilerInstance;
        var callTarget = call._controlExpr;
        if (callTarget is null)
        {
            assert((call._callType is not CT_INDIRECT) && (!call.IsVirtual || call.IsVirtualStubRelativeIndir));
            if (!call.IsVirtual)
            {
                return;
            }

            var cellArg = call.Args.FindWellKnownArg(WellKnownArg.VirtualStubCell);
            assert((cellArg is not null) && (cellArg.Node.Oper is GT_PUTARG_REG));
            var putArg = cellArg.Node.AsUnOp();
            var cellUse = new LIR.Use(BlockRange(), ref putArg.Op1Ref, putArg);

#if TARGET_XARCH
            const bool cloneConsts = true;
#else
            const bool cloneConsts = false;
#endif
            // Native allocates this first clone before selecting the final clone below.
            var cellClone = CloneCFGUse(ref cellUse, cloneConsts);
            if ((cellUse.Def().Oper is GT_LCL_VAR) || (cloneConsts && cellUse.Def().Oper.IsCnsIntOrI))
            {
                var clone = compiler.gtClone(cellUse.Def());
                assert(clone is not null);
                cellClone = clone;
            }
            else
            {
                var local = cellUse.ReplaceWithLclVar(compiler);
                cellClone = compiler.gtNewLclvNode(TYP_I_IMPL, local);
            }

            callTarget = Ind(cellClone);
            var controlRange = LIR.SeqTree(compiler, callTarget);
            ContainCheckRange(controlRange);
            BlockRange().InsertBefore(call, controlRange);
            call._controlExpr = callTarget;
        }
        else if (callTarget.Oper.IsIntegralConst)
        {
            return;
        }

        switch (GetCFGCallKind(call))
        {
            case CFGCallKind.ValidateAndCall:
            {
                // The validator preserves its target in a designated register; reloading it
                // from memory after validation would allow the checked and called targets to differ.
#if TARGET_AMD64 || TARGET_ARM64
                var regNode = new GenTreePhysReg(REG_VALIDATE_INDIRECT_CALL_ADDR, TYP_I_IMPL);
#elif TARGET_X86
                var regNode = new GenTreePhysReg(REG_ECX, TYP_I_IMPL);
#elif TARGET_ARM
                var regNode = new GenTreePhysReg(REG_R0, TYP_I_IMPL);
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
                var regNode = new GenTreePhysReg(REG_T3, TYP_I_IMPL);
#elif TARGET_WASM
                var regNode = new GenTreePhysReg(REG_NA, TYP_I_IMPL);
#else
                NYI("Lowering.LowerCFGCall validation register for this target");
                fatal(CORJIT_IMPLLIMITATION);
                return;
#endif
                var gotUse = BlockRange().TryGetUse(callTarget, out var targetUse);
                assert(gotUse);
                // Managed LIR.Use requires its replacement to be linked before updating the edge.
                BlockRange().InsertBefore(call, regNode);
                targetUse.ReplaceWith(regNode);

                var placeholder = compiler.gtNewZeroConNode(callTarget.Type);
                var validate = compiler.gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_VALIDATE_INDIRECT_CALL);
                validate.Args.PushFront(NewCallArg.CreateForPrimitive(placeholder)
                    .WithWellKnownArg(WellKnownArg.ValidateIndirectCallTarget));
                _ = compiler.fgMorphTree(validate);

                var validateRange = LIR.SeqTree(compiler, validate);
                var validateFirst = validateRange.FirstNode;
                var validateLast = validateRange.LastNode;
                BlockRange().InsertBefore(call, validateRange);

                gotUse = BlockRange().TryGetUse(placeholder, out var placeholderUse);
                assert(gotUse);
                placeholderUse.ReplaceWith(callTarget);
                placeholder.IsUnusedValue = true;

                LowerRange(validateFirst, validateLast);
                BlockRange().Remove(regNode);
                BlockRange().InsertAfter(validate, regNode);
                _ = LowerNode(regNode);
                MovePutArgNodesUpToCall(call);
                break;
            }

            case CFGCallKind.Dispatch:
            {
#if TARGET_AMD64 || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
#if TARGET_LOONGARCH64 || TARGET_RISCV64
                const regNumber dispatchRegister = REG_T0;
#elif TARGET_WASM
                const regNumber dispatchRegister = REG_NA;
#else
                const regNumber dispatchRegister = REG_DISPATCH_INDIRECT_CALL_ADDR;
#endif
                var targetArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(callTarget)
                    .WithWellKnownArg(WellKnownArg.DispatchIndirectCallTarget));
                targetArg.EarlyNode = null;
                targetArg.LateNode = callTarget;
                call.Args.PushLateBack(targetArg);
                targetArg.AbiInfo = AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(dispatchRegister, 0, TARGET_POINTER_SIZE));

                LowerArg(call, targetArg);

                call._callType = CT_HELPER;
                call._controlExpr = null;
                call._callMethHnd = Compiler.eeFindHelper(CORINFO_HELP_DISPATCH_INDIRECT_CALL);
                call.Flags &= ~GTF_CALL_VIRT_KIND_MASK;
#if FEATURE_READYTORUN
                call._entryPoint.addr = null;
                call._entryPoint.accessType = IAT_VALUE;
#endif
                call._controlExpr = LowerDirectCall(call);
                if (call._controlExpr is not null)
                {
                    var dispatchRange = LIR.SeqTree(compiler, call._controlExpr);
                    ContainCheckRange(dispatchRange);
                    BlockRange().InsertBefore(call, dispatchRange);
                }
#else
                unreached();
#endif
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }
}
