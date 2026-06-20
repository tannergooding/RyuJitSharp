// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    private ref struct ImportCallHelper
    {
        /// <summary>tree for this pointer or uninitialized newobj temp (or null)</summary>
        public GenTree? newObjThis;

        /// <summary>resolved token for the call target</summary>
        public ref readonly CORINFO_RESOLVED_TOKEN resolvedToken;

        /// <summary>resolved constraint token (or null ref)</summary>
        public ref readonly CORINFO_RESOLVED_TOKEN constrainedResolvedToken;

        /// <summary>EE supplied info for the call</summary>
        public ref CORINFO_CALL_INFO callInfo;

        /// <summary>opcode that inspires the call</summary>
        public OPCODE opcode;

        /// <summary>IL offset of the opcode</summary>
        public IL_OFFSET opcodeOffs;

        /// <summary>IL prefix flags for the call</summary>
        public int prefixFlags;

        private int tailCallFlags;

        private CorInfoFlag clsFlags;

        private var_types callRetTyp;

        private ReadOnlySpan<byte> canTailCallFailReasonUtf8;

        private methodPointerInfo? ldftnInfo;

        private unsafe CORINFO_CLASS_HANDLE clsHnd;

        private unsafe CORINFO_METHOD_HANDLE methHnd;

        private unsafe CORINFO_CONTEXT_HANDLE exactContextHnd;

        private ref CORINFO_SIG_INFO sigInfo;

        private CORINFO_SIG_INFO otherSigInfo;

        private bool exactContextNeedsRuntimeLookup;

        private bool bIntrinsicImported;

        private bool canTailCall;

        /// <summary>We only need to cast the return value of pinvoke inlined calls that return small types</summary>
        private bool checkForSmallType;

        public unsafe bool TryImport(Compiler compiler, byte* codeAddr, byte* codeEndp, byte sz, ref var_types callTyp)
        {
            // memberRef should be set.
            // newObjThisPtr should be set for CEE_NEWOBJ

            JITDUMP($" {resolvedToken.token:X8}");

            var constrainedCall = (prefixFlags & PREFIX_CONSTRAINED) is not 0;
            assert(Unsafe.IsNullRef(in constrainedResolvedToken) || constrainedCall);

            var newBBcreatedForTailcallStress = false;
            var passedStressModeValidation = true;

            if (compiler.compIsForInlining)
            {
                if (compiler.compDonotInline)
                {
                    return false;
                }
                // We rule out inlinees with explicit tail calls in fgMakeBasicBlocks.
                assert((prefixFlags & PREFIX_TAILCALL_EXPLICIT) is 0);
            }
#if DEBUG
            else if (compiler.compTailCallStress)
            {
                // Have we created a new BB after the "call" instruction in fgMakeBasicBlocks()?
                // Tail call stress only recognizes call+ret patterns and forces them to be
                // explicit tail prefixed calls.  Also fgMakeBasicBlocks() under tail call stress
                // doesn't import 'ret' opcode following the call into the basic block containing
                // the call instead imports it to a new basic block.  Note that fgMakeBasicBlocks()
                // is already checking that there is an opcode following call and hence it is
                // safe here to read next opcode without bounds check.

                // Next opcode is a CEE_RET
                newBBcreatedForTailcallStress = impOpcodeIsCallOpcode(opcode) && ((OPCODE)(codeAddr[sz]) is CEE_RET);

                var hasTailPrefix = (prefixFlags & PREFIX_TAILCALL_EXPLICIT) is not 0;

                if (newBBcreatedForTailcallStress && !hasTailPrefix)
                {
                    // Don't stress-tailcall named intrinsics: many of them are imported as
                    // non-CALL IR nodes (e.g. GC.KeepAlive -> GT_KEEPALIVE), which would
                    // leave a BBJ_RETURN block that doesn't end in a CALL/RETURN and
                    // confuse later phases (see
                    // https://github.com/dotnet/runtime/issues/122479). Suppress both the
                    // explicit and the implicit tailcall promotion in that case.
                    if ((callInfo.methodFlags & CORINFO_FLG_INTRINSIC) != 0)
                    {
                        JITDUMP(" (Tailcall stress: skipping intrinsic)");
                        passedStressModeValidation = false;
                    }
                    else
                    {
                        // Do a more detailed evaluation of legality
                        var passedConstraintCheck = compiler.checkTailCallConstraint(opcode, resolvedToken, constrainedCall ? ref constrainedResolvedToken : ref Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>());

                        // Avoid setting compHasBackwardsJump = true via tail call stress if the method cannot have patchpoints.
                        var mayHavePatchpoints = compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0) && (JitConfig.TC_OnStackReplacement > 0) && compiler.compCanHavePatchpoints();

                        if (passedConstraintCheck && (mayHavePatchpoints || compiler.compHasBackwardJump))
                        {
                            // Now check with the runtime
                            var declaredCalleeHnd = callInfo.hMethod;
                            var isVirtual = callInfo.kind is CORINFO_VIRTUALCALL_STUB or CORINFO_VIRTUALCALL_VTABLE;
                            var exactCalleeHnd = isVirtual ? null : declaredCalleeHnd;

                            if (compiler.info.compCompHnd->canTailCall(compiler.info.compMethodHnd, declaredCalleeHnd, exactCalleeHnd, hasTailPrefix))
                            {
                                // Stress the tailcall.
                                JITDUMP(" (Tailcall stress: prefixFlags |= PREFIX_TAILCALL_EXPLICIT)");
                                prefixFlags |= PREFIX_TAILCALL_EXPLICIT | PREFIX_TAILCALL_STRESS;
                            }
                            else
                            {
                                // Runtime disallows this tail call
                                JITDUMP(" (Tailcall stress: runtime preventing tailcall)");
                                passedStressModeValidation = false;
                            }
                        }
                        else
                        {
                            // Constraints disallow this tail call
                            JITDUMP(" (Tailcall stress: constraint check failed)");
                            passedStressModeValidation = false;
                        }
                    }
                }
            }
#endif

            var isRecursive = !compiler.compIsForInlining && (callInfo.hMethod == compiler.info.compMethodHnd);

            // If we've already disqualified this call as a tail call under tail call stress,
            // don't consider it for implicit tail calling either.
            //
            // When not running under tail call stress, we may mark this call as an implicit
            // tail call candidate. We'll do an "equivalent" validation during impImportCall.
            //
            // Note that when running under tail call stress, a call marked as explicit
            // tail prefixed will not be considered for implicit tail calling.
            if (passedStressModeValidation && compiler.impIsImplicitTailCallCandidate(opcode, codeAddr + sz, codeEndp, prefixFlags, isRecursive))
            {
                if (compiler.compIsForInlining)
                {
#if FEATURE_TAILCALL_OPT_SHARED_RETURN
                    // Are we inlining at an implicit tail call site? If so the we can flag
                    // implicit tail call sites in the inline body. These call sites
                    // often end up in non BBJ_RETURN blocks, so only flag them when
                    // we're able to handle shared returns.
                    assert(compiler.impInlineInfo.iciCall is not null);

                    if (compiler.impInlineInfo.iciCall.IsImplicitTailCall)
                    {
                        JITDUMP("\n (Inline Implicit Tail call: prefixFlags |= PREFIX_TAILCALL_IMPLICIT)");
                        prefixFlags |= PREFIX_TAILCALL_IMPLICIT;
                    }
#endif
                }
                else
                {
                    JITDUMP("\n (Implicit Tail call: prefixFlags |= PREFIX_TAILCALL_IMPLICIT)");
                    prefixFlags |= PREFIX_TAILCALL_IMPLICIT;
                }
            }

            // Treat this call as tail call for verification only if "tail" prefixed (i.e. explicit tail call).
            var explicitTailCall = (prefixFlags & PREFIX_TAILCALL_EXPLICIT) is not 0;
            var readonlyCall = (prefixFlags & PREFIX_READONLY) is not 0;

            if (opcode is not CEE_CALLI and not CEE_NEWOBJ)
            {
                // All calls and delegates need a security callout.
                // For delegates, this is the call to the delegate constructor, not the access check on the
                // LD(virt)FTN.
                compiler.impHandleAccessAllowed(callInfo.accessAllowed, callInfo.callsiteCalloutHelper);
            }

            callTyp = Import(compiler);

            if (compiler.compDonotInline)
            {
                // We do not check fails after lvaGrabTemp. It is covered with CoreCLR_13272 issue.
                assert((callTyp is TYP_UNDEF) || (compiler.compInlineResult.Observation is InlineObservation.CALLSITE_TOO_MANY_LOCALS));
                return false;
            }

            if (explicitTailCall || newBBcreatedForTailcallStress)
            {
                // If newBBcreatedForTailcallStress is true, we have created a new BB after the "call" instruction in fgMakeBasicBlocks(). So we need to jump to RET regardless.
                assert(!compiler.compIsForInlining);
                return compiler.impReturnInstruction(prefixFlags, ref opcode);
            }
            return true;
        }

        /// <summary>import a call-inspiring opcode</summary>
        /// <returns>Type of the call's return value.</returns>
        /// <remarks>
        ///   <para>If we're importing an inlinee and have realized the inline must fail, the call return type should be TYP_UNDEF. However we can't assert for this here yet because there are cases we miss. See issue #13272.</para>
        ///   <para>opcode can be CEE_CALL, CEE_CALLI, CEE_CALLVIRT, or CEE_NEWOBJ.</para>
        ///   <para>For CEE_NEWOBJ, newobjThis should be the temp grabbed for the allocated uninitialized object.</para>
        /// </remarks>
        private unsafe var_types Import(Compiler compiler)
        {
            assert(opcode is CEE_CALL or CEE_CALLVIRT or CEE_NEWOBJ or CEE_CALLI);
            assert(compiler.compCurBB is not null);

            // The current statement DI may not refer to the exact call, but for calls
            // we wish to be able to attach the exact IL instruction to get "return
            // value" support in the debugger, so create one with the exact IL offset.
            var debugInfo = compiler.impCreateDIWithCurrentStackInfo(opcodeOffs, isCall: true);

            callRetTyp = TYP_COUNT;
            canTailCall = true;
            tailCallFlags = (prefixFlags & PREFIX_TAILCALL);

            var mflags = (CorInfoFlag)(0);
            var call = null as GenTreeCall;
            var constraintCallThisTransform = CORINFO_NO_THIS_TRANSFORM;
            var isReadonlyCall = (prefixFlags & PREFIX_READONLY) is not 0;

            // Synchronized methods need to call CORINFO_HELP_MON_EXIT at the end. We could
            // do that before tailcalls, but that is probably not the intended
            // semantic. So just disallow tailcalls from synchronized methods.
            // Also, popping arguments in a varargs function is more work and NYI
            // If we have a security object, we have to keep our frame around for callers
            // to see any imperative security.
            // Reverse P/Invokes need a call to CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT
            // at the end, so tailcalls should be disabled.
            // Async methods need to restore contexts, so tailcalls should be disabled.
            if ((compiler.info.compFlags & CORINFO_FLG_SYNCH) is not 0)
            {
                canTailCall = false;
                canTailCallFailReasonUtf8 = "Caller is synchronized"u8;
            }
            else if (compiler.opts.IsReversePInvoke)
            {
                canTailCall = false;
                canTailCallFailReasonUtf8 = "Caller is Reverse P/Invoke"u8;
            }
            else if (compiler.compIsAsync)
            {
                canTailCall = false;
                canTailCallFailReasonUtf8 = "Caller is async method"u8;
            }
#if !FEATURE_FIXED_OUT_ARGS
            else if (compiler.info.compIsVarArgs)
            {
                canTailCall = false;
                canTailCallFailReasonUtf8 = "Caller is varargs"u8;
            }
#endif

            var varArgsCookie = null as GenTree;
            var instParam = null as GenTree;
            var asyncContinuation = null as GenTree;

            // Swift calls that might throw use a SwiftError* arg that requires additional IR to handle,
            // so if we're importing a Swift call, look for this type in the signature
            var swiftErrorNode = null as GenTree;

            // First create the call node

            if (opcode is CEE_CALLI)
            {
                if (compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
                {
                    var wasConverted = false;

                    fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
                    {
                        wasConverted = compiler.info.compCompHnd->convertPInvokeCalliToCall(pResolvedToken, !compiler.impCanPInvokeInlineCallSite(compiler.compCurBB));
                    }

                    if (wasConverted)
                    {
                        compiler.eeGetCallInfo(resolvedToken, in Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(), CORINFO_CALLINFO_ALLOWINSTPARAM, out var calliInfo);

                        var importCallHelper = new ImportCallHelper {
                            opcode = CEE_CALL,
                            resolvedToken = ref resolvedToken,
                            prefixFlags = prefixFlags,
                            callInfo = ref calliInfo,
                            opcodeOffs = opcodeOffs,
                        };
                        return importCallHelper.Import(compiler);
                    }
                }

                // Get the call site sig
                compiler.eeGetSig(resolvedToken.token, resolvedToken.tokenScope, resolvedToken.tokenContext, out otherSigInfo);
                sigInfo = ref Unsafe.AsRef(in otherSigInfo);

                callRetTyp = sigInfo.retType.VarType;
                call = compiler.impImportIndirectCall(sigInfo, debugInfo);

                // We don't know the target method, so we have to infer the flags,   or assume the worst-case.
                mflags = ((sigInfo.callConv & CORINFO_CALLCONV_HASTHIS) is not 0) ? 0 : CORINFO_FLG_STATIC;

#if DEBUG
                if (compiler.verbose)
                {
                    var structSize = (callRetTyp is TYP_STRUCT) ? compiler.eeTryGetClassSize(sigInfo.retTypeSigClass) : 0;
                    jitprintf($"\nIn Compiler::impImportCall: opcode is {opcode.Name}, kind={(int)(callInfo.kind)}, callRetType is {callRetTyp.Name}, structSize is {structSize}\n");
                }
#endif
            }
            else
            {
                var ni = NI_Illegal;

                // Passing CORINFO_CALLINFO_ALLOWINSTPARAM indicates that this JIT is prepared to
                // supply the instantiation parameters necessary to make direct calls to underlying
                // shared generic code, rather than calling through instantiating stubs.  If the
                // returned signature has CORINFO_CALLCONV_PARAMTYPE then this indicates that the JIT
                // must indeed pass an instantiation parameter.

                methHnd = callInfo.hMethod;

                sigInfo = ref callInfo.sig;
                callRetTyp = sigInfo.retType.VarType;

                mflags = callInfo.methodFlags;

#if DEBUG
                if (compiler.verbose)
                {
                    var structSize = (callRetTyp is TYP_STRUCT) ? compiler.eeTryGetClassSize(sigInfo.retTypeSigClass) : 0;
                    jitprintf($"\nIn Compiler::impImportCall: opcode is {opcode.Name}, kind={(int)(callInfo.kind)}, callRetType is {callRetTyp.Name}, structSize is {structSize}\n");
                }
#endif
                if (compiler.compIsForInlining)
                {
                    // Does the inlinee use StackCrawlMark

                    if ((mflags & CORINFO_FLG_DONT_INLINE_CALLER) is not 0)
                    {
                        compiler.compInlineResult.NoteFatal(InlineObservation.CALLEE_STACK_CRAWL_MARK);
                        return TYP_UNDEF;
                    }

                    // For now ignore varargs

                    if ((sigInfo.callConv & CORINFO_CALLCONV_MASK) == CORINFO_CALLCONV_NATIVEVARARG)
                    {
                        compiler.compInlineResult.NoteFatal(InlineObservation.CALLEE_HAS_NATIVE_VARARGS);
                        return TYP_UNDEF;
                    }

                    if ((sigInfo.callConv & CORINFO_CALLCONV_MASK) == CORINFO_CALLCONV_VARARG)
                    {
                        compiler.compInlineResult.NoteFatal(InlineObservation.CALLEE_HAS_MANAGED_VARARGS);
                        return TYP_UNDEF;
                    }
                }

                clsHnd = resolvedToken.hClass;
                clsFlags = callInfo.classFlags;

#if DEBUG
                // If this is a call to JitTestLabel.Mark, do "early inlining", and record the test attribute.

                // This recognition should really be done by knowing the methHnd of the relevant Mark method(s).
                // These should be in corelib.h, and available through a JIT/EE interface call.
                byte* pNamespaceName, pClassName;
                var pMethodName = compiler.info.compCompHnd->getMethodNameFromMetadata(methHnd, &pClassName, &pNamespaceName, enclosingClassName: null, maxEnclosingClassNames: 0);

                if ((pNamespaceName is not null) && (pClassName is not null) && (pMethodName is not null))
                {
                    var namespaceNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pNamespaceName);

                    if (namespaceNameUtf8.SequenceEqual("System.Runtime.CompilerServices"u8))
                    {
                        var classNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pClassName);

                        if (classNameUtf8.SequenceEqual("JitTestLabel"u8))
                        {
                            var methodNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pMethodName);

                            if (methodNameUtf8.SequenceEqual("Mark"u8))
                            {
                                return compiler.impImportJitTestLabelMark(sigInfo.numArgs);
                            }
                        }
                    }
                }
