// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Diagnostics;

namespace RyuJitSharp;

public partial class Compiler
{
    private GenTreeCall fgMorphArgs(GenTreeCall call)
    {
        var flagsSummary = GTF_EMPTY;
        var remorphing = call.Args.AreArgsComplete;
        call.Args.AddFinalArgsAndDetermineAbiInfo(this, call);
#if DEBUG
        JITDUMP($"{(remorphing ? "Re" : "")}Morphing args for {call.TreeId}.{call.Oper.Name}:\n");
#endif
        if (remorphing)
        {
            foreach (var argument in call.Args.LateArgs)
            {
                assert(argument.LateNode is not null);
                argument.LateNode = fgMorphTree(argument.LateNode);
                flagsSummary |= argument.LateNode.Flags;
            }
        }

        foreach (var argument in call.Args.Args)
        {
            var value = argument.EarlyNode;
            if (value is null)
            {
                assert(remorphing);
                continue;
            }

            argument.EarlyNode = value = fgMorphTree(value);
            if ((argument.WellKnownArg is WellKnownArg.ThisPointer) && !remorphing
                && call.IsExpandedEarly && call.IsVirtualVtable && !value.Oper.IsLocal)
            {
                call.Args.SetNeedsTemp(argument);
            }

            if (value.Oper is GT_LCL_ADDR)
            {
                value.Type = TYP_I_IMPL;
            }

            if (varTypeIsStruct(argument.SignatureType) && !remorphing
                && (argument.AbiInfo.IsPassedByReference || !fgTryMorphStructArg(argument)))
            {
                fgMakeOutgoingStructArgCopy(call, argument);
                if (argument.EarlyNode is GenTree early)
                {
                    flagsSummary |= early.Flags;
                }
            }

            assert(argument.EarlyNode is not null);
            flagsSummary |= argument.EarlyNode.Flags;
        }

        if (!remorphing)
        {
            call.Args.ArgsComplete(this, call);
        }
#if FEATURE_FIXED_OUT_ARGS && UNIX_AMD64_ABI
        if (!call.IsFastTailCall)
        {
            opts.compNeedToAlignFrame = true;
        }
#endif
        call.Flags &= ~GTF_ASG;
        if (!call.MayThrow(this))
        {
            call.Flags &= ~GTF_EXCEPT;
        }
        call.Flags &= ~GTF_ORDER_SIDEEFF;
        call.Flags |= flagsSummary & GTF_ALL_EFFECT;

        if (!remorphing && (call.Args.HasRegArgs || call.Args.NeedsTemps))
        {
            call.Args.EvalArgsToTemps(this, call);
        }
#if DEBUG
        if (verbose)
        {
            JITDUMP($"Args for [{call.TreeId:D6}].{call.Oper.Name} after fgMorphArgs:\n");
            foreach (var argument in call.Args.Args)
            {
                argument.Dump();
            }
            jitprintf($"OutgoingArgsStackSize is {call.Args.OutgoingArgsStackSize}\n\n");
        }
#endif

        return call;
    }

