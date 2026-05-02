// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial struct LclVarDsc
{
    private Flags _flags;

    /// <summary>weighted reference count</summary>
    private weight_t _lvRefCntWtd;

    /// <summary>class handle for the local or null if not known or not a class</summary>
    private unsafe CORINFO_CLASS_HANDLE lvClassHnd;

    /// <summary>layout info for structs</summary>
    private ClassLayout? _layout;

    private _Anonymous_e__Union _anonymous;

    /// <summary>stack offset of home in bytes.</summary>
    private int lvStkOffs;

    /// <summary>original slot # (if remapped)</summary>
    private uint lvSlotNum;

#if DEBUG
    private DebugFlags _debugFlags;
#endif

    /// <summary>variable tracking index</summary>
    private ushort _varIndex;

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

#if !TARGET_64BIT
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

    /// <summary>Gets or set the layout of a struct variable or implicit byref.</summary>
    public ClassLayout? Layout
    {
        readonly get
        {
#if FEATURE_IMPLICIT_BYREFS
            assert(Debugger.IsAttached || varTypeIsStruct(lvType) || (IsImplicitByRef && (lvType is TYP_BYREF)));
#else
            assert(varTypeIsStruct(lvType));
#endif
            return _layout;
        }

        set
        {
            assert(varTypeIsStruct(lvType));
            assert((_layout is null) || ClassLayout.AreCompatible(_layout, value));
            _layout = value;
        }
    }

    // TYP_INT/LONG/FLOAT/DOUBLE/REF
    public var_types lvType
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

#if !FEATURE_HFA_FIELDS_PRESENT
    /// <summary>What kind of an HFA this is (CORINFO_HFA_ELEM_NONE if it is not an HFA).</summary>
    public CorInfoHFAElemType _lvHfaElemKind
    {
        readonly get
        {
            return (CorInfoHFAElemType)((_bitfield >>> 5) & 0x07u);
        }

        set
        {
            _bitfield = (byte)((_bitfield & ~(0x07u << 5)) | (((byte)(value) & 0x07u) << 5));
        }
    }
#endif

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

    public readonly bool lvTrackedNonStruct => lvTracked && (lvType is not TYP_STRUCT);

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
    private bool m_addrExposed
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

    /// <summary>The variable is live in or out of an exception handler, and therefore must be on the stack (at least at those boundaries.)</summary>
    public bool lvLiveInOutOfHndlr
    {
        readonly get
        {
            return (_flags & Flags.LiveInOutOfHndlr) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.LiveInOutOfHndlr) | (value ? Flags.LiveInOutOfHndlr : Flags.None);
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
    public bool lvUsedInSIMDIntrinsic
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

    public uint lvFieldLclStart
    {
        readonly get
        {
            return _anonymous.lvFieldLclStart;
        }
        set
        {
            _anonymous.lvFieldLclStart = value;
        }
    }

    public uint lvParentLcl
    {
        readonly get
        {
            return _anonymous.lvParentLcl;
        }
        set
        {
            _anonymous.lvParentLcl = value;
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

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous_e__Union
    {
        /// <summary>The index of the local var representing the first field in the promoted struct local.</summary>
        /// <remarks>For implicit byref parameters, this gets hijacked between fgRetypeImplicitByRefArgs and fgMarkDemotedImplicitByRefArgs to point to the struct local created to model the parameter's struct promotion, if any.</remarks>
        [FieldOffset(0)]
        public uint lvFieldLclStart;

        /// <summary>The index of the local var representing the parent (i.e. the promoted struct local).</summary>
        /// <remarks>Valid on promoted struct local fields.</remarks>
        [FieldOffset(0)]
        public uint lvParentLcl;
    }
}
