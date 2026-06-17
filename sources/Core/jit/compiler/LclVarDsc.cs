// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public partial struct LclVarDsc
{
    private Flags _flags;

    /// <summary>weighted reference count</summary>
    private weight_t _lvRefCntWtd;

    /// <summary>class handle for the local or null if not known or not a class</summary>
    public unsafe CORINFO_CLASS_HANDLE lvClassHnd;

    /// <summary>layout info for structs</summary>
    private ClassLayout? _layout;

    private int _anonymous;

    /// <summary>stack offset of home in bytes.</summary>
    private int lvStkOffs;

    /// <summary>original slot # (if remapped)</summary>
    internal int lvSlotNum;

#if DEBUG
    private DoNotEnregisterReason _doNotEnregReason;

    private AddressExposedReason _addrExposedReason;

    private DebugFlags _debugFlags;
#endif

    /// <summary>variable tracking index</summary>
    internal ushort _varIndex;

    /// <summary>unweighted (real) reference count.</summary>
    /// <remarks>For implicit by reference parameters, this gets hijacked from fgResetImplicitByRefRefCount through fgMarkDemotedImplicitByRefArgs, to provide a static appearance count (computed during address-exposed analysis) that fgMakeOutgoingStructArgCopy consults during global morph to determine if eliding its copy is legal.</remarks>
    private ushort _lvRefCnt;

    private byte _bitfield;

    private byte _fieldCnt;

    private byte _fldOffset;

    private byte _fldOrdinal;

    /// <summary>Used to store the register this variable is in (or, the low register of a register pair).</summary>
    /// <remarks>It is set during codegen any time the variable is enregistered (lvRegister is only set to non-zero if the variable gets the same register assignment for its entire lifetime).</remarks>
    private regNumber _lvRegNum;

#if TARGET_32BIT
    /// <summary>Used for "upper half" of long var.</summary>
    private regNumber _lvOtherReg;
#endif

    /// <summary>the register into which the argument is moved at entry</summary>
    private regNumber _lvArgInitReg;

    /// <summary>Set if the argument is an implicit byref.</summary>
#if FEATURE_IMPLICIT_BYREFS
    public bool IsImplicitByRef
    {
        readonly get
        {
            return (_flags & Flags.IsImplicitByRef) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsImplicitByRef) | (value ? Flags.IsImplicitByRef : Flags.None);
        }
    }
#else
    public bool IsImplicitByRef => false;
#endif

    /// <summary>The local is known to be never negative</summary>
    public bool IsNeverNegative
    {
        readonly get
        {
            return (_flags & Flags.IsNeverNegative) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsNeverNegative) | (value ? Flags.IsNeverNegative : Flags.None);
        }
    }

    public readonly bool IsStackAllocatedObject => lvStackAllocatedObject;

    /// <summary>Gets or set the layout of a struct variable or implicit byref.</summary>
    public ClassLayout? Layout
    {
        readonly get
        {
#if FEATURE_IMPLICIT_BYREFS
            assert(Debugger.IsAttached || varTypeIsStruct(Type) || (IsImplicitByRef && (Type is TYP_BYREF)));
#else
            assert(Debugger.IsAttached || varTypeIsStruct(Type));
#endif
            return _layout;
        }

        set
        {
            assert(varTypeIsStruct(Type));
            assert((_layout is null) || ClassLayout.AreCompatible(_layout, value));
            _layout = value;
        }
    }

#if !FEATURE_HFA_FIELDS_PRESENT
    /// <summary>What kind of an HFA this is (CORINFO_HFA_ELEM_NONE if it is not an HFA).</summary>
    public CorInfoHFAElemType _lvHfaElemKind
    {
        readonly get
        {
            return (CorInfoHFAElemType)((_bitfield >>> 5) & 0x07);
        }

        set
        {
            _bitfield = (byte)((_bitfield & ~(0x07 << 5)) | (((byte)(value) & 0x07) << 5));
        }
    }
