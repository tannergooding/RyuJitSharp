// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct CallArgs
{
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
