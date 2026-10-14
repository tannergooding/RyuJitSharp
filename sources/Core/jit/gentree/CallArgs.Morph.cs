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
            if (!Configuration.FEATURE_FIXED_OUT_ARGS && !argument.AbiInfo.HasAnyRegisterSegment)
            {
                canEvaluateToTemp = false;
            }
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
                    if (!Configuration.FEATURE_FIXED_OUT_ARGS && !previous.AbiInfo.HasAnyRegisterSegment)
                    {
                        continue;
                    }
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
            // Debug inline throws use helpers and can overwrite the outgoing stack area.
            if (Configuration.FEATURE_FIXED_OUT_ARGS && !treatLikeCall && ((node.Flags & GTF_EXCEPT) != 0) &&
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
                    if (!Configuration.FEATURE_FIXED_OUT_ARGS && !previous.AbiInfo.HasAnyRegisterSegment)
                    {
                        continue;
                    }
                    if ((previous.EarlyNode is GenTree previousNode) && ((previousNode.Flags & GTF_ALL_EFFECT) != 0))
                    {
                        SetNeedsTemp(previous);
                    }
                    else if (Configuration.FEATURE_FIXED_OUT_ARGS && !previous.AbiInfo.HasAnyRegisterSegment)
                    {
                        previous.NeedPlace = true;
                    }
                    else if (Configuration.FEATURE_FIXED_OUT_ARGS && Configuration.FEATURE_ARG_SPLIT &&
                        previous.AbiInfo.IsSplitAcrossRegistersAndStack)
                    {
                        previous.NeedPlace = true;
                    }
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
                        JITDUMP($"Exception set for arg [{node.TreeId:D6}] interferes with previous tree " +
                            $"[{previousExceptionTree.TreeId:D6}]; must evaluate previous trees with exceptions to temps\n");
                        // Unspilled throwing predecessors can only share the previous
                        // tree's single exception; all interfere with this different set.
                        foreach (var previous in Args)
                        {
                            if (previous == argument)
                            {
                                break;
                            }
                            if (!Configuration.FEATURE_FIXED_OUT_ARGS && !previous.AbiInfo.HasAnyRegisterSegment)
                            {
                                continue;
                            }
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

        var hasRelevantStackArgs = HasStackArgs;
        if (Configuration.TARGET_WASM)
        {
            hasRelevantStackArgs = compiler.compLocallocUsed;
        }
        else if (Configuration.FEATURE_FIXED_OUT_ARGS)
        {
            hasRelevantStackArgs &= compiler.compLocallocUsed;
        }

        if (hasRelevantStackArgs)
        {
            foreach (var argument in EarlyArgs)
            {
                var node = argument.EarlyNode;
                assert(node is not null);
                assert(!compiler.gtTreeContainsOper(node, GT_QMARK));
                if (!argument.NeedTmp && argument.AbiInfo.HasAnyRegisterSegment)
                {
                    if ((!Configuration.FEATURE_FIXED_OUT_ARGS && ((node.Flags & GTF_EXCEPT) != 0)) ||
                        (compiler.compLocallocUsed && compiler.gtTreeContainsOper(node, GT_LCLHEAP)))
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
                assert(argument.EarlyNode is not null);
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

        if (!Configuration.HAS_FIXED_REGISTER_SET)
        {
            return;
        }

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
    }

    public unsafe void AddFinalArgsAndDetermineAbiInfo(Compiler compiler, GenTreeCall call)
    {
        assert(Unsafe.AreSame(ref call.Args, ref this));
        if ((_flags & Flags.HasAddedFinalArgs) != 0)
        {
            return;
        }

        JITDUMP($"Adding final args and determining ABI info for [{call.TreeId:D6}]:\n");
        _flags &= ~(Flags.HasRegArgs | Flags.HasStackArgs);
        assert(_lateHead is null);
        if (TargetOS.IsUnix && IsVarArgs)
        {
            throw new NotImplementedException("Morphing Vararg call not yet implemented on non Windows targets.");
        }

        // Keep the non-standard argument insertion rules in sync with fast tail-call mapping.
        var addStubCellArg = true;
        if (Configuration.TARGET_WASM)
        {
            addStubCellArg = false;
        }
        else if (Configuration.TARGET_X86)
        {
            addStubCellArg = compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI);
        }

        if (call.IsVirtualStub && addStubCellArg && !call.IsTailCallViaJitHelper)
        {
            var stubAddress = compiler.fgGetStubAddrArg(call);
            _ = InsertAfterThisOrFirst(
                NewCallArg.CreateForPrimitive(stubAddress).WithWellKnownArg(WellKnownArg.VirtualStubCell));
        }

        var needsIndirectionCell = Configuration.FEATURE_READYTORUN && !Configuration.TARGET_WASM &&
            call.IsR2RRelativeIndir && !call.IsDelegateInvoke &&
            (!Configuration.TARGET_XARCH || call.IsFastTailCall);
        if (needsIndirectionCell)
        {
            assert(call._entryPoint.addr is not null);
            var address = compiler.gtNewIconHandleNode((nint)call._entryPoint.addr, GTF_ICON_FTN_ADDR);
#if DEBUG
            address.TargetHandle = (nint)call._callMethHnd;
#endif
            if (Configuration.TARGET_ARM)
            {
                // LSRA does not yet account for this register kill on non-VSD calls.
                address.RegNum = TargetRuntimeMetadata.R2RIndirectParameterRegister;
                address.Flags |= GTF_DONT_CSE;
            }

            _ = InsertAfterThisOrFirst(
                NewCallArg.CreateForPrimitive(address).WithWellKnownArg(WellKnownArg.R2RIndirectionCell));
        }

        if (Configuration.TARGET_WASM && !call.IsUnmanaged)
        {
            var stackPointer = compiler.gtNewLclVarNode(TYP_I_IMPL, compiler.lvaWasmSpArg);
            _ = PushFront(NewCallArg.CreateForPrimitive(stackPointer)
                .WithWellKnownArg(WellKnownArg.WasmShadowStackPointer));
        }

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
            return register == TargetRuntimeMetadata.NoRegister
                ? new AbiPassingInformation(0)
                : AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(register, 0, Configuration.TARGET_POINTER_SIZE));
        }

        return classifier.Classify(compiler, argument.SignatureType, layout, argument.WellKnownArg);
    }

    public readonly int OutgoingArgsStackSize
        => int.Max(Compiler.GetOutgoingArgByteSize(_argsStackSize), Configuration.MIN_ARG_AREA_FOR_CALL);

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
                JITDUMP($"Removing arg {link.WellKnownArg} [{link.Node.TreeId:D6}] to prepare for re-morphing call\n");
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
           (register != TargetRuntimeMetadata.NoRegister);

    public static bool GetCustomRegister(
        Compiler compiler, CorInfoCallConvExtension callConv, WellKnownArg argument, out regNumber register)
    {
        register = TargetRuntimeMetadata.NoRegister;
        switch (argument)
        {
            case WellKnownArg.PInvokeFrame when Configuration.HAS_FIXED_REGISTER_SET &&
                (Configuration.TARGET_X86 || Configuration.TARGET_ARM):
            {
                register = TargetRuntimeMetadata.PInvokeFrameRegister;
                return true;
            }

            case WellKnownArg.ShiftLow when Configuration.HAS_FIXED_REGISTER_SET && Configuration.TARGET_X86:
            {
                register = TargetRuntimeMetadata.LongArgumentLowRegister;
                return true;
            }

            case WellKnownArg.ShiftHigh when Configuration.HAS_FIXED_REGISTER_SET && Configuration.TARGET_X86:
            {
                register = TargetRuntimeMetadata.LongArgumentHighRegister;
                return true;
            }

            case WellKnownArg.RetBuffer when Configuration.HAS_FIXED_REGISTER_SET:
            {
                if (hasFixedRetBuffReg(callConv))
                {
                    register = theFixedRetBuffReg(callConv);
                    return true;
                }

                break;
            }

            case WellKnownArg.VirtualStubCell when Configuration.HAS_FIXED_REGISTER_SET:
            {
                assert(compiler.virtualStubParamInfo is not null);
                register = compiler.virtualStubParamInfo.Reg;
                return true;
            }

            case WellKnownArg.R2RIndirectionCell when Configuration.HAS_FIXED_REGISTER_SET:
            {
                register = TargetRuntimeMetadata.R2RIndirectParameterRegister;
                return true;
            }

            case WellKnownArg.ValidateIndirectCallTarget when Configuration.HAS_FIXED_REGISTER_SET:
            {
#pragma warning disable CA1508 // Register assignments differ between targets.
                if (TargetRuntimeMetadata.ValidateIndirectCallAddressRegister !=
                    TargetRuntimeMetadata.FirstIntegerArgumentRegister)
#pragma warning restore CA1508
                {
                    register = TargetRuntimeMetadata.ValidateIndirectCallAddressRegister;
                    return true;
                }

                break;
            }

            case WellKnownArg.DispatchIndirectCallTarget when Configuration.HAS_FIXED_REGISTER_SET &&
                (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64):
            {
                register = TargetRuntimeMetadata.DispatchIndirectCallAddressRegister;
                return true;
            }

            case WellKnownArg.SwiftError when Configuration.HAS_FIXED_REGISTER_SET && Configuration.SWIFT_SUPPORT:
            {
                assert(callConv is CorInfoCallConvExtension.Swift);
                register = TargetRuntimeMetadata.SwiftErrorRegister;
                return true;
            }

            case WellKnownArg.SwiftSelf when Configuration.HAS_FIXED_REGISTER_SET && Configuration.SWIFT_SUPPORT:
            {
                assert(callConv is CorInfoCallConvExtension.Swift);
                register = TargetRuntimeMetadata.SwiftSelfRegister;
                return true;
            }
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