#endif

    /// <summary>true if this is a multireg LclVar struct used in an argument context or if this is a multireg LclVar struct assigned from a multireg call</summary>
    public readonly bool lvIsMultiRegArgOrRet => lvIsMultiRegArg || lvIsMultiRegRet;

    public bool IsMultiRegDest
    {
        readonly get
        {
            return lvIsMultiRegDest;
        }

        set
        {
            lvIsMultiRegDest = true;
            // TODO-Quirk: Set the old lvIsMultiRegRet, which is used for heuristics
            lvIsMultiRegRet = true;
        }
    }

    /// <summary>is this a parameter?</summary>
    public bool lvIsParam
    {
        readonly get
        {
            return (_flags & Flags.IsParam) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsParam) | (value ? Flags.IsParam : Flags.None);
        }
    }

    /// <summary>is any part of this parameter passed in a register?</summary>
    public bool lvIsRegArg
    {
        readonly get
        {
            return (_flags & Flags.IsRegArg) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsRegArg) | (value ? Flags.IsRegArg : Flags.None);
        }
    }

    /// <summary>is this the target of a param reg to local mapping?</summary>
    public bool lvIsParamRegTarget
    {
        readonly get
        {
            return (_flags & Flags.IsParamRegTarget) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsParamRegTarget) | (value ? Flags.IsParamRegTarget : Flags.None);
        }
    }

    /// <summary>0 = off of REG_SPBASE (e.g., ESP), 1 = off of REG_FPBASE (e.g., EBP)</summary>
    public bool lvFramePointerBased
    {
        readonly get
        {
            return (_flags & Flags.FramePointerBased) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.FramePointerBased) | (value ? Flags.FramePointerBased : Flags.None);
        }
    }

    /// <summary>(part of) the variable lives on the frame</summary>
    public bool lvOnFrame
    {
        readonly get
        {
            return (_flags & Flags.OnFrame) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.OnFrame) | (value ? Flags.OnFrame : Flags.None);
        }
    }

    /// <summary>assigned to live in a register? For RyuJIT backend, this is only set if the variable is in the same register for the entire function.</summary>
    public bool lvRegister
    {
        readonly get
        {
            return (_flags & Flags.Register) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.Register) | (value ? Flags.Register : Flags.None);
        }
    }

    /// <summary>is this a tracked variable?</summary>
    public bool lvTracked
    {
        readonly get
        {
            return (_flags & Flags.Tracked) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.Tracked) | (value ? Flags.Tracked : Flags.None);
        }
    }

    public readonly bool lvTrackedNonStruct => lvTracked && (Type is not TYP_STRUCT);

    /// <summary>is this a pinned variable?</summary>
    public bool lvPinned
    {
        readonly get
        {
            return (_flags & Flags.Pinned) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.Pinned) | (value ? Flags.Pinned : Flags.None);
        }
    }

    /// <summary>must be initialized</summary>
    public bool lvMustInit
    {
        readonly get
        {
            return (_flags & Flags.MustInit) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.MustInit) | (value ? Flags.MustInit : Flags.None);
        }
    }

    /// <summary>The address of this variable is "exposed" -- passed as an argument, stored in a global location, etc.</summary>
    /// <remarks>We cannot reason reliably about the value of the variable.</remarks>
    private bool _addrExposed
    {
        readonly get
        {
            return (_flags & Flags.AddrExposed) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.AddrExposed) | (value ? Flags.AddrExposed : Flags.None);
        }
    }

    /// <summary>The variable is live in or out of an exception handler.</summary>
    private bool _lvLiveInOutOfHandler
    {
        readonly get
        {
            return (_flags & Flags.LiveInOutOfHandler) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.LiveInOutOfHandler) | (value ? Flags.LiveInOutOfHandler : Flags.None);
        }
    }

    /// <summary>Do not enregister this variable.</summary>
    public bool lvDoNotEnregister
    {
        readonly get
        {
            return (_flags & Flags.DoNotEnregister) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.DoNotEnregister) | (value ? Flags.DoNotEnregister : Flags.None);
        }
    }

    /// <summary>The var is a struct local, and a field of the variable is accessed.</summary>
    /// <remarks>Affects struct promotion.</remarks>
    public bool lvFieldAccessed
    {
        readonly get
        {
            return (_flags & Flags.FieldAccessed) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.FieldAccessed) | (value ? Flags.FieldAccessed : Flags.None);
        }
    }

    /// <summary>The variable is in SSA form (set by SsaBuilder)</summary>
    public bool lvInSsa
    {
        readonly get
        {
            return (_flags & Flags.InSsa) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.InSsa) | (value ? Flags.InSsa : Flags.None);
        }
    }

    /// <summary>Indicates if this LclVar is a CSE variable.</summary>
    public bool lvIsCSE
    {
        readonly get
        {
            return (_flags & Flags.IsCse) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsCse) | (value ? Flags.IsCse : Flags.None);
        }
    }

    /// <summary>has ldloca or ldarga opcode on this local.</summary>
    public bool lvHasLdAddrOp
    {
        readonly get
        {
            return (_flags & Flags.HasLdAddrOp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasLdAddrOp) | (value ? Flags.HasLdAddrOp : Flags.None);
        }
    }

    /// <summary>there is at least one STLOC or STARG on this local</summary>
    public bool lvHasILStoreOp
    {
        readonly get
        {
            return (_flags & Flags.HasIlStoreOp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasIlStoreOp) | (value ? Flags.HasIlStoreOp : Flags.None);
        }
    }

    /// <summary>there is more than one STLOC on this local</summary>
    public bool lvHasMultipleILStoreOp
    {
        readonly get
        {
            return (_flags & Flags.HasMultipleIlStoreOp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasMultipleIlStoreOp) | (value ? Flags.HasMultipleIlStoreOp : Flags.None);
        }
    }

    /// <summary>Short-lifetime compiler temp</summary>
    public bool lvIsTemp
    {
        readonly get
        {
            return (_flags & Flags.IsTemp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsTemp) | (value ? Flags.IsTemp : Flags.None);
        }
    }

    /// <summary>variable has a single def. Used to identify ref type locals that can get type updates</summary>
    public bool lvSingleDef
    {
        readonly get
        {
            return (_flags & Flags.SingleDef) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.SingleDef) | (value ? Flags.SingleDef : Flags.None);
        }
    }

    /// <summary>variable has a single def and hence is a register candidate</summary>
    /// <remarks>Currently, this is only used to decide if an EH variable can be a register candidate or not.</remarks>
    public bool lvSingleDefRegCandidate
    {
        readonly get
        {
            return (_flags & Flags.SingleDefRegCandidate) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.SingleDefRegCandidate) | (value ? Flags.SingleDefRegCandidate : Flags.None);
        }
    }

    /// <summary>tracks variable that are disqualified from register candidancy</summary>
    public bool lvDisqualifySingleDefRegCandidate
    {
        readonly get
        {
            return (_flags & Flags.DisqualifySingleDefRegCandidate) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.DisqualifySingleDefRegCandidate) | (value ? Flags.DisqualifySingleDefRegCandidate : Flags.None);
        }
    }

    /// <summary>variable has a single def (as determined by LSRA interval scan) and is spilled making it candidate to spill right after the first (and only) definition.</summary>
    /// <remarks>Note: We cannot reuse lvSingleDefRegCandidate because it is set in earlier phase and the information might not be appropriate in LSRA.</remarks>
    public bool lvSpillAtSingleDef
    {
        readonly get
        {
            return (_flags & Flags.SpillAtSingleDef) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.SpillAtSingleDef) | (value ? Flags.SpillAtSingleDef : Flags.None);
        }
    }

    /// <summary>hint for CopyProp</summary>
    public bool lvHasExceptionalUsesHint
    {
        readonly get
        {
            return (_flags & Flags.HasExceptionalUsesHint) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasExceptionalUsesHint) | (value ? Flags.HasExceptionalUsesHint : Flags.None);
        }
    }

    /// <summary>Might this be used in an address computation? (used by buffer overflow security checks)</summary>
    public bool lvIsPtr
    {
        readonly get
        {
            return (_flags & Flags.IsPtr) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsPtr) | (value ? Flags.IsPtr : Flags.None);
        }
    }

    /// <summary>Does this contain an unsafe buffer requiring buffer overflow security checks?</summary>
    public bool lvIsUnsafeBuffer
    {
        readonly get
        {
            return (_flags & Flags.IsUnsafeBuffer) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsUnsafeBuffer) | (value ? Flags.IsUnsafeBuffer : Flags.None);
        }
    }

    /// <summary>True when this local is a promoted struct, a normed struct, or a "split" long on a 32-bit target.</summary>
    /// <remarks>For implicit byref parameters, this gets hijacked between fgRetypeImplicitByRefArgs and fgMarkDemotedImplicitByRefArgs to indicate whether references to the arg are being rewritten as references to a promoted shadow local.</remarks>
    public bool lvPromoted
    {
        readonly get
        {
            return (_flags & Flags.Promoted) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.Promoted) | (value ? Flags.Promoted : Flags.None);
        }
    }

    /// <summary>Is this local var a field of a promoted struct local?</summary>
    public bool lvIsStructField
    {
        readonly get
        {
            return (_flags & Flags.IsStructField) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsStructField) | (value ? Flags.IsStructField : Flags.None);
        }
    }

    /// <summary>Is this a promoted struct whose fields do not cover the struct local?</summary>
    public bool lvContainsHoles
    {
        readonly get
        {
            return (_flags & Flags.ContainsHoles) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.ContainsHoles) | (value ? Flags.ContainsHoles : Flags.None);
        }
    }

    /// <summary>true if this is a multireg LclVar struct used in an argument context</summary>
    public bool lvIsMultiRegArg
    {
        readonly get
        {
            return (_flags & Flags.IsMultiRegArg) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsMultiRegArg) | (value ? Flags.IsMultiRegArg : Flags.None);
        }
    }

    /// <summary>true if this is a multireg LclVar struct assigned from a multireg call</summary>
    public bool lvIsMultiRegRet
    {
        readonly get
        {
            return (_flags & Flags.IsMultiRegRet) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsMultiRegRet) | (value ? Flags.IsMultiRegRet : Flags.None);
        }
    }

    /// <summary>true if this is a multireg LclVar struct that is stored from a multireg node</summary>
    public bool lvIsMultiRegDest
    {
        readonly get
        {
            return (_flags & Flags.IsMultiRegDest) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsMultiRegDest) | (value ? Flags.IsMultiRegDest : Flags.None);
        }
    }

    /// <summary>Tracked for linear scan register allocation purposes</summary>
    public bool lvLRACandidate
    {
        readonly get
        {
            return (_flags & Flags.LraCandidate) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.LraCandidate) | (value ? Flags.LraCandidate : Flags.None);
        }
    }

    /// <summary>This is a reg-sized non-field-addressed struct.</summary>
    public bool lvRegStruct
    {
        readonly get
        {
            return (_flags & Flags.RegStruct) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.RegStruct) | (value ? Flags.RegStruct : Flags.None);
        }
    }

    /// <summary>lvClassHandle is the exact type</summary>
    public bool lvClassIsExact
    {
        readonly get
        {
            return (_flags & Flags.ClassIsExact) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.ClassIsExact) | (value ? Flags.ClassIsExact : Flags.None);
        }
    }

    /// <summary>true if there are non-IR references to this local (prolog, epilog, gc, eh)</summary>
    public bool lvImplicitlyReferenced
    {
        readonly get
        {
            return (_flags & Flags.ImplicitlyReferenced) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.ImplicitlyReferenced) | (value ? Flags.ImplicitlyReferenced : Flags.None);
        }
    }

    /// <summary>local needs zero init if we transform tail call to loop</summary>
    public bool lvSuppressedZeroInit
    {
        readonly get
        {
            return (_flags & Flags.SuppressedZeroInit) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.SuppressedZeroInit) | (value ? Flags.SuppressedZeroInit : Flags.None);
        }
    }

    /// <summary>The local is explicitly initialized and doesn't need zero initialization in the prolog.</summary>
    /// <remarks>If the local has gc pointers, there are no gc-safe points between the prolog and the explicit initialization.</remarks>
    public bool lvHasExplicitInit
    {
        readonly get
        {
            return (_flags & Flags.HasExplicitInit) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasExplicitInit) | (value ? Flags.HasExplicitInit : Flags.None);
        }
    }

    /// <summary>Root method local in an OSR method. Any stack home will be on the Tier0 frame.</summary>
    /// <remarks>Initial value will be defined by Tier0. Requires special handing in prolog.</remarks>
    public bool lvIsOSRLocal
    {
        readonly get
        {
            return (_flags & Flags.IsOsrLocal) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsOsrLocal) | (value ? Flags.IsOsrLocal : Flags.None);
        }
    }

    /// <summary>OSR local that was address exposed in Tier0</summary>
    public bool lvIsOSRExposedLocal
    {
        readonly get
        {
            return (_flags & Flags.IsOsrExposedLocal) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsOsrExposedLocal) | (value ? Flags.IsOsrExposedLocal : Flags.None);
        }
    }

    /// <summary>Local has redefinitions inside embedded statements that disqualify it from local copy prop.</summary>
    public bool lvRedefinedInEmbeddedStatement
    {
        readonly get
        {
            return (_flags & Flags.RedefinedInEmbeddedStatement) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.RedefinedInEmbeddedStatement) | (value ? Flags.RedefinedInEmbeddedStatement : Flags.None);
        }
    }

    /// <summary>Local is assigned exact class where : IEnumerable&lt;T&gt; via GDV</summary>
    public bool lvIsEnumerator
    {
        readonly get
        {
            return (_flags & Flags.IsEnumerator) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsEnumerator) | (value ? Flags.IsEnumerator : Flags.None);
        }
    }

    /// <summary>The local is a Span&lt;T&gt;</summary>
    private bool lvIsSpan
    {
        readonly get
        {
            return (_flags & Flags.IsSpan) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsSpan) | (value ? Flags.IsSpan : Flags.None);
        }
    }

    /// <summary>For pinned locals: true if all defs of this local are no-gc</summary>
    public bool lvAllDefsAreNoGc
    {
        readonly get
        {
            return (_flags & Flags.AllDefsAreNoGc) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.AllDefsAreNoGc) | (value ? Flags.AllDefsAreNoGc : Flags.None);
        }
    }

    /// <summary>Local is a stack allocated object (class, box, array, ...)</summary>
    public bool lvStackAllocatedObject
    {
        readonly get
        {
            return (_flags & Flags.StackAllocatedObject) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.StackAllocatedObject) | (value ? Flags.StackAllocatedObject : Flags.None);
        }
    }

