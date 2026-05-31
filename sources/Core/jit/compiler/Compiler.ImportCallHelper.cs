// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
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

        public unsafe bool TryImport(Compiler compiler, byte* codeAddr, byte* codeEndp, byte sz)
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
                        var passedConstraintCheck = compiler.checkTailCallConstraint(opcode, resolvedToken, constrainedCall ? constrainedResolvedToken : Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>());

                        // Avoid setting compHasBackwardsJump = true via tail call stress if the method cannot have patchpoints.
                        var mayHavePatchpoints = compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0) && (JitConfig[ConfigInteger.TC_OnStackReplacement] > 0) && compiler.compCanHavePatchpoints();

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

            var callTyp = Import(compiler);

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
                        compiler.eeGetCallInfo(resolvedToken, Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(), CORINFO_CALLINFO_ALLOWINSTPARAM, out var calliInfo);

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
                    jitprintf($"\nIn Compiler.impImportCall: opcode is {opcode.Name}, kind={callInfo.kind}, callRetType is {callRetTyp.Name}, structSize is {structSize}\n");
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
                    jitprintf($"\nIn Compiler.impImportCall: opcode is {opcode.Name}, kind={callInfo.kind}, callRetType is {callRetTyp.Name}, structSize is {structSize}\n");
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

                    var intrinsicCall = Intrinsic(mflags, isReadonlyCall, isTailCall, ref entryPoint, out ni, out isSpecialIntrinsic);

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
                        assert(thisPtrCopy is not null);

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
                        assert((sigInfo.callConv & CORINFO_CALLCONV_MASK) is not CORINFO_CALLCONV_VARARG and not not CORINFO_CALLCONV_NATIVEVARARG);

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
        /// <param name="methodFlags">CORINFO_FLG_XXX flags of the intrinsic method</param>
        /// <param name="readonlyCall">true if call has a readonly prefix</param>
        /// <param name="tailCall">true if call is in tail position</param>
        /// <param name="entryPoint">The entry point information required for R2R scenarios</param>
        /// <param name="pIntrinsicName"> intrinsic name (see enumeration in namedintrinsiclist.h) for "traditional" jit intrinsics</param>
        /// <param name="isSpecialIntrinsic">set true if intrinsic expansion is a call that is amenable to special downstream optimization opportunities</param>
        /// <returns>IR tree to use in place of the call, or null if the jit should treat the intrinsic call like a normal call.</returns>
        private unsafe GenTree? Intrinsic(CorInfoFlag methodFlags, bool readonlyCall, bool tailCall, ref CORINFO_CONST_LOOKUP entryPoint, out NamedIntrinsic pIntrinsicName, out bool isSpecialIntrinsic)
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

            // TODO: Port impIntrinsic

            pIntrinsicName = NI_Illegal;
            isSpecialIntrinsic = false;

            return null;
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
                            JITDUMP($"\nGTF_CALL_M_EXPLICIT_TAILCALL set for call [{call.TreeId:D6}]\n");

#if DEBUG
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
                            JITDUMP($"\nGTF_CALL_M_IMPLICIT_TAILCALL set for call [{call.TreeId:D6}]\n");
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
                        JITDUMP($"\ninfo.compCompHnd->canTailCall returned false for call [{call.TreeId:D6}]\n");
                    }
                }
                else
                {
                    // If this assert fires it means that canTailCall was set to false without setting a reason!
                    assert(!canTailCallFailReasonUtf8.IsEmpty);
                    JITDUMP($"\nRejecting {(isExplicitTailCall ? "ex" : "im")}plicit tail call for [{call.TreeId:D6}], reason: '{Encoding.UTF8.GetString(canTailCallFailReasonUtf8)}'\n");

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
                    JITDUMP($"\nSaving late devirtualization info for call [{call.TreeId:D6}]\n");
                    assert(call._inlineContext == compiler.impCurStmtDI.InlineContext);

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

                    JITDUMP($"\nTail recursive call [{call.TreeId:D6}] in the method. Mark {FMT_BB(loopHead.bbNum)} to {FMT_BB(compiler.compCurBB.bbNum)} as having a backward branch.\n");
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
                            if (JitConfig[ConfigInteger.JitProfileValues] is not 0)
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
                            call.SingleInlineCandidateInfo.retExpr = retExpr;
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

                                if ((JitConfig[ConfigInteger.JitProfileValues] is not 0) && resultCall.IsSpecialIntrinsic(compiler, NI_System_SpanHelpers_SequenceEqual))
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
                            compiler.impSpillSideEffects(true, CHECK_SPILL_ALL, ("non-inline candidate call"));
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