    private unsafe bool fgTryMorphStructArg(CallArg argument)
    {
        ref var use = ref GenTree.EffectiveUse(ref argument.NodeRef);
        var node = use;
        assert(varTypeIsStruct(node.Type));
        if (argument.AbiInfo.NumSegments == 0)
        {
            // Pseudo arguments such as AsyncAwaiter may be decomposed for the
            // later stores into a continuation.
            if (fgTryReplaceStructLocalWithFields(ref argument.NodeRef))
            {
                argument.Node.SetMorphed(this, doChilren: true);
            }

            return true;
        }

        var isSplit = argument.AbiInfo.IsSplitAcrossRegistersAndStack;
#if TARGET_ARM
        if ((isSplit && (argument.AbiInfo.CountRegsAndStackSlots() > 4)) || (!isSplit && argument.AbiInfo.HasAnyStackSegment))
#else
        if (!argument.AbiInfo.HasAnyRegisterSegment)
#endif
        {
            if ((node.Oper is GT_LCL_VAR) && (lvaGetPromotionType(node.AsLclVar().LclNum) is PROMOTION_TYPE_INDEPENDENT))
            {
                if (!isSplit)
                {
                    var fields = fgMorphLclToFieldList(node.AsLclVar());
#if TARGET_X86
                    use = fields;
#else
                    use = fields.SoleFieldOrThis;
#endif
                    use = fgMorphTree(use);
                }
                else
                {
                    lvaSetVarDoNotEnregister(node.AsLclVar().LclNum, DoNotEnregisterReason.IsStructArg);
                }
            }
            else if (node.Oper is GT_LCL_FLD)
            {
                lvaSetVarDoNotEnregister(node.AsLclFld().LclNum, DoNotEnregisterReason.LocalField);
            }
            else if (node.Oper is GT_BLK)
            {
                var primitiveType = node.AsBlk().Layout.RegisterType;
                if (primitiveType is not TYP_UNDEF)
                {
#if DEBUG
                    JITDUMP($"Converting argument [{node.TreeId:D6}] to primitive indirection\n");
#endif
                    use = new GenTreeIndir(GT_IND, primitiveType, node.AsBlk().Addr, null, node, NodeThreading.None) {
                        Flags = node.Flags,
                    };
                }
            }

            argument.Node.ChangeType(use.Type);
            return true;
        }

        GenTree? newArgument = null;
        if (node.Oper is GT_LCL_VAR)
        {
            var local = node.AsLclVar();
            var localNumber = local.LclNum;
            ref var descriptor = ref lvaTable[localNumber];
            if (!argument.AbiInfo.HasExactlyOneRegisterSegment)
            {
                descriptor.lvIsMultiRegArg = true;
            }
            JITDUMP($"Struct argument V{localNumber:D2}: ");
#if DEBUG
            if (verbose)
            {
                argument.Dump();
            }
#endif
            if (descriptor.lvPromoted && !descriptor.lvDoNotEnregister
                && (!isSplit || FieldsMatchAbi(descriptor, argument.AbiInfo)))
            {
                newArgument = fgMorphTree(fgMorphLclToFieldList(local).SoleFieldOrThis);
            }
        }
        else if (node.Oper is GT_FIELD_LIST)
        {
            newArgument = node.AsFieldList().SoleFieldOrThis;
            if (newArgument == node)
            {
                return true;
            }
        }

        if (newArgument is null)
        {
            if ((node.Type is not TYP_STRUCT) && argument.AbiInfo.HasExactlyOneRegisterSegment)
            {
                return true;
            }
            if (!node.Oper.IsLocalRead && !node.Oper.IsLoad)
            {
                return false;
            }

            var layout = node.Type is TYP_STRUCT ? node.GetLayout(this) : null;
            var structSize = layout is not null ? layout.Size : node.Type.Size;
            if (layout is not null)
            {
                assert(ClassLayout.AreCompatible(typGetObjLayout(argument.SignatureClassHandle), layout));
            }
            else
            {
                assert(varTypeIsSimd(node.Type) && varTypeIsSimd(argument.SignatureType));
            }

            if (node.Oper.IsLoad)
            {
                var lastLoadSize = structSize % TARGET_POINTER_SIZE;
                if ((lastLoadSize != 0) && !int.IsPow2(lastLoadSize))
                {
                    // Unlike local slots, a non-local tail cannot be overread.
                    return false;
                }
                if (((node.AsIndir().Addr.Flags & GTF_PERSISTENT_SIDE_EFFECTS) != 0)
                    && (argument.AbiInfo.CountRegsAndStackSlots() > 1))
                {
                    // Multiple slot loads must not duplicate address effects.
                    return false;
                }
            }

            GenTree CreateSlotAccess(int offset, var_types type)
            {
                assert(offset < structSize);
                if (type is TYP_UNDEF)
                {
                    var sizeLeft = structSize - offset;
                    if (sizeLeft < TARGET_POINTER_SIZE)
                    {
                        type = sizeLeft switch {
                            1 => TYP_UBYTE,
                            2 => TYP_USHORT,
                            3 or 4 => TYP_INT,
                            5 or 6 or 7 or 8 => TYP_LONG,
                            _ => throw new UnreachableException(),
                        };
#if TARGET_ARM64
                        if ((offset > 0) && node.Oper.IsLocalRead)
                        {
                            // Native-sized local tails permit paired loads.
                            type = TYP_I_IMPL;
                        }
#endif
                    }
                    else if ((layout is not null) && ((offset % TARGET_POINTER_SIZE) == 0))
                    {
                        type = layout.GetGCPtrType(offset / TARGET_POINTER_SIZE);
                    }
                    else
                    {
                        type = TYP_I_IMPL;
                    }
                }

                if (node.Oper.IsLocalRead)
                {
                    var local = node.AsLclVarCommon();
                    ref var descriptor = ref lvaTable[local.LclNum];
                    GenTree result;
                    // Retyping a struct reinterpretation can expose a scalar local.
                    if ((local.LclOffs == 0) && (offset == 0) && (type.Size == descriptor.Type.Size))
                    {
                        result = gtNewLclVarNode(TYP_UNDEF, local.LclNum);
                    }
                    else
                    {
                        result = gtNewLclFldNode(type, local.LclNum, checked((ushort)(local.LclOffs + offset)));
                        if (!descriptor.lvDoNotEnregister)
                        {
                            lvaSetVarDoNotEnregister(local.LclNum, DoNotEnregisterReason.LocalField);
                        }
                    }

                    return fgMorphTree(result);
                }

                assert(node.Oper.IsLoad);
                var address = node.AsIndir().Addr;
                if (offset != 0)
                {
                    address = gtNewBinaryNode(GT_ADD, address.Type, gtCloneExpr(address), gtNewIconNode(TYP_I_IMPL, offset));
                }
                var load = gtNewIndir(type, address);
                load.SetMorphed(this, doChilren: true);

                return load;
            }

            var fieldList = new GenTreeFieldList();
            fieldList.SetMorphed(this);
            foreach (var segment in argument.AbiInfo.Segments)
            {
                if (segment.IsPassedInRegister)
                {
                    var access = CreateSlotAccess(segment.Offset, segment.GetRegisterType(layout));
                    fieldList.AddField(this, access, checked((ushort)segment.Offset), access.Type);
                }
                else
                {
                    for (var slotOffset = 0; slotOffset < segment.Size; slotOffset += TARGET_POINTER_SIZE)
                    {
                        var layoutOffset = segment.Offset + slotOffset;
                        var access = CreateSlotAccess(layoutOffset, TYP_UNDEF);
                        fieldList.AddField(this, access, checked((ushort)layoutOffset), access.Type);
                    }
                }
            }
            newArgument = fieldList.SoleFieldOrThis;
        }

        JITDUMP("fgTryMorphStructArg created tree:\n");
        DISPTREE(newArgument);
        use = newArgument;
        argument.Node.ChangeType(use.Type);

        return true;
    }

