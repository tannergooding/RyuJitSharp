// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{

    /// <summary>Only present for inlinees</summary>
    public InlineInfo? impInlineInfo;

    /// <summary>Size of the full stack</summary>
    protected uint impStkSize;

    protected NodeToUnsignedMap? impEnumeratorGdvLocalMap;

    protected VarToLikelyClassMap? impEnumeratorLikelyTypeMap;

    /// <summary>Statements for the BB being imported.</summary>
    protected Statement? impStmtList;

    /// <summary>The last statement for the current BB.</summary>
    protected Statement? impLastStmt;

#if DEBUG
    private uint impCurOpcOffs;

    private string? impCurOpcName;

    private bool impNestedStackSpill;

    /// <summary>oldest stmt added for which we did not call SetLastILOffset().</summary>
    /// <remarks>For displaying instrs with generated native code (-n:B)</remarks>
    private Statement? impLastILoffsStmt;
#endif

    /// <summary>The context used for looking up tokens.</summary>
    private unsafe CORINFO_CONTEXT_HANDLE impTokenLookupContextHandle;

    /// <summary>Debug info of current statement being imported</summary>
    /// <remarks>
    ///   <para>It gets set to contain no IL location (!impCurStmtDI.GetLocation().IsValid) after it has been set in the appended trees.</para>
    ///   <para>Then it gets updated at IL instructions for which we have to report mapping info.</para>
    ///   <para>It will always contain the current inline context.</para>
    /// </remarks>
    private DebugInfo impCurStmtDI;

    /// <summary>list of BBs currently waiting to be imported.</summary>
    private PendingDsc? impPendingList;

    /// <summary>Freed up dscs that can be reused</summary>
    private PendingDsc? impPendingFree;

    // We keep a byte-per-block map (dynamically extended) in the top-level Compiler object of a compilation.
    private List<byte> impPendingBlockMembers;

    private bool impCanReimport;

    // When we compute a "spill clique" (see above) these byte-maps are allocated to have a byte per basic
    // block, and represent the predecessor and successor members of the clique currently being computed.
    // *** Access to these will need to be locked in a parallel compiler.

    private List<byte> impSpillCliquePredMembers;

    private List<byte> impSpillCliqueSuccMembers;

    private BlockListNode? impBlockListNodeFreeList;

    /// <summary>the temp below is valid and available</summary>
    public bool impBoxTempInUse;

    /// <summary>a temporary that is used for boxing</summary>
    public uint impBoxTemp;

#if DEBUG
    public uint impInlinedCodeSize;