#if TARGET_64BIT
    /// <summary>Quirk to allocate this LclVar as a 64-bit long</summary>
    public bool lvQuirkToLong
    {
        readonly get
        {
            return (_flags & Flags.QuirkToLong) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.QuirkToLong) | (value ? Flags.QuirkToLong : Flags.None);
        }
    }
#else
    /// <summary>Must we double align this struct?</summary>
    public bool lvStructDoubleAlign
    {
        readonly get
        {
            return (_flags & Flags.StructDoubleAlign) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.StructDoubleAlign) | (value ? Flags.StructDoubleAlign : Flags.None);
        }
    }
#endif

#if FEATURE_SIMD
    /// <summary>This tells lclvar is used for simd intrinsic</summary>
    public bool lvUsedInSimdIntrinsic
    {
        readonly get
        {
            return (_flags & Flags.UsedInSimdIntrinsic) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.UsedInSimdIntrinsic) | (value ? Flags.UsedInSimdIntrinsic : Flags.None);
        }
    }
#endif

#if FEATURE_IMPLICIT_BYREFS
    /// <summary>Set if the local appears as a last use that will be passed as an implicit byref.</summary>
    public bool lvIsLastUseCopyOmissionCandidate
    {
        readonly get
        {
            return (_flags & Flags.IsLastUseCopyOmissionCandidate) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsLastUseCopyOmissionCandidate) | (value ? Flags.IsLastUseCopyOmissionCandidate : Flags.None);
        }
    }