    private unsafe void fgMakeOutgoingStructArgCopy(GenTreeCall call, CallArg argument)
    {
        var value = argument.EarlyNode;
        assert(value is not null);
#if FEATURE_IMPLICIT_BYREFS
        if (opts.OptimizationEnabled && argument.AbiInfo.IsPassedByReference)
        {
            GenTree? implicitAddress = null;
            target_ssize_t implicitOffset = 0;
            var implicitLocal = value.IsImplicitByrefParameterValuePostMorph(this, ref implicitAddress, ref implicitOffset);
            GenTreeLclVarCommon? local = implicitLocal;
            if ((local is null) && value.Oper.IsLocal)
            {
                local = value.AsLclVarCommon();
                implicitOffset = local.LclOffs;
            }

            if (local is not null)
            {
                var localNumber = local.LclNum;
                ref var descriptor = ref lvaTable[localNumber];
                var omitCopy = call.IsTailCall;
                // Tail calls must omit the copy even without tracked liveness.
                if (!omitCopy && fgGlobalMorph)
                {
                    omitCopy = (descriptor.lvIsLastUseCopyOmissionCandidate || (implicitLocal is not null))
                        && !descriptor.lvPromoted && !descriptor.lvIsStructField && ((local.Flags & GTF_VAR_DEATH) != 0);
                }

                if (omitCopy)
                {
                    // The argument must not overlap the call's return buffer.
                    var returnBuffer = gtCallGetDefinedRetBufLclAddr(call);
                    if ((returnBuffer is not null) && (returnBuffer.LclNum == localNumber))
                    {
                        var returnBufferSize = typGetObjLayout(call.RetClsHnd).Size;
                        target_ssize_t returnBufferStart = returnBuffer.LclOffs;
                        var returnBufferEnd = returnBufferStart + returnBufferSize;
                        var argumentSize = argument.SignatureType is TYP_STRUCT
                            ? typGetObjLayout(argument.SignatureClassHandle).Size : argument.SignatureType.Size;
                        var implicitEnd = implicitOffset + argumentSize;
                        omitCopy = (returnBufferEnd <= implicitOffset) || (implicitEnd <= returnBufferStart);
                    }
                }

                if (omitCopy)
                {
                    if (implicitLocal is not null)
                    {
                        assert(implicitAddress is not null);
                        argument.EarlyNode = implicitAddress;
                    }
                    else
                    {
                        var replacement = new GenTreeLclFld(GT_LCL_ADDR, TYP_I_IMPL, localNumber, local.LclOffs,
                            data: null, layout: null, local, NodeThreading.None) {
                            Flags = local.Flags & GTF_COMMON_MASK & ~GTF_ALL_EFFECT,
                        };
                        argument.EarlyNode = replacement;
                        lvaSetVarAddrExposed(localNumber, AddressExposedReason.ESCAPE_ADDRESS);
                        // Copy propagation must not create a later use of this local.
                        fgKillDependentAssertions(localNumber, replacement);
                    }
                    JITDUMP($"did not need to make outgoing copy for last use of V{localNumber:D2}\n");
                    return;
                }
            }
        }
#endif
        JITDUMP("making an outgoing copy for struct arg\n");
        assert(!call.IsTailCall || !argument.AbiInfo.IsPassedByReference);
        var copyClass = argument.SignatureClassHandle;
        var temporary = 0;
        var found = false;
        if (!opts.MinOpts && (fgOrder is FGOrderTree))
        {
            assert(fgAvailableOutgoingArgTemps is not null);
            found = ForEachHbvBitSet(fgAvailableOutgoingArgTemps, localNumber => {
                var layout = lvaTable[(int)localNumber].Layout;
                assert(layout is not null);
                if (!layout.IsCustomLayout && (layout.ClassHandle == copyClass))
                {
                    temporary = (int)localNumber;
                    JITDUMP($"reusing outgoing struct arg V{temporary:D2}\n");
                    fgAvailableOutgoingArgTemps.clearBit(localNumber);
                    return HbvWalk.Abort;
                }

                return HbvWalk.Continue;
            }) == HbvWalk.Abort;
        }

        if (!found)
        {
            temporary = lvaGrabTemp(shortLifetime: true, "by-value struct argument");
            lvaSetStruct(temporary, copyClass, unsafeValueClsCheck: false);
        }
        if (fgUsedSharedTemps is not null)
        {
            fgUsedSharedTemps.Push(temporary);
        }
        else
        {
            assert(!fgGlobalMorph);
        }

        call.Args.SetNeedsTemp(argument);
        var copy = fgMorphCopyBlock(gtNewStoreLclVarNode(temporary, value));
        GenTree argumentNode;
        if (argument.AbiInfo.IsPassedByReference)
        {
            argumentNode = gtNewLclVarAddrNode(TYP_I_IMPL, temporary);
            lvaSetVarAddrExposed(temporary, AddressExposedReason.ESCAPE_ADDRESS);
        }
        else
        {
            argumentNode = gtNewLclvNode(lvaTable[temporary].Type, temporary);
        }
        argumentNode.SetMorphed(this);
#if FEATURE_FIXED_OUT_ARGS
        argument.EarlyNode = copy;
        argument.LateNode = argumentNode;
#else
        argumentNode = gtNewCommaNode(argumentNode.Type, copy, argumentNode);
        argumentNode.SetMorphed(this);
        argument.EarlyNode = argumentNode;
#endif
        if (!argument.AbiInfo.IsPassedByReference)
        {
            var morphed = fgTryMorphStructArg(argument);
            assert(morphed);
        }
    }
}

