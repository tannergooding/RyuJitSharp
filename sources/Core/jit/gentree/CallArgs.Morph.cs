// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct CallArgs
{
    public void SetNeedsTemp(CallArg argument)
    {
        argument.NeedTmp = true;
        _flags |= Flags.NeedsTemps;
    }

    /// <summary>Decide which argument values must be evaluated before late argument placement.</summary>
    public void ArgsComplete(Compiler compiler, GenTreeCall call)
    {
        var argumentCount = CountArgs();
        GenTree? previousExceptionTree = null;
        var previousExceptionFlags = ExceptionSetFlags.None;

        foreach (var argument in Args)
        {
            var node = argument.EarlyNode;
            assert(node is not null);
            var canEvaluateToTemp = true;
#if !FEATURE_FIXED_OUT_ARGS
            if (!argument.AbiInfo.HasAnyRegisterSegment)
            {
                canEvaluateToTemp = false;
            }
#endif
            if ((node.Flags & GTF_ASG) != 0)
            {
                if (!node.IsValue)
                {
                    // Outgoing struct copies may already have an early setup store.
                    assert(argument.NeedTmp);
                }
                else if (canEvaluateToTemp && (argumentCount > 1))
                {
                    SetNeedsTemp(argument);
                }

                // In "a, a = 5, a", the first read must precede the store.
                foreach (var previous in Args)
                {
                    if (previous == argument)
                    {
                        break;
                    }
#if !FEATURE_FIXED_OUT_ARGS
                    if (!previous.AbiInfo.HasAnyRegisterSegment)
                    {
                        continue;
                    }
#endif
                    if ((previous.EarlyNode is null) || previous.NeedTmp)
                    {
                        continue;
                    }

                    if (((previous.EarlyNode.Flags & GTF_ALL_EFFECT) != 0) ||
                        compiler.gtMayHaveStoreInterference(node, previous.EarlyNode))
                    {
                        SetNeedsTemp(previous);
                    }
                }
            }

            var treatLikeCall = (node.Flags & GTF_CALL) != 0;
            var exceptionFlags = ExceptionSetFlags.None;
#if FEATURE_FIXED_OUT_ARGS
            // Debug inline throws use helpers and can overwrite the outgoing stack area.
            if (!treatLikeCall && ((node.Flags & GTF_EXCEPT) != 0) &&
                (argumentCount > 1) && compiler.opts.compDbgCode)
            {
                exceptionFlags = compiler.gtCollectExceptions(node);
                if ((exceptionFlags & (ExceptionSetFlags.IndexOutOfRangeException | ExceptionSetFlags.OverflowException)) !=
                    ExceptionSetFlags.None)
                {
                    foreach (var other in Args)
                    {
                        if (other == argument)
                        {
                            continue;
                        }

                        if (!other.AbiInfo.HasAnyRegisterSegment)
                        {
                            treatLikeCall = true;
                            break;
                        }
                    }
                }
            }
#endif
            if (treatLikeCall)
            {
                if (canEvaluateToTemp)
                {
                    if (argumentCount > 1)
                    {
                        SetNeedsTemp(argument);
                    }
                    else if (varTypeIsFloating(node.Type) && (node.Oper is GT_CALL))
                    {
                        SetNeedsTemp(argument);
                    }
                }

                foreach (var previous in Args)
                {
                    if (previous == argument)
                    {
                        break;
                    }
#if !FEATURE_FIXED_OUT_ARGS
                    if (!previous.AbiInfo.HasAnyRegisterSegment)
                    {
                        continue;
                    }
#endif
                    if ((previous.EarlyNode is GenTree previousNode) && ((previousNode.Flags & GTF_ALL_EFFECT) != 0))
                    {
                        SetNeedsTemp(previous);
                    }
#if FEATURE_FIXED_OUT_ARGS
                    else if (!previous.AbiInfo.HasAnyRegisterSegment)
                    {
                        previous.NeedPlace = true;
                    }
#if FEATURE_ARG_SPLIT
                    else if (previous.AbiInfo.IsSplitAcrossRegistersAndStack)
                    {
                        previous.NeedPlace = true;
                    }
#endif
#endif
                }
            }
            else if ((node.Flags & GTF_EXCEPT) != 0)
            {
                if (previousExceptionTree is not null)
                {
                    if (previousExceptionFlags is ExceptionSetFlags.None)
                    {
                        previousExceptionFlags = compiler.gtCollectExceptions(previousExceptionTree);
                    }
                    if (exceptionFlags is ExceptionSetFlags.None)
                    {
                        exceptionFlags = compiler.gtCollectExceptions(node);
                    }

                    var exactlyOneKnown = uint.IsPow2((uint)exceptionFlags) &&
                        ((exceptionFlags & ExceptionSetFlags.UnknownException) == ExceptionSetFlags.None);
                    if (!exactlyOneKnown || (exceptionFlags != previousExceptionFlags))
                    {
#if DEBUG
                        JITDUMP($"Exception set for arg [{node.TreeId:D6}] interferes with previous tree " +
                            $"[{previousExceptionTree.TreeId:D6}]; must evaluate previous trees with exceptions to temps\n");
#endif
                        // Unspilled throwing predecessors can only share the previous
                        // tree's single exception; all interfere with this different set.
                        foreach (var previous in Args)
                        {
                            if (previous == argument)
                            {
                                break;
                            }
#if !FEATURE_FIXED_OUT_ARGS
                            if (!previous.AbiInfo.HasAnyRegisterSegment)
                            {
                                continue;
                            }
#endif
                            if ((previous.EarlyNode is GenTree previousNode) && ((previousNode.Flags & GTF_EXCEPT) != 0))
                            {
                                SetNeedsTemp(previous);
                            }
                        }
                    }
                }

                previousExceptionTree = node;
                previousExceptionFlags = exceptionFlags;
            }
        }

#if TARGET_WASM
        var hasRelevantStackArgs = compiler.compLocallocUsed;
#elif FEATURE_FIXED_OUT_ARGS
        var hasRelevantStackArgs = HasStackArgs && compiler.compLocallocUsed;
#else
        var hasRelevantStackArgs = HasStackArgs;
#endif
        if (hasRelevantStackArgs)
        {
            foreach (var argument in EarlyArgs)
            {
                var node = argument.EarlyNode;
                assert(!compiler.gtTreeContainsOper(node, GT_QMARK));
                if (!argument.NeedTmp && argument.AbiInfo.HasAnyRegisterSegment)
                {
#if !FEATURE_FIXED_OUT_ARGS
                    if (((node.Flags & GTF_EXCEPT) != 0) ||
                        (compiler.compLocallocUsed && compiler.gtTreeContainsOper(node, GT_LCLHEAP)))
#else
                    if (compiler.compLocallocUsed && compiler.gtTreeContainsOper(node, GT_LCLHEAP))
#endif
                    {
                        SetNeedsTemp(argument);
                        continue;
                    }
                }
            }
        }

        if (compiler.opts.IsCFGEnabled && (call.IsVirtual || call.IsDelegateInvoke))
        {
            // CFG target validation null-checks 'this' before late argument placement.
            assert(HasThisPointer);
            SetNeedsTemp(ThisArg);
            foreach (var argument in EarlyArgs)
            {
                if ((argument.EarlyNode.Flags & GTF_ALL_EFFECT) != 0)
                {
                    SetNeedsTemp(argument);
                }
            }
        }

        _flags |= Flags.ArgsComplete;
    }

    /// <summary>Order calls, spilled values, cost-ranked expressions, locals and integer constants for late placement.</summary>
    public readonly void SortArgs(Compiler compiler, GenTreeCall call, Span<CallArg> sortedArgs)
    {
        assert(AreArgsComplete);
        JITDUMP("\nSorting the arguments:\n");
        var argumentCount = 0;
        foreach (var argument in Args)
        {
            sortedArgs[argumentCount++] = argument;
        }

#if HAS_FIXED_REGISTER_SET
        assert(argumentCount > 0);
        var beginning = 0;
        var end = argumentCount - 1;
        var remaining = argumentCount;

        // Match the native partition swaps, including their ordering within each group.
        var current = argumentCount;
        do
        {
            current--;
            var argument = sortedArgs[current];
            if (!argument.Processed)
            {
                var node = argument.EarlyNode;
                assert(node is not null);
                if (node.Oper is GT_CNS_INT)
                {
                    noway_assert(current <= end);
                    argument.Processed = true;
                    if (current != end)
                    {
                        sortedArgs[current] = sortedArgs[end];
                        sortedArgs[end] = argument;
                    }

                    end--;
                    remaining--;
                }
            }
        }
        while (current > 0);

        if (remaining > 0)
        {
            for (current = beginning; current <= end; current++)
            {
                var argument = sortedArgs[current];
                if (!argument.Processed)
                {
                    var node = argument.EarlyNode;
                    assert(node is not null);
                    if ((node.Flags & GTF_CALL) != 0)
                    {
                        argument.Processed = true;
                        if (current != beginning)
                        {
                            sortedArgs[current] = sortedArgs[beginning];
                            sortedArgs[beginning] = argument;
                        }

                        beginning++;
                        remaining--;
                    }
                }
            }
        }

        if (remaining > 0)
        {
            for (current = beginning; current <= end; current++)
            {
                var argument = sortedArgs[current];
                if (!argument.Processed && argument.NeedTmp)
                {
                    argument.Processed = true;
                    if (current != beginning)
                    {
                        sortedArgs[current] = sortedArgs[beginning];
                        sortedArgs[beginning] = argument;
                    }

                    beginning++;
                    remaining--;
                }
            }
        }

        if (remaining > 0)
        {
            current = end + 1;
            do
            {
                current--;
                var argument = sortedArgs[current];
                if (!argument.Processed)
                {
                    var node = argument.EarlyNode;
                    assert(node is not null);
                    if ((node.Type is not TYP_STRUCT) && (node.Oper is GT_LCL_VAR or GT_LCL_FLD))
                    {
                        noway_assert(current <= end);
                        argument.Processed = true;
                        if (current != end)
                        {
                            sortedArgs[current] = sortedArgs[end];
                            sortedArgs[end] = argument;
                        }

                        end--;
                        remaining--;
                    }
                }
            }
            while (current > beginning);
        }

        var costsPrepared = false;
        while (remaining > 0)
        {
            CallArg? expensiveArgument = null;
            var expensiveIndex = -1;
            var expensiveCost = 0;

            for (current = beginning; current <= end; current++)
            {
                var argument = sortedArgs[current];
                if (!argument.Processed)
                {
                    var node = argument.EarlyNode;
                    assert(node is not null);
                    assert(((node.Oper is not GT_LCL_VAR and not GT_LCL_FLD) || (node.Type is TYP_STRUCT)) &&
                        (node.Oper is not GT_CNS_INT));
                    if (remaining == 1)
                    {
                        expensiveIndex = current;
                        expensiveArgument = argument;
                        assert(beginning == end);
                        break;
                    }
                    else if (compiler.opts.OptimizationEnabled)
                    {
                        if (!costsPrepared)
                        {
                            compiler.gtPrepareCost(node);
                        }
                        if (node.CostEx > expensiveCost)
                        {
                            expensiveCost = node.CostEx;
                            expensiveIndex = current;
                            expensiveArgument = argument;
                        }
                    }
                    else
                    {
                        // Native selects the last remaining expression when costs are unavailable.
                        expensiveIndex = current;
                        expensiveArgument = argument;
                    }
                }
            }

            noway_assert(expensiveIndex != -1);
            assert(expensiveArgument is not null);
            expensiveArgument.Processed = true;
            if (expensiveIndex != beginning)
            {
                sortedArgs[expensiveIndex] = sortedArgs[beginning];
                sortedArgs[beginning] = expensiveArgument;
            }

            beginning++;
            remaining--;
            costsPrepared = true;
        }

        assert(beginning == end + 1);
        assert(remaining == 0);
#endif
    }

    public unsafe void AddFinalArgsAndDetermineAbiInfo(Compiler compiler, GenTreeCall call)
    {
        assert(Unsafe.AreSame(ref call.Args, ref this));
        if ((_flags & Flags.HasAddedFinalArgs) != 0)
        {
            return;
        }

#if DEBUG
        JITDUMP($"Adding final args and determining ABI info for [{call.TreeId:D6}]:\n");
#endif
        _flags &= ~(Flags.HasRegArgs | Flags.HasStackArgs);
        assert(_lateHead is null);
        if (TargetOS.IsUnix && IsVarArgs)
        {
            throw new NotImplementedException("Morphing Vararg call not yet implemented on non Windows targets.");
        }

#if TARGET_WASM
        throw new NotImplementedException("Wasm shadow-stack call arguments are not yet ported.");
#else
        // Keep the non-standard argument insertion rules in sync with fast tail-call mapping.
        var addStubCellArg = true;
#if TARGET_X86
        addStubCellArg = compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI);
#endif
        if (call.IsVirtualStub && addStubCellArg && !call.IsTailCallViaJitHelper)
        {
            var stubAddress = compiler.fgGetStubAddrArg(call);
            _ = InsertAfterThisOrFirst(
                NewCallArg.CreateForPrimitive(stubAddress).WithWellKnownArg(WellKnownArg.VirtualStubCell));
        }

#if FEATURE_READYTORUN
#if TARGET_XARCH
        // Ordinary xarch calls recover the cell from the return address, but fast tail calls cannot.
        var needsIndirectionCell = call.IsR2RRelativeIndir && !call.IsDelegateInvoke && call.IsFastTailCall;
#else
        var needsIndirectionCell = call.IsR2RRelativeIndir && !call.IsDelegateInvoke;
#endif
        if (needsIndirectionCell)
        {
            assert(call._entryPoint.addr is not null);
            var address = compiler.gtNewIconHandleNode((nint)call._entryPoint.addr, GTF_ICON_FTN_ADDR);
#if DEBUG
            address.TargetHandle = (nint)call._callMethHnd;
#endif
#if TARGET_ARM
            // LSRA does not yet account for this register kill on non-VSD calls.
            address.RegNum = REG_R2R_INDIRECT_PARAM;
            address.Flags |= GTF_DONT_CSE;
#endif
            _ = InsertAfterThisOrFirst(
                NewCallArg.CreateForPrimitive(address).WithWellKnownArg(WellKnownArg.R2RIndirectionCell));
        }
#endif

        var info = new ClassifierInfo {
            CallConv = call.UnmanagedCallConv,
            IsVarArgs = IsVarArgs && !call.IsTailCallViaJitHelper,
            HasThis = HasThisPointer,
            HasRetBuff = HasRetBuffer,
        };
        var classifier = new PlatformClassifier(info);

        foreach (var argument in Args)
        {
            var node = argument.EarlyNode;
            assert(node is not null);
            if (node.Oper is GT_LCL_ADDR)
            {
                node.Type = TYP_I_IMPL;
            }

            argument.AbiInfo = ClassifyArgument(compiler, call, argument, ref classifier);
            JITDUMP($"Argument {GetIndex(argument)} ABI info: ");
#if DEBUG
            if (compiler.verbose)
            {
                argument.AbiInfo.Dump();
            }
#endif
            foreach (ref readonly var segment in argument.AbiInfo.Segments)
            {
                if (segment.IsPassedOnStack)
                {
                    _flags |= Flags.HasStackArgs;
                }
                else
                {
                    _flags |= Flags.HasRegArgs;
                    compiler.compFloatingPointUsed |= genIsValidFloatReg(segment.Register);
                }
            }
        }

        _argsStackSize = classifier.StackSize;
#if DEBUG
        if (compiler.verbose)
        {
            JITDUMP($"Args for call [{call.TreeId:D6}] {call.Oper.Name} after AddFinalArgsAndDetermineABIInfo:\n");
            foreach (var argument in Args)
            {
                argument.Dump();
            }
            JITDUMP("\n");
        }
#endif
        _flags |= Flags.AbiInformationDetermined | Flags.HasAddedFinalArgs;
#endif
    }

    /// <summary>Reclassify arguments without adding arguments or changing their IR.</summary>
    public void DetermineAbiInfo(Compiler compiler, GenTreeCall call)
    {
        var info = new ClassifierInfo {
            CallConv = call.UnmanagedCallConv,
            IsVarArgs = call.Args.IsVarArgs && !call.IsTailCallViaJitHelper,
            HasThis = call.Args.HasThisPointer,
            HasRetBuff = call.Args.HasRetBuffer,
        };
        var classifier = new PlatformClassifier(info);
        foreach (var argument in Args)
        {
            argument.AbiInfo = ClassifyArgument(compiler, call, argument, ref classifier);
        }

        _argsStackSize = classifier.StackSize;
        _flags |= Flags.AbiInformationDetermined;
    }

    private static unsafe AbiPassingInformation ClassifyArgument(
        Compiler compiler, GenTreeCall call, CallArg argument, ref PlatformClassifier classifier)
    {
        // ABI decisions use the signature, not the potentially retyped argument expression.
        var signatureClass = argument.SignatureClassHandle;
        var layout = signatureClass == NO_CLASS_HANDLE ? null : compiler.typGetObjLayout(signatureClass);
        if (GetCustomRegister(compiler, call.UnmanagedCallConv, argument.WellKnownArg, out var register))
        {
            return register == REG_NA
                ? new AbiPassingInformation(0)
                : AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(register, 0, TARGET_POINTER_SIZE));
        }

        return classifier.Classify(compiler, argument.SignatureType, layout, argument.WellKnownArg);
    }

    public readonly int OutgoingArgsStackSize
        => int.Max(Compiler.GetOutgoingArgByteSize(_argsStackSize), MIN_ARG_AREA_FOR_CALL);

    /// <summary>Remove final arguments before ABI reclassification; argument morphing must not have finished.</summary>
    public void ResetFinalArgsAndAbiInfo()
    {
        if ((_flags & Flags.HasAddedFinalArgs) == 0)
        {
            return;
        }

        assert(!AreArgsComplete);
        ref var link = ref _head;
        while (link is not null)
        {
            if (link.IsArgAddedLate)
            {
#if DEBUG
                JITDUMP($"Removing arg {link.WellKnownArg} [{link.Node.TreeId:D6}] to prepare for re-morphing call\n");
#endif
                link = link.NextRef;
            }
            else
            {
                link = ref link.NextRef;
            }
        }

        _flags &= ~(Flags.HasAddedFinalArgs | Flags.AbiInformationDetermined);
    }

    public static bool IsNonStandard(Compiler compiler, GenTreeCall call, CallArg argument)
        => GetCustomRegister(compiler, call.UnmanagedCallConv, argument.WellKnownArg, out var register) &&
           (register != REG_NA);

    public static bool GetCustomRegister(
        Compiler compiler, CorInfoCallConvExtension callConv, WellKnownArg argument, out regNumber register)
    {
        register = REG_NA;
        switch (argument)
        {
#if HAS_FIXED_REGISTER_SET
#if TARGET_X86 || TARGET_ARM
            case WellKnownArg.PInvokeFrame:
            {
                register = REG_PINVOKE_FRAME;
                return true;
            }
#endif
#if TARGET_X86
            case WellKnownArg.ShiftLow:
            {
                register = REG_LNGARG_LO;
                return true;
            }

            case WellKnownArg.ShiftHigh:
            {
                register = REG_LNGARG_HI;
                return true;
            }
#endif
            case WellKnownArg.RetBuffer:
            {
                if (hasFixedRetBuffReg(callConv))
                {
                    register = theFixedRetBuffReg(callConv);
                    return true;
                }

                break;
            }

            case WellKnownArg.VirtualStubCell:
            {
                assert(compiler.virtualStubParamInfo is not null);
                register = compiler.virtualStubParamInfo.Reg;
                return true;
            }

            case WellKnownArg.R2RIndirectionCell:
            {
                register = REG_R2R_INDIRECT_PARAM;
                return true;
            }

            case WellKnownArg.ValidateIndirectCallTarget:
            {
#pragma warning disable CA1508 // Register assignments differ between targets.
                if (REG_VALIDATE_INDIRECT_CALL_ADDR != REG_ARG_0)
#pragma warning restore CA1508
                {
                    register = REG_VALIDATE_INDIRECT_CALL_ADDR;
                    return true;
                }

                break;
            }

#if TARGET_AMD64 || TARGET_ARM64
            case WellKnownArg.DispatchIndirectCallTarget:
            {
                register = REG_DISPATCH_INDIRECT_CALL_ADDR;
                return true;
            }
#endif
#if SWIFT_SUPPORT
            case WellKnownArg.SwiftError:
            {
                assert(callConv is CorInfoCallConvExtension.Swift);
                register = REG_SWIFT_ERROR;
                return true;
            }

            case WellKnownArg.SwiftSelf:
            {
                assert(callConv is CorInfoCallConvExtension.Swift);
                register = REG_SWIFT_SELF;
                return true;
            }
#endif
#endif
            case WellKnownArg.StackArrayLocal:
            case WellKnownArg.AsyncAwaiter:
            case WellKnownArg.AsyncExecutionContext:
            case WellKnownArg.AsyncSynchronizationContext:
            case WellKnownArg.AsyncResumedUse:
            case WellKnownArg.AsyncResumedDef:
            {
                // Pseudo-arguments represent uses, not physical ABI argument locations.
                return true;
            }

            default:
            {
                break;
            }
        }

        return false;
    }
}