#endif

                var isIntrinsic = (mflags & CORINFO_FLG_INTRINSIC) is not 0;

                // <NICE> Factor this into getCallInfo </NICE>
                var isSpecialIntrinsic = false;

                if (isIntrinsic || (!compiler.info.compMatchedVM && !compiler.RunningSuperPmiReplay))
                {
                    // For mismatched VM (AltJit) we want to check all methods as intrinsic to ensure
                    // we get more accurate codegen. This particularly applies to HWIntrinsic usage.
                    // But don't do this under SuperPMI replay, because it's unlikely we'll have
                    // the right data in the MethodContext in that case.

                    var isTailCall = canTailCall && (tailCallFlags is not 0);

#if FEATURE_READYTORUN
                    CORINFO_CONST_LOOKUP entryPoint;

                    if (compiler.IsAot && (callInfo.kind is CORINFO_CALL))
                    {
                        entryPoint = callInfo.codePointerLookup.constLookup;
                    }
                    else
                    {
                        entryPoint = new CORINFO_CONST_LOOKUP();
                    }
#endif

                    var intrinsicCall = Intrinsic(compiler, mflags, isReadonlyCall, isTailCall, ref entryPoint, out ni, out isSpecialIntrinsic);

                    if (compiler.compDonotInline)
                    {
                        return TYP_UNDEF;
                    }

                    if (intrinsicCall is not null)
                    {
                        bIntrinsicImported = true;
                        return DoneCall(compiler, intrinsicCall);
                    }
                }

                if (((mflags & (CORINFO_FLG_VIRTUAL | CORINFO_FLG_EnC)) is (CORINFO_FLG_VIRTUAL | CORINFO_FLG_EnC)) && (opcode is CEE_CALLVIRT))
                {
                    NO_WAY("Virtual call to a function added via EnC is not supported");
                }

                if ((sigInfo.callConv & CORINFO_CALLCONV_MASK) is not CORINFO_CALLCONV_DEFAULT
                                                              and not CORINFO_CALLCONV_VARARG
                                                              and not CORINFO_CALLCONV_NATIVEVARARG)
                {
                    BADCODE("Bad calling convention");
                }

                //-------------------------------------------------------------------------
                //  Construct the call node
                //
                // Work out what sort of call we're making.
                // Dispense with virtual calls implemented via LDVIRTFTN immediately.

                constraintCallThisTransform = callInfo.thisTransform;
                exactContextHnd = callInfo.contextHandle;
                exactContextNeedsRuntimeLookup = callInfo.exactContextNeedsRuntimeLookup;

                switch (callInfo.kind)
                {
                    case CORINFO_VIRTUALCALL_STUB:
                    {
                        // can't call a static method
                        assert((mflags & CORINFO_FLG_STATIC) is 0);
                        assert((clsFlags & CORINFO_FLG_VALUECLASS) is 0);

                        if (callInfo.stubLookup.lookupKind.needsRuntimeLookup)
                        {
                            if (callInfo.stubLookup.lookupKind.runtimeLookupKind is CORINFO_LOOKUP_NOT_SUPPORTED)
                            {
                                // Runtime does not support inlining of all shapes of runtime lookups
                                // Inlining has to be aborted in such a case
                                assert(compiler.compInlineResult is not null);
                                compiler.compInlineResult.NoteFatal(InlineObservation.CALLSITE_HAS_COMPLEX_HANDLE);
                                return TYP_UNDEF;
                            }

                            var stubAddr = compiler.impRuntimeLookupToTree(callInfo.stubLookup, methHnd);

                            // stubAddr tree may require a new temp.
                            // If we're inlining, this may trigger the too many locals inline failure.
                            //
                            // If so, we need to bail out.

                            if (compiler.compDonotInline)
                            {
                                return TYP_UNDEF;
                            }

                            // This is the rough code to set up an indirect stub call
                            assert(stubAddr is not null);

                            // The stubAddr may be a
                            // complex expression. As it is evaluated after the args,
                            // it may cause registered args to be spilled. Simply spill it.
                            //
                            if (stubAddr.Oper is not GT_LCL_VAR)
                            {
                                var lclNum = compiler.lvaGrabTemp(shortLifetime: true, "VirtualCall with runtime lookup");

                                if (compiler.compDonotInline)
                                {
                                    return TYP_UNDEF;
                                }

                                compiler.impStoreToTemp(lclNum, stubAddr, CHECK_SPILL_NONE);
                                stubAddr = compiler.gtNewLclvNode(TYP_I_IMPL, lclNum);
                            }

                            // Create the actual call node
                            assert((sigInfo.callConv & CORINFO_CALLCONV_MASK) is not CORINFO_CALLCONV_VARARG and not CORINFO_CALLCONV_NATIVEVARARG);

                            call = compiler.gtNewIndCallNode(callRetTyp, stubAddr, debugInfo);

                            call.Flags |= (GTF_EXCEPT | GTF_CALL_VIRT_STUB | (stubAddr.Flags & GTF_GLOB_EFFECT));

#if TARGET_X86
                            // No tailcalls allowed for these yet...
                            canTailCall = false;
                            canTailCallFailReasonUtf8 = "VirtualCall with runtime lookup"u8;
#endif
                        }
                        else
                        {
                            // The stub address is known at compile time
                            call = compiler.gtNewCallNode(callRetTyp, CT_USER_FUNC, callInfo.hMethod, debugInfo);

                            call.StubCallStubAddr = callInfo.stubLookup.constLookup.addr;
                            call.Flags |= GTF_CALL_VIRT_STUB;

                            assert(callInfo.stubLookup.constLookup.accessType is not IAT_PPVALUE and not IAT_RELPVALUE);

                            if (callInfo.stubLookup.constLookup.accessType == IAT_PVALUE)
                            {
                                call._callMoreFlags |= GTF_CALL_M_VIRTSTUB_REL_INDIRECT;
                            }
                        }

#if FEATURE_READYTORUN
                        if (compiler.IsAot)
                        {
                            // Null check is sometimes needed for ready to run to handle
                            // non-virtual <-> virtual changes between versions
                            if (callInfo.nullInstanceCheck)
                            {
                                call.Flags |= GTF_CALL_NULLCHECK;
                            }
                        }
#endif

                        break;
                    }

                    case CORINFO_VIRTUALCALL_VTABLE:
                    {
                        // can't call a static method
                        assert((mflags & CORINFO_FLG_STATIC) is 0);
                        assert((clsFlags & CORINFO_FLG_VALUECLASS) is 0);

                        call = compiler.gtNewCallNode(callRetTyp, CT_USER_FUNC, callInfo.hMethod, debugInfo);
                        call.Flags |= GTF_CALL_VIRT_VTABLE;

                        if (compiler.opts.OptimizationEnabled)
                        {
                            // Mark this method to expand the virtual call target early in fgMorphCall
                            call.IsExpandedEarly = true;
                        }
                        break;
                    }

                    case CORINFO_VIRTUALCALL_LDVIRTFTN:
                    {
                        // can't call a static method
                        assert((mflags & CORINFO_FLG_STATIC) is 0);
                        assert((clsFlags & CORINFO_FLG_VALUECLASS) is 0);

                        var needsFatPointerHandling = (sigInfo.sigInst.methInstCount is not 0) && compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI);

                        if (needsFatPointerHandling)
                        {
                            // NativeAOT generic virtual method: need to handle potential fat function pointers
                            // Spill any side-effecting arguments before we do the LDVIRTFTN
                            compiler.impSpillSideEffects(false, CHECK_SPILL_ALL, ("fat pointer arg spill"));
                        }

                        // OK, We've been told to call via LDVIRTFTN, so just
                        // take the call now....
                        var indCall = compiler.gtNewIndCallNode(callRetTyp, null, debugInfo);

                        if (sigInfo.isAsyncCall())
                        {
                            compiler.impSetupAsyncCall(indCall, opcode, prefixFlags, debugInfo);

                            if (compiler.compDonotInline)
                            {
                                return TYP_UNDEF;
                            }
                        }

                        compiler.impPopCallArgs(sigInfo, indCall);

                        if (indCall.IsAsync)
                        {
                            compiler.impInsertAsyncArgsForLdvirtftnCall(indCall);
                        }

                        var thisPtr = compiler.impPopStack().val;
                        thisPtr = compiler.impTransformThis(thisPtr, constrainedResolvedToken, callInfo.thisTransform);
                        assert(thisPtr is not null);

                        var origThisPtr = thisPtr;

                        // Clone the (possibly transformed) "this" pointer
                        thisPtr = compiler.impCloneExpr(thisPtr, out var thisPtrCopy, CHECK_SPILL_ALL, "LDVIRTFTN this pointer");

                        // We cloned the "this" pointer, mark it as a single def and set the class for it
                        if (thisPtr.Oper.IsLocal && (thisPtr.Type is TYP_REF) && (origThisPtr != thisPtr))
                        {
                            var lclNum = thisPtr.AsLclVarCommon().LclNum;
                            compiler.lvaGetDesc(lclNum).lvSingleDef = true;
                            compiler.lvaSetClass(lclNum, origThisPtr);
                        }

                        var fptr = compiler.impImportLdvirtftn(thisPtr, resolvedToken, callInfo);
                        assert(fptr is not null);

                        indCall.Args.PushFront(NewCallArg.CreateForPrimitive(thisPtrCopy).WithWellKnownArg(WellKnownArg.ThisPointer));

                        // Now make an indirect call through the function pointer
                        indCall.ControlExpr = fptr;
                        indCall.Flags |= (GTF_EXCEPT | (fptr.Flags & GTF_GLOB_EFFECT));

                        if (needsFatPointerHandling)
                        {
                            var fptrLclNum = compiler.lvaGrabTemp(shortLifetime: true, "fat pointer temp");
                            compiler.impStoreToTemp(fptrLclNum, fptr, CHECK_SPILL_ALL);

                            indCall.ControlExpr = compiler.gtNewLclvNode(fptr.Type.ActualType, fptrLclNum);
                            compiler.addFatPointerCandidate(indCall);
                        }

#if FEATURE_READYTORUN
                        if (compiler.IsAot)
                        {
                            // Null check is needed for ready to run to handle
                            // non-virtual <-> virtual changes between versions
                            indCall.Flags |= GTF_CALL_NULLCHECK;
                        }
#endif

                        // Since we are jumping over some code, check that its OK to skip that code
                        assert((sigInfo.callConv & CORINFO_CALLCONV_MASK) is not CORINFO_CALLCONV_VARARG and not CORINFO_CALLCONV_NATIVEVARARG);

                        return Devirt(compiler, indCall);
                    }

                    case CORINFO_CALL:
                    {
                        // This is for a non-virtual, non-interface etc. call
                        call = compiler.gtNewCallNode(callRetTyp, CT_USER_FUNC, callInfo.hMethod, debugInfo);

                        // We remove the nullcheck for the GetType call intrinsic.
                        // TODO-CQ: JIT64 does not introduce the null check for many more helper calls and intrinsics.
                        if (callInfo.nullInstanceCheck && (((mflags & CORINFO_FLG_INTRINSIC) is 0) || (ni is not NI_System_Object_GetType)))
                        {
                            call.Flags |= GTF_CALL_NULLCHECK;
                        }

#if FEATURE_READYTORUN
                        if (compiler.IsAot)
                        {
                            call._entryPoint = callInfo.codePointerLookup.constLookup;
                        }
#endif
                        break;
                    }

                    case CORINFO_CALL_CODE_POINTER:
                    {
                        // The EE has asked us to call by computing a code pointer and then doing an
                        // indirect call.  This is because a runtime lookup is required to get the code entry point.

                        // These calls always follow a uniform calling convention, i.e. no extra hidden params
                        assert((sigInfo.callConv & CORINFO_CALLCONV_PARAMTYPE) is 0);
                        assert((sigInfo.callConv & CORINFO_CALLCONV_MASK) is not CORINFO_CALLCONV_VARARG and not CORINFO_CALLCONV_NATIVEVARARG);

                        var fptr = compiler.impLookupToTree(callInfo.codePointerLookup, GTF_ICON_FTN_ADDR, callInfo.hMethod);
                        assert(fptr is not null);

                        if (compiler.compDonotInline)
                        {
                            return TYP_UNDEF;
                        }

                        // Now make an indirect call through the function pointer

                        var lclNum = compiler.lvaGrabTemp(shortLifetime: true, "Indirect call through function pointer");
                        compiler.impStoreToTemp(lclNum, fptr, CHECK_SPILL_ALL);
                        fptr = compiler.gtNewLclvNode(TYP_I_IMPL, lclNum);

                        call = compiler.gtNewIndCallNode(callRetTyp, fptr, debugInfo);
                        call.Flags |= (GTF_EXCEPT | (fptr.Flags & GTF_GLOB_EFFECT));

                        if (callInfo.nullInstanceCheck)
                        {
                            call.Flags |= GTF_CALL_NULLCHECK;
                        }
                        break;
                    }

                    default:
                    {
                        NO_WAY("unknown call kind");
                        break;
                    }
                }

                // Set more flags

                assert(call is not null);

                if ((mflags & CORINFO_FLG_NOGCCHECK) is not 0)
                {
                    call._callMoreFlags |= GTF_CALL_M_NOGCCHECK;
                }

                if (isSpecialIntrinsic)
                {
                    // Mark call if it's one of the ones we will maybe treat as an intrinsic
                    call._callMoreFlags |= GTF_CALL_M_SPECIAL_INTRINSIC;
                }
            }

            // We're never verifying for CALLI, so this is not set.
            assert((clsHnd is not null) || (opcode is CEE_CALLI));

            // CALL_VIRT and NEWOBJ must have a THIS pointer
            assert((opcode is not CEE_CALLVIRT and not CEE_NEWOBJ) || ((sigInfo.callConv & CORINFO_CALLCONV_HASTHIS) is not 0));

            // static bit and hasThis are negations of one another
            assert(((mflags & CORINFO_FLG_STATIC) is not 0) == ((sigInfo.callConv & CORINFO_CALLCONV_HASTHIS) is 0));

            // Check special-cases etc

            if ((mflags & CORINFO_FLG_DELEGATE_INVOKE) is not 0)
            {
                // Special case - Check if it is a call to Delegate.Invoke().

                // can't call a static method
                assert((mflags & CORINFO_FLG_STATIC) is 0);
                assert((mflags & CORINFO_FLG_FINAL) is not 0);

                call._callMoreFlags |= GTF_CALL_M_DELEGATE_INV;

                if (callInfo.wrapperDelegateInvoke)
                {
                    call._callMoreFlags |= GTF_CALL_M_WRAPPER_DELEGATE_INV;
                }

                if (opcode is CEE_CALLVIRT)
                {
                    assert((mflags & CORINFO_FLG_FINAL) is not 0);

                    // It should have the GTF_CALL_NULLCHECK flag set. Reset it
                    assert((call.Flags & GTF_CALL_NULLCHECK) is not 0);

                    call.Flags &= ~GTF_CALL_NULLCHECK;
                }
            }

            var actualMethodRetTypeSigClass = sigInfo.retTypeSigClass;

            // Check for varargs

            if ((sigInfo.callConv & CORINFO_CALLCONV_MASK) is CORINFO_CALLCONV_VARARG or CORINFO_CALLCONV_NATIVEVARARG)
            {
                if (!compFeatureVarArg())
                {
                    BADCODE("Varargs not supported.");
                }

                call.Flags |= GTF_CALL_POP_ARGS;
                call.Args.IsVarArgs = true;

                // Can't allow tailcall for varargs as it is caller-pop. The caller
                // will be expecting to pop a certain number of arguments, but if we
                // tailcall to a function with a different number of arguments, we
                // are hosed. There are ways around this (caller remembers esp value,
                // varargs is not caller-pop, etc), but not worth it.

#if TARGET_X86
                if (canTailCall)
                {
                    canTailCall = false;
                    canTailCallFailReasonUtf8 = "Callee is varargs"u8;
                }
#endif

                // Get the total number of arguments - this is already correct for CALLI - for methods we have to get it from the call site

                if (opcode is not CEE_CALLI)
                {
#if DEBUG
                    var numArgsDef = sigInfo.numArgs;
#endif
                    compiler.eeGetCallSiteSig(resolvedToken.token, resolvedToken.tokenScope, resolvedToken.tokenContext, out otherSigInfo);
                    sigInfo = ref Unsafe.AsRef(in otherSigInfo);

                    // For vararg calls we must be sure to load the return type of the
                    // method actually being called, as well as the return types of the
                    // specified in the vararg signature. With type equivalency, these types
                    // may not be the same.
                    if (sigInfo.retTypeSigClass != actualMethodRetTypeSigClass)
                    {
                        if ((actualMethodRetTypeSigClass is not null) && (sigInfo.retType is not CORINFO_TYPE_CLASS
                                                                                         and not CORINFO_TYPE_BYREF
                                                                                         and not CORINFO_TYPE_PTR))
                        {
                            // Make sure that all valuetypes (including enums) that we push are loaded.
                            // This is to guarantee that if a GC is triggered from the prestub of this methods,
                            // all valuetypes in the method signature are already loaded.
                            // We need to be able to find the size of the valuetypes, but we cannot
                            // do a class-load from within GC.
                            compiler.info.compCompHnd->classMustBeLoadedBeforeCodeIsRun(actualMethodRetTypeSigClass);
                        }
                    }

#if DEBUG
                    assert(numArgsDef <= sigInfo.numArgs);
#endif
                }

                // We will have "cookie" as the last argument but we cannot push
                // it on the operand stack because we may overflow, so we append it
                // to the arg list next after we pop them
            }

            //--------------------------- Inline PInvoke ------------------------------
            // If this is a call to a PInvoke method, we may be able to inline the invocation frame.

            compiler.impCheckForPInvokeCall(call, methHnd, sigInfo, mflags, compiler.compCurBB);

#if UNIX_X86_ABI
            if ((call.Flags & GTF_CALL_UNMANAGED) is 0)
            {
                // On Unix x86 we use caller-cleaned convention.
                call.Flags |= GTF_CALL_POP_ARGS;
            }