#endif

#if DEBUG
    /// <summary>Tracked but has no lvVarIndex (i.e. only valid GTF_VAR_DEATH flags, used by physical promotion)</summary>
    public bool lvTrackedWithoutIndex
    {
        readonly get
        {
            return (_debugFlags & DebugFlags.TrackedWithoutIndex) != 0;
        }

        set
        {
            _debugFlags = (_debugFlags & ~DebugFlags.TrackedWithoutIndex) | (value ? DebugFlags.TrackedWithoutIndex : DebugFlags.None);
        }
    }

    /// <summary>true if this var has updated class handle or exactness</summary>
    public bool lvClassInfoUpdated
    {
        readonly get
        {
            return (_debugFlags & DebugFlags.ClassInfoUpdated) != 0;
        }

        set
        {
            _debugFlags = (_debugFlags & ~DebugFlags.ClassInfoUpdated) | (value ? DebugFlags.ClassInfoUpdated : DebugFlags.None);
        }
    }

    /// <summary>CSE temp for a hoisted tree</summary>
    public bool lvIsHoist
    {
        readonly get
        {
            return (_debugFlags & DebugFlags.IsHoist) != 0;
        }

        set
        {
            _debugFlags = (_debugFlags & ~DebugFlags.IsHoist) | (value ? DebugFlags.IsHoist : DebugFlags.None);
        }
    }

    /// <summary>CSE temp for a multi-def CSE</summary>
    public bool lvIsMultiDefCSE
    {
        readonly get
        {
            return (_debugFlags & DebugFlags.IsMultiDefCse) != 0;
        }

        set
        {
            _debugFlags = (_debugFlags & ~DebugFlags.IsMultiDefCse) | (value ? DebugFlags.IsMultiDefCse : DebugFlags.None);
        }
    }

    /// <summary>Don't change the type of this variable</summary>
    public bool lvKeepType
    {
        readonly get
        {
            return (_debugFlags & DebugFlags.KeepType) != 0;
        }

        set
        {
            _debugFlags = (_debugFlags & ~DebugFlags.KeepType) | (value ? DebugFlags.KeepType : DebugFlags.None);
        }
    }

    /// <summary>Can't apply local field stress on this one</summary>
    public bool lvNoLclFldStress
    {
        readonly get
        {
            return (_debugFlags & DebugFlags.NoLclFldStress) != 0;
        }

        set
        {
            _debugFlags = (_debugFlags & ~DebugFlags.NoLclFldStress) | (value ? DebugFlags.NoLclFldStress : DebugFlags.None);
        }
    }

    /// <summary>True when this local may have LCL_ADDRs representing definitions</summary>
    public bool lvDefinedViaAddress
    {
        readonly get
        {
            return (_debugFlags & DebugFlags.DefinedViaAddress) != 0;
        }

        set
        {
            _debugFlags = (_debugFlags & ~DebugFlags.DefinedViaAddress) | (value ? DebugFlags.DefinedViaAddress : DebugFlags.None);
        }
    }

    // TODO-Cleanup: this flag is only in use by asserts that are checking for struct types, and is needed because of cases where TYP_STRUCT is bashed to an integral type. Consider cleaning this up so this workaround is not required.

    /// <summary>All references to this promoted struct are through its field locals.</summary>
    /// <remarks>
    ///   <para>I.e. there is no longer any reference to the struct directly.</para>
    ///   <para>In this case we can simply remove this struct local.</para>
    /// </remarks>
    public bool lvUnusedStruct
    {
        readonly get
        {
            return (_debugFlags & DebugFlags.UnusedStruct) != 0;
        }

        set
        {
            _debugFlags = (_debugFlags & ~DebugFlags.UnusedStruct) | (value ? DebugFlags.UnusedStruct : DebugFlags.None);
        }
    }

    /// <summary>The struct promotion was undone and hence there should be no reference to the fields of this struct.</summary>
    public bool lvUndoneStructPromotion
    {
        readonly get
        {
            return (_debugFlags & DebugFlags.UndoneStructPromotion) != 0;
        }

        set
        {
            _debugFlags = (_debugFlags & ~DebugFlags.UndoneStructPromotion) | (value ? DebugFlags.UndoneStructPromotion : DebugFlags.None);
        }
    }

    public readonly byte lvSingleDefDisqualifyReason => (byte)('H');