#endif

    // The Compiler that is the root of the inlining tree of which "this" is a member.
    public Compiler impInlineRoot
    {
        get
        {
            var result = this;

            if (impInlineInfo is not null)
            {
                result = impInlineInfo.InlineRoot;
            }
            return result;
        }
    }

    /// <summary>check that the node's type is compatible with the signature's type using ECMA implicit argument coercion table.</summary>
    /// <param name="sigType">the type in the call signature</param>
    /// <param name="nodeType">the node type</param>
    /// <returns>true if they are compatible, false otherwise</returns>
    /// <remarks>
    ///   <para>it is currently allowing byref->long passing, should be fixed in VM</para>
    ///   <para>it can't check long -> native int case on 64-bit platforms, so the behavior is different depending on the target bitness</para>
    /// </remarks>
    public static bool impCheckImplicitArgumentCoercion(var_types sigType, var_types nodeType)
    {
        if (sigType == nodeType)
        {
            return true;
        }

        assert(AreContiguous(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT));

        if (sigType is >= TYP_BYTE and <= TYP_UINT)
        {
            if (nodeType is (>= TYP_BYTE and <= TYP_UINT) or TYP_I_IMPL)
            {
                return true;
            }
        }
        else if (sigType is TYP_LONG or TYP_ULONG)
        {
            if (nodeType is TYP_LONG)
            {
                return true;
            }
        }
        else if (sigType is TYP_FLOAT or TYP_DOUBLE)
        {
            if (nodeType is TYP_FLOAT or TYP_DOUBLE)
            {
                return true;
            }
        }
        else if (sigType is TYP_BYREF)
        {
            if (nodeType is TYP_I_IMPL)
            {
                return true;
            }

            // This condition tolerates such IL:
            // ;  V00 this              ref  this class-hnd
            // ldarg.0
            // call(byref)
            if (nodeType is TYP_REF)
            {
                return true;
            }
        }
        else if (varTypeIsStruct(sigType))
        {
            if (varTypeIsStruct(nodeType))
            {
                return true;
            }
        }

        // This condition should not be under `else` because `TYP_I_IMPL`
        // intersects with `TYP_LONG` or `TYP_INT`.
        if (sigType is TYP_I_IMPL or TYP_U_IMPL)
        {
            // Note that it allows `ldc.i8 1; call(nint)` on 64-bit platforms,
            // but we can't distinguish `nint` from `long` there.
            if (nodeType is TYP_I_IMPL or TYP_U_IMPL or TYP_INT or TYP_UINT)
            {
                return true;
            }

            // It tolerates IL that ECMA does not allow but that is commonly used.
            // Example:
            //   V02 loc1           struct <RTL_OSVERSIONINFOEX, 32>
            //   ldloca.s     0x2
            //   call(native int)
            if (nodeType is TYP_BYREF)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>screen inline candate based on info from the method header</summary>
    /// <param name="fncHandle">inline candidate method</param>
    /// <param name="methInfo">method info from VM</param>
    /// <param name="forceInline">true if method is marked with AggressiveInlining</param>
    /// <param name="inlineResult">ongoing inline evaluation</param>
    public unsafe void impCanInlineIL(CORINFO_METHOD_HANDLE fncHandle, CORINFO_METHOD_INFO* methInfo, bool forceInline, InlineResult inlineResult)
    {
        var codeSize = methInfo->ILCodeSize;

        // We shouldn't have made up our minds yet...
        assert(!inlineResult.IsDecided);

        if (methInfo->EHcount > 0)
        {
            if (!opts.compInlineMethodsWithEH)
            {
                inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_EH);
                return;
            }
        }

        if ((methInfo->ILCode is null) || (codeSize == 0))
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_NO_BODY);
            return;
        }

        // For now we don't inline varargs (import code can't handle it)

        if (methInfo->args.isVarArg())
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_MANAGED_VARARGS);
            return;
        }

        // Reject if it has too many locals.
        // This is currently an implementation limit due to fixed-size arrays in the
        // inline info, rather than a performance heuristic.

        inlineResult.NoteInt(InlineObservation.CALLEE_NUMBER_OF_LOCALS, methInfo->locals.numArgs);

        if (methInfo->locals.numArgs > MAX_INL_LCLS)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_TOO_MANY_LOCALS);
            return;
        }

        // Make sure there aren't too many arguments.
        // This is currently an implementation limit due to fixed-size arrays in the
        // inline info, rather than a performance heuristic.

        inlineResult.NoteInt(InlineObservation.CALLEE_NUMBER_OF_ARGUMENTS, methInfo->args.numArgs);

        if (methInfo->args.numArgs > MAX_INL_ARGS)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_TOO_MANY_ARGUMENTS);
            return;
        }

        // Note force inline state

        inlineResult.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, forceInline);

        // Note IL code size

        inlineResult.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, codeSize);

        if (inlineResult.IsFailure)
        {
            return;
        }

        // Make sure maxstack is not too big

        inlineResult.NoteInt(InlineObservation.CALLEE_MAXSTACK, methInfo->maxStack);

        if (inlineResult.IsFailure)
        {
            return;
        }
    }

    public unsafe var_types impNormStructType(CORINFO_CLASS_HANDLE structHnd)
        => impNormStructType(structHnd, out Unsafe.NullRef<var_types>());

    /// <summary>Normalize the type of a (known to be) struct class handle.</summary>
    /// <param name="structHnd">The class handle for the struct type of interest.</param>
    /// <param name="pSimdBaseJitType">if the struct is a SIMD type, set to the SIMD base JIT type</param>
    /// <returns>The JIT type for the struct (e.g. TYP_STRUCT, or TYP_SIMD*).</returns>
    /// <remarks>
    ///   <para>This may also modify the compFloatingPointUsed flag if the type is a SIMD type.</para>
    ///   <para>Normalizing the type involves examining the struct type to determine if it should be modified to one that is handled specially by the JIT, possibly being a candidate for full enregistration, e.g. TYP_SIMD16.</para>
    ///   <para>If the size of the struct is already known call <see cref="structSizeMightRepresentSimdType" /> to determine if this api needs to be called.</para>
    /// </remarks>
    public unsafe var_types impNormStructType(CORINFO_CLASS_HANDLE structHnd, out var_types pSimdBaseJitType)
    {
        Unsafe.SkipInit(out pSimdBaseJitType);

        assert(structHnd != NO_CLASS_HANDLE);
        var structType = TYP_STRUCT;

#if FEATURE_SIMD
        var structFlags = info.compCompHnd->getClassAttribs(structHnd);

        // Don't bother if the struct contains GC references of byrefs, it can't be a SIMD type.
        if ((structFlags & (CORINFO_FLG_CONTAINS_GC_PTR | CORINFO_FLG_BYREF_LIKE)) == 0)
        {
            var originalSize = info.compCompHnd->getClassSize(structHnd);

            if (structSizeMightRepresentSimdType(originalSize))
            {
                var simdBaseType = getBaseTypeAndSizeOfSimdType(structHnd, out var sizeBytes);

                if (simdBaseType != TYP_UNDEF)
                {
                    assert((sizeBytes == originalSize )|| (sizeBytes is SIZE_UNKNOWN));
                    structType = GetSimdTypeForSize(sizeBytes);

                    if (!Unsafe.IsNullRef(in pSimdBaseJitType))
                    {
                        pSimdBaseJitType = simdBaseType;
                    }

                    // Also indicate that we use floating point registers.
                    compFloatingPointUsed = true;
                }
            }
        }
#endif

        return structType;
    }
}