#endif

            if ((call.Flags & GTF_CALL_UNMANAGED) is not 0)
            {
                // We set up the unmanaged call by linking the frame, disabling GC, etc
                // This needs to be cleaned up on return.
                // In addition, native calls have different normalization rules than managed code
                // (managed calling convention always widens return values in the callee)
                if (canTailCall)
                {
                    canTailCall = false;
                    canTailCallFailReasonUtf8 = "Callee is native"u8;
                }

                checkForSmallType = true;

                compiler.impPopArgsForUnmanagedCall(call, sigInfo, ref swiftErrorNode);
                return Done(compiler, call);
            }
            else if ((opcode is CEE_CALLI) && ((sigInfo.callConv & CORINFO_CALLCONV_MASK) is not CORINFO_CALLCONV_DEFAULT and not CORINFO_CALLCONV_VARARG))
            {
                void* cookie, pCookie;

                fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
                {
                    cookie = compiler.info.compCompHnd->GetCookieForPInvokeCalliSig(pSigInfo, &pCookie);
                }

                var cookieLookup = compiler.eeConvertToLookup(cookie, pCookie);
                call._callCookie = cookieLookup;

                if (canTailCall)
                {
                    canTailCall = false;
                    canTailCallFailReasonUtf8 = "PInvoke calli"u8;
                }
            }

            if (sigInfo.isAsyncCall())
            {
                compiler.impSetupAsyncCall(call, opcode, prefixFlags, debugInfo);

                if (compiler.compDonotInline)
                {
                    return TYP_UNDEF;
                }

                if (compiler.lvaNextCallAsyncContinuation is not BAD_VAR_NUM)
                {
                    asyncContinuation = compiler.gtNewLclVarNode(TYP_UNDEF, compiler.lvaNextCallAsyncContinuation);
                    compiler.lvaNextCallAsyncContinuation = BAD_VAR_NUM;
                }
                else
                {
                    asyncContinuation = compiler.gtNewNull();
                }
            }

            // Now create the argument list.

            if ((sigInfo.callConv & CORINFO_CALLCONV_MASK) == CORINFO_CALLCONV_VARARG)
            {
                // Special case - for varargs we have an extra argument
                void* varCookie, pVarCookie;

                fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
                {
                    varCookie = compiler.info.compCompHnd->getVarArgsHandle(pSigInfo, methHnd, &pVarCookie);
                    assert((varCookie is not null) != (pVarCookie is not null));
                    varArgsCookie = compiler.gtNewIconEmbHndNode(varCookie, pVarCookie, GTF_ICON_VARG_HDL, pSigInfo);
                }
            }

            //-------------------------------------------------------------------------
            // Extra arg for shared generic code and array methods
            //
            // Extra argument containing instantiation information is passed in the
            // following circumstances:
            // (a) To the "Address" method on array classes; the extra parameter is
            //     the array's type handle (a TypeDesc)
            // (b) To shared-code instance methods in generic structs; the extra parameter
            //     is the struct's type handle (a vtable ptr)
            // (c) To shared-code per-instantiation non-generic static methods in generic
            //     classes and structs; the extra parameter is the type handle
            // (d) To shared-code generic methods; the extra parameter is an
            //     exact-instantiation MethodDesc
            //
            // We also set the exact type context associated with the call so we can
            // inline the call correctly later on.

            if (sigInfo.hasTypeArg())
            {
                if (compiler.lvaNextCallGenericContext != BAD_VAR_NUM)
                {
                    instParam = compiler.gtNewLclVarNode(TYP_UNDEF, compiler.lvaNextCallGenericContext);
                    compiler.lvaNextCallGenericContext = BAD_VAR_NUM;
                }
                else
                {
                    assert(call._callType is CT_USER_FUNC);

                    if (clsHnd is null)
                    {
                        NO_WAY("CALLI on parameterized type");
                    }

                    assert(opcode != CEE_CALLI);

                    // Instantiated generic method
                    if (((nuint)(exactContextHnd) & (nuint)(CORINFO_CONTEXTFLAGS_MASK)) == (nuint)(CORINFO_CONTEXTFLAGS_METHOD))
                    {
                        assert(exactContextHnd != METHOD_BEING_COMPILED_CONTEXT());
                        var exactMethodHandle = (CORINFO_METHOD_HANDLE)((nuint)(exactContextHnd) & ~(nuint)(CORINFO_CONTEXTFLAGS_MASK));

                        if (!exactContextNeedsRuntimeLookup)
                        {
#if FEATURE_READYTORUN
                            if (compiler.IsAot)
                            {
                                instParam = compiler.gtNewIconEmbHndNode(callInfo.instParamLookup, GTF_ICON_METHOD_HDL, exactMethodHandle);

                                if (instParam is null)
                                {
                                    assert(compiler.compDonotInline);
                                    return TYP_UNDEF;
                                }
                            }
                            else
#endif
                            {
                                instParam = compiler.gtNewIconEmbMethHndNode(exactMethodHandle);
                                compiler.info.compCompHnd->methodMustBeLoadedBeforeCodeIsRun(exactMethodHandle);
                            }
                        }
                        else
                        {
                            instParam = compiler.impTokenToHandle(resolvedToken, mustRestoreHandle: true);

                            if (instParam is null)
                            {
                                assert(compiler.compDonotInline);
                                return TYP_UNDEF;
                            }
                        }
                    }
                    else
                    {
                        // otherwise must be an instance method in a generic struct,
                        // a static method in a generic type, or a runtime-generated array method

                        assert(((nuint)(exactContextHnd) & (nuint)(CORINFO_CONTEXTFLAGS_MASK)) == (nuint)(CORINFO_CONTEXTFLAGS_CLASS));
                        var exactClassHandle = compiler.eeGetClassFromContext(exactContextHnd);

                        if (compiler.compIsForInlining && ((clsFlags & CORINFO_FLG_ARRAY) is not 0))
                        {
                            compiler.compInlineResult.NoteFatal(InlineObservation.CALLEE_IS_ARRAY_METHOD);
                            return TYP_UNDEF;
                        }

                        if (((clsFlags & CORINFO_FLG_ARRAY) is not 0) && isReadonlyCall)
                        {
                            // We indicate "readonly" to the Address operation by using a null instParam.
                            instParam = compiler.gtNewIconNode(TYP_REF, 0);
                        }
                        else if (!exactContextNeedsRuntimeLookup)
                        {
#if FEATURE_READYTORUN
                            if (compiler.IsAot)
                            {
                                instParam = compiler.gtNewIconEmbHndNode(callInfo.instParamLookup, GTF_ICON_CLASS_HDL, exactClassHandle);

                                if (instParam is null)
                                {
                                    assert(compiler.compDonotInline);
                                    return TYP_UNDEF;
                                }
                            }
                            else
#endif
                            {
                                instParam = compiler.gtNewIconEmbClsHndNode(exactClassHandle);
                                compiler.info.compCompHnd->classMustBeLoadedBeforeCodeIsRun(exactClassHandle);
                            }
                        }
                        else
                        {
                            instParam = compiler.impParentClassTokenToHandle(resolvedToken, mustRestoreHandle: true);

                            if (instParam is null)
                            {
                                assert(compiler.compDonotInline);
                                return TYP_UNDEF;
                            }
                        }
                    }
                }
            }

            if ((opcode is CEE_NEWOBJ) && ((clsFlags & CORINFO_FLG_DELEGATE) is not 0))
            {
                // Only verifiable cases are supported.
                // dup; ldvirtftn; newobj; or ldftn; newobj.
                // IL test could contain unverifiable sequence, in this case optimization should not be done.
                if (compiler.impStackHeight > 0)
                {
                    var delegateTypeInfo = compiler.impStackTop().seTypeInfo;

                    if (delegateTypeInfo.IsMethod)
                    {
                        ldftnInfo = delegateTypeInfo.MethodPointerInfo;
                    }
                }
            }

            // The main group of arguments, and the this pointer.

            // 'this' is pushed on the IL stack before all call args, but if this is a
            // constrained call 'this' is a byref that may need to be dereferenced.
            // That dereference should happen _after_ all args, so we need to spill
            // them if they can interfere.

            var hasThis = ((mflags & CORINFO_FLG_STATIC) is 0) && ((sigInfo.callConv & CORINFO_CALLCONV_EXPLICITTHIS) is 0) && ((opcode is not CEE_NEWOBJ) || (newObjThis is not null));

            if (hasThis && (constraintCallThisTransform is CORINFO_DEREF_THIS))
            {
                compiler.impSpillSideEffects(spillGlobEffects: false, CHECK_SPILL_ALL, "constrained call requires dereference for 'this' right before call");
            }

            compiler.impPopCallArgs(sigInfo, call);

            // Extra args
            if ((instParam is not null) || (asyncContinuation is not null) || (varArgsCookie is not null))
            {
                if (Target.TgtArgOrder is Target.ARG_ORDER_R2L)
                {
                    if (varArgsCookie is not null)
                    {
                        call.Args.PushFront(NewCallArg.CreateForPrimitive(varArgsCookie).WithWellKnownArg(WellKnownArg.VarArgsCookie));
                    }

                    if (asyncContinuation is not null)
                    {
                        call.Args.PushFront(NewCallArg.CreateForPrimitive(asyncContinuation).WithWellKnownArg(WellKnownArg.AsyncContinuation));
                    }

                    if (instParam is not null)
                    {
                        call.Args.PushFront(NewCallArg.CreateForPrimitive(instParam).WithWellKnownArg(WellKnownArg.InstParam));
                    }
                }
                else
                {
                    if (asyncContinuation is not null)
                    {
                        call.Args.PushBack(NewCallArg.CreateForPrimitive(asyncContinuation).WithWellKnownArg(WellKnownArg.AsyncContinuation));
                    }

                    if (instParam is not null)
                    {
                        call.Args.PushBack(NewCallArg.CreateForPrimitive(instParam).WithWellKnownArg(WellKnownArg.InstParam));
                    }

                    if (varArgsCookie is not null)
                    {
                        call.Args.PushBack(NewCallArg.CreateForPrimitive(varArgsCookie).WithWellKnownArg(WellKnownArg.VarArgsCookie));
                    }
                }
            }

            if (asyncContinuation is not null)
            {
                compiler.impInheritAsyncContextsFromInliner(call);
            }

            // The "this" pointer
            if (hasThis)
            {
                GenTree? obj;

                if (opcode is CEE_NEWOBJ)
                {
                    assert(newObjThis is not null);
                    obj = newObjThis;
                }
                else
                {
                    obj = compiler.impPopStack().val;
                    obj = compiler.impTransformThis(obj, constrainedResolvedToken, constraintCallThisTransform);

                    if (compiler.compDonotInline)
                    {
                        return TYP_UNDEF;
                    }
                    assert(obj is not null);
                }

                // Store the "this" value in the call
                call.Flags |= (obj.Flags & GTF_GLOB_EFFECT);
                call.Args.PushFront(NewCallArg.CreateForPrimitive(obj).WithWellKnownArg(WellKnownArg.ThisPointer));

                if (compiler.impIsThis(obj))
                {
                    call._callMoreFlags |= GTF_CALL_M_NONVIRT_SAME_THIS;
                }
            }

            return Devirt(compiler, call);
        }

        /// <summary>possibly expand intrinsic call into alternate IR sequence</summary>
        /// <param name="compiler"></param>
        /// <param name="methodFlags">CORINFO_FLG_XXX flags of the intrinsic method</param>
        /// <param name="isReadonlyCall">true if call has a readonly prefix</param>
        /// <param name="isTailCall">true if call is in tail position</param>
        /// <param name="entryPoint">The entry point information required for R2R scenarios</param>
        /// <param name="intrinsicName"> intrinsic name (see enumeration in namedintrinsiclist.h) for "traditional" jit intrinsics</param>
        /// <param name="isSpecialIntrinsic">set true if intrinsic expansion is a call that is amenable to special downstream optimization opportunities</param>
        /// <returns>IR tree to use in place of the call, or null if the jit should treat the intrinsic call like a normal call.</returns>
        private unsafe GenTree? Intrinsic(Compiler compiler, CorInfoFlag methodFlags, bool isReadonlyCall, bool isTailCall, ref CORINFO_CONST_LOOKUP entryPoint, out NamedIntrinsic intrinsicName, out bool isSpecialIntrinsic)
        {
            // On success the IR tree may be a call to a different method or an inline
            // sequence. If it is a call, then the intrinsic processing here is responsible
            // for handling all the special cases, as upon return to impImportCall
            // expanded intrinsics bypass most of the normal call processing.
            //
            // Intrinsics are generally not recognized in minopts and debug codegen.
            //
            // However, certain traditional intrinsics are identifed as "must expand"
            // if there is no fallback implementation to invoke; these must be handled
            // in all codegen modes.
            //
            // New style intrinsics (where the fallback implementation is in IL) are
            // identified as "must expand" if they are invoked from within their
            // own method bodies.

            assert(compiler.impInlineRoot._inlineStrategy is not null);

            intrinsicName = NI_Illegal;
            isSpecialIntrinsic = false;

            var mustExpand = false;
            var isSpecial = false;
            var isIntrinsic = (methodFlags & CORINFO_FLG_INTRINSIC) is not 0;
            var memberRef = resolvedToken.token;

            var ni = compiler.lookupNamedIntrinsic(methHnd);

            if (isIntrinsic)
            {
                // The recursive non-virtual calls to Jit intrinsics are must-expand by convention.
                mustExpand = compiler.gtIsRecursiveCall(methHnd, false) && ((methodFlags & CORINFO_FLG_VIRTUAL) is 0);
            }
            else
            {
                // For mismatched VM (AltJit) we want to check all methods as intrinsic to ensure
                // we get more accurate codegen. This particularly applies to HWIntrinsic usage
                assert(!compiler.info.compMatchedVM);
            }

            // We specially support the following on all platforms to allow for dead
            // code optimization and to more generally support recursive intrinsics.

            if (isIntrinsic && (ni > NI_SPECIAL_IMPORT_START) && (ni < NI_PRIMITIVE_END))
            {
                if (ni < NI_SPECIAL_IMPORT_END)
                {
                    assert(ni > NI_SPECIAL_IMPORT_START);

                    switch (ni)
                    {
                        case NI_IsSupported_True:
                        {
                            assert(sigInfo.numArgs == 0);
                            compiler.impInlineRoot._inlineStrategy.NoteHardwareIntrinsicCheckObserved();
                            return compiler.gtNewIconNode(TYP_INT, 1);
                        }

                        case NI_IsSupported_False:
                        {
                            assert(sigInfo.numArgs == 0);
                            return compiler.gtNewIconNode(TYP_INT, 0);
                        }

                        case NI_IsSupported_Dynamic:
                        {
                            assert(compiler.impInlineRoot._inlineStrategy is not null);
                            compiler.impInlineRoot._inlineStrategy.NoteHardwareIntrinsicCheckObserved();
                            break;
                        }

                        case NI_IsSupported_Type:
                        {
                            CORINFO_CLASS_HANDLE typeArgHnd;
                            CorInfoType simdBaseJitType;

                            compiler.impInlineRoot._inlineStrategy.NoteHardwareIntrinsicCheckObserved();

                            typeArgHnd = compiler.info.compCompHnd->getTypeInstantiationArgument(clsHnd, 0);
                            simdBaseJitType = compiler.info.compCompHnd->getTypeForPrimitiveNumericClass(typeArgHnd);

                            switch (simdBaseJitType)
                            {
                                case CORINFO_TYPE_BYTE:
                                case CORINFO_TYPE_UBYTE:
                                case CORINFO_TYPE_SHORT:
                                case CORINFO_TYPE_USHORT:
                                case CORINFO_TYPE_INT:
                                case CORINFO_TYPE_UINT:
                                case CORINFO_TYPE_LONG:
                                case CORINFO_TYPE_ULONG:
                                case CORINFO_TYPE_FLOAT:
                                case CORINFO_TYPE_DOUBLE:
                                case CORINFO_TYPE_NATIVEINT:
                                case CORINFO_TYPE_NATIVEUINT:
                                {
                                    return compiler.gtNewIconNode(TYP_INT, 1);
                                }

                                default:
                                {
                                    return compiler.gtNewIconNode(TYP_INT, 0);
                                }
                            }
                        }

                        case NI_Throw_PlatformNotSupportedException:
                        {
                            return compiler.impUnsupportedNamedIntrinsic(CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED, methHnd, sigInfo, mustExpand);
                        }

                        case NI_Vector_GetCount:
                        {
                            var typeArgHnd = compiler.info.compCompHnd->getTypeInstantiationArgument(clsHnd, 0);
                            var simdBaseJitType = compiler.info.compCompHnd->getTypeForPrimitiveNumericClass(typeArgHnd);
                            var simdSize = compiler.info.compCompHnd->getClassSize(clsHnd);

                            switch (simdBaseJitType)
                            {
                                case CORINFO_TYPE_BYTE:
                                case CORINFO_TYPE_UBYTE:
                                case CORINFO_TYPE_SHORT:
                                case CORINFO_TYPE_USHORT:
                                case CORINFO_TYPE_INT:
                                case CORINFO_TYPE_UINT:
                                case CORINFO_TYPE_LONG:
                                case CORINFO_TYPE_ULONG:
                                case CORINFO_TYPE_FLOAT:
                                case CORINFO_TYPE_DOUBLE:
                                case CORINFO_TYPE_NATIVEINT:
                                case CORINFO_TYPE_NATIVEUINT:
                                {
                                    var simdBaseType = simdBaseJitType.PreciseVarType;
                                    var elementSize = simdBaseType.Size;
                                    var countNode = compiler.gtNewIconNode(TYP_INT, simdSize / elementSize);

#if FEATURE_SIMD
                                    countNode.Flags |= GTF_ICON_SIMD_COUNT;
#endif

                                    return countNode;
                                }

                                default:
                                {
                                    return compiler.impUnsupportedNamedIntrinsic(CORINFO_HELP_THROW_TYPE_NOT_SUPPORTED, methHnd, sigInfo, mustExpand);
                                }
                            }
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                }
                else if (ni < NI_SRCS_UNSAFE_END)
                {
                    assert(ni > NI_SRCS_UNSAFE_START);
                    assert(!mustExpand);
                    return compiler.impSRCSUnsafeIntrinsic(ni, clsHnd, methHnd, sigInfo, resolvedToken);
                }
                else
                {
                    assert((ni > NI_PRIMITIVE_START) && (ni < NI_PRIMITIVE_END));
                    return compiler.impPrimitiveNamedIntrinsic(ni, clsHnd, methHnd, sigInfo, entryPoint, mustExpand);
                }
            }

#if FEATURE_HW_INTRINSICS
            if ((ni > NI_HW_INTRINSIC_START) && (ni < NI_HW_INTRINSIC_END))
            {
                if (!isIntrinsic)
                {
#if TARGET_XARCH
                    // We can't guarantee that all overloads for the xplat intrinsics can be
                    // handled by the AltJit, so limit only the platform specific intrinsics
                    assert((LAST_NI_Vector512 + 1) == FIRST_NI_X86Base);

                    if (ni < LAST_NI_Vector512)
#elif TARGET_ARM64
                    // We can't guarantee that all overloads for the xplat intrinsics can be
                    // handled by the AltJit, so limit only the platform specific intrinsics
                    assert((LAST_NI_Vector128 + 1) == FIRST_NI_AdvSimd);

                    if (ni < LAST_NI_Vector128)
#else
#error Unsupported platform
#endif
                    {
                        // Several of the NI_Vector64/128/256 APIs do not have
                        // all overloads as intrinsic today so they will assert
                        return null;
                    }
                }

                var hwintrinsic = compiler.impHWIntrinsic(ni, clsHnd, methHnd, sigInfo, entryPoint, mustExpand);

                if (hwintrinsic is null)
                {
                    if (mustExpand)
                    {
                        return compiler.impUnsupportedNamedIntrinsic(CORINFO_HELP_THROW_NOT_IMPLEMENTED, methHnd, sigInfo, mustExpand);
                    }
                    return null;
                }

                // Fold result, if possible
                return compiler.gtFoldExpr(hwintrinsic);
            }
#endif

            if (ni is NI_System_Numerics_Intrinsic or NI_System_Runtime_Intrinsics_Intrinsic)
            {
                // These are special markers used just to ensure we still get the inlining profitability
                // boost. We actually have the implementation in managed, however, to keep the JIT simpler.
                return null;
            }

            if (!isIntrinsic)
            {
                // Outside the cases above, there are many intrinsics which apply to only a
                // subset of overload and where simply matching by name may cause downstream
                // asserts or other failures. Math.Min is one example, where it only applies
                // to the floating-point overloads.
                return null;
            }

            intrinsicName = ni;

            if (ni is NI_System_StubHelpers_GetStubContext)
            {
                // must be done regardless of DbgCode and MinOpts
                return compiler.gtNewLclvNode(TYP_I_IMPL, compiler.lvaStubArgumentVar);
            }
            else if (ni is NI_System_StubHelpers_NextCallReturnAddress)
            {
                // For now we just avoid inlining anything into these methods since
                // this intrinsic is only rarely used. We could do this better if we
                // wanted to by trying to match which call is the one we need to get
                // the return address of.
                compiler.info.compHasNextCallRetAddr = true;
                return new GenTree(GT_LABEL, TYP_I_IMPL);
            }
            else if (ni is NI_System_Runtime_CompilerServices_RuntimeHelpers_SetNextCallGenericContext)
            {
                var lvaNextCallGenericContext = compiler.lvaGrabTemp(shortLifetime: false, "Upcoming generic context");
                compiler.lvaGetDesc(lvaNextCallGenericContext).Type = TYP_I_IMPL;

                compiler.lvaNextCallGenericContext = lvaNextCallGenericContext;
                return compiler.gtNewStoreLclVarNode(lvaNextCallGenericContext, compiler.impPopStack().val);
            }
            else if (ni is NI_System_Runtime_CompilerServices_RuntimeHelpers_SetNextCallAsyncContinuation)
            {
                var lvaNextCallAsyncContinuation = compiler.lvaGrabTemp(shortLifetime: false, "Upcoming async continuation");
                compiler.lvaGetDesc(lvaNextCallAsyncContinuation).Type = TYP_REF;

                compiler.lvaNextCallAsyncContinuation = lvaNextCallAsyncContinuation;
                return compiler.gtNewStoreLclVarNode(lvaNextCallAsyncContinuation, compiler.impPopStack().val);
            }
            else if (ni is NI_System_Runtime_CompilerServices_AsyncHelpers_AsyncCallContinuation)
            {
                var node = new GenTree(GT_ASYNC_CONTINUATION, TYP_REF) {
                    HasOrderingSideEffect = true
                };
                node.Flags |= (GTF_CALL | GTF_GLOB_REF);

                compiler.info.compUsesAsyncContinuation = true;
                return node;
            }
            else if (ni is NI_System_Runtime_CompilerServices_AsyncHelpers_AsyncSuspend)
            {
                if (compiler.compIsForInlining)
                {
                    compiler.compInlineResult.NoteFatal(InlineObservation.CALLEE_ASYNC_SUSPEND);
                    return null;
                }

                var node = compiler.gtNewUnaryNode(GT_RETURN_SUSPEND, TYP_VOID, compiler.impPopStack().val);
                node.HasOrderingSideEffect = true;
                node.Flags |= (GTF_CALL | GTF_GLOB_REF);
                return node;
            }
            else if (ni is NI_System_Runtime_CompilerServices_AsyncHelpers_Await)
            {
                // These are marked intrinsics simply to match them by name in
                // the Await pattern optimization. Make sure we keep pIntrinsicName assigned
                // (it would be overridden if we left this up to the rest of this function).
                intrinsicName = ni;
                return null;
            }
            else if (ni is NI_System_Runtime_CompilerServices_AsyncHelpers_TailAwait)
            {
                if ((compiler.info.compMethodInfo->options & CORINFO_ASYNC_SAVE_CONTEXTS) is not 0)
                {
                    BADCODE("TailAwait is not supported in async methods that capture contexts");
                }

                compiler._nextAwaitIsTail = true;
                return compiler.gtNewNothingNode();
            }

            var betterToExpand = false;

            // Allow some lightweight intrinsics in Tier0 which can improve throughput
            // we're fine if intrinsic decides to not expand itself in this case unlike mustExpand.
            if (!mustExpand && compiler.opts.Tier0OptimizationEnabled)
            {
                switch (ni)
                {
                    // This one is just `return true/false`
                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsKnownConstant:
                    {
                        betterToExpand = true;
                        break;
                    }

                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_WriteBarrier:
                    {
                        betterToExpand = true;
                        break;
                    }

                    // Not expanding this can lead to noticeable allocations in T0
                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_CreateSpan:
                    {
                        betterToExpand = true;
                        break;
                    }

                    // We need these to be able to fold "typeof(...) == typeof(...)"
                    case NI_System_Type_GetTypeFromHandle:
                    case NI_System_Type_op_Equality:
                    case NI_System_Type_op_Inequality:
                    {
                        betterToExpand = true;
                        break;
                    }

                    // This allows folding "typeof(...).GetGenericTypeDefinition() == typeof(...)"
                    case NI_System_Type_GetGenericTypeDefinition:
                    {
                        betterToExpand = true;
                        break;
                    }

                    // These may lead to early dead code elimination
                    case NI_System_Type_get_IsValueType:
                    case NI_System_Type_get_IsPrimitive:
                    case NI_System_Type_get_IsEnum:
                    case NI_System_Type_get_IsByRefLike:
                    case NI_System_Type_IsAssignableFrom:
                    case NI_System_Type_IsAssignableTo:
                    case NI_System_Type_get_IsGenericType:
                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsReferenceOrContainsReferences:
                    {
                        betterToExpand = true;
                        break;
                    }

                    // Lightweight intrinsics
                    case NI_System_String_get_Chars:
                    case NI_System_String_get_Length:
                    case NI_System_Span_get_Item:
                    case NI_System_Span_get_Length:
                    case NI_System_ReadOnlySpan_get_Item:
                    case NI_System_ReadOnlySpan_get_Length:
                    case NI_System_BitConverter_DoubleToInt64Bits:
                    case NI_System_BitConverter_Int32BitsToSingle:
                    case NI_System_BitConverter_Int64BitsToDouble:
                    case NI_System_BitConverter_SingleToInt32Bits:
                    case NI_System_Buffers_Binary_BinaryPrimitives_ReverseEndianness:
                    case NI_System_Type_GetEnumUnderlyingType:
                    case NI_System_Type_get_TypeHandle:
                    case NI_System_RuntimeType_get_TypeHandle:
                    case NI_System_RuntimeTypeHandle_ToIntPtr:
                    {
                        betterToExpand = true;
                        break;
                    }

                    // This one is not simple, but it will help us
                    // to avoid some unnecessary boxing
                    case NI_System_Enum_HasFlag:
                    {
                        betterToExpand = true;
                        break;
                    }

                    // This one is made intrinsic specifically to avoid boxing in Tier0
                    case NI_System_ArgumentNullException_ThrowIfNull:
                    {
                        betterToExpand = true;
                        break;
                    }

                    // Most atomics are compiled to single instructions
                    case NI_System_Threading_Interlocked_And:
                    case NI_System_Threading_Interlocked_Or:
                    case NI_System_Threading_Interlocked_CompareExchange:
                    case NI_System_Threading_Interlocked_Exchange:
                    case NI_System_Threading_Interlocked_ExchangeAdd:
                    case NI_System_Threading_Interlocked_MemoryBarrier:
                    case NI_System_Threading_Volatile_Read:
                    case NI_System_Threading_Volatile_Write:
                    case NI_System_Threading_Volatile_ReadBarrier:
                    case NI_System_Threading_Volatile_WriteBarrier:
                    {
                        betterToExpand = true;
                        break;
                    }

                    case NI_System_SpanHelpers_Memmove:
                    case NI_System_SpanHelpers_SequenceEqual:
                    {
                        // We're going to instrument these
                        betterToExpand = compiler.opts.IsInstrumented;
                        break;
                    }

                    default:
                    {
                        // Various intrinsics are all small enough to prefer expansions.
                        betterToExpand |= ni is (>= NI_SYSTEM_MATH_START and <= NI_SYSTEM_MATH_END);
                        betterToExpand |= ni is (>= NI_SRCS_UNSAFE_START and <= NI_SRCS_UNSAFE_END);
                        betterToExpand |= ni is (>= NI_PRIMITIVE_START and <= NI_PRIMITIVE_END);
                        break;
                    }
                }
            }

            if (compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
            {
                switch (ni)
                {
                    // Intrinsics that we should make every effort to expand for NativeAOT.
                    // If the intrinsic cannot possibly be expanded, it's fine, but
                    // if it can be, it should expand.
                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_CreateSpan:
                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_InitializeArray:
                    {
                        betterToExpand = true;
                        break;
                    }

                    // Intrinsics that we should always expand for NativeAOT. These are
                    // required to be expanded due to ILScanner assumptions.
                    case NI_Internal_Runtime_MethodTable_Of:
                    case NI_System_Activator_AllocatorOf:
                    case NI_System_Activator_DefaultConstructorOf:
                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsReferenceOrContainsReferences:
                    {
                        mustExpand = true;
                        break;
                    }

                    case NI_System_Runtime_InteropService_MemoryMarshal_GetArrayDataReference:
                    {
                        mustExpand |= (sigInfo.sigInst.methInstCount is 1);
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
            }

            var retNode = null as GenTree;

            // Under debug and minopts, only expand what is required.
            // NextCallReturnAddress intrinsic returns the return address of the next call.
            // If that call is an intrinsic and is expanded, codegen for NextCallReturnAddress will fail.
            // To avoid that we conservatively expand only required intrinsics in methods that call
            // the NextCallReturnAddress intrinsic.

            if (!mustExpand && ((compiler.opts.OptimizationDisabled && !betterToExpand) || compiler.info.compHasNextCallRetAddr))
            {
                intrinsicName = NI_Illegal;
                return retNode;
            }

            var callJitType = sigInfo.retType;
            var callType = callJitType.VarType;

            // First do the intrinsics which are always smaller than a call

            if (ni is not NI_Illegal)
            {
                var isMinMaxIntrinsic = false;
                var isMax = false;
                var isMagnitude = false;
                var isNative = false;
                var isNumber = false;

                switch (ni)
                {
                    case NI_Array_Address:
                    case NI_Array_Get:
                    case NI_Array_Set:
                    {
                        retNode = compiler.impArrayAccessIntrinsic(clsHnd, sigInfo, memberRef, isReadonlyCall, ni);
                        break;
                    }

                    case NI_System_String_Equals:
                    {
                        retNode = compiler.impUtf16StringComparison(StringComparisonKind.Equals, sigInfo, methodFlags);
                        break;
                    }

                    case NI_System_MemoryExtensions_Equals:
                    case NI_System_MemoryExtensions_SequenceEqual:
                    {
                        retNode = compiler.impUtf16SpanComparison(StringComparisonKind.Equals, sigInfo, methodFlags);
                        break;
                    }

                    case NI_System_String_StartsWith:
                    {
                        retNode = compiler.impUtf16StringComparison(StringComparisonKind.StartsWith, sigInfo, methodFlags);
                        break;
                    }

                    case NI_System_String_EndsWith:
                    {
                        retNode = compiler.impUtf16StringComparison(StringComparisonKind.EndsWith, sigInfo, methodFlags);
                        break;
                    }

                    case NI_System_MemoryExtensions_StartsWith:
                    {
                        retNode = compiler.impUtf16SpanComparison(StringComparisonKind.StartsWith, sigInfo, methodFlags);
                        break;
                    }

                    case NI_System_MemoryExtensions_EndsWith:
                    {
                        retNode = compiler.impUtf16SpanComparison(StringComparisonKind.EndsWith, sigInfo, methodFlags);
                        break;
                    }

                    case NI_System_MemoryExtensions_AsSpan:
                    case NI_System_String_op_Implicit:
                    {
                        assert(sigInfo.numArgs is 1);
                        isSpecial = compiler.impStackTop().val.Oper is GT_CNS_STR;
                        break;
                    }

                    case NI_System_String_get_Chars:
                    {
                        var op2 = compiler.impPopStack().val;
                        var op1 = compiler.impPopStack().val;

                        var addr = compiler.gtNewIndexAddr(op1, op2, TYP_USHORT, NO_CLASS_HANDLE, OFFSETOF__CORINFO_String__chars, OFFSETOF__CORINFO_String__stringLen);
                        retNode = compiler.gtNewIndexIndir(addr);
                        break;
                    }

                    case NI_System_String_get_Length:
                    {
                        var op1 = compiler.impPopStack().val;

                        if (op1.Oper is GT_CNS_STR)
                        {
                            // Optimize `ldstr + String.get_Length()` to CNS_INT
                            // e.g. "Hello".Length => 5
                            var iconNode = compiler.gtNewStringLiteralLength(op1.AsStrCon());

                            if (iconNode is not null)
                            {
                                retNode = iconNode;
                                break;
                            }
                        }

                        var arrLen = compiler.gtNewArrLen(TYP_INT, op1, OFFSETOF__CORINFO_String__stringLen);
                        op1 = arrLen;

                        retNode = op1;
                        break;
                    }

                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_CreateSpan:
                    {
                        retNode = compiler.impCreateSpanIntrinsic(sigInfo);
                        break;
                    }

                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_InitializeArray:
                    {
                        retNode = compiler.impInitializeArrayIntrinsic(sigInfo);
                        break;
                    }

                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_WriteBarrier:
                    {
                        var val = compiler.impPopStack().val;
                        var dst = compiler.impPopStack().val;
                        retNode = compiler.gtNewStoreIndNode(TYP_REF, dst, val, GTF_IND_TGT_HEAP);
                        break;
                    }

                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsKnownConstant:
                    {
                        var op1 = compiler.impPopStack().val;

                        if (op1.Oper.IsConst || compiler.gtIsTypeof(op1))
                        {
                            // op1 is a known constant, replace with 'true'.
                            retNode = compiler.gtNewTrue();
                            JITDUMP("\nExpanding RuntimeHelpers.IsKnownConstant to true early\n");
                            // We can also consider FTN_ADDR here
                        }
                        else if (compiler.opts.OptimizationDisabled)
                        {
                            // It doesn't make sense to carry it as GT_INTRINSIC till Morph in Tier0
                            retNode = compiler.gtNewFalse();
                            JITDUMP("\nExpanding RuntimeHelpers.IsKnownConstant to false early\n");
                        }
                        else
                        {
                            // op1 is not a known constant, we'll do the expansion in morph
                            retNode = new GenTreeIntrinsic(TYP_INT, op1, ni, methHnd) {
                                EntryPoint = entryPoint,
                            };
                            JITDUMP("\nConverting RuntimeHelpers.IsKnownConstant to:\n");
                            DISPTREE(retNode);
                        }
                        break;
                    }

                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsReferenceOrContainsReferences:
                    {
                        assert(sigInfo.sigInst.methInstCount is 1);

                        var fromTypeHnd = sigInfo.sigInst.methInst[0];
                        var fromType = compiler.TypeHandleToVarType(fromTypeHnd, out var fromLayout);

                        var refOrContains = varTypeIsGC(fromType) || ((fromLayout is not null) && fromLayout.HasGCPtr);
                        retNode = refOrContains ? compiler.gtNewTrue() : compiler.gtNewFalse();
                        break;
                    }

                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_GetMethodTable:
                    {
                        retNode = compiler.gtNewMethodTableLookup(compiler.impPopStack().val);
                        break;
                    }

                    case NI_System_Runtime_InteropService_MemoryMarshal_GetArrayDataReference:
                    {
                        assert(sigInfo.numArgs is 1);

                        var array = compiler.impStackTop().val;
                        var notNull = false;
                        var elemHnd = NO_CLASS_HANDLE;

                        CorInfoType jitType;
                        if (sigInfo.sigInst.methInstCount is 1)
                        {
                            elemHnd = sigInfo.sigInst.methInst[0];
                            jitType = compiler.info.compCompHnd->asCorInfoType(elemHnd);
                        }
                        else
                        {
                            var arrayHnd = compiler.gtGetClassHandle(array, out _, out notNull);

                            if ((arrayHnd == NO_CLASS_HANDLE) || !compiler.info.compCompHnd->isSDArray(arrayHnd))
                            {
                                return null;
                            }
                            jitType = compiler.info.compCompHnd->getChildType(arrayHnd, &elemHnd);
                        }

                        array = compiler.impPopStack().val;

                        assert(jitType != CORINFO_TYPE_UNDEF);
                        assert((jitType != CORINFO_TYPE_VALUECLASS) || (elemHnd != NO_CLASS_HANDLE));

                        if (!notNull && compiler.fgAddrCouldBeNull(array))
                        {
                            array = compiler.impCloneExpr(array, out var arrayClone, CHECK_SPILL_ALL, "MemoryMarshal.GetArrayDataReference array");
                            compiler.impAppendTree(compiler.gtNewNullCheck(array), CHECK_SPILL_ALL, compiler.impCurStmtDI);
                            array = arrayClone;
                        }

                        var index = compiler.gtNewIconNode(TYP_I_IMPL, 0);
                        var indexAddr = compiler.gtNewArrayIndexAddr(array, index, jitType.VarType, elemHnd);

                        indexAddr.Flags &= ~GTF_INX_RNGCHK;
                        indexAddr.Flags |= GTF_INX_ADDR_NONNULL;

                        retNode = indexAddr;
                        break;
                    }

                    case NI_Internal_Runtime_MethodTable_Of:
                    case NI_System_Activator_AllocatorOf:
                    case NI_System_Activator_DefaultConstructorOf:
                    {
                        assert(compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI)); // Only NativeAOT supports it.

                        var resolvedToken = new CORINFO_RESOLVED_TOKEN {
                            tokenContext = compiler.impTokenLookupContextHandle,
                            tokenScope = compiler.info.compScopeHnd,
                            token = memberRef,
                            tokenType = CORINFO_TOKENKIND_Method,
                        };

                        CORINFO_GENERICHANDLE_RESULT embedInfo;
                        compiler.info.compCompHnd->expandRawHandleIntrinsic(&resolvedToken, compiler.info.compMethodHnd, &embedInfo);

                        var rawHandle = compiler.impLookupToTree(embedInfo.lookup, compiler.gtTokenToIconFlags(memberRef), embedInfo.compileTimeHandle);

                        if (rawHandle is null)
                        {
                            return null;
                        }

                        noway_assert(rawHandle.Type.Size == TYP_I_IMPL.Size);

                        var rawHandleSlot = compiler.lvaGrabTemp(shortLifetime: true, "rawHandle");
                        compiler.impStoreToTemp(rawHandleSlot, rawHandle, CHECK_SPILL_NONE);

                        var lclVarAddr = compiler.gtNewLclVarAddrNode(TYP_I_IMPL, rawHandleSlot);
                        var resultType = sigInfo.retType.VarType;

                        if (resultType == TYP_STRUCT)
                        {
                            retNode = compiler.gtNewBlkIndir(lclVarAddr, compiler.typGetObjLayout(sigInfo.retTypeClass));
                        }
                        else
                        {
                            retNode = compiler.gtNewIndir(resultType, lclVarAddr);
                        }
                        break;
                    }

                    case NI_System_Span_get_Item:
                    case NI_System_ReadOnlySpan_get_Item:
                    {
                        compiler.optMethodFlags |= OMF_HAS_ARRAYREF;

                        // Have index, stack pointer-to Span<T> s on the stack. Expand to:
                        //
                        // For Span<T>
                        //   Comma
                        //     BoundsCheck(index, s->_length)
                        //     s->_reference + index * sizeof(T)
                        //
                        // For ReadOnlySpan<T> -- same expansion, as it now returns a readonly ref
                        //
                        // Signature should show one class type parameter, which
                        // we need to examine.
                        assert(sigInfo.sigInst.classInstCount is 1);
                        assert(sigInfo.numArgs is 1);

                        var spanElemHnd = sigInfo.sigInst.classInst[0];
                        var elemSize = compiler.info.compCompHnd->getClassSize(spanElemHnd);
                        assert(elemSize > 0);

                        var isReadOnly = ni is NI_System_ReadOnlySpan_get_Item;

                        JITDUMP($"\nimpIntrinsic: Expanding {(isReadOnly ? "ReadOnly" : "")}Span<T>.get_Item, T={compiler.eeGetClassName(spanElemHnd)}, sizeof(T)={elemSize}\n");

                        var index = compiler.impPopStack().val;
                        var ptrToSpan = compiler.impPopStack().val;

                        assert(index.Type.ActualType is TYP_INT);
                        assert(ptrToSpan.Type is TYP_BYREF or  TYP_I_IMPL);

#if DEBUG
                        if (compiler.verbose)
                        {
                            jitprintf("with ptr-to-span\n");
                            compiler.gtDispTree(ptrToSpan);

                            jitprintf("and index\n");
                            compiler.gtDispTree(index);
                        }
#endif

                        // We need to use both index and ptr-to-span twice, so clone or spill.
                        index = compiler.impCloneExpr(index, out var indexClone, CHECK_SPILL_ALL, "Span.get_Item index");

                        GenTree? ptrToSpanClone;

                        if (compiler.impIsAddressInLocal(ptrToSpan))
                        {
                            ptrToSpanClone = compiler.gtCloneExpr(ptrToSpan);
                            assert(ptrToSpanClone is not null);
                        }
                        else
                        {
                            ptrToSpan = compiler.impCloneExpr(ptrToSpan, out ptrToSpanClone, CHECK_SPILL_ALL, "Span.get_Item ptrToSpan");
                        }

                        // Bounds check
                        var lengthHnd = compiler.info.compCompHnd->getFieldInClass(clsHnd, 1);
                        var lengthOffset = compiler.info.compCompHnd->getFieldOffset(lengthHnd);

                        var lengthFieldAddr = compiler.gtNewFieldAddrNode(ptrToSpan, lengthHnd, lengthOffset);
                        var length = compiler.gtNewIndir(TYP_INT, lengthFieldAddr);
                        lengthFieldAddr.IsSpanLength = true;

                        var boundsCheck = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL);

                        // Element access
                        index = indexClone;
                        index = compiler.impImplicitIorI4Cast(index, TYP_I_IMPL, zeroExtend: true);

                        if (elemSize is not 1)
                        {
                            var sizeofNode = compiler.gtNewIconNode(TYP_I_IMPL, elemSize);
                            index = compiler.gtNewBinaryNode(GT_MUL, TYP_I_IMPL, index, sizeofNode);
                        }

                        var ptrHnd = compiler.info.compCompHnd->getFieldInClass(clsHnd, 0);
                        var ptrOffset = compiler.info.compCompHnd->getFieldOffset(ptrHnd);
                        var dataFieldAddr = compiler.gtNewFieldAddrNode(ptrToSpanClone, ptrHnd, ptrOffset);
                        var data = compiler.gtNewIndir(TYP_BYREF, dataFieldAddr);
                        var result = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, data, index);

                        // Prepare result
                        var resultType = sigInfo.retType.VarType;
                        assert(resultType == result.Type);

                        // Add an ordering dependency between the bounds check and
                        // forming the byref to prevent these from being reordered. The
                        // JIT is not allowed to create arbitrary illegal byrefs.
                        boundsCheck.HasOrderingSideEffect = true;
                        result.HasOrderingSideEffect = true;

                        retNode = compiler.gtNewCommaNode(resultType, boundsCheck, result);
                        break;
                    }

                    case NI_System_Span_get_Length:
                    case NI_System_ReadOnlySpan_get_Length:
                    {
                        assert(sigInfo.sigInst.classInstCount is 1);
                        assert(sigInfo.numArgs is 0);

                        var spanElemHnd = sigInfo.sigInst.classInst[0];
                        var elemSize = compiler.info.compCompHnd->getClassSize(spanElemHnd);
                        assert(elemSize > 0);

                        var isReadOnly = (ni is NI_System_ReadOnlySpan_get_Length);
                        JITDUMP($"\nimpIntrinsic: Expanding {(isReadOnly ? "ReadOnly" : "")}Span<T>.get_Length, T={compiler.eeGetClassName(spanElemHnd)}, sizeof(T)={elemSize}\n");

                        var ptrToSpan = compiler.impPopStack().val;

#if DEBUG
                        if (compiler.verbose)
                        {
                            jitprintf("with ptr-to-span\n");
                            compiler.gtDispTree(ptrToSpan);
                        }
#endif

                        var lengthHnd = compiler.info.compCompHnd->getFieldInClass(clsHnd, 1);
                        var lengthOffset = compiler.info.compCompHnd->getFieldOffset(lengthHnd);

                        var lengthFieldAddr = compiler.gtNewFieldAddrNode(ptrToSpan, lengthHnd, lengthOffset);
                        var lengthField = compiler.gtNewIndir(TYP_INT, lengthFieldAddr);
                        lengthFieldAddr.IsSpanLength = true;

                        return lengthField;
                    }

                    case NI_System_RuntimeTypeHandle_ToIntPtr:
                    {
                        var op1 = compiler.impStackTop(0).val;

                        var call = null as GenTreeCall;
                        var retExpr = null as GenTreeRetExpr;

                        if (op1.Oper.IsCall)
                        {
                            call = op1.AsCall();

                            if (call.IsHelperCall() && compiler.gtIsTypeHandleToRuntimeTypeHandleHelper(call))
                            {
                                // Old tree
                                // Helper-RuntimeTypeHandle -> TreeToGetNativeTypeHandle
                                //
                                // New tree
                                // TreeToGetNativeTypeHandle

                                // Remove call to helper and return the native TypeHandle pointer that was the parameter
                                // to that helper.

                                op1 = compiler.impPopStack().val;

                                // Get native TypeHandle argument to old helper
                                assert(call.Args.CountArgs() is 1);

                                var arg = call.Args.GetArgByIndex(0);
                                assert(arg is not null);

                                op1 = arg.Node;
                                retNode = op1;
                                break;
                            }
                        }

                        if (op1.Oper is GT_RET_EXPR)
                        {
                            retExpr = op1.AsRetExpr();  
                            call = retExpr.InlineCandidate.AsCall();
                        }

                        if (call is not null)
                        {
                            // Skip roundtrip "handle -> RuntimeType -> handle" for
                            // RuntimeTypeHandle.ToIntPtr(typeof(T).TypeHandle)
                            if (compiler.lookupNamedIntrinsic(call._callMethHnd) is NI_System_RuntimeType_get_TypeHandle)
                            {
                                // Check that the arg is CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE helper call
                                var arg = call.Args.GetArgByIndex(0);
                                assert(arg is not null);

                                var argNode = arg.Node;

                                if (argNode.Oper.IsCall)
                                {
                                    var argCall = argNode.AsCall();

                                    if (argCall.IsHelperCall() && compiler.gtIsTypeHandleToRuntimeTypeHelper(argCall))
                                    {
                                        compiler.impPopStack();

                                        // Bash the RET_EXPR's call to no-op since it's unused now
                                        retExpr?.InlineCandidate = compiler.gtNewNothingNode();

                                        // Skip roundtrip and return the type handle directly

                                        arg = argCall.Args.GetArgByIndex(0);
                                        assert(arg is not null);

                                        retNode = arg.Node;
                                    }
                                }
                            }
                        }
                        break;
                    }

                    case NI_System_Type_GetTypeFromHandle:
                    {
                        var op1 = compiler.impStackTop(0).val;

                        if (op1.Oper.IsCall)
                        {
                            var call = op1.AsCall();

                            if (call.IsHelperCall() && compiler.gtIsTypeHandleToRuntimeTypeHandleHelper(call, out var typeHandleHelper))
                            {
                                op1 = compiler.impPopStack().val;

                                // Replace helper with a more specialized helper that returns RuntimeType
                                if (typeHandleHelper == CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE)
                                {
                                    typeHandleHelper = CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE;
                                }
                                else
                                {
                                    assert(typeHandleHelper == CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL);
                                    typeHandleHelper = CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL;
                                }
                                assert(call.Args.CountArgs() is 1);

                                var arg = call.Args.GetArgByIndex(0);
                                assert(arg is not null);

                                op1 = compiler.gtNewHelperCallNode(TYP_REF, typeHandleHelper, arg.EarlyNode);
                                op1.Type = TYP_REF;

                                retNode = op1;
                                break;
                            }
                        }

                        if (compiler.RuntimeHandleUnderlyingType is TYP_I_IMPL)
                        {
                            // We'll try to expand it later.
                            isSpecial = true;
                        }
                        break;
                    }

                    case NI_System_Type_GetGenericTypeDefinition:
                    {
                        var type = compiler.impStackTop(0).val;
                        retNode = compiler.impGetGenericTypeDefinition(type);
                        break;
                    }

                    case NI_System_Type_op_Equality:
                    case NI_System_Type_op_Inequality:
                    {
                        JITDUMP("Importing Type.op_*Equality intrinsic\n");

                        var op1 = compiler.impStackTop(1).val;
                        var op2 = compiler.impStackTop(0).val;

                        var optTree = compiler.gtFoldTypeEqualityCall(ni is NI_System_Type_op_Equality, op1, op2) as GenTree;

                        if (optTree is not null)
                        {
                            // Success, clean up the evaluation stack.
                            _ = compiler.impPopStack();
                            _ = compiler.impPopStack();

                            // See if we can optimize even further, to a handle compare.
                            optTree = compiler.gtFoldTypeCompare(optTree.AsOp());

                            // See if we can now fold a handle compare to a constant.
                            optTree = compiler.gtFoldExpr(optTree);
                            retNode = optTree;
                        }
                        else
                        {
                            // Retry optimizing these later
                            isSpecial = true;
                        }
                        break;
                    }

                    case NI_System_ArgumentNullException_ThrowIfNull:
                    case NI_System_String_FastAllocateString:
                    {
                        isSpecial = true;
                        break;
                    }

                    case NI_System_Enum_HasFlag:
                    {
                        var thisOp = compiler.impStackTop(1).val;
                        var flagOp = compiler.impStackTop(0).val;

                        var optTree = compiler.gtOptimizeEnumHasFlag(thisOp, flagOp);

                        if (optTree is not null)
                        {
                            // Optimization successful. Pop the stack for real.
                            _ = compiler.impPopStack();
                            _ = compiler.impPopStack();
                            retNode = optTree;
                        }
                        else
                        {
                            // Retry optimizing this during morph.
                            isSpecial = true;
                        }

                        break;
                    }

                    case NI_System_Type_IsAssignableFrom:
                    {
                        var typeTo = compiler.impStackTop(1).val;
                        var typeFrom = compiler.impStackTop(0).val;

                        retNode = compiler.impTypeIsAssignable(typeTo, typeFrom);
                        break;
                    }

                    case NI_System_Type_IsAssignableTo:
                    {
                        var typeTo = compiler.impStackTop(0).val;
                        var typeFrom = compiler.impStackTop(1).val;

                        retNode = compiler.impTypeIsAssignable(typeTo, typeFrom);
                        break;
                    }

                    case NI_System_Type_get_TypeHandle:
                    {
                        // We can only expand this on NativeAOT where RuntimeTypeHandle looks like this:
                        //
                        //   struct RuntimeTypeHandle { IntPtr _value; }

                        var op1 = compiler.impStackTop(0).val;

                        if (compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && op1.Oper.IsCall && (opcode is CEE_CALLVIRT))
                        {
                            var call = op1.AsCall();

                            if (call.IsHelperCall() && compiler.gtIsTypeHandleToRuntimeTypeHelper(call))
                            {
                                assert(compiler.info.compCompHnd->getClassNumInstanceFields(sigInfo.retTypeClass) is 1);

                                var structLcl = compiler.lvaGrabTemp(shortLifetime: true, "RuntimeTypeHandle");
                                compiler.lvaSetStruct(structLcl, sigInfo.retTypeClass, unsafeValueClsCheck: false);

                                var arg = call.Args.GetUserArgByIndex(0);
                                assert(arg is not null);

                                var realHandle = arg.Node;
                                var storeHandleFld = compiler.gtNewStoreLclFldNode(realHandle.Type, structLcl, offset: 0, realHandle);
                                compiler.impAppendTree(storeHandleFld, CHECK_SPILL_NONE, compiler.impCurStmtDI);

                                retNode = compiler.gtNewLclVarNode(TYP_UNDEF, structLcl);
                                _ = compiler.impPopStack();
                            }
                        }
                        break;
                    }

                    case NI_System_Type_get_IsEnum:
                    case NI_System_Type_get_IsValueType:
                    case NI_System_Type_get_IsPrimitive:
                    case NI_System_Type_get_IsByRefLike:
                    case NI_System_Type_get_IsGenericType:
                    {
                        // Optimize
                        //
                        //   call Type.GetTypeFromHandle (which is replaced with CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE)
                        //   call Type.IsXXX
                        //
                        // to `true` or `false`
                        // e.g., `typeof(int).IsValueType` => `true`
                        // e.g., `typeof(Span<int>).IsByRefLike` => `true`

                        if (compiler.gtIsTypeof(compiler.impStackTop().val, out var hClass))
                        {
                            assert(hClass != NO_CLASS_HANDLE);

                            switch (ni)
                            {
                                case NI_System_Type_get_IsEnum:
                                {
                                    var state = compiler.info.compCompHnd->isEnum(hClass, underlyingType: null);

                                    if (state is TypeCompareState.May)
                                    {
                                        retNode = null;
                                        break;
                                    }
                                    retNode = (state is TypeCompareState.Must) ? compiler.gtNewTrue() : compiler.gtNewFalse();
                                    break;
                                }

                                case NI_System_Type_get_IsValueType:
                                {
                                    retNode = compiler.eeIsValueClass(hClass) ? compiler.gtNewTrue() : compiler.gtNewFalse();
                                    break;
                                }

                                case NI_System_Type_get_IsByRefLike:
                                {
                                    retNode = compiler.eeIsByrefLike(hClass) ? compiler.gtNewTrue() : compiler.gtNewFalse();
                                    break;
                                }

                                case NI_System_Type_get_IsPrimitive:
                                {
                                    // getTypeForPrimitiveValueClass returns underlying type for enums, so we check it first because enums are not primitive types.

                                    if ((compiler.info.compCompHnd->isEnum(hClass, underlyingType: null) is TypeCompareState.MustNot) && (compiler.info.compCompHnd->getTypeForPrimitiveValueClass(hClass) is not CORINFO_TYPE_UNDEF))
                                    {
                                        retNode = compiler.gtNewTrue();
                                    }
                                    else
                                    {
                                        retNode = compiler.gtNewFalse();
                                    }
                                    break;
                                }

                                case NI_System_Type_get_IsGenericType:
                                {
                                    var state = compiler.info.compCompHnd->isGenericType(hClass);

                                    if (state is TypeCompareState.May)
                                    {
                                        retNode = null;
                                    }
                                    else
                                    {
                                        retNode = (state is TypeCompareState.Must) ? compiler.gtNewTrue() : compiler.gtNewFalse();
                                    }
                                    break;
                                }

                                default:
                                {
                                    NO_WAY("Intrinsic not supported in this path.");
                                    break;
                                }
                            }

                            if (retNode is not null)
                            {
                                // drop CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE call
                                _ = compiler.impPopStack();
                            }
                        }
                        break;
                    }

                    case NI_System_Type_GetEnumUnderlyingType:
                    {
                        var type = compiler.impStackTop().val;
                        var hClassUnderlying = NO_CLASS_HANDLE;

                        if (compiler.gtIsTypeof(type, out var hClassEnum) && (hClassEnum != NO_CLASS_HANDLE) && (compiler.info.compCompHnd->isEnum(hClassEnum, &hClassUnderlying) is TypeCompareState.Must) && (hClassUnderlying != NO_CLASS_HANDLE))
                        {
                            var handle = compiler.gtNewIconEmbClsHndNode(hClassUnderlying);
                            retNode = compiler.gtNewHelperCallNode(TYP_REF, CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE, handle);
                            _ = compiler.impPopStack();
                        }
                        break;
                    }

                    case NI_System_Threading_Thread_get_ManagedThreadId:
                    {
                        var op1 = compiler.impStackTop().val;

                        if (op1.Oper is GT_RET_EXPR)
                        {
                            var retExpr = op1.AsRetExpr();
                            var call = retExpr.InlineCandidate.AsCall();

                            if (call.IsSpecialIntrinsic())
                            {
                                if (compiler.lookupNamedIntrinsic(call._callMethHnd) is NI_System_Threading_Thread_get_CurrentThread)
                                {
                                    // drop get_CurrentThread() call
                                    compiler.impPopStack();

                                    retExpr.InlineCandidate = compiler.gtNewNothingNode();
                                    retNode = compiler.gtNewHelperCallNode(TYP_INT, CORINFO_HELP_GETCURRENTMANAGEDTHREADID);
                                }
                            }
                        }
                        break;
                    }

                    case NI_System_Threading_Thread_FastPollGC:
                    {
                        assert(compiler.compCurBB is not null);

                        compiler.optMethodFlags |= OMF_NEEDS_GCPOLLS;
                        compiler.compCurBB.SetFlags(BBF_NEEDS_GCPOLL);

                        var gcpoll = new GenTree(GT_GCPOLL, TYP_VOID);

                        // Prevent both reordering and removal. Invalid optimizations of Thread.FastPollGC are
                        // very subtle and hard to observe. Thus we are conservatively marking it with both
                        // GTF_CALL and GTF_GLOB_REF side-effects even though it may be more than strictly
                        // necessary. The conservative side-effects are unlikely to have negative impact
                        // on code quality in this case.
                        gcpoll.Flags |= (GTF_CALL | GTF_GLOB_REF);

                        retNode = gcpoll;
                        break;
                    }

#if TARGET_ARM64 || TARGET_RISCV64 || TARGET_XARCH
                    case NI_System_Threading_Interlocked_Or:
                    case NI_System_Threading_Interlocked_And:
                    {
#if TARGET_X86
                        // On x86, TYP_LONG is not supported as an intrinsic
                        if (callType.ActualType is TYP_LONG)
                        {
                            break;
                        }
#endif

                        if (callType is not TYP_INT and not TYP_LONG)
                        {
                            // TODO: Implement support for XAND/XORR with small integer types (byte/short)
                            break;
                        }

#if TARGET_ARM64
                        if (compOpportunisticallyDependsOn(InstructionSet_Atomics))
#endif
                        {
                            assert(sigInfo.numArgs is 2);

                            var op2 = compiler.impPopStack().val;
                            var op1 = compiler.impPopStack().val;

                            var op = (ni == NI_System_Threading_Interlocked_Or) ? GT_XORR : GT_XAND;
                            retNode = compiler.gtNewAtomicNode(op, callType.ActualType, op1, op2);
                        }
                        break;
                    }
#endif

#if TARGET_XARCH || TARGET_ARM64 || TARGET_RISCV64
                    // TODO-ARM-CQ: reenable treating InterlockedCmpXchg32 operation as intrinsic
                    case NI_System_Threading_Interlocked_CompareExchange:
                    {
                        var retType = sigInfo.retType.VarType;

                        if (retType.Size > TARGET_POINTER_SIZE)
                        {
                            break;
                        }
#if !TARGET_XARCH && !TARGET_ARM64
                        else if (retType.Size < 4)
                        {
                            break;
                        }
#endif

                        var op2 = compiler.impStackTop(1).val;
                        var canHandle = varTypeIsIntegral(retType);

                        if ((retType is TYP_REF) && op2.Oper.IsIntegralConst)
                        {
                            var intCon = op2.AsIntCon();

                            if (intCon.IsIntegralConst(0) || intCon.IsIconHandle(GTF_ICON_OBJ_HDL))
                            {
                                // Intrinsify "object" overload in case of null or NonGC assignment since we don't need the write barrier.
                                canHandle = true;
                            }
                        }

                        if (!canHandle)
                        {
                            break;
                        }

                        assert(callType is not TYP_STRUCT);
                        assert(sigInfo.numArgs is 3);

                        var op3 = compiler.impPopStack().val;

                        if (varTypeIsSmall(callType))
                        {
                            // small types need the comparand to have its upper bits zeroed
                            op3 = compiler.gtNewCastNode(callType.ActualType, op3, fromUnsigned: false, varTypeToUnsigned(callType));
                        }

                        _ = compiler.impPopStack(); // value
                        var op1 = compiler.impPopStack().val; // location

                        retNode = compiler.gtNewAtomicNode(GT_CMPXCHG, callType, op1, op2, op3);
                        break;
                    }

                    case NI_System_Threading_Interlocked_Exchange:
                    case NI_System_Threading_Interlocked_ExchangeAdd:
                    {
                        var retType = sigInfo.retType.VarType;

                        if (retType.Size > TARGET_POINTER_SIZE)
                        {
                            break;
                        }
#if !TARGET_XARCH && !TARGET_ARM64
                        else if (retType.Size < 4)
                        {
                            break;
                        }
#endif
                        var op2 = compiler.impStackTop().val;
                        var canHandle = varTypeIsIntegral(retType);

                        if ((retType is TYP_REF) && op2.Oper.IsIntegralConst)
                        {
                            var intCon = op2.AsIntCon();

                            if (intCon.IsIntegralConst(0) || intCon.IsIconHandle(GTF_ICON_OBJ_HDL))
                            {
                                // Intrinsify "object" overload in case of null or NonGC assignment since we don't need the write barrier.
                                canHandle = true;
                            }
                        }

                        if (!canHandle)
                        {
                            break;
                        }

                        assert(callType is not TYP_STRUCT);
                        assert(sigInfo.numArgs is 2);
                        assert((retType.Size >= 4) || (ni is NI_System_Threading_Interlocked_Exchange));

                        _ = compiler.impPopStack();
                        var op1 = compiler.impPopStack().val;

                        // This creates:
                        // XAdd
                        //   val
                        //   field_addr (for example)
                        //
                        retNode = compiler.gtNewAtomicNode((ni is NI_System_Threading_Interlocked_ExchangeAdd) ? GT_XADD : GT_XCHG, callType, op1, op2);
                        break;
                    }
#endif

                    case NI_System_Threading_Interlocked_MemoryBarrier:
                    {
                        assert(sigInfo.numArgs is 0);
                        retNode = compiler.gtNewMemoryBarrierNode(BARRIER_FULL);
                        break;
                    }

                    case NI_System_Threading_Volatile_ReadBarrier:
                    {
                        // On XARCH `NI_System_Threading_Volatile_ReadBarrier` fences need not be emitted.
                        // However, we still need to capture the effect on reordering.

                        assert(sigInfo.numArgs is 0);
                        retNode = compiler.gtNewMemoryBarrierNode(BARRIER_LOAD_ONLY);
                        break;
                    }

                    case NI_System_Threading_Volatile_WriteBarrier:
                    {
                        assert(sigInfo.numArgs is 0);
                        // On XARCH `NI_System_Threading_Volatile_WriteBarrier` fences need not be emitted.
                        // However, we still need to capture the effect on reordering.
                        retNode = compiler.gtNewMemoryBarrierNode(BARRIER_STORE_ONLY);
                        break;
                    }

#if FEATURE_HW_INTRINSICS
                    case NI_System_Half_op_Explicit:
                    {
                        assert(sigInfo.numArgs is 1);

                        var retClsHnd = sigInfo.retTypeSigClass;
                        var retJitType = sigInfo.retType;
                        var retType = retJitType.PreciseVarType;

                        CORINFO_CLASS_HANDLE op1ClsHnd;
                        CorInfoType op1JitType;

                        fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
                        {
                            op1JitType = strip(compiler.info.compCompHnd->getArgType(pSigInfo, sigInfo.args, &op1ClsHnd));
                        }
                        var op1Type = op1JitType.PreciseVarType;

                        if (retType == TYP_STRUCT)
                        {
                            assert(compiler.IsSystemHalfClass(retClsHnd));
                            assert(varTypeIsArithmetic(op1Type));

                            switch (op1Type)
                            {
                                case TYP_FLOAT:
                                {
#if TARGET_XARCH
                                    if (compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
                                    {
                                        var op1 = compiler.impPopStack().val;
                                        op1 = compiler.gtNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op1, TYP_FLOAT, 16);

                                        retNode = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX2_ConvertToVector128Half, TYP_FLOAT, 16, op1, compiler.gtNewIconNode(TYP_INT, 0));
                                        retNode = compiler.impSimdToScalarHalf(retNode, retClsHnd);
                                    }
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
                        else
                        {
                            assert(varTypeIsArithmetic(retType));
                            assert((op1Type is TYP_STRUCT) && compiler.IsSystemHalfClass(op1ClsHnd));

                            switch (retType)
                            {
                                case TYP_FLOAT:
                                {
#if TARGET_XARCH
                                    if (compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
                                    {
                                        var op1 = compiler.impPopStack().val;
                                        op1 = compiler.impSimdCreateScalarHalf(op1);

                                        retNode = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX2_ConvertToVector128Single, TYP_USHORT, 16, op1);
                                        retNode = compiler.gtNewSimdToScalarNode(TYP_FLOAT, retNode, TYP_FLOAT, 16);
                                    }
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
                        break;
                    }

                    case NI_System_Math_FusedMultiplyAdd:
                    {
                        assert(varTypeIsFloating(callType));
#if TARGET_XARCH
                        if (compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
                        {
                            // We are constructing a chain of intrinsics similar to:
                            //    return FMA.MultiplyAddScalar(
                            //        Vector128.CreateScalarUnsafe(x),
                            //        Vector128.CreateScalarUnsafe(y),
                            //        Vector128.CreateScalarUnsafe(z)
                            //    ).ToScalar();

                            var op3 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);
                            var op2 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);
                            var op1 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);

                            var simdBaseType = callJitType.PreciseVarType;

                            op3 = compiler.gtNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op3, simdBaseType, 16);
                            op2 = compiler.gtNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op2, simdBaseType, 16);
                            op1 = compiler.gtNewSimdCreateScalarUnsafeNode(TYP_SIMD16, op1, simdBaseType, 16);

                            retNode = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX2_MultiplyAddScalar, simdBaseType, 16, op1, op2, op3);
                            retNode = compiler.gtNewSimdToScalarNode(callType, retNode, simdBaseType, 16);
                            break;
                        }
#elif TARGET_ARM64
                        // We are constructing a chain of intrinsics similar to:
                        //    return AdvSimd.FusedMultiplyAddScalar(
                        //        Vector64.Create{ScalarUnsafe}(z),
                        //        Vector64.Create{ScalarUnsafe}(y),
                        //        Vector64.Create{ScalarUnsafe}(x)
                        //    ).ToScalar();

                        compiler.impSpillSideEffect(spillGlobEffects: true, stackState.esStackDepth - 3, "Spilling op1 side effects for FusedMultiplyAdd");
                        compiler.impSpillSideEffect(spillGlobEffects: true, stackState.esStackDepth - 2, "Spilling op2 side effects for FusedMultiplyAdd");

                        var op3 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);
                        var op2 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);
                        var op1 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);

                        var simdBaseType = callJitType.PreciseVarType;

                        op3 = compiler.gtNewSimdCreateScalarUnsafeNode(TYP_SIMD8, op3, simdBaseType, 8);
                        op2 = compiler.gtNewSimdCreateScalarUnsafeNode(TYP_SIMD8, op2, simdBaseType, 8);
                        op1 = compiler.gtNewSimdCreateScalarUnsafeNode(TYP_SIMD8, op1, simdBaseType, 8);

                        // Note that AdvSimd.FusedMultiplyAddScalar(op1,op2,op3) corresponds to op1 + op2 * op3
                        // while Math{F}.FusedMultiplyAddScalar(op1,op2,op3) corresponds to op1 * op2 + op3

                        retNode = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD8, op3, op2, op1, NI_AdvSimd_FusedMultiplyAddScalar, simdBaseType, 8);
                        retNode = compiler.gtNewSimdToScalarNode(callType, retNode, simdBaseType, 8);
                        break;
#endif

                        // TODO-CQ-XArch: Ideally we would create a GT_INTRINSIC node for fma, however, that currently
                        // requires more extensive changes to valuenum to support methods with 3 operands

                        // We want to generate a GT_INTRINSIC node in the case the call can't be treated as
                        // a target intrinsic so that we can still benefit from CSE and constant folding.

                        break;
                    }
#endif

                    case NI_System_Math_Abs:
                    case NI_System_Math_Acos:
                    case NI_System_Math_Acosh:
                    case NI_System_Math_Asin:
                    case NI_System_Math_Asinh:
                    case NI_System_Math_Atan:
                    case NI_System_Math_Atanh:
                    case NI_System_Math_Atan2:
                    case NI_System_Math_Cbrt:
                    case NI_System_Math_Ceiling:
                    case NI_System_Math_Cos:
                    case NI_System_Math_Cosh:
                    case NI_System_Math_Exp:
                    case NI_System_Math_Floor:
                    case NI_System_Math_ILogB:
                    case NI_System_Math_Log:
                    case NI_System_Math_Log2:
                    case NI_System_Math_Log10:
                    {
                        retNode = compiler.impMathIntrinsic(methHnd, sigInfo, entryPoint, callType, ni, isTailCall, out isSpecial);
                        break;
                    }

                    case NI_System_Math_Max:
                    {
                        isMinMaxIntrinsic = true;
                        isMax = true;
                        break;
                    }

                    case NI_System_Math_MaxMagnitude:
                    {
                        isMinMaxIntrinsic = true;
                        isMax = true;
                        isMagnitude = true;
                        break;
                    }

                    case NI_System_Math_MaxMagnitudeNumber:
                    {
                        isMinMaxIntrinsic = true;
                        isMax = true;
                        isMagnitude = true;
                        isNumber = true;
                        break;
                    }

                    case NI_System_Math_MaxNative:
                    {
                        isMinMaxIntrinsic = true;
                        isMax = true;
                        isNative = true;
                        break;
                    }

                    case NI_System_Math_MaxNumber:
                    {
                        isMinMaxIntrinsic = true;
                        isMax = true;
                        isNumber = true;
                        break;
                    }

                    case NI_System_Math_Min:
                    {
                        isMinMaxIntrinsic = true;
                        break;
                    }

                    case NI_System_Math_MinMagnitude:
                    {
                        isMinMaxIntrinsic = true;
                        isMagnitude = true;
                        break;
                    }

                    case NI_System_Math_MinMagnitudeNumber:
                    {
                        isMinMaxIntrinsic = true;
                        isMagnitude = true;
                        isNumber = true;
                        break;
                    }

                    case NI_System_Math_MinNative:
                    {
                        isMinMaxIntrinsic = true;
                        isNative = true;
                        break;
                    }

                    case NI_System_Math_MinNumber:
                    {
                        isMinMaxIntrinsic = true;
                        isNumber = true;
                        break;
                    }

                    case NI_System_Math_Pow:
                    case NI_System_Math_Round:
                    case NI_System_Math_Sin:
                    case NI_System_Math_Sinh:
                    case NI_System_Math_Sqrt:
                    case NI_System_Math_Tan:
                    case NI_System_Math_Tanh:
                    case NI_System_Math_Truncate:
                    {
                        retNode = compiler.impMathIntrinsic(methHnd, sigInfo, entryPoint, callType, ni, isTailCall, out isSpecial);
                        break;
                    }

                    case NI_System_Math_MultiplyAddEstimate:
                    case NI_System_Math_ReciprocalEstimate:
                    case NI_System_Math_ReciprocalSqrtEstimate:
                    {
                        retNode = compiler.impEstimateIntrinsic(methHnd, sigInfo, callJitType, ni, mustExpand);
                        break;
                    }

                    case NI_System_Array_Clone:
                    case NI_System_Collections_Generic_Comparer_get_Default:
                    case NI_System_Collections_Generic_EqualityComparer_get_Default:
                    case NI_System_Object_MemberwiseClone:
                    case NI_System_Threading_Thread_get_CurrentThread:
                    {
                        // Flag for later handling.
                        isSpecial = true;
                        break;
                    }

                    case NI_System_Object_GetType:
                    {
                        JITDUMP("\n impIntrinsic: call to Object.GetType\n");
                        var op1 = compiler.impStackTop(0).val;

                        // If we're calling GetType on a boxed value, just get the type directly.
                        if (op1.Oper is GT_BOX)
                        {
                            var box = op1.AsBox();

                            if (box.IsBoxedValue)
                            {
                                JITDUMP("Attempting to optimize box(...).getType() to direct type construction\n");

                                // Try and clean up the box. Obtain the handle we
                                // were going to pass to the newobj.
                                var boxTypeHandle = compiler.gtTryRemoveBoxUpstreamEffects(box, BR_REMOVE_AND_NARROW_WANT_TYPE_HANDLE);

                                if (boxTypeHandle is not null)
                                {
                                    // Note we don't need to play the TYP_STRUCT games here like do for LDTOKEN since the return value of this operator is Type, not RuntimeTypeHandle.
                                    compiler.impPopStack();
                                    retNode = compiler.gtNewHelperCallNode(TYP_REF, CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE, boxTypeHandle);
                                }
                            }
                        }

                        // If we have a constrained isCallVirt with a "box this" transform
                        // we know we have a value class and hence an exact type.
                        //
                        // If so, instead of boxing and then extracting the type, just
                        // construct the type directly.
                        if ((retNode is null) && !Unsafe.IsNullRef(in constrainedResolvedToken) && (callInfo.thisTransform is CORINFO_BOX_THIS))
                        {
                            // Ensure this is one of the simple box cases (in particular, rule out nullables).
                            var boxHelper = compiler.info.compCompHnd->getBoxHelper(constrainedResolvedToken.hClass);
                            var isSafeToOptimize = (boxHelper is CORINFO_HELP_BOX);

                            if (isSafeToOptimize)
                            {
                                JITDUMP("Optimizing constrained box-this obj.getType() to direct type construction\n");
                                _ = compiler.impPopStack();

                                var typeHandleOp = compiler.impTokenToHandle(constrainedResolvedToken, mustRestoreHandle: true);

                                if (typeHandleOp is null)
                                {
                                    assert(compiler.compDonotInline);
                                    return null;
                                }
                                retNode = compiler.gtNewHelperCallNode(TYP_REF, CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE, typeHandleOp);
                            }
                        }

#if DEBUG
                        if (retNode is not null)
                        {
                            JITDUMP("Optimized result for call to GetType is\n");

                            if (compiler.verbose)
                            {
                                compiler.gtDispTree(retNode);
                            }
                        }
#endif

                        // Else expand as an intrinsic, unless the call is constrained,
                        // in which case we defer expansion to allow impImportCall do the
                        // special constraint processing.
                        if ((retNode is null) && Unsafe.IsNullRef(in constrainedResolvedToken))
                        {
                            JITDUMP("Expanding as special intrinsic\n");
                            _ = compiler.impPopStack();

                            op1 = new GenTreeIntrinsic(callType.ActualType, op1, ni, methHnd) {
                                EntryPoint = entryPoint,
                            };

                            // Set the CALL flag to indicate that the operator is implemented by a call.
                            // Set also the EXCEPTION flag because the native implementation of
                            // NI_System_Object_GetType intrinsic can throw NullReferenceException.

                            op1.Flags |= (GTF_CALL | GTF_EXCEPT);
                            retNode = op1;

                            // Might be further optimizable, so arrange to leave a mark behind
                            isSpecial = true;
                        }

                        if (retNode is null)
                        {
                            JITDUMP("Leaving as normal call\n");
                            // Might be further optimizable, so arrange to leave a mark behind
                            isSpecial = true;
                        }
                        break;
                    }

                    case NI_System_Array_GetLength:
                    case NI_System_Array_GetLowerBound:
                    case NI_System_Array_GetUpperBound:
                    {
                        // System.Array.GetLength(Int32) methHnd:
                        //     public int GetLength(int dimension)
                        // System.Array.GetLowerBound(Int32) methHnd:
                        //     public int GetLowerBound(int dimension)
                        // System.Array.GetUpperBound(Int32) methHnd:
                        //     public int GetUpperBound(int dimension)
                        //
                        // Only implement these as intrinsics for multi-dimensional arrays.
                        // Only handle constant dimension arguments.

                        var gtDim = compiler.impStackTop().val;
                        var gtArr = compiler.impStackTop(1).val;

                        if (gtDim.Oper.IsIntegralConst)
                        {
                            var arrCls = compiler.gtGetClassHandle(gtArr, out var isExact, out var isNonNull);

                            if (arrCls != NO_CLASS_HANDLE)
                            {
                                var rank = compiler.info.compCompHnd->getArrayRank(arrCls);

                                if ((rank > 1) && !compiler.info.compCompHnd->isSDArray(arrCls))
                                {
                                    // `rank` is guaranteed to be <=32 (see MAX_RANK in vm\array.h). Any constant argument is `int` sized.
                                    var dimValue = gtDim.AsIntConCommon().IntegralValue;

                                    var dim = unchecked((int)(dimValue));
                                    assert(dim == dimValue);

                                    if (dim < rank)
                                    {
                                        // This is now known to be a multi-dimension array with a constant dimension
                                        // that is in range; we can expand it as an intrinsic.

                                        compiler.impPopStack(2); // Pop the dim and array object; we already have a pointer to them.

                                        // Make sure there are no global effects in the array (such as it being a function
                                        // call), so we can mark the generated indirection with GTF_IND_INVARIANT. In the
                                        // GetUpperBound case we need the cloned object, since we refer to the array
                                        // object twice. In the other cases, we don't need to clone.
                                        var gtArrClone = null as GenTree;

                                        if (((gtArr.Flags & GTF_GLOB_EFFECT) is not 0) || (ni is NI_System_Array_GetUpperBound))
                                        {
                                            gtArr = compiler.impCloneExpr(gtArr, out gtArrClone, CHECK_SPILL_ALL, "MD intrinsics array");
                                        }

                                        switch (ni)
                                        {
                                            case NI_System_Array_GetLength:
                                            {
                                                // Generate *(array + offset-to-length-array + sizeof(int) * dim)
                                                var offs = eeGetMDArrayLengthOffset(rank, dim);

                                                var gtOffs = compiler.gtNewIconNode(TYP_I_IMPL, offs);
                                                var gtAddr = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, gtArr, gtOffs);

                                                retNode = compiler.gtNewIndir(TYP_INT, gtAddr, GTF_IND_INVARIANT);
                                                break;
                                            }

                                            case NI_System_Array_GetLowerBound:
                                            {
                                                // Generate *(array + offset-to-bounds-array + sizeof(int) * dim)
                                                var offs = eeGetMDArrayLowerBoundOffset(rank, dim);

                                                var gtOffs = compiler.gtNewIconNode(TYP_I_IMPL, offs);
                                                var gtAddr = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, gtArr, gtOffs);

                                                retNode = compiler.gtNewIndir(TYP_INT, gtAddr, GTF_IND_INVARIANT);
                                                break;
                                            }

                                            case NI_System_Array_GetUpperBound:
                                            {
                                                assert(gtArrClone is not null);

                                                // Generate:
                                                //    *(array + offset-to-length-array + sizeof(int) * dim) +
                                                //    *(array + offset-to-bounds-array + sizeof(int) * dim) - 1
                                                var offs = eeGetMDArrayLowerBoundOffset(rank, dim);

                                                var gtOffs = compiler.gtNewIconNode(TYP_I_IMPL, offs);
                                                var gtAddr = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, gtArr, gtOffs);
                                                var gtLowerBound = compiler.gtNewIndir(TYP_INT, gtAddr, GTF_IND_INVARIANT);

                                                offs = eeGetMDArrayLengthOffset(rank, dim);
                                                gtOffs = compiler.gtNewIconNode(TYP_I_IMPL, offs);
                                                gtAddr = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, gtArrClone, gtOffs);

                                                var gtLength = compiler.gtNewIndir(TYP_INT, gtAddr, GTF_IND_INVARIANT);
                                                var gtSum = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, gtLowerBound, gtLength);
                                                var gtOne = compiler.gtNewIconNode(TYP_INT, 1);

                                                retNode = compiler.gtNewBinaryNode(GT_SUB, TYP_INT, gtSum, gtOne);
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
                            }
                        }
                        break;
                    }

                    case NI_System_Buffers_Binary_BinaryPrimitives_ReverseEndianness:
                    {
                        assert(sigInfo.numArgs is 1);

                        // We expect the return type of the ReverseEndianness routine to match the type of the
                        // one and only argument to the methHnd. We use a special instruction for 16-bit
                        // BSWAPs since on x86 processors this is implemented as ROR <16-bit reg>, 8. Additionally,
                        // we only emit 64-bit BSWAP instructions on 64-bit archs; if we're asked to perform a
                        // 64-bit byte swap on a 32-bit arch, we'll fall to the default case in the switch block below.

                        switch (sigInfo.retType)
                        {
                            case CORINFO_TYPE_SHORT:
                            case CORINFO_TYPE_USHORT:
                            {
                                retNode = compiler.gtNewCastNode(TYP_INT, compiler.gtNewUnaryNode(GT_BSWAP16, TYP_INT, compiler.impPopStack().val), fromUnsigned: false, callType);
                                break;
                            }

                            case CORINFO_TYPE_INT:
                            case CORINFO_TYPE_UINT:
#if TARGET_64BIT
                            case CORINFO_TYPE_LONG:
                            case CORINFO_TYPE_ULONG:
#endif
                            {
                                retNode = compiler.gtNewUnaryNode(GT_BSWAP, callType, compiler.impPopStack().val);
                                break;
                            }

                            default:
                            {
                                // This default case gets hit on 32-bit archs when a call to a 64-bit overload
                                // of ReverseEndianness is encountered. In that case we'll let JIT treat this as a standard
                                // methHnd call, where the implementation decomposes the operation into two 32-bit
                                // bswap routines. If the input to the 64-bit function is a constant, then we rely
                                // on inlining + constant folding of 32-bit bswaps to effectively constant fold
                                // the 64-bit call site.
                                break;
                            }
                        }
                        break;
                    }

                    case NI_System_GC_KeepAlive:
                    {
                        retNode = compiler.impKeepAliveIntrinsic(compiler.impPopStack().val);
                        break;
                    }

                    case NI_System_Text_UTF8Encoding_UTF8EncodingSealed_ReadUtf8:
                    case NI_System_SpanHelpers_SequenceEqual:
                    case NI_System_SpanHelpers_ClearWithoutReferences:
                    case NI_System_SpanHelpers_Memmove:
                    {
                        if (sigInfo.sigInst.methInstCount is 0)
                        {
                            // We'll try to unroll this in lower for constant input.
                            isSpecial = true;
                        }

                        // The generic version is also marked as [Intrinsic] just as a hint for the inliner
                        break;
                    }

                    case NI_System_SpanHelpers_Fill:
                    {
                        if (sigInfo.sigInst.methInstCount is 1)
                        {
                            // We'll try to unroll this in lower for constant input.
                            isSpecial = true;
                        }
                        break;
                    }

                    case NI_System_SZArrayHelper_GetEnumerator:
                    case NI_System_Array_T_GetEnumerator:
                    {
                        // We may know the exact type these return
                        isSpecial = true;
                        break;
                    }

                    case NI_System_BitConverter_DoubleToInt64Bits:
                    {
                        var op1 = compiler.impStackTop().val;
                        assert(varTypeIsFloating(op1.Type));

                        if (op1.Oper.IsCnsFltOrDbl)
                        {
                            compiler.impPopStack();

                            var f64Cns = op1.AsDblCon().DconVal;
                            retNode = compiler.gtNewLconNode(BitConverter.DoubleToInt64Bits(f64Cns));
                        }
#if TARGET_64BIT
                        else
                        {
                            // TODO-Cleanup: We should support this on 32-bit but it requires decomposition work
                            compiler.impPopStack();

                            op1 = compiler.impImplicitR4orR8Cast(op1, TYP_DOUBLE);
                            retNode = compiler.gtNewBitCastNode(TYP_LONG, op1);
                        }
#endif
                        break;
                    }

                    case NI_System_BitConverter_Int32BitsToSingle:
                    {
                        var op1 = compiler.impPopStack().val;
                        assert(varTypeIsInt(op1.Type));

                        if (op1.Oper.IsIntegralConst)
                        {
                            var f32Cns = BitConverter.Int32BitsToSingle((int)(op1.AsIntConCommon().IconValue));
                            retNode = compiler.gtNewDconNode(TYP_FLOAT, f32Cns);
                        }
                        else
                        {
                            retNode = compiler.gtNewBitCastNode(TYP_FLOAT, op1);
                        }
                        break;
                    }

                    case NI_System_BitConverter_Int64BitsToDouble:
                    {
                        var op1 = compiler.impStackTop().val;
                        assert(varTypeIsLong(op1.Type));

                        if (op1.Oper.IsIntegralConst)
                        {
                            compiler.impPopStack();

                            var i64Cns = op1.AsIntConCommon().LngValue;
                            retNode = compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(i64Cns));
                        }