#endif

    /// <summary>The index of the local var representing the first field in the promoted struct local.</summary>
    /// <remarks>For implicit byref parameters, this gets hijacked between fgRetypeImplicitByRefArgs and fgMarkDemotedImplicitByRefArgs to point to the struct local created to model the parameter's struct promotion, if any.</remarks>
    public int lvFieldLclStart
    {
        readonly get
        {
            return _anonymous;
        }
        set
        {
            _anonymous = value;
        }
    }

    /// <summary>The index of the local var representing the parent (i.e. the promoted struct local).</summary>
    /// <remarks>Valid on promoted struct local fields.</remarks>
    public int lvParentLcl
    {
        readonly get
        {
            return _anonymous;
        }
        set
        {
            _anonymous = value;
        }
    }

    /// <summary>Number of fields in the promoted VarDsc.</summary>
    public byte lvFieldCnt
    {
        readonly get
        {
            return _fieldCnt;
        }

        set
        {
            _fieldCnt = value;
        }
    }

    public byte lvFldOffset
    {
        readonly get
        {
            return _fldOffset;
        }

        set
        {
            _fldOffset = value;
        }
    }

    public byte lvFldOrdinal
    {
        readonly get
        {
            return _fldOrdinal;
        }

        set
        {
            _fldOrdinal = value;
        }
    }

#if DEBUG
    public DoNotEnregisterReason DoNotEnregisterReason
    {
        readonly get
        {
            return _doNotEnregReason;
        }

        set
        {
            _doNotEnregReason = value;
        }
    }
#else
    public readonly DoNotEnregisterReason DoNotEnregisterReason => DoNotEnregisterReason.None;
#endif

#if DEBUG
    public readonly AddressExposedReason AddrExposedReason => _addrExposedReason;
#else
    public readonly AddressExposedReason AddrExposedReason => AddressExposedReason.NONE;
#endif

    public void SetAddressExposed(bool value, AddressExposedReason reason)
    {
        _addrExposed = value;

#if DEBUG
        _addrExposedReason = reason;
#endif
    }

    public void CleanAddressExposed()
    {
        _addrExposed = false;
    }

    public readonly bool IsAddressExposed => _addrExposed;

#if DEBUG
    public bool IsDefinedViaAddress
    {
        readonly get
        {
            return lvDefinedViaAddress;
        }

        set
        {
            lvDefinedViaAddress = value;
        }
    }
#endif

    public regNumber RegNum
    {
        readonly get
        {
            return _lvRegNum;
        }

        set
        {
            _lvRegNum = value;
        }
    }

#if TARGET_64BIT
    public readonly regNumber OtherReg
    {
        get
        {
            unreached();
            return REG_NA;
        }

        set
        {
            unreached();
        }
    }
#else
    public regNumber OtherReg
    {
        readonly get
        {
            return _lvOtherReg;
        }

        set
        {
            _lvOtherReg = value;
        }
    }
#endif

#if FEATURE_SIMD
    public readonly bool lvIsUsedInSimdIntrinsic => lvUsedInSimdIntrinsic;
#else
    public const bool lvIsUsedInSimdIntrinsic = false;
#endif

    public bool IsSpan
    {
        readonly get
        {
            return lvIsSpan;
        }

        set
        {
            lvIsSpan = value;
        }
    }

    public regNumber ArgInitReg
    {
        readonly get
        {
            return _lvArgInitReg;
        }

        set
        {
            _lvArgInitReg = value;
        }
    }

    public readonly bool lvIsRegCandidate => lvLRACandidate;

    public readonly bool lvIsInReg => lvIsRegCandidate && (_lvRegNum != REG_STK);

#if HAS_FIXED_REGISTER_SET
    public readonly regMask lvRegMask
    {
        get
        {
            if (_lvRegNum != REG_STK)
            {
                return (regMask)(1L << (int)(_lvRegNum));
            }
            else
            {
                return SRBM_NONE;
            }
        }
    }