public partial struct CallArgs
{
    public void EvalArgsToTemps(Compiler compiler, GenTreeCall call)
    {
        InlineArray32<CallArg> inlineTable = default;
        var argumentCount = CountArgs();
        var sortedArgs = argumentCount <= 32 ? inlineTable[..argumentCount] : new CallArg[argumentCount];
        SortArgs(compiler, call, sortedArgs);

        ref var lateTail = ref _lateHead;
        foreach (var argument in sortedArgs)
        {
            if (argument.LateNode is not null)
            {
                // Outgoing struct copies may already have their setup and value.
                lateTail = argument;
                lateTail = ref argument.LateNextRef;
                continue;
            }

            var value = argument.EarlyNode;
            assert(value is not null);
            GenTree? setup = null;
            GenTree late;
#if !FEATURE_FIXED_OUT_ARGS
            assert(!argument.NeedPlace);
            // On push-based targets, stack arguments are evaluated in order.
            if (!argument.AbiInfo.HasAnyRegisterSegment)
            {
                continue;
            }
#endif
            if (argument.NeedTmp)
            {
#if DEBUG
                if (compiler.verbose)
                {
                    jitprintf("Argument with 'side effect'...\n");
                    compiler.gtDispTree(value);
                }
#endif
                var effectiveValue = value.EffectiveVal;
                if (effectiveValue.Oper is GT_FIELD_LIST)
                {
                    var fields = effectiveValue.AsFieldList();
                    fields.Flags &= ~GTF_ALL_EFFECT;

                    void AppendEffect(GenTree effect)
                    {
                        if (setup is null)
                        {
                            setup = effect;
                        }
                        else
                        {
                            setup = compiler.gtNewCommaNode(TYP_VOID, setup, effect);
                            setup.SetMorphed(compiler);
                        }
                    }

                    for (var comma = value; comma.Oper is GT_COMMA; comma = comma.AsOp().Op2)
                    {
                        AppendEffect(comma.AsOp().Op1);
                    }

                    foreach (var use in fields.Uses)
                    {
                        var temporary = compiler.lvaGrabTemp(shortLifetime: true, "argument with side effect");
                        var store = compiler.gtNewTempStore(temporary, use.Node);
                        store.SetMorphed(compiler);
                        AppendEffect(store);

                        var setupUse = compiler.gtNewLclvNode(use.Node.Type.ActualType, temporary);
                        setupUse.SetMorphed(compiler);
                        use.Node = setupUse;
                        fields.AddAllEffectsFlags(use.Node);
                    }

                    late = fields;
                }
                else
                {
                    var temporary = compiler.lvaGrabTemp(shortLifetime: true, "argument with side effect");
                    setup = compiler.gtNewTempStore(temporary, value);
                    setup.SetMorphed(compiler, doChilren: true);
                    var localType = value.Type.ActualType;
                    if (setup.IsCopyBlkOp)
                    {
                        setup = compiler.fgMorphCopyBlock(setup);
                    }

                    late = compiler.gtNewLclvNode(localType, temporary);
                    late.SetMorphed(compiler);
                }
#if DEBUG
                if (compiler.verbose)
                {
                    jitprintf("\n  Evaluate to a temp:\n");
                    compiler.gtDispTree(setup);
                }
#endif
            }
            else
            {
                // A later nested call may clobber the outgoing stack area even
                // when this argument does not need a temporary.
                if (!argument.AbiInfo.HasAnyRegisterSegment && !argument.NeedPlace)
                {
                    continue;
                }

                late = value;
#if DEBUG
                if (compiler.verbose)
                {
                    jitprintf(argument.AbiInfo.HasAnyRegisterSegment ? "Deferred argument:\n" : "Deferred stack argument:\n");
                    compiler.gtDispTree(value);
                    jitprintf("Moved to late list\n");
                }
#endif
                argument.EarlyNode = null;
            }

            if (setup is not null)
            {
                argument.EarlyNode = setup;
                call.Flags |= setup.Flags & GTF_SIDE_EFFECT;
                // An unnecessary retbuf temporary would break recognition of
                // the local definition represented by this call.
                noway_assert((argument.WellKnownArg is not WellKnownArg.RetBuffer) || !call.IsOptimizingRetBufAsLocal);
            }

            argument.LateNode = late;
            lateTail = argument;
            lateTail = ref argument.LateNextRef;
        }
#if DEBUG
        if (compiler.verbose)
        {
            jitprintf("\nRegister placement order:");
            foreach (var argument in LateArgs)
            {
                foreach (var segment in argument.AbiInfo.Segments)
                {
                    if (segment.IsPassedInRegister)
                    {
                        jitprintf($" {segment.Register.Name}");
                    }
                }
            }
            jitprintf("\n");
        }
#endif
    }
}