#if TARGET_64BIT
                        else
                        {
                            // TODO-Cleanup: We should support this on 32-bit but it requires decomposition work
                            _ = compiler.impPopStack();

                            retNode = compiler.gtNewBitCastNode(TYP_DOUBLE, op1);
                        }
#endif
                        break;
                    }

                    case NI_System_BitConverter_SingleToInt32Bits:
                    {
                        var op1 = compiler.impPopStack().val;
                        assert(varTypeIsFloating(op1.Type));

                        if (op1.Oper.IsCnsFltOrDbl)
                        {
                            var f32Cns = (float)(op1.AsDblCon().DconVal);
                            retNode = compiler.gtNewIconNode(TYP_INT, BitConverter.SingleToInt32Bits(f32Cns));
                        }
                        else
                        {
                            op1 = compiler.impImplicitR4orR8Cast(op1, TYP_FLOAT);
                            retNode = compiler.gtNewBitCastNode(TYP_INT, op1);
                        }
                        break;
                    }

                    case NI_System_Runtime_CompilerServices_StaticsHelpers_VolatileReadAsByref:
                    {
                        retNode = compiler.gtNewIndir(TYP_BYREF, compiler.impPopStack().val, GTF_IND_VOLATILE);
                        break;
                    }

                    case NI_System_Threading_Volatile_Read:
                    {
                        assert(sigInfo.sigInst.methInstCount is 0 or 1);
                        var retType = (sigInfo.sigInst.methInstCount is 0) ? sigInfo.retType.VarType : TYP_REF;

#if !TARGET_64BIT
                        if (retType is TYP_LONG or TYP_DOUBLE)
                        {
                            break;
                        }
#endif
                        assert((retType is TYP_REF) || compiler.impIsPrimitive(sigInfo.retType));
                        retNode = compiler.gtNewIndir(retType, compiler.impPopStack().val, GTF_IND_VOLATILE);
                        break;
                    }

                    case NI_System_Threading_Volatile_Write:
                    {
                        var type = TYP_REF;

                        if (sigInfo.sigInst.methInstCount is 0)
                        {
                            CorInfoType jitType;

                            fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
                            {
                                CORINFO_CLASS_HANDLE typeHnd = null;
                                jitType = strip(compiler.info.compCompHnd->getArgType(pSigInfo, compiler.info.compCompHnd->getArgNext(sigInfo.args), &typeHnd));
                            }
                            assert(compiler.impIsPrimitive(jitType));

                            type = jitType.VarType;
#if !TARGET_64BIT
                            if (type is TYP_LONG or TYP_DOUBLE)
                            {
                                break;
                            }
#endif
                        }
                        else
                        {
                            assert(sigInfo.sigInst.methInstCount is 1);
                            assert(!compiler.eeIsValueClass(sigInfo.sigInst.methInst[0]));
                        }

                        var value = compiler.impPopStack().val;
                        var addr = compiler.impPopStack().val;

                        retNode = compiler.gtNewStoreIndNode(type, addr, value, GTF_IND_VOLATILE);
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }

                if (isMinMaxIntrinsic)
                {
                    if (varTypeIsIntegral(callType))
                    {
                        assert(!isMagnitude && !isNative && !isNumber);

#if TARGET_RISCV64
                        if (compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb))
                        {
                            var op2 = compiler.impPopStack().val;
                            var op1 = compiler.impPopStack().val;

                            // RISC-V integer min/max instructions operate on whole registers with preferrably ABI-extended
                            // values. We currently don't know if a register is ABI-extended so always cast, even for 'int' and
                            // 'uint'.
                            var preciseType = callJitType.PreciseVarType;

                            if (preciseType.Size < REGSIZE_BYTES)
                            {
                                // Zero-extended 'uint' is unnatural on RISC-V
                                var zeroExtend = varTypeIsUnsigned(preciseType) && (preciseType is not TYP_UINT);

                                op2 = compiler.gtNewCastNode(TYP_I_IMPL, op2, zeroExtend, TYP_I_IMPL);
                                op1 = compiler.gtNewCastNode(TYP_I_IMPL, op1, zeroExtend, TYP_I_IMPL);
                            }

                            if (varTypeIsUnsigned(preciseType))
                            {
                                ni = isMax ? NI_System_Math_Maxuint : NI_System_Math_Minuint;
                            }
                            retNode = new GenTreeIntrinsic(TYP_I_IMPL, op1, op2, ni, null CORINFO_CONST_LOOKUP{IAT_VALUE});
                        }
#endif
                    }
                    else if (!isNative || !compiler.BlockNonDeterministicIntrinsics(mustExpand))
                    {
#if FEATURE_HW_INTRINSICS
                        var op2 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);
                        var op1 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);

                        if (isNative)
                        {
                            assert(!isMagnitude && !isNumber);
                            retNode = compiler.gtNewSimdMinMaxNativeNode(callType, op1, op2, callJitType.PreciseVarType, 0, isMax);
                        }
                        else
                        {
                            retNode = compiler.gtNewSimdMinMaxNode(callType, op1, op2, callJitType.PreciseVarType, 0, isMax, isMagnitude, isNumber);
                        }