#endif

    /// <summary>Get a bitset of flags that represents all fields dying.</summary>
    /// <remarks>Only usable for promoted locals.</remarks>
    public readonly GenTreeFlags AllFieldDeathFlags
    {
        get
        {
            assert(lvPromoted && (lvFieldCnt is > 0 and <= 4));
            var flags = (GenTreeFlags)(((1 << lvFieldCnt) - 1) << FIELD_LAST_USE_SHIFT);

            assert((flags & ~GTF_VAR_DEATH_MASK) == 0);
            return flags;
        }
    }

    /// <summary>Get a bitset of flags that represents this local fully dying.</summary>
    public readonly GenTreeFlags FullDeathFlags => lvPromoted? AllFieldDeathFlags : GTF_VAR_DEATH;

    /// <summary>access reference count for this local var</summary>
    /// <param name="state">the requestor's expected ref count state; defaults to RCS_NORMAL</param>
    /// <returns>Ref count for the local.</returns>
    public readonly ushort lvRefCnt(RefCountState state = RCS_NORMAL)
    {
#if DEBUG
        assert(state != RCS_INVALID);

        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        assert(compiler.lvaRefCountState == state);
#endif

        var refCnt = _lvRefCnt;

        if (lvImplicitlyReferenced && (refCnt == 0))
        {
            return 1;
        }
        return refCnt;
    }

    /// <summary>increment reference count for this local var</summary>
    /// <param name="delta">the amount of the increment</param>
    /// <param name="state">the requestor's expected ref count state; defaults to RCS_NORMAL</param>
    /// <remarks>It is currently the caller's responsibility to ensure this increment will not cause overflow.</remarks>
    public void incLvRefCnt(ushort delta, RefCountState state = RCS_NORMAL)
        => setLvRefCnt((ushort)(_lvRefCnt + delta), state);

    /// <summary>set the reference count for this local var</summary>
    /// <param name="newValue">the desired new reference count</param>
    /// <param name="state">the requestor's expected ref count state; defaults to RCS_NORMAL</param>
    /// <remarks>
    ///   <para>Generally after calling v->setLvRefCnt(Y), v->lvRefCnt() == Y.</para>
    ///   <para>However this may not be true when v->lvImplicitlyReferenced == 1.</para>
    /// </remarks>
    public void setLvRefCnt(ushort newValue, RefCountState state = RCS_NORMAL)
    {
#if DEBUG
        assert(state != RCS_INVALID);

        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        assert(compiler.lvaRefCountState == state);
#endif

        _lvRefCnt = newValue;
    }

    /// <summary>increment reference count for this local var (with saturating semantics)</summary>
    /// <param name="delta">the amount of the increment</param>
    /// <param name="state">the requestor's expected ref count state; defaults to RCS_NORMAL</param>
    public void incLvRefCntSaturating(ushort delta, RefCountState state = RCS_NORMAL)
        => setLvRefCnt((ushort)(int.Min(_lvRefCnt + delta, ushort.MaxValue)), state);

    /// <summary>access weighted reference count for this local var</summary>
    /// <param name="state">the requestor's expected ref count state; defaults to RCS_NORMAL</param>
    /// <returns>Weighted ref count for the local.</returns>
    public readonly weight_t lvRefCntWtd(RefCountState state = RCS_NORMAL)
    {
#if DEBUG
        assert(state != RCS_INVALID);

        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        assert(compiler.lvaRefCountState == state);
#endif

        if (lvImplicitlyReferenced && (_lvRefCntWtd == 0))
        {
            return BB_UNITY_WEIGHT;
        }
        return _lvRefCntWtd;
    }

    /// <summary>increment weighted reference count for this local var</summary>
    /// <param name="delta">the amount of the increment</param>
    /// <param name="state">the requestor's expected ref count state; defaults to RCS_NORMAL</param>
    /// <remarks>It is currently the caller's responsibility to ensure this increment will not cause overflow.</remarks>
    public void incLvRefCntWtd(weight_t delta, RefCountState state = RCS_NORMAL)
        => setLvRefCntWtd(_lvRefCntWtd + delta, state);

    /// <summary>set the weighted reference count for this local var</summary>
    /// <param name="newValue">the desired new weighted reference count</param>
    /// <param name="state">the requestor's expected ref count state; defaults to RCS_NORMAL</param>
    /// <remarks>
    ///   <para>Generally after calling v->setLvRefCntWtd(Y), v->lvRefCntWtd() == Y.</para>
    ///   <para>However this may not be true when v->lvImplicitlyReferenced == 1.</para>
    /// </remarks>
    public void setLvRefCntWtd(weight_t newValue, RefCountState state = RCS_NORMAL)
    {
#if DEBUG
        assert(state != RCS_INVALID);

        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        assert(compiler.lvaRefCountState == state);
#endif

        _lvRefCntWtd = newValue;
    }

    public int StackOffset
    {
        readonly get
        {
            assert(Debugger.IsAttached || lvValueSize.IsExact);
            return lvStkOffs;
        }

        set
        {
            assert(lvValueSize.IsExact);
            lvStkOffs = value;
        }
    }

    /// <summary>This is only used for locals that have an unknown size, such as TYP_SIMD/TYP_MASK on Arm64. These locals do not have an absolute stack offset.</summary>
    public int UnknownSizeFrameIndex
    {
        readonly get
        {
            assert(Debugger.IsAttached || !lvValueSize.IsExact);
            return lvStkOffs;
        }

        set
        {
            assert(!lvValueSize.IsExact);
            lvStkOffs = value;
        }
    }

    /// <summary>Get the exact size of the type of this local.</summary>
    public readonly int lvExactSize
    {
        get
        {
            assert(!varTypeHasUnknownSize(Type));
            return lvValueSize.ExactSize;
        }
    }

    /// <summary>Get the value size of the type of this local.</summary>
    public readonly ValueSize lvValueSize
    {
        get
        {
            if (Type is TYP_STRUCT)
            {
                assert(_layout is not null);
                return new ValueSize(_layout.Size); 
            }
            else
            {
                return ValueSize.FromJitType(Type);
            }
        }
    }

    public var_types Type
    {
        readonly get
        {
            return (var_types)(_bitfield & 0x1F);
        }

        set
        {
            _bitfield = (byte)((_bitfield & ~0x1F) | ((byte)(value) & 0x1F));
        }
    }

    // NormalizeOnLoad Rules:
    //   1. All small locals are actually TYP_INT locals.
    //   2. NOL locals are such that not all definitions can be controlled by the compiler and so the upper bits can
    //      be undefined.For parameters this is the case because of ABI.For struct fields - because of padding.For
    //      address - exposed locals - because not all stores are direct.
    //   3. Hence, all NOL uses(unless proven otherwise) are assumed in morph to have undefined upper bits and
    //      explicit casts have be inserted to "normalize" them back to conform to IL semantics.
    // OSR exposed locals were normalize on load in the Tier0 frame so must be so for OSR too.
    public readonly bool lvNormalizeOnLoad => varTypeIsSmall(Type) && (lvIsParam || _addrExposed || lvIsStructField || lvIsOSRExposedLocal);

    // OSR exposed locals were normalize on load in the Tier0 frame so must be so for OSR too.
    public readonly bool lvNormalizeOnStore => varTypeIsSmall(Type) && !(lvIsParam || _addrExposed || lvIsStructField || lvIsOSRExposedLocal);

    public void incRefCnts(weight_t weight, Compiler compiler, RefCountState state = RCS_NORMAL, bool propagate = true)
    {
        // In minopts and debug codegen, we don't maintain normal ref counts.
        if ((state == RCS_NORMAL) && !compiler.PreciseRefCountsRequired)
        {
            // Note, at least, that there is at least one reference.
            lvImplicitlyReferenced = true;
            return;
        }

        var promotionType = Compiler.PROMOTION_TYPE_NONE;

        if (varTypeIsPromotable(Type))
        {
            promotionType = compiler.lvaGetPromotionType(this);
        }

        // Increment counts on the local itself.
        if ((Type is not TYP_STRUCT) || (promotionType is not Compiler.PROMOTION_TYPE_INDEPENDENT))
        {
            // We increment ref counts of this local for primitive types, including structs that have been retyped as their
            // only field, as well as for structs whose fields are not independently promoted.

            // Increment lvRefCnt
            var newRefCnt = lvRefCnt(state) + 1;

            if (newRefCnt == (ushort)(newRefCnt)) // lvRefCnt is an "unsigned short". Don't overflow it.
            {
                setLvRefCnt((ushort)(newRefCnt), state);
            }

            // Increment lvRefCntWtd
            if (weight != 0)
            {
                // We double the weight of internal temps

                var doubleWeight = lvIsTemp;

#if FEATURE_IMPLICIT_BYREFS
                // and, for the time being, implicit byref params
                doubleWeight |= IsImplicitByRef;
#endif

                if (doubleWeight && (weight * 2 > weight))
                {
                    weight *= 2;
                }

                var newWeight = lvRefCntWtd(state) + weight;
                assert(newWeight >= lvRefCntWtd(state));
                setLvRefCntWtd(newWeight, state);
            }
        }

        if (varTypeIsPromotable(Type) && propagate)
        {
            // For promoted struct locals, increment lvRefCnt on its field locals as well.
            if (promotionType is Compiler.PROMOTION_TYPE_INDEPENDENT or Compiler.PROMOTION_TYPE_DEPENDENT)
            {
                for (var i = lvFieldLclStart; i < lvFieldLclStart + lvFieldCnt; ++i)
                {
                    // Don't propagate
                    compiler.lvaTable[i].incRefCnts(weight, compiler, state, false);
                }
            }
        }

        if (lvIsStructField && propagate)
        {
            // Depending on the promotion type, increment the ref count for the parent struct as well.
            promotionType = compiler.lvaGetParentPromotionType(this);

            ref var parentvarDsc = ref compiler.lvaGetDesc(lvParentLcl);
            assert(!parentvarDsc.lvRegStruct);

            if (promotionType is Compiler.PROMOTION_TYPE_DEPENDENT)
            {
                // Don't propagate
                parentvarDsc.incRefCnts(weight, compiler, state, false);
            }
        }

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"New refCnts for V{compiler.lvaGetLclNum(this):D2}: refCnt = {lvRefCnt(state),2}, refCntWtd = {refCntWtd2str(lvRefCntWtd(state))}\n");
        }