#endif

#if TARGET_RISCV64
                        if (!isMagnitude)
                        {
                            var op2 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);
                            var op1 = compiler.impImplicitR4orR8Cast(compiler.impPopStack().val, callType);

                            var op1Clone = null as GenTree;
                            var op2Clone = null as GenTree;

                            if (isNative)
                            {
                                assert(!isMagnitude && !isNumber);
                            }
                            else
                            {
                                if (!isNumber)
                                {
                                    op2 = compiler.impCloneExpr(op2, out op2Clone, CHECK_SPILL_ALL, "Clone op2 for Math.Min/Max non-Number");
                                }
                                op1 = compiler.impCloneExpr(op1, out op1Clone, CHECK_SPILL_ALL, "Clone op1 for Math.Min/Max");
                            }

                            var nullEntry = CORINFO_CONST_LOOKUP{IAT_VALUE};

                            // Native RISC-V fmin/fmax instructions implement IEEE 754-2019 Minimum/MaximumNumber functions
                            // which don't specify what kind of NaN is returned if both arguments are NaN. RISC-V returns quiet
                            // canonical NaN in this case.
                            ni = isMax ? NI_System_Math_MaxNative : NI_System_Math_MinNative;
                            var minMax = new GenTreeIntrinsic(callType, op1, op2, ni, null nullEntry);

                            if (!isNative)
                            {
                                // Make sure we return the NaN argument verbatim (if both are NaN, the first one), which is an
                                // additional requirement for .NET Min/Max APIs on top of IEEE 754.

                                if (isNumber)
                                {
                                    // Build expression:  isNumber(minMax) ? minMax : op1
                                    minMax = compiler.impCloneExpr(minMax, out var minMaxClone, CHECK_SPILL_NONE, "Clone min/max result in Math.Min/MaxNumber");

                                    var isNumber = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, minMax, minMaxClone);

                                    minMax = compiler.gtNewQmarkNode(callType, isNumber, compiler.gtNewColonNode(callType, gtCloneExpr(minMaxClone), op1Clone));
                                }
                                else
                                {
                                    // Build expression:  isNumber(op1) ? (isNumber(op2) ? minMax : op2) : op1
                                    var isOp1Number = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, op1Clone, gtCloneExpr(op1Clone));
                                    var isOp2Number = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, op2Clone, gtCloneExpr(op2Clone));

                                    minMax = compiler.gtNewQmarkNode(callType, isOp2Number, compiler.gtNewColonNode(callType, minMax, gtCloneExpr(op2Clone)));
                                    minMax = compiler.gtNewQmarkNode(callType, isOp1Number, compiler.gtNewColonNode(callType, minMax, gtCloneExpr(op1Clone)));
                                }

                                // Top-level QMARK needs to be in a variable
                                assert(minMax->OperIs(GT_QMARK));

                                var tmpTop = compiler.lvaGrabTemp(shortLifetime: true, "Temp for top qmark in Math.Min/Max");
                                compiler.impStoreToTemp(tmpTop, minMax, CHECK_SPILL_NONE);
                                minMax = compiler.gtNewLclvNode(callType, tmpTop);
                            }
                            retNode = minMax;
                        }
#endif
                    }

                    // TODO-CQ: Returning this as an intrinsic blocks inlining and is undesirable
                    //
                    // if (retNode is not null)
                    // {
                    //     retNode = impMathIntrinsic(methHnd, sigInfo, callType, ni, tailCall, isSpecial);
                    // }
                }
            }

            if (mustExpand && (retNode is null))
            {
#if TARGET_WASM
                NYI_WASM("Unhandled must expand intrinsic");
#else
                NO_WAY("Unhandled must expand intrinsic, throwing PlatformNotSupportedException");
#endif
                return compiler.impUnsupportedNamedIntrinsic(CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED, methHnd, sigInfo, mustExpand);
            }

            // report if this intrinsic is special
            // (that is, potentially re-optimizable during morph).

            isSpecialIntrinsic = isSpecial;
            return retNode;
        }

        private unsafe var_types Devirt(Compiler compiler, GenTreeCall call)
        {
            var probing = compiler.impConsiderCallProbe(call, opcodeOffs);

            // See if we can devirt if we aren't probing.
            if (!probing && compiler.opts.OptimizationEnabled)
            {
                if (call.IsDevirtualizationCandidate(compiler))
                {
                    // only true object pointers can be virtual
                    assert(call.Args.HasThisPointer && (call.Args.ThisArg.Node.Type is TYP_REF));

                    // See if we can devirtualize.
                    // Take care to pass raw IL offset here as the 'debug info' might be different for inlinees.
                    compiler.impDevirtualizeCall(call, resolvedToken, ref callInfo.hMethod, ref callInfo.methodFlags, ref callInfo.contextHandle, out exactContextHnd, isLateDevirtualization: false, isExplicitTailCall: (tailCallFlags & PREFIX_TAILCALL_EXPLICIT) is not 0, opcodeOffs);

                    var wasDevirtualized = !call.IsDevirtualizationCandidate(compiler);

                    if (wasDevirtualized)
                    {
                        // Devirtualization may change which method gets invoked. Update our local cache.
                        //
                        methHnd = callInfo.hMethod;

                        // If we devirtualized to an intrinsic, assume this is one of the special cases.
                        //
                        if ((callInfo.methodFlags & CORINFO_FLG_INTRINSIC) is not 0)
                        {
                            call._callMoreFlags |= GTF_CALL_M_SPECIAL_INTRINSIC;

                            var foldedCall = compiler.gtFoldExprCall(call);

                            if ((foldedCall != call) || !call.Oper.IsCall)
                            {
                                compiler.impPushOnStack(foldedCall, new typeInfo(foldedCall.Type));
                                return foldedCall.Type;
                            }
                        }
                    }
                }
                else if (call.IsDelegateInvoke)
                {
                    var contextHandle = (CORINFO_CONTEXT_HANDLE)(null);
                    compiler.considerGuardedDevirtualization(call, opcodeOffs, isInterface: false, call._callMethHnd, NO_CLASS_HANDLE, ref contextHandle);
                }
            }

            //-------------------------------------------------------------------------
            // The "this" pointer for "newobj"

            if (opcode is CEE_NEWOBJ)
            {
                if ((clsFlags & CORINFO_FLG_VAROBJSIZE) is not 0)
                {
                    // arrays handled separately
                    //
                    // This is a 'new' of a variable sized object, wher
                    // the constructor is to return the object.  In this case
                    // the constructor claims to return VOID but we know it
                    // actually returns the new object

                    assert((clsFlags & CORINFO_FLG_ARRAY) is 0);
                    assert(callRetTyp == TYP_VOID);

                    callRetTyp = TYP_REF;
                    call.Type = TYP_REF;

                    compiler.impSpillSpecialSideEff();
                    compiler.impPushOnStack(call, new typeInfo(clsHnd));
                }
                else
                {
                    if ((clsFlags & CORINFO_FLG_DELEGATE) is not 0)
                    {
                        // New inliner morph it in impImportCall.
                        // This will allow us to inline the call to the delegate constructor.
                        call = compiler.fgOptimizeDelegateConstructor(call, ref exactContextHnd, ldftnInfo);

                        if (compiler.compDonotInline)
                        {
                            return TYP_UNDEF;
                        }
                    }

                    if (!bIntrinsicImported)
                    {
                        assert(compiler.compInlineContext is not null);

#if DEBUG
                        // Keep track of the raw IL offset of the call
                        call._rawILOffset = opcodeOffs;
#endif

                        // Is it an inline candidate?
                        compiler.impMarkInlineCandidate(call, exactContextHnd, exactContextNeedsRuntimeLookup, callInfo, compiler.compInlineContext);
                    }

                    // append the call node.
                    compiler.impAppendTree(call, CHECK_SPILL_ALL, compiler.impCurStmtDI);

                    // Now push the value of the 'new onto the stack

                    // This is a 'new' of a non-variable sized object.
                    // Append the new node (op1) to the statement list,
                    // and then push the local holding the value of this
                    // new instruction on the stack.

                    assert(newObjThis is not null);

                    if ((clsFlags & CORINFO_FLG_VALUECLASS) is not 0)
                    {
                        assert(newObjThis.IsLclVarAddr);

                        var lclNum = newObjThis.AsLclVarCommon().LclNum;
                        compiler.impPushOnStack(compiler.gtNewLclvNode(compiler.lvaGetDesc(lclNum).Type, lclNum), compiler.makeTypeInfo(clsHnd));
                    }
                    else
                    {
                        if (newObjThis.Oper is GT_COMMA)
                        {
                            // We must have inserted the callout. Get the real newobj.
                            newObjThis = newObjThis.AsOp().Op2;
                        }

                        assert(newObjThis.Oper is GT_LCL_VAR);
                        compiler.impPushOnStack(compiler.gtNewLclvNode(TYP_REF, newObjThis.AsLclVarCommon().LclNum), new typeInfo(clsHnd));
                    }
                }
                return callRetTyp;
            }
            return Done(compiler, call);
        }

        private unsafe var_types Done(Compiler compiler, GenTreeCall call)
        {
            assert(compiler.compCurBB is not null);

#if DEBUG || TARGET_WASM
            // In debug we want to be able to register callsites with the EE.
            call._callSig = sigInfo;
#endif

            // Final importer checks for calls flagged as tail calls.
            //
            if (tailCallFlags is not 0)
            {
                var isExplicitTailCall = (tailCallFlags & PREFIX_TAILCALL_EXPLICIT) is not 0;
                var isImplicitTailCall = (tailCallFlags & PREFIX_TAILCALL_IMPLICIT) is not 0;

                // Exactly one of these should be true.
                assert(isExplicitTailCall != isImplicitTailCall);

                // This check cannot be performed for implicit tail calls for the reason
                // that impIsImplicitTailCallCandidate() is not checking whether return
                // types are compatible before marking a call node with PREFIX_TAILCALL_IMPLICIT.
                // As a result it is possible that in the following case, we find that
                // the type stack is non-empty if Callee() is considered for implicit
                // tail calling.
                //      int Caller(..) { .... void Callee(); ret val; ... }
                //
                // Note that we cannot check return type compatibility before ImpImportCall()
                // as we don't have required info or need to duplicate some of the logic of
                // ImpImportCall().
                //
                // For implicit tail calls, we perform this check after return types are
                // known to be compatible.
                if (isExplicitTailCall && (compiler.stackState.esStackDepth is not 0))
                {
                    BADCODE("Stack should be empty after tailcall");
                }

                // For opportunistic tailcalls we allow implicit widening, i.e. tailcalls from int32 -> int16, since the
                // managed calling convention dictates that the callee widens the value. For explicit tailcalls or async
                // functions we don't want to require this detail of the calling convention to bubble up to helper
                // infrastructure.
                var allowWidening = isImplicitTailCall && !call.IsAsync;

                if (canTailCall && !compiler.impTailCallRetTypeCompatible(allowWidening, compiler.info.compRetType, compiler.info.compMethodInfo->args.retTypeClass, compiler.info.compCallConv, callRetTyp, sigInfo.retTypeClass, call.UnmanagedCallConv))
                {
                    canTailCall = false;
                    canTailCallFailReasonUtf8 = "Return types are not tail call compatible"u8;
                }

                // Stack empty check for implicit tail calls.
                if (canTailCall && isImplicitTailCall && (compiler.stackState.esStackDepth is not 0))
                {
                    BADCODE("Stack should be empty after tailcall");
                }

                // assert(compCurBB is not a catch, finally or filter block);
                // assert(compCurBB is not a try block protected by a finally block);
                assert(!isExplicitTailCall || (compiler.compCurBB.Kind is BBJ_RETURN));

                // Ask VM for permission to tailcall
                if (canTailCall)
                {
                    // True virtual or indirect calls, shouldn't pass in a callee handle.
                    var exactCalleeHnd = ((call._callType != CT_USER_FUNC) || call.IsVirtual) ? null : methHnd;

                    if (compiler.info.compCompHnd->canTailCall(compiler.info.compMethodHnd, methHnd, exactCalleeHnd, isExplicitTailCall))
                    {
                        if (isExplicitTailCall)
                        {
                            // In case of explicit tail calls, mark it so that it is not considered
                            // for in-lining.
                            call._callMoreFlags |= GTF_CALL_M_EXPLICIT_TAILCALL;

#if DEBUG
                            JITDUMP($"\nGTF_CALL_M_EXPLICIT_TAILCALL set for call [{call.TreeId:D6}]\n");

                            if ((prefixFlags & PREFIX_TAILCALL_STRESS) is not 0)
                            {
                                call._callDebugFlags |= GTF_CALL_MD_STRESS_TAILCALL;
                                JITDUMP($"\nGTF_CALL_MD_STRESS_TAILCALL set for call [{call.TreeId:D6}]\n");
                            }
#endif
                        }
                        else
                        {
#if FEATURE_TAILCALL_OPT
                            // Must be an implicit tail call.
                            assert(isImplicitTailCall);

                            // It is possible that a call node is both an inline candidate and marked
                            // for opportunistic tail calling.  Inlining happens before morphing of
                            // trees.  If inlining of an inline candidate gets aborted for whatever
                            // reason, it will survive to the morphing stage at which point it will be
                            // transformed into a tail call after performing additional checks.

                            call._callMoreFlags |= GTF_CALL_M_IMPLICIT_TAILCALL;

#if DEBUG
                            JITDUMP($"\nGTF_CALL_M_IMPLICIT_TAILCALL set for call [{call.TreeId:D6}]\n");
#endif
#else
                            NYI("Implicit tail call prefix on a target which doesn't support opportunistic tail calls");
#endif
                        }

                        // This might or might not turn into a tailcall. We do more
                        // checks in morph. For explicit tailcalls we need more
                        // information in morph in case it turns out to be a
                        // helper-based tailcall.
                        if (isExplicitTailCall)
                        {
                            call._tailCallInfo = new TailCallSiteInfo();

                            switch (opcode)
                            {
                                case CEE_CALLI:
                                {
                                    call._tailCallInfo.SetCalli(sigInfo);
                                    break;
                                }

                                case CEE_CALLVIRT:
                                {
                                    call._tailCallInfo.SetCallvirt(sigInfo, resolvedToken);
                                    break;
                                }

                                default:
                                {
                                    call._tailCallInfo.SetCall(sigInfo, resolvedToken);
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // canTailCall reported its reasons already
                        canTailCall = false;

#if DEBUG
                        JITDUMP($"\ninfo.compCompHnd->canTailCall returned false for call [{call.TreeId:D6}]\n");
#endif
                    }
                }
                else
                {
                    // If this assert fires it means that canTailCall was set to false without setting a reason!

#if DEBUG
                    assert(!canTailCallFailReasonUtf8.IsEmpty);
                    JITDUMP($"\nRejecting {(isExplicitTailCall ? "ex" : "im")}plicit tail call for [{call.TreeId:D6}], reason: '{Encoding.UTF8.GetString(canTailCallFailReasonUtf8)}'\n");
#endif

                    fixed (byte* pCanTailCallFailReasonUtf8 = canTailCallFailReasonUtf8)
                    {
                        compiler.info.compCompHnd->reportTailCallDecision(compiler.info.compMethodHnd, methHnd, isExplicitTailCall, TAILCALL_FAIL, pCanTailCallFailReasonUtf8);
                    }
                }
            }

            // Note: we assume that small return types are already normalized by the managed callee
            // or by the pinvoke stub for calls to unmanaged code.

            if (!bIntrinsicImported)
            {
                // Things needed to be checked when bIntrinsicImported is false.

                assert(call.Oper is GT_CALL);

                if (compiler.compIsForInlining && (opcode is CEE_CALLVIRT))
                {
                    assert(call.Args.HasThisPointer);
                    var callObj = call.Args.ThisArg.EarlyNode;

                    if ((call.IsVirtual || ((call.Flags & GTF_CALL_NULLCHECK) is not 0)) && compiler.impInlineIsGuaranteedThisDerefBeforeAnySideEffects(additionalTree: null, call.Args, callObj, compiler.impInlineInfo.inlArgInfo))
                    {
                        compiler.impInlineInfo.thisDereferencedFirst = true;
                    }
                }

                assert(compiler.compInlineContext is not null);

#if DEBUG
                // Keep track of the raw IL offset of the call
                call._rawILOffset = opcodeOffs;
#endif

                // Is it an inline candidate?
                compiler.impMarkInlineCandidate(call, exactContextHnd, exactContextNeedsRuntimeLookup, callInfo, compiler.compInlineContext);

                // If the call is virtual, extra information for possible use during late devirt inlining.
                //
                if (call.IsDevirtualizationCandidate(compiler))
                {
#if DEBUG
                    JITDUMP($"\nSaving late devirtualization info for call [{call.TreeId:D6}]\n");
                    assert(call._inlineContext == compiler.impCurStmtDI.InlineContext);
#endif

                    call._lateDevirtualizationInfo = new LateDevirtualizationInfo {
                        methodHnd = callInfo.hMethod,
                        exactContextHnd = exactContextHnd,
                        ilLocation = compiler.impCurStmtDI.Location,
                    };
                }
            }

            // Extra checks for tail calls and tail recursion.
            //
            // A tail recursive call is a potential loop from the current block to the start of the root method.
            // If we see a tail recursive call, mark the blocks from the call site back to the entry as potentially
            // being in a loop.
            //
            // Note: if we're importing an inlinee we don't mark the right set of blocks, but by then it's too
            // late. Currently this doesn't lead to problems. See GitHub issue 33529.
            //
            // OSR also needs to handle tail calls specially:
            // * block profiling in OSR methods needs to ensure probes happen before tail calls, not after.
            // * the root method entry must be imported if there's a recursive tail call or a potentially
            //   inlineable tail call.
            //
            if ((tailCallFlags is not 0) && canTailCall)
            {
                if (compiler.gtIsRecursiveCall(methHnd))
                {
                    assert(compiler.stackState.esStackDepth is 0);
                    var loopHead = null as BasicBlock;

                    if (!compiler.compIsForInlining && compiler.opts.IsOSR)
                    {
                        // For root method OSR we may branch back to the actual method entry,
                        // which is not fgFirstBB, and which we will need to import.
                        assert(compiler.fgEntryBB is not null);
                        loopHead = compiler.fgEntryBB;
                    }
                    else
                    {
                        // For normal jitting we may branch back to the firstBB; this
                        // should already be imported.
                        loopHead = compiler.fgGetFirstILBlock();
                    }

#if DEBUG
                    JITDUMP($"\nTail recursive call [{call.TreeId:D6}] in the method. Mark {FMT_BB(loopHead.bbNum)} to {FMT_BB(compiler.compCurBB.bbNum)} as having a backward branch.\n");
#endif

                    compiler.fgMarkBackwardJump(loopHead, compiler.compCurBB);

                    compiler.MethodHasRecursiveTailCall = true;
                    compiler.compCurBB.SetFlags(BBF_RECURSIVE_TAILCALL);
                }

                // If we might be instrumenting, flag blocks that might be tail call successors
                // so we can relocate probes before the calls.

                if (compiler.opts.IsInstrumentedAndOptimized || compiler.opts.IsOSR)
                {
                    // If a root method tail call candidate block is not a BBJ_RETURN, it should have a unique
                    // BBJ_RETURN successor. Mark that successor so we can handle it specially during profile
                    // instrumentation.

                    if (compiler.compCurBB.Kind is not BBJ_RETURN)
                    {
                        var successor = compiler.compCurBB.UniqueSucc;

                        assert(successor is not null);
                        assert(successor.Kind is BBJ_RETURN);

                        successor.SetFlags(BBF_TAILCALL_SUCCESSOR);
                        compiler.optMethodFlags |= OMF_HAS_TAILCALL_SUCCESSOR;
                    }
                }
            }

            if ((sigInfo.flags & CORINFO_SIGFLAG_FAT_CALL) is not 0)
            {
                assert((opcode is CEE_CALLI) || (callInfo.kind == CORINFO_CALL_CODE_POINTER));
                compiler.addFatPointerCandidate(call);
            }
            return DoneCall(compiler, call);
        }

        private unsafe var_types DoneCall(Compiler compiler, GenTree result)
        {
            assert(compiler.compCurBB is not null);

            // Push or append the result of the call

            if (callRetTyp is TYP_VOID)
            {
                if (opcode is CEE_NEWOBJ)
                {
                    // we actually did push something, so don't spill the thing we just pushed.
                    assert(compiler.stackState.esStackDepth > 0);
                    _ = compiler.impAppendTree(result, compiler.stackState.esStackDepth - 1, compiler.impCurStmtDI);
                }
                else
                {
                    if (result.Oper.IsCall)
                    {
                        var call = result.AsCall();

                        if (call.IsSpecialIntrinsic(compiler, NI_System_SpanHelpers_Memmove))
                        {
                            if (JitConfig.JitProfileValues is not 0)
                            {
                                if (compiler.opts.IsOptimizedWithProfile)
                                {
                                    result = compiler.impDuplicateWithProfiledArg(call, opcodeOffs);
                                }
                                else if (compiler.opts.IsInstrumented)
                                {
                                    // We might want to instrument it for optimized versions too, but we don't currently.
                                    call._handleHistogramProfileCandidateInfo = new HandleHistogramProfileCandidateInfo {
                                        ilOffset = opcodeOffs,
                                        probeIndex = 0,

                                    };

                                    compiler.compCurBB.SetFlags(BBF_HAS_VALUE_PROFILE);
                                }
                            }
                        }
                        else if (call.IsSpecialIntrinsic(compiler, NI_System_ArgumentNullException_ThrowIfNull))
                        {
                            result = compiler.impThrowIfNull(call);
                        }
                    }
                    compiler.impAppendTree(result, CHECK_SPILL_ALL, compiler.impCurStmtDI);
                }
            }
            else
            {
                compiler.impSpillSpecialSideEff();

                if ((clsFlags & CORINFO_FLG_ARRAY) is not 0)
                {
                    compiler.eeGetCallSiteSig(resolvedToken.token, resolvedToken.tokenScope, resolvedToken.tokenContext, out sigInfo);
                }

                var retTypeClass = sigInfo.retTypeClass;

                // Sometimes "call" is not a GT_CALL (if we imported an intrinsic that didn't turn into a call)
                if (!bIntrinsicImported)
                {
                    assert(result.Oper.IsCall);
                    var call = result.AsCall();

                    // If the call is a special intrinsic, we may know a more exact return type.
                    if (call.IsSpecialIntrinsic())
                    {
                        var updatedRetTypeClass = compiler.impGetSpecialIntrinsicExactReturnType(call);

                        if (updatedRetTypeClass != NO_CLASS_HANDLE)
                        {
                            JITDUMP($"Updating method return type to {compiler.eeGetClassName(updatedRetTypeClass)}\n");
                            retTypeClass = updatedRetTypeClass;
                        }
                    }

                    var isFatPointerCandidate = call.IsFatPointerCandidate;
                    var isInlineCandidate = call.IsInlineCandidate;
                    var isGuardedDevirtualizationCandidate = call.IsGuardedDevirtualizationCandidate;

                    if (varTypeIsStruct(callRetTyp))
                    {
                        // Need to treat all "split tree" cases here, not just inline candidates
                        result = compiler.impFixupCallStructReturn(call, retTypeClass);
                        callRetTyp = result.Type;
                    }

                    // TODO: consider handling fatcalli cases this way too...?
                    if (isInlineCandidate || isGuardedDevirtualizationCandidate)
                    {
                        // We should not have made any adjustments in impFixupCallStructReturn
                        // as we defer those until we know the fate of the call.
                        assert(result == call);

                        assert(compiler.opts.OptEnabled(CLFLG_INLINING));
                        assert(!isFatPointerCandidate); // We should not try to inline calli.

                        // Make the call its own tree (spill the stack if needed).
                        // Do not consume the debug info here. This is particularly
                        // important if we give up on the inline, in which case the
                        // call will typically end up in the statement that contains
                        // the GT_RET_EXPR that we leave on the stack.
                        compiler.impAppendTree(result, CHECK_SPILL_ALL, compiler.impCurStmtDI, false);

                        // TODO: Still using the widened type.
                        var retExpr = compiler.gtNewInlineCandidateReturnExpr(call, callRetTyp.ActualType);

                        // Link the retExpr to the call so if necessary we can manipulate it later.
                        if (call.IsGuardedDevirtualizationCandidate)
                        {
                            for (byte i = 0; i < call.InlineCandidatesCount; i++)
                            {
                                call.GetGdvCandidateInfo(i).retExpr = retExpr;
                            }
                        }
                        else
                        {
                            call.GetGdvCandidateInfo(0).retExpr = retExpr;
                        }

                        // Propagate retExpr as the placeholder for the call.
                        result = retExpr;
                    }
                    else
                    {
                        if (result.Oper.IsCall && isFatPointerCandidate)
                        {
                            var resultCall = result.AsCall();

                            // these calls should be in statements of the form call() or var = call().
                            // Such form allows to find statements with fat calls without walking through whole trees
                            // and removes problems with cutting trees.

                            var resultLcl = compiler.lvaGrabTemp(shortLifetime: true, "calli");
                            ref var varDsc = ref compiler.lvaGetDesc(resultLcl);

                            // Keep the information about small typedness to avoid
                            // inserting unnecessary casts around normalization.
                            if (varTypeIsSmall(resultCall._returnType))
                            {
                                assert(resultCall.NormalizesSmallTypesOnReturn);
                                varDsc.Type = resultCall._returnType;
                            }

                            compiler.impStoreToTemp(resultLcl, resultCall, CHECK_SPILL_ALL);

                            // impStoreToTemp can change src arg list and return type for call that returns struct.
                            result = compiler.gtNewLclvNode(varDsc.Type.ActualType, resultLcl);
                        }

                        // For non-candidates we must also spill, since we
                        // might have locals live on the eval stack that this
                        // call can modify.
                        //
                        // Suppress this for certain well-known call targets
                        // that we know won't modify locals, eg calls that are
                        // recognized in gtCanOptimizeTypeEquality. Otherwise
                        // we may break key fragile pattern matches later on.
                        var spillStack = true;

                        if (result.Oper.IsCall)
                        {
                            var resultCall = result.AsCall();

                            if (resultCall.IsHelperCall() && (compiler.gtIsTypeHandleToRuntimeTypeHelper(resultCall) || compiler.gtIsTypeHandleToRuntimeTypeHandleHelper(resultCall)))
                            {
                                spillStack = false;
                            }
                            else if (resultCall.IsSpecialIntrinsic())
                            {
                                spillStack = false;

                                if ((JitConfig.JitProfileValues is not 0) && resultCall.IsSpecialIntrinsic(compiler, NI_System_SpanHelpers_SequenceEqual))
                                {
                                    if (compiler.opts.IsOptimizedWithProfile)
                                    {
                                        result = compiler.impDuplicateWithProfiledArg(resultCall, opcodeOffs);

                                        if (result.Oper is GT_QMARK)
                                        {
                                            // QMARK has to be a root node
                                            var tmp = compiler.lvaGrabTemp(shortLifetime: true, "Grabbing temp for Qmark");
                                            compiler.impStoreToTemp(tmp, result, CHECK_SPILL_ALL);
                                            result = compiler.gtNewLclvNode(result.Type, tmp);
                                        }
                                    }
                                    else if (compiler.opts.IsInstrumented)
                                    {
                                        // We might want to instrument it for optimized versions too, but we don't currently.
                                        resultCall._handleHistogramProfileCandidateInfo = new HandleHistogramProfileCandidateInfo {
                                            ilOffset = opcodeOffs,
                                            probeIndex = 0,
                                        };

                                        compiler.compCurBB.SetFlags(BBF_HAS_VALUE_PROFILE);
                                    }
                                }
                            }
                        }

                        if (spillStack)
                        {
                            compiler.impSpillSideEffects(true, CHECK_SPILL_ALL, "non-inline candidate call");
                        }
                    }

                    // If the call is of a small type and the callee is managed, the callee will normalize the result before returning.
                    // However, we need to normalize small type values returned by unmanaged functions (pinvoke).
                    // The pinvoke stub does the normalization, but we need to do it here if we use the shorter inlined pinvoke stub.

                    if (checkForSmallType && varTypeIsIntegral(callRetTyp) && (callRetTyp.Size < TYP_INT.Size))
                    {
                        result = compiler.gtNewCastNode(callRetTyp.ActualType, result, fromUnsigned: false, callRetTyp);
                    }
                }

                var tiRetVal = compiler.makeTypeInfo(sigInfo.retType, retTypeClass);
                compiler.impPushOnStack(result, tiRetVal);
            }

#if SWIFT_SUPPORT
            // If call is a Swift call with error handling, append additional IR
            // to handle storing the error register's value post-call.
            if (swiftErrorNode is not null)
            {
                compiler.impAppendSwiftErrorStore(swiftErrorNode);
            }
#endif

            return callRetTyp;
        }
    }
}