#endif
    }

    /// <summary>Returns true if this variable contains GC pointers (including being a GC pointer itself).</summary>
    public readonly bool HasGCPtr
    {
        get
        {
            var result = varTypeIsGC(Type);

            if (!result && (Type is TYP_STRUCT))
            {
                assert(_layout is not null);
                result = _layout.HasGCPtr;
            }

            return result;
        }
    }

    // Change the layout to one that may not be compatible.
    public void ChangeLayout(ClassLayout layout)
    {
        assert(varTypeIsStruct(Type));
        _layout = layout;
    }

    // Grow the size of a block layout local.
    public void GrowBlockLayout(ClassLayout layout)
    {
        assert(varTypeIsStruct(Type));
        assert((_layout is null) || (_layout.IsBlockLayout && (_layout.Size <= layout.Size)));
        assert(layout.IsBlockLayout);
        _layout = layout;
    }

    public SsaDefArray<LclSsaVarDsc> lvPerSsaData;

    // True if ssaNum is a viable ssaNum for this local.
    public readonly bool IsValidSsaNum(int ssaNum) => lvPerSsaData.IsValidSsaNum(ssaNum);

    // Returns the address of the per-Ssa data for the given ssaNum (which is required
    // not to be the SsaConfig::RESERVED_SSA_NUM, which indicates that the variable is
    // not an SSA variable).
    public readonly ref LclSsaVarDsc GetPerSsaData(int ssaNum) => ref lvPerSsaData.GetSsaDef(ssaNum);

    // Returns the SSA number for "ssaDef". Requires "ssaDef" to be a valid definition of this variable.
    public readonly int GetSsaNumForSsaDef(in LclSsaVarDsc ssaDef) => lvPerSsaData.GetSsaNum(ssaDef);

    /// <summary>Determine register type for this local var.</summary>
    /// <param name="tree">node that uses the local, its type is checked first.</param>
    /// <returns>TYP_UNDEF if the layout is not enregistrable, the register type otherwise.</returns>
    public readonly var_types GetRegisterType(GenTreeLclVarCommon tree)
    {
        var targetType = tree.Type;

        if (targetType is TYP_STRUCT)
        {
            ClassLayout? layout;

            if (tree.Oper is GT_LCL_FLD or GT_STORE_LCL_FLD)
            {
                layout = tree.AsLclFld().Layout;
            }
            else
            {
                assert((Type is TYP_STRUCT) && (tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR));
                layout = _layout;
            }

            assert(layout is not null);
            targetType = layout.RegisterType;
        }

#if DEBUG
        if ((targetType is not TYP_UNDEF) && (tree.Oper is GT_STORE_LCL_VAR) && lvNormalizeOnStore)
        {
            // Ensure that the lclVar node is typed correctly, does not apply to phi-stores because they do not produce code in the merge block.
            assert(!tree.AsUnOp().Op1.Oper.IsNonPhiLocal || (targetType == Type.ActualType));
        }
#endif

        return targetType;
    }

    /// <summary>Determine register type for this local var.</summary>
    /// <returns>TYP_UNDEF if the layout is not enregistrable, the register type otherwise.</returns>
    public readonly var_types GetRegisterType()
    {
        var type = Type;

        if (type is not TYP_STRUCT)
        {
#if LOWER_DECOMPOSE_LONGS
        if (type is TYP_LONG)
        {
            return TYP_UNDEF;
        }
#endif
            return type;
        }

        assert(_layout is not null);
        return _layout.RegisterType;
    }

    /// <summary>Get the canonical type of the stack slot that this enregistrable local is using when stored on the stack.</summary>
    /// <returns>TYP_UNDEF if the layout is not enregistrable. Otherwise returns the type of the stack slot home for the local.</returns>
    /// <remarks>
    ///   <para>This function always returns a canonical type for all 4-byte types (structs, floats, ints) it will return TYP_INT.</para>
    ///   <para>It is meant to be used when moving locals between register and stack.</para>
    ///   <para>Because of this the returned type is usually at least one 4-byte stack slot.</para>
    ///   <para>However, there are certain exceptions for promoted fields in OSR methods (that may refer back to the original frame) and due to Apple arm64 where subsequent small parameters can be packed into the same stack slot.</para>
    /// </remarks>
    public readonly var_types GetStackSlotHomeType()
    {
        if (varTypeIsSmall(Type))
        {
            if (compAppleArm64Abi() && lvIsParam && !lvIsRegArg)
            {
                // Allocated by caller and potentially only takes up a small slot
                return GetRegisterType();
            }

            if (lvIsOSRLocal && lvIsStructField)
            {
#if TARGET_X86
                // Revisit when we support OSR on x86
                unreached();
                return TYP_UNDEF;
#else
                return GetRegisterType();
#endif
            }
        }

        return GetRegisterType().ActualType;
    }

    public readonly bool IsEnregisterableType => GetRegisterType() is not TYP_UNDEF;

    public readonly bool IsEnregisterableLcl => !lvDoNotEnregister && IsEnregisterableType;

    public readonly bool IsLiveInOutOfHandler
    {
        get
        {
            assert(Debugger.IsAttached || lvTracked);
            return _lvLiveInOutOfHandler;
        }
    }

    /// <summary>Determines if this variable's value is always up-to-date on stack.</summary>
    /// <remarks>This is possible if this is an EH-var or we decided to spill after single-def.</remarks>
    public readonly bool IsAlwaysAliveInMemory => IsLiveInOutOfHandler || lvSpillAtSingleDef;

    /// <summary>check if a whole struct reference could be replaced by a field.</summary>
    /// <param name="compiler">the compiler instance</param>
    /// <returns>true if that can be replaced, false otherwise.</returns>
    /// <remarks>The replacement can be made only for independently promoted structs with 1 field without holes.</remarks>
    public readonly bool CanBeReplacedWithItsField(Compiler compiler)
    {
        if (!lvPromoted)
        {
            return false;
        }
        else if (compiler.lvaGetPromotionType(this) != Compiler.PROMOTION_TYPE_INDEPENDENT)
        {
            return false;
        }
        else if (lvFieldCnt != 1)
        {
            return false;
        }
        else if (lvContainsHoles)
        {
            return false;
        }

#if FEATURE_SIMD
        // If we return `struct A { simd16 a; }` we split the struct into several fields.
        // In order to do that we have to have its field `a` in memory. Right now lowering cannot
        // handle RETURN struct(multiple registers)->simd16(one register), but it can be improved.
        ref var fieldDsc = ref compiler.lvaGetDesc(lvFieldLclStart);

        if (varTypeIsSimd(fieldDsc.Type))
        {
            return false;
        }
#endif

        return true;
    }

#if DEBUG
    public string lvReason;

    public readonly void PrintVarReg()
    {
        jitprintf($"{_lvRegNum.Name}");
    }
#endif
}
