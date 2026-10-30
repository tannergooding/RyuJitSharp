// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

internal enum FatalJitArgument
{
    Base,
    Buffer,
    Bytes,
    ExpectedAttributes,
    Field,
    Id,
    Index,
    Key,
    Oper,
    Ordinal,
    PCallSites,
    PCallSiteSizes,
    PCorJitInfo,
    PMethodInfo,
    Precision,
    Status,
    StructLayout,
    TableIndex,
    Count,
}

/// <summary>Identifies a JIT failure; its numeric value is the stable default RJS message ID.</summary>
public enum FatalJitErrorCode
{
    /// <summary>The failure has no specific stable identifier.</summary>
    Unspecified = 0,

    /// <summary>The jump kind is outside the valid range.</summary>
    InvalidJumpKind = 1,

    /// <summary>The jump kind has no reverse mapping for the current target.</summary>
    MissingReverseJumpMapping = 2,

    /// <summary>The jump kind is not supported by the current target.</summary>
    UnsupportedJumpKind = 3,

    /// <summary>No write-barrier helper is defined for the requested form.</summary>
    UnsupportedWriteBarrierForm = 4,

    /// <summary>The data section has an unexpected type.</summary>
    UnexpectedDataSectionType = 5,

    /// <summary>The data section has an unexpected element size.</summary>
    UnexpectedDataSectionElementSize = 6,

    /// <summary>The initialization block kind is unsupported by xarch register allocation.</summary>
    UnsupportedInitializationBlockKind = 7,

    /// <summary>The copy block kind is unsupported by xarch register allocation.</summary>
    UnsupportedCopyBlockKind = 8,

    /// <summary>The stack argument kind is unsupported by xarch register allocation.</summary>
    UnsupportedStackArgumentKind = 9,

    /// <summary>A bound jump has no target group.</summary>
    BoundJumpMissingTarget = 10,

    /// <summary>A gather instruction has an unsupported stack format.</summary>
    GatherWithStackMemoryFormat = 11,

    /// <summary>A gather instruction has an unsupported static-memory format.</summary>
    GatherWithStaticMemoryFormat = 12,

    /// <summary>A jump target has no emitter label cookie.</summary>
    JumpTargetMissingEmitterLabelCookie = 13,

    /// <summary>A jump's instruction group is absent from the group list.</summary>
    JumpInstructionGroupMissingFromList = 14,

    /// <summary>A label instruction has no bound target group.</summary>
    LabelInstructionMissingBoundTargetGroup = 15,

    /// <summary>A relative-label table entry has no block.</summary>
    RelativeLabelTableMissingBlock = 16,

    /// <summary>A relative-label table entry has no emitted block label.</summary>
    RelativeLabelTableMissingEmittedBlockLabel = 17,

    /// <summary>A relative-label table has no first basic block.</summary>
    RelativeLabelTableMissingFirstBasicBlock = 18,

    /// <summary>A relative-label table has no first emitted label.</summary>
    RelativeLabelTableMissingFirstEmittedLabel = 19,

    /// <summary>A removable jump has no resolved basic-block label cookie.</summary>
    UnresolvedRemovableJumpTarget = 20,

    /// <summary>A saved jump has no instruction group.</summary>
    SavedJumpMissingInstructionGroup = 21,

    /// <summary>A saved jump requires an instruction group.</summary>
    SavedJumpRequiresInstructionGroup = 22,

    /// <summary>An address-mode display has no active compiler.</summary>
    AddressModeDisplayMissingCompiler = 23,

    /// <summary>An absolute-label table entry has no block.</summary>
    AbsoluteLabelTableMissingBlock = 24,

    /// <summary>An absolute-label table entry has no emitted block label.</summary>
    AbsoluteLabelTableMissingEmittedBlockLabel = 25,

    /// <summary>An alignment descriptor has no containing group.</summary>
    AlignmentDescriptorMissingContainingGroup = 26,

    /// <summary>An alignment descriptor has no loop-head group.</summary>
    AlignmentDescriptorMissingLoopHeadGroup = 27,

    /// <summary>An alignment descriptor has no loop-head predecessor.</summary>
    AlignmentDescriptorMissingLoopHeadPredecessor = 28,

    /// <summary>Stack output has no initialized compiler.</summary>
    StackOutputMissingCompiler = 29,

    /// <summary>An unbound jump has no basic-block target.</summary>
    UnboundJumpMissingBasicBlockTarget = 30,

    /// <summary>Asynchronous diagnostics have no valid code location.</summary>
    AsyncDiagnosticsMissingCodeLocation = 31,

    /// <summary>A bound label descriptor has no target group.</summary>
    BoundLabelDescriptorMissingTargetGroup = 32,

    /// <summary>Code-size computation has no active compiler.</summary>
    CodeSizeComputationMissingCompiler = 33,

    /// <summary>Data-section diagnostics have no active compiler.</summary>
    DataSectionDiagnosticsMissingCompiler = 34,

    /// <summary>Data-section output has no allocated chunks or nonempty sections.</summary>
    DataSectionOutputMissingChunksOrSections = 35,

    /// <summary>Data-section output has no active compiler.</summary>
    DataSectionOutputMissingCompiler = 36,

    /// <summary>Final instruction emission has no active compiler.</summary>
    FinalInstructionEmissionMissingCompiler = 37,

    /// <summary>Frame-reference display has no active compiler.</summary>
    FrameReferenceDisplayMissingCompiler = 38,

    /// <summary>Handle display has no active compiler.</summary>
    HandleDisplayMissingCompiler = 39,

    /// <summary>Instruction display has no active compiler.</summary>
    InstructionDisplayMissingCompiler = 40,

    /// <summary>Instruction-group label formatting has no active compiler.</summary>
    InstructionGroupLabelsMissingCompiler = 41,

    /// <summary>Instruction issue has no active compiler.</summary>
    InstructionIssueMissingCompiler = 42,

    /// <summary>An EVEX rounding mode is invalid.</summary>
    InvalidEvexRoundingMode = 43,

    /// <summary>A mixed-memory tuple has an invalid instruction format.</summary>
    InvalidMixedMemoryTupleFormat = 44,

    /// <summary>An instruction tuple type is invalid.</summary>
    InvalidInstructionTupleType = 45,

    /// <summary>A move instruction is invalid for side-effect classification.</summary>
    InvalidMoveSideEffectClassification = 46,

    /// <summary>An operand register is invalid for display.</summary>
    InvalidDisplayOperandRegister = 47,

    /// <summary>Label output received a non-jump descriptor.</summary>
    LabelOutputRequiresJumpDescriptor = 48,

    /// <summary>Label output has no active compiler.</summary>
    LabelOutputMissingCompiler = 49,

    /// <summary>A label-table diagnostic entry has no block.</summary>
    LabelTableDiagnosticsMissingBlock = 50,

    /// <summary>A label-table diagnostic entry has no emitted block label.</summary>
    LabelTableDiagnosticsMissingEmittedBlockLabel = 51,

    /// <summary>Label-table diagnostics have no first basic block.</summary>
    LabelTableDiagnosticsMissingFirstBasicBlock = 52,

    /// <summary>Label-table diagnostics have no first emitted label.</summary>
    LabelTableDiagnosticsMissingFirstEmittedLabel = 53,

    /// <summary>Local-address emission has no active compiler.</summary>
    LocalAddressEmissionMissingCompiler = 54,

    /// <summary>Loop alignment has no active compiler.</summary>
    LoopAlignmentMissingCompiler = 55,

    /// <summary>Loop alignment has no alignment descriptor.</summary>
    LoopAlignmentMissingDescriptor = 56,

    /// <summary>Loop-alignment adjustment has no active compiler.</summary>
    LoopAlignmentAdjustmentMissingCompiler = 57,

    /// <summary>Loop-alignment padding has no active compiler.</summary>
    LoopAlignmentPaddingMissingCompiler = 58,

    /// <summary>Loop-size calculation has no active compiler.</summary>
    LoopSizeCalculationMissingCompiler = 59,

    /// <summary>A move-label operation has no label descriptor.</summary>
    MoveLabelMissingDescriptor = 60,

    /// <summary>Static-reference display has no active compiler.</summary>
    StaticReferenceDisplayMissingCompiler = 61,

    /// <summary>An unbound label descriptor has no target block.</summary>
    UnboundLabelDescriptorMissingTargetBlock = 62,

    /// <summary>An APX immediate instruction format is unexpected.</summary>
    UnexpectedApxImmediateInstructionFormat = 63,

    /// <summary>An APX immediate operand size is unexpected.</summary>
    UnexpectedApxImmediateOperandSize = 64,

    /// <summary>An immediate-only instruction is unexpected.</summary>
    UnexpectedImmediateOnlyInstruction = 65,

    /// <summary>A memory operand format is unexpected.</summary>
    UnexpectedMemoryOperandFormat = 66,

    /// <summary>A memory operand tuple type is unexpected.</summary>
    UnexpectedMemoryOperandTupleType = 67,

    /// <summary>A stack immediate size is unexpected.</summary>
    UnexpectedStackImmediateSize = 68,

    /// <summary>A stack operand size is unexpected.</summary>
    UnexpectedStackOperandSize = 69,

    /// <summary>A static immediate size is unexpected.</summary>
    UnexpectedStaticImmediateSize = 70,

    /// <summary>A static operand size is unexpected.</summary>
    UnexpectedStaticOperandSize = 71,

    /// <summary>A three-register stack-store format is unexpected.</summary>
    UnexpectedThreeRegisterStackStoreFormat = 72,

    /// <summary>A three-register static-store format is unexpected.</summary>
    UnexpectedThreeRegisterStaticStoreFormat = 73,

    /// <summary>An instruction-output format is unknown.</summary>
    UnknownInstructionOutputFormat = 74,

    /// <summary>An REX.WX instruction is unsupported.</summary>
    UnsupportedRexWxInstruction = 75,

    /// <summary>Emitter initialization occurred before code generation began.</summary>
    EmitterInitializationBeforeCodegenStart = 76,

    /// <summary>Stable direct-failure message identifier 77.</summary>
    InvalidABIClassifierKind = 77,
    /// <summary>Stable direct-failure message identifier 78.</summary>
    BlockStatementMustHaveACloneableRoot = 78,
    /// <summary>Stable direct-failure message identifier 79.</summary>
    EHToVMClauseOrderHasNotBeenInitialized = 79,
    /// <summary>Stable direct-failure message identifier 80.</summary>
    EHTryEndHasNoSuccessor = 80,
    /// <summary>Stable direct-failure message identifier 81.</summary>
    EHHandlerEndHasNoSuccessor = 81,
    /// <summary>Stable direct-failure message identifier 82.</summary>
    EHFilterHasNoStartBlock = 82,
    /// <summary>Stable direct-failure message identifier 83.</summary>
    VMToEHClauseOrderHasNotBeenInitialized = 83,
    /// <summary>Stable direct-failure message identifier 84.</summary>
    UnknownEHHandlerType = 84,
    /// <summary>Stable direct-failure message identifier 85.</summary>
    IPMappingHasNoPredecessorAtADuplicateNativeOffset = 85,
    /// <summary>Stable direct-failure message identifier 86.</summary>
    MultiRegisterReturnsIntoSIMDRegistersAreUnsupportedOnWindowsAMD64 = 86,
    /// <summary>Stable direct-failure message identifier 87.</summary>
    Failure32BitLongShiftReachedA64BitTarget = 87,
    /// <summary>Stable direct-failure message identifier 88.</summary>
    SwiftErrorReturnsAreUnavailableForThisTarget = 88,
    /// <summary>Stable direct-failure message identifier 89.</summary>
    X86LongMultiplyNodeReachedAnotherTarget = 89,
    /// <summary>Stable direct-failure message identifier 90.</summary>
    SwiftErrorValuesAreUnavailableForThisTarget = 90,
    /// <summary>Stable direct-failure message identifier 91.</summary>
    Failure32BitLongNodeReachedA64BitTarget = 91,
    /// <summary>Stable direct-failure message identifier 92.</summary>
    InsertionIntoAHomedParameterRegisterIsNotSupportedOnThisTarget = 92,
    /// <summary>Stable direct-failure message identifier 93.</summary>
    PushStructArgumentsAreOnlySupportedOnX86 = 93,
    /// <summary>Stable direct-failure message identifier 94.</summary>
    FixedRegisterTableIsNotInitialized = 94,
    /// <summary>Stable direct-failure message identifier 95.</summary>
    UnexpectedBlockKindAtTheEndOfCodeGeneration = 95,
    /// <summary>Stable direct-failure message identifier 96.</summary>
    InvalidScalarMemmoveWidth = 96,
    /// <summary>Stable direct-failure message identifier 97.</summary>
    MissingJMPEpilogNode = 97,
    /// <summary>Stable direct-failure message identifier 98.</summary>
    InvalidShiftOperator = 98,
    /// <summary>Stable direct-failure message identifier 99.</summary>
    MDArrayTemporaryMustBeAnIntOrObjectReference = 99,
    /// <summary>Stable direct-failure message identifier 100.</summary>
    BoundsCheckCoalescingRequiresValueNumbering = 100,
    /// <summary>Stable direct-failure message identifier 101.</summary>
    UnexpectedBlockKindForCodeSizeEstimation = 101,
    /// <summary>Stable direct-failure message identifier 102.</summary>
    InvalidGPRSize = 102,
    /// <summary>Stable direct-failure message identifier 103.</summary>
    CloningATryRegionRequiresABlockMap = 103,
    /// <summary>Stable direct-failure message identifier 104.</summary>
    CloningATryRegionRequiresAnInsertionPoint = 104,
    /// <summary>Stable direct-failure message identifier 105.</summary>
    CallfinallyContinuationRequiresAPrecedingBlock = 105,
    /// <summary>Stable direct-failure message identifier 106.</summary>
    CloningATryRegionRequiresItsBlocks = 106,
    /// <summary>Stable direct-failure message identifier 107.</summary>
    FinallyReturnRequiresItsSuccessorTable = 107,
    /// <summary>Stable direct-failure message identifier 108.</summary>
    ClonedBlockRequiresItsBlockMapEntry = 108,
    /// <summary>Stable direct-failure message identifier 109.</summary>
    ForwardSubstitutionBlockHasABrokenStatementList = 109,
    /// <summary>Stable direct-failure message identifier 110.</summary>
    NonFinalStatementMustHaveASuccessor = 110,
    /// <summary>Stable direct-failure message identifier 111.</summary>
    InsertedLocalSegmentIsNotConnected = 111,
    /// <summary>Stable direct-failure message identifier 112.</summary>
    CollectedForwardSubstitutionUseLostItsOwner = 112,
    /// <summary>Stable direct-failure message identifier 113.</summary>
    CheapForwardSubstitutionAddressCouldNotBeCloned = 113,
    /// <summary>Stable direct-failure message identifier 114.</summary>
    FinalForwardSubstitutionUseLostItsOwner = 114,
    /// <summary>Stable direct-failure message identifier 115.</summary>
    ForwardSubstitutionCandidateNeedsAFollowingStatement = 115,
    /// <summary>Stable direct-failure message identifier 116.</summary>
    LastForwardSubstitutionUseLostItsOwner = 116,
    /// <summary>Stable direct-failure message identifier 117.</summary>
    ForwardedStoreMustHaveALocalsList = 117,
    /// <summary>Stable direct-failure message identifier 118.</summary>
    SWITCHShouldBeACallRatherThanAnInlinedPoll = 118,
    /// <summary>Stable direct-failure message identifier 119.</summary>
    UnknownBlockTypeForUpdatingPredecessorLists = 119,
    /// <summary>Stable direct-failure message identifier 120.</summary>
    NonFirstStatementNeedsAPredecessor = 120,
    /// <summary>Stable direct-failure message identifier 121.</summary>
    UnexpectedScalarComparison = 121,
    /// <summary>Stable direct-failure message identifier 122.</summary>
    UnexpectedSpecialComparison = 122,
    /// <summary>Stable direct-failure message identifier 123.</summary>
    UnexpectedMaskedMemoryIntrinsic = 123,
    /// <summary>Stable direct-failure message identifier 124.</summary>
    UnsupportedSIMDConversionTarget = 124,
    /// <summary>Stable direct-failure message identifier 125.</summary>
    MissingInlineCandidateInformation = 125,
    /// <summary>Stable direct-failure message identifier 126.</summary>
    IVOptimizationRequiresDominators = 126,
    /// <summary>Stable direct-failure message identifier 127.</summary>
    ConditionalLoopExitMustHaveATerminalStatement = 127,
    /// <summary>Stable direct-failure message identifier 128.</summary>
    CountedLoopMustHaveAPreheader = 128,
    /// <summary>Stable direct-failure message identifier 129.</summary>
    LoopOccurrencesMustBeInitializedBeforeVisiting = 129,
    /// <summary>Stable direct-failure message identifier 130.</summary>
    StrengthReductionRequiresDominators = 130,
    /// <summary>Stable direct-failure message identifier 131.</summary>
    ActiveIVCursorNeedsARecurrence = 131,
    /// <summary>Stable direct-failure message identifier 132.</summary>
    RephrasingAnIVRequiresConstantSteps = 132,
    /// <summary>Stable direct-failure message identifier 133.</summary>
    Only32And64BitIVsCanBeRephrased = 133,
    /// <summary>Stable direct-failure message identifier 134.</summary>
    GCPointerStrengthReductionRequiresValueNumbering = 134,
    /// <summary>Stable direct-failure message identifier 135.</summary>
    ConstantIVStepMustMaterialize = 135,
    /// <summary>Stable direct-failure message identifier 136.</summary>
    ReplacementCursorNeedsItsRecurrence = 136,
    /// <summary>Stable direct-failure message identifier 137.</summary>
    StrengthReducedUseWasLostFromItsStatement = 137,
    /// <summary>Stable direct-failure message identifier 138.</summary>
    InductionVariableUseIsNotOwnedByItsStatement = 138,
    /// <summary>Stable direct-failure message identifier 139.</summary>
    IVWideningRequiresNaturalLoops = 139,
    /// <summary>Stable direct-failure message identifier 140.</summary>
    IVWideningRequiresAnEntryBlock = 140,
    /// <summary>Stable direct-failure message identifier 141.</summary>
    NonPreheaderIVInitializerNeedsItsSSADefinition = 141,
    /// <summary>Stable direct-failure message identifier 142.</summary>
    LoopCloningVisitorRequiresACurrentBlock = 142,
    /// <summary>Stable direct-failure message identifier 143.</summary>
    LoopCloningVisitorRequiresACurrentStatement = 143,
    /// <summary>Stable direct-failure message identifier 144.</summary>
    ArrayCandidateHasNoUseBlock = 144,
    /// <summary>Stable direct-failure message identifier 145.</summary>
    UnknownLoopCloningCandidate = 145,
    /// <summary>Stable direct-failure message identifier 146.</summary>
    UnrecognizedLoopCloningExitComparison = 146,
    /// <summary>Stable direct-failure message identifier 147.</summary>
    LoopCloningRequiresDiscoveredNaturalLoops = 147,
    /// <summary>Stable direct-failure message identifier 148.</summary>
    SpanCandidateHasNoUseBlock = 148,
    /// <summary>Stable direct-failure message identifier 149.</summary>
    SpanCandidateHasNoBoundsCheck = 149,
    /// <summary>Stable direct-failure message identifier 150.</summary>
    UnknownLoopCloningOptimization = 150,
    /// <summary>Stable direct-failure message identifier 151.</summary>
    ClonedHoistExpressionHasFewerOperandsThanItsSource = 151,
    /// <summary>Stable direct-failure message identifier 152.</summary>
    LoopHoistingRequiresACloneOfItsInvariantExpression = 152,
    /// <summary>Stable direct-failure message identifier 153.</summary>
    LoopHoistingRequiresLoopDiscovery = 153,
    /// <summary>Stable direct-failure message identifier 154.</summary>
    LoopHoistingRequiresLoopSideEffectAnalysis = 154,
    /// <summary>Stable direct-failure message identifier 155.</summary>
    LoopHoistingRequiresADFSTree = 155,
    /// <summary>Stable direct-failure message identifier 156.</summary>
    LoopHoistingRequiresDominators = 156,
    /// <summary>Stable direct-failure message identifier 157.</summary>
    LoopHoistingRequiresAnSSADefinitionBlock = 157,
    /// <summary>Stable direct-failure message identifier 158.</summary>
    LoopUnrollingRequiresDiscoveredNaturalLoops = 158,
    /// <summary>Stable direct-failure message identifier 159.</summary>
    AnalyzedLoopMustHaveATestBlock = 159,
    /// <summary>Stable direct-failure message identifier 160.</summary>
    AnalyzedLoopMustHaveATestTree = 160,
    /// <summary>Stable direct-failure message identifier 161.</summary>
    AnalyzedLoopMustHaveAnIncrement = 161,
    /// <summary>Stable direct-failure message identifier 162.</summary>
    BadPreconditionInCompilerOptUnrollLoops = 162,
    /// <summary>Stable direct-failure message identifier 163.</summary>
    UnexpectedLoopIncrementOperator = 163,
    /// <summary>Stable direct-failure message identifier 164.</summary>
    LoopTryRegionPassedFeasibilityButCouldNotBeCloned = 164,
    /// <summary>Stable direct-failure message identifier 165.</summary>
    UnexpectedLoopIteratorType = 165,
    /// <summary>Stable direct-failure message identifier 166.</summary>
    UnexpectedLoopIteratorOperator = 166,
    /// <summary>Stable direct-failure message identifier 167.</summary>
    UnexpectedLoopTestOperator = 167,
    /// <summary>Stable direct-failure message identifier 168.</summary>
    RangeCheckCloningRequiresValueNumbering = 168,
    /// <summary>Stable direct-failure message identifier 169.</summary>
    FinalRangeCheckIsNotReachableFromTheFirst = 169,
    /// <summary>Stable direct-failure message identifier 170.</summary>
    RangeCheckCloningExhaustedTheNativeHashTableSizes = 170,
    /// <summary>Stable direct-failure message identifier 171.</summary>
    ClonedFastPathLostItsPredecessor = 171,
    /// <summary>Stable direct-failure message identifier 172.</summary>
    BoundsCheckToRemoveIsNotInItsOriginalStatement = 172,
    /// <summary>Stable direct-failure message identifier 173.</summary>
    FastPathMustContainAStatement = 173,
    /// <summary>Stable direct-failure message identifier 174.</summary>
    FirstBoundsCheckIndexCannotBeCloned = 174,
    /// <summary>Stable direct-failure message identifier 175.</summary>
    FirstBoundsCheckLengthCannotBeCloned = 175,
    /// <summary>Stable direct-failure message identifier 176.</summary>
    ClonedBoundsCheckIndexCannotBeClonedAgain = 176,
    /// <summary>Stable direct-failure message identifier 177.</summary>
    LowerBoundIndexCannotBeCloned = 177,
    /// <summary>Stable direct-failure message identifier 178.</summary>
    SplitFastPathMustHaveASuccessor = 178,
    /// <summary>Stable direct-failure message identifier 179.</summary>
    LowerBoundBlockMustHaveATest = 179,
    /// <summary>Stable direct-failure message identifier 180.</summary>
    UpperBoundBlockMustHaveATest = 180,
    /// <summary>Stable direct-failure message identifier 181.</summary>
    RuntimeLookupHelperRequiresASignatureArgument = 181,
    /// <summary>Stable direct-failure message identifier 182.</summary>
    RuntimeLookupSignatureIsNotRegistered = 182,
    /// <summary>Stable direct-failure message identifier 183.</summary>
    ExpandableRuntimeLookupRequiresIndirectionsAndANullCheck = 183,
    /// <summary>Stable direct-failure message identifier 184.</summary>
    CannotCloneRuntimeLookupResultLocal = 184,
    /// <summary>Stable direct-failure message identifier 185.</summary>
    RuntimeLookupHelperRequiresAContextArgument = 185,
    /// <summary>Stable direct-failure message identifier 186.</summary>
    CannotCloneRuntimeLookupContext = 186,
    /// <summary>Stable direct-failure message identifier 187.</summary>
    CannotCloneIndirectRuntimeLookupOffset = 187,
    /// <summary>Stable direct-failure message identifier 188.</summary>
    MissingIndirectRuntimeLookupOffset = 188,
    /// <summary>Stable direct-failure message identifier 189.</summary>
    CannotCloneDictionarySizeCheckBase = 189,
    /// <summary>Stable direct-failure message identifier 190.</summary>
    CannotCloneRuntimeLookupSlot = 190,
    /// <summary>Stable direct-failure message identifier 191.</summary>
    DynamicRuntimeLookupRequiresANonzeroFinalOffset = 191,
    /// <summary>Stable direct-failure message identifier 192.</summary>
    UnsupportedSIMDConversionWidth = 192,
    /// <summary>Stable direct-failure message identifier 193.</summary>
    UnsupportedSIMDConversionSource = 193,
    /// <summary>Stable direct-failure message identifier 194.</summary>
    UnsupportedSIMDIndexType = 194,
    /// <summary>Stable direct-failure message identifier 195.</summary>
    UnsupportedSIMDZipElementWidth = 195,
    /// <summary>Stable direct-failure message identifier 196.</summary>
    UnsupportedSIMDUnzipElementWidth = 196,
    /// <summary>Stable direct-failure message identifier 197.</summary>
    UnsupportedSaturatingNarrowingType = 197,
    /// <summary>Stable direct-failure message identifier 198.</summary>
    OnlySIMDORAndXOROperationsAreSupported = 198,
    /// <summary>Stable direct-failure message identifier 199.</summary>
    StaticInitializationCallHasNoOwningUse = 199,
    /// <summary>Stable direct-failure message identifier 200.</summary>
    NativeAOTTLSExpansionIsNotSupportedForThisTarget = 200,
    /// <summary>Stable direct-failure message identifier 201.</summary>
    CoreCLRTLSExpansionIsNotSupportedForThisTarget = 201,
    /// <summary>Stable direct-failure message identifier 202.</summary>
    ThreadStaticHelperRequiresATypeIndex = 202,
    /// <summary>Stable direct-failure message identifier 203.</summary>
    CannotCloneAThreadStaticExpression = 203,
    /// <summary>Stable direct-failure message identifier 204.</summary>
    CannotCloneReadUtf8DestinationLength = 204,
    /// <summary>Stable direct-failure message identifier 205.</summary>
    CannotCloneReadUtf8DestinationPointer = 205,
    /// <summary>Stable direct-failure message identifier 206.</summary>
    CannotCloneReadUtf8ByteLength = 206,
    /// <summary>Stable direct-failure message identifier 207.</summary>
    LoopMemoryDependenceRequiresAValueNumberStore = 207,
    /// <summary>Stable direct-failure message identifier 208.</summary>
    LoopEntryGuardRequiresACondition = 208,
    /// <summary>Stable direct-failure message identifier 209.</summary>
    InvalidCompilerPhase = 209,
    /// <summary>Stable direct-failure message identifier 210.</summary>
    UnknownIPMappingKind = 210,
    /// <summary>Stable direct-failure message identifier 211.</summary>
    InvalidInstructionInputSize = 211,
    /// <summary>Stable direct-failure message identifier 212.</summary>
    InvalidInstructionFormatForAPXNDD = 212,
    /// <summary>Stable direct-failure message identifier 213.</summary>
    JumpRemovalRequiresAFinalInstructionGroup = 213,
    /// <summary>Stable direct-failure message identifier 214.</summary>
    AlignmentAdjustmentRequiresAFinalInstructionGroup = 214,
    /// <summary>Stable direct-failure message identifier 215.</summary>
    InvalidDisplayOperandSize = 215,
    /// <summary>Stable direct-failure message identifier 216.</summary>
    NonInterruptibleGroupHasNoInstructions = 216,
    /// <summary>Stable direct-failure message identifier 217.</summary>
    InvalidStackSlotBase = 217,
    /// <summary>Stable direct-failure message identifier 218.</summary>
    InvalidGCSlotFlags = 218,
    /// <summary>Stable direct-failure message identifier 219.</summary>
    FilterHasNoStartingBlock = 219,
    /// <summary>Stable direct-failure message identifier 220.</summary>
    UnknownGenericContextParameterType = 220,
    /// <summary>Stable direct-failure message identifier 221.</summary>
    GCStructLocalHasNoLayout = 221,
    /// <summary>Stable direct-failure message identifier 222.</summary>
    StackSlotMapWasNotInitialized = 222,
    /// <summary>Stable direct-failure message identifier 223.</summary>
    LiveStackSlotWasNotAssigned = 223,
    /// <summary>Stable direct-failure message identifier 224.</summary>
    RegisterSlotMapWasNotInitialized = 224,
    /// <summary>Stable direct-failure message identifier 225.</summary>
    LiveRegisterSlotWasNotAssigned = 225,
    /// <summary>Stable direct-failure message identifier 226.</summary>
    OutgoingArgumentTerminatorIsMissing = 226,
    /// <summary>Stable direct-failure message identifier 227.</summary>
    GTSLLIUWScaleIndexHandlingIsOnlySupportedOnRISCV64 = 227,
    /// <summary>Stable direct-failure message identifier 228.</summary>
    InvalidNodeTypeForGetRegTypeByIndex = 228,
    /// <summary>Stable direct-failure message identifier 229.</summary>
    InvalidNodeTypeForGetRegSpillFlagByIdx = 229,
    /// <summary>Stable direct-failure message identifier 230.</summary>
    InvalidNodeTypeForIndexedRegisterSpillFlags = 230,
    /// <summary>Stable direct-failure message identifier 231.</summary>
    UnsupportedCallKindInVzeroupperClassification = 231,
    /// <summary>Stable direct-failure message identifier 232.</summary>
    ARM64ConditionalSelectNodesAreOnlySupportedOnARM64 = 232,
    /// <summary>Stable direct-failure message identifier 233.</summary>
    ARM64BitfieldNodesAreOnlySupportedOnARM64 = 233,
    /// <summary>Stable direct-failure message identifier 234.</summary>
    SIMDConstantsAreUnsupportedOnThisTarget = 234,
    /// <summary>Stable direct-failure message identifier 235.</summary>
    ScalableSIMDConstantsAreOnlySupportedOnARM64 = 235,
    /// <summary>Stable direct-failure message identifier 236.</summary>
    ThisVectorOperationRequiresFixedWidthConstantStorage = 236,
    /// <summary>Stable direct-failure message identifier 237.</summary>
    UnexpectedSIMDConstantType = 237,
    /// <summary>Stable direct-failure message identifier 238.</summary>
    UnexpectedScalarBroadcastInstruction = 238,
    /// <summary>Stable direct-failure message identifier 239.</summary>
    UnexpectedInlineDecision = 239,
    /// <summary>Stable direct-failure message identifier 240.</summary>
    InvalidInlineObservation = 240,
    /// <summary>Stable direct-failure message identifier 241.</summary>
    InvalidIntegralToFloatingConversion = 241,
    /// <summary>Stable direct-failure message identifier 242.</summary>
    InvalidFloatingConversionSourceType = 242,
    /// <summary>Stable direct-failure message identifier 243.</summary>
    InvalidSignExtensionSize = 243,
    /// <summary>Stable direct-failure message identifier 244.</summary>
    UnexpectedInstructionWithPseudoName = 244,
    /// <summary>Stable direct-failure message identifier 245.</summary>
    NoPointInMeasuringDEBUGCode = 245,
    /// <summary>Stable direct-failure message identifier 246.</summary>
    CycleCountersOnlyHookedUpOnX86X64 = 246,
    /// <summary>Stable direct-failure message identifier 247.</summary>
    EHCodeOffsetRequiresAnEmitter = 247,
    /// <summary>Stable direct-failure message identifier 248.</summary>
    GCCallDescriptorIsRequiredBeforeAllocatingItsArgumentTable = 248,
    /// <summary>Stable direct-failure message identifier 249.</summary>
    LCArrayToGenTreeDoesNotSupportMDArraysInThePinnedNativeImplementation = 249,
    /// <summary>Stable direct-failure message identifier 250.</summary>
    CannotMaterializeAnInvalidLoopCloningExpression = 250,
    /// <summary>Stable direct-failure message identifier 251.</summary>
    UnknownLoopCloningIdentifierType = 251,
    /// <summary>Stable direct-failure message identifier 252.</summary>
    CannotMaterializeAnInvalidLoopCloningIdentifier = 252,
    /// <summary>Stable direct-failure message identifier 253.</summary>
    ConditionCodeLoweringRequiresAFlagProducingHardwareIntrinsic = 253,
    /// <summary>Stable direct-failure message identifier 254.</summary>
    VectorElementExtractionRequiresAnArithmeticBaseType = 254,
    /// <summary>Stable direct-failure message identifier 255.</summary>
    FusedMultiplyLoweringRequiresAnFMAIntrinsic = 255,
    /// <summary>Stable direct-failure message identifier 256.</summary>
    UnexpectedFloatingComparisonIntrinsic = 256,
    /// <summary>Stable direct-failure message identifier 257.</summary>
    UnexpectedIntegerComparisonIntrinsic = 257,
    /// <summary>Stable direct-failure message identifier 258.</summary>
    UnexpectedScalarComparisonIntrinsic = 258,
    /// <summary>Stable direct-failure message identifier 259.</summary>
    UnsupportedSIMDMemoryComparisonOperation = 259,
    /// <summary>Stable direct-failure message identifier 260.</summary>
    SwiftErrorReturnsAreNotSupportedForThisTarget = 260,
    /// <summary>Stable direct-failure message identifier 261.</summary>
    EntryAllocationRequiresBlockInformation = 261,
    /// <summary>Stable direct-failure message identifier 262.</summary>
    EntryAllocationRequiresAFirstBlock = 262,
    /// <summary>Stable direct-failure message identifier 263.</summary>
    StackEntryDefinitionRequiresANextReference = 263,
    /// <summary>Stable direct-failure message identifier 264.</summary>
    NextStackEntryReferenceRequiresATree = 264,
    /// <summary>Stable direct-failure message identifier 265.</summary>
    UpperVectorReferencesRequireARelatedLocal = 265,
    /// <summary>Stable direct-failure message identifier 266.</summary>
    UpperVectorSaveRequiresAnAssignedInterval = 266,
    /// <summary>Stable direct-failure message identifier 267.</summary>
    SpecialPutargRequiresASourceLocal = 267,
    /// <summary>Stable direct-failure message identifier 268.</summary>
    ExposedUseRequiresAnIncomingLocalMap = 268,
    /// <summary>Stable direct-failure message identifier 269.</summary>
    PreassignedLocalRequiresALastReference = 269,
    /// <summary>Stable direct-failure message identifier 270.</summary>
    ConflictingPreassignedLocalRequiresANextReference = 270,
    /// <summary>Stable direct-failure message identifier 271.</summary>
    LSRABlockSequencingCanOnlyBeInitializedOnce = 271,
    /// <summary>Stable direct-failure message identifier 272.</summary>
    LSRABlockSequenceWasAlreadyInitialized = 272,
    /// <summary>Stable direct-failure message identifier 273.</summary>
    LSRABlockMetadataRequiresComputedPredecessorLists = 273,
    /// <summary>Stable direct-failure message identifier 274.</summary>
    LSRABlockSequenceDoesNotCoverTheCompilerFlowGraph = 274,
    /// <summary>Stable direct-failure message identifier 275.</summary>
    LSRABlockSequenceDoesNotCoverEveryCompilerBlock = 275,
    /// <summary>Stable direct-failure message identifier 276.</summary>
    LSRARequiresANonEmptyCompilerFlowGraph = 276,
    /// <summary>Stable direct-failure message identifier 277.</summary>
    LSRABlockTraversalRequiresAnInitializedBlockSequence = 277,
    /// <summary>Stable direct-failure message identifier 278.</summary>
    LSRABlockVisitationRequiresInitializedBlockTraits = 278,
    /// <summary>Stable direct-failure message identifier 279.</summary>
    CallReferenceBuildingRequiresInitializedCodegenState = 279,
    /// <summary>Stable direct-failure message identifier 280.</summary>
    WindowsX64VarargsRequiresAnXMM0XMM3ArgumentRegister = 280,
    /// <summary>Stable direct-failure message identifier 281.</summary>
    LSRARequiresInitializedCodegenState = 281,
    /// <summary>Stable direct-failure message identifier 282.</summary>
    LSRAInternalDefinitionStateContainsAnEmptyEntry = 282,
    /// <summary>Stable direct-failure message identifier 283.</summary>
    LSRADefinitionListEntryMustRetainItsReference = 283,
    /// <summary>Stable direct-failure message identifier 284.</summary>
    UpperVectorSavesRequireTheCurrentBasicBlock = 284,
    /// <summary>Stable direct-failure message identifier 285.</summary>
    LSRADefinitionListEntryMustRetainItsDefiningNode = 285,
    /// <summary>Stable direct-failure message identifier 286.</summary>
    LSRAStructTemporariesMustBeLocalVariablesOrCalls = 286,
    /// <summary>Stable direct-failure message identifier 287.</summary>
    InvalidLSRAEdgeResolutionType = 287,
    /// <summary>Stable direct-failure message identifier 288.</summary>
    LSRAEdgeResolutionRequiresAllocatedLocalMapsAndBlockSequence = 288,
    /// <summary>Stable direct-failure message identifier 289.</summary>
    SplitEdgeRequiresAUniqueOriginalPredecessor = 289,
    /// <summary>Stable direct-failure message identifier 290.</summary>
    SplitEdgeRequiresASingleSuccessor = 290,
    /// <summary>Stable direct-failure message identifier 291.</summary>
    SplitEdgeRequiresASinglePredecessor = 291,
    /// <summary>Stable direct-failure message identifier 292.</summary>
    CriticalEdgeRequiresAnOutgoingRegisterMap = 292,
    /// <summary>Stable direct-failure message identifier 293.</summary>
    SwitchEdgeRequiresItsSwitchTable = 293,
    /// <summary>Stable direct-failure message identifier 294.</summary>
    SwitchEdgeResolutionRequiresCodegenState = 294,
    /// <summary>Stable direct-failure message identifier 295.</summary>
    ConditionalEdgeRequiresItsBranch = 295,
    /// <summary>Stable direct-failure message identifier 296.</summary>
    CriticalEdgeResolutionRequiresTheSharedMap = 296,
    /// <summary>Stable direct-failure message identifier 297.</summary>
    CriticalEdgeRequiresASuccessorRegisterMap = 297,
    /// <summary>Stable direct-failure message identifier 298.</summary>
    BranchResolutionRequiresATerminator = 298,
    /// <summary>Stable direct-failure message identifier 299.</summary>
    SwapResolutionRequiresATerminator = 299,
    /// <summary>Stable direct-failure message identifier 300.</summary>
    ResolutionRequiresAPredecessorOutgoingMap = 300,
    /// <summary>Stable direct-failure message identifier 301.</summary>
    ResolutionRequiresASuccessorBlock = 301,
    /// <summary>Stable direct-failure message identifier 302.</summary>
    ResolutionRequiresASuccessorRegisterMap = 302,
    /// <summary>Stable direct-failure message identifier 303.</summary>
    SplitResolutionRequiresASuccessor = 303,
    /// <summary>Stable direct-failure message identifier 304.</summary>
    CriticalResolutionRequiresASuccessor = 304,
    /// <summary>Stable direct-failure message identifier 305.</summary>
    UnknownLSRAEdgeResolutionType = 305,
    /// <summary>Stable direct-failure message identifier 306.</summary>
    ReadyResolutionTargetRequiresAnInterval = 306,
    /// <summary>Stable direct-failure message identifier 307.</summary>
    SwapResolutionRequiresTheDisplacedInterval = 307,
    /// <summary>Stable direct-failure message identifier 308.</summary>
    SwapResolutionRequiresTheSourceInterval = 308,
    /// <summary>Stable direct-failure message identifier 309.</summary>
    CycleSpillRequiresADisplacedInterval = 309,
    /// <summary>Stable direct-failure message identifier 310.</summary>
    CycleResolutionRequiresASourceInterval = 310,
    /// <summary>Stable direct-failure message identifier 311.</summary>
    CycleScratchMoveRequiresADisplacedInterval = 311,
    /// <summary>Stable direct-failure message identifier 312.</summary>
    StackResolutionRequiresAPendingInterval = 312,
    /// <summary>Stable direct-failure message identifier 313.</summary>
    EdgeResolutionRequiresThePredecessorSRegisterMap = 313,
    /// <summary>Stable direct-failure message identifier 314.</summary>
    EdgeResolutionRequiresTheSuccessorSRegisterMap = 314,
    /// <summary>Stable direct-failure message identifier 315.</summary>
    SharedCriticalResolutionRequiresARegisterMap = 315,
    /// <summary>Stable direct-failure message identifier 316.</summary>
    LSRAFrameTypeWasNotSelected = 316,
    /// <summary>Stable direct-failure message identifier 317.</summary>
    IntrinsicReferenceRequiresAnInitializedBlockSequence = 317,
    /// <summary>Stable direct-failure message identifier 318.</summary>
    LSRAIntervalRequiresAConcreteRegisterType = 318,
    /// <summary>Stable direct-failure message identifier 319.</summary>
    WriteBarrierKillSetConstructionRequiresInitializedCodeGen = 319,
    /// <summary>Stable direct-failure message identifier 320.</summary>
    CallKillSetConstructionRequiresInitializedCodeGen = 320,
    /// <summary>Stable direct-failure message identifier 321.</summary>
    VirtualStubCallsRequireInitializedRegisterMetadata = 321,
    /// <summary>Stable direct-failure message identifier 322.</summary>
    LocalBlockLocationsRequireEnregisteredLocals = 322,
    /// <summary>Stable direct-failure message identifier 323.</summary>
    BlockLocationsRequireAnOutgoingVariableMap = 323,
    /// <summary>Stable direct-failure message identifier 324.</summary>
    BlockLocationsRequireInitializedLSRABlockInformation = 324,
    /// <summary>Stable direct-failure message identifier 325.</summary>
    BlockLocationsRequireAnIncomingVariableMap = 325,
    /// <summary>Stable direct-failure message identifier 326.</summary>
    BlockLocationsRequireAPredecessorOutgoingMap = 326,
    /// <summary>Stable direct-failure message identifier 327.</summary>
    ResolutionOfAPredecessorSpilledRegisterRequiresANextReference = 327,
    /// <summary>Stable direct-failure message identifier 328.</summary>
    OutOfOrderIntervalRequiresANextReference = 328,
    /// <summary>Stable direct-failure message identifier 329.</summary>
    AssignedIntervalRequiresItsRegisterRecord = 329,
    /// <summary>Stable direct-failure message identifier 330.</summary>
    LocalIntervalConstructionRequiresEnregisteredLocals = 330,
    /// <summary>Stable direct-failure message identifier 331.</summary>
    IntervalConstructionRequiresInitializedCodegenState = 331,
    /// <summary>Stable direct-failure message identifier 332.</summary>
    TailcallBlockMustContainItsJumpOrCall = 332,
    /// <summary>Stable direct-failure message identifier 333.</summary>
    WriteThroughHeuristicsRequireBlockSequencing = 333,
    /// <summary>Stable direct-failure message identifier 334.</summary>
    TrackedLocalMappingIsRequiredForIntervalConstruction = 334,
    /// <summary>Stable direct-failure message identifier 335.</summary>
    LocalRegisterResolutionRequiresMatchingBlockNodeOwnershipAndEnregisteredLocals = 335,
    /// <summary>Stable direct-failure message identifier 336.</summary>
    ContainedLocalResolutionRequiresAnIncomingVariableMap = 336,
    /// <summary>Stable direct-failure message identifier 337.</summary>
    ContainedLocalUseRequiresItsTreeNode = 337,
    /// <summary>Stable direct-failure message identifier 338.</summary>
    ReloadResolutionRequiresAnIncomingVariableMap = 338,
    /// <summary>Stable direct-failure message identifier 339.</summary>
    CopiedOrMovedLocalReferenceRequiresItsTreeNode = 339,
    /// <summary>Stable direct-failure message identifier 340.</summary>
    CopiedLocalReferenceRequiresItsOwningBlock = 340,
    /// <summary>Stable direct-failure message identifier 341.</summary>
    LocalRegisterResolutionRequiresEnregisteredLocals = 341,
    /// <summary>Stable direct-failure message identifier 342.</summary>
    LocalRegisterResolutionRequiresAnEntryBlock = 342,
    /// <summary>Stable direct-failure message identifier 343.</summary>
    LocalRegisterResolutionRequiresAnEntryVariableMap = 343,
    /// <summary>Stable direct-failure message identifier 344.</summary>
    LocalRegisterResolutionRequiresABlockReference = 344,
    /// <summary>Stable direct-failure message identifier 345.</summary>
    LocalRegisterResolutionRequiresAnOrderedBlockReference = 345,
    /// <summary>Stable direct-failure message identifier 346.</summary>
    UpperVectorSavesRequireAKillNode = 346,
    /// <summary>Stable direct-failure message identifier 347.</summary>
    UpperVectorIntervalRequiresItsLocal = 347,
    /// <summary>Stable direct-failure message identifier 348.</summary>
    UpperVectorSaveRequiresItsInterval = 348,
    /// <summary>Stable direct-failure message identifier 349.</summary>
    UpperVectorRestoresRequireALocal = 349,
    /// <summary>Stable direct-failure message identifier 350.</summary>
    SpilledOrMovedTemporaryRequiresItsNextReference = 350,
    /// <summary>Stable direct-failure message identifier 351.</summary>
    SavedTemporaryRequiresALaterUse = 351,
    /// <summary>Stable direct-failure message identifier 352.</summary>
    RegisterParameterRequiresAnInitialReference = 352,
    /// <summary>Stable direct-failure message identifier 353.</summary>
    UpperVectorSaveRequiresItsLocalInterval = 353,
    /// <summary>Stable direct-failure message identifier 354.</summary>
    UpperVectorRestoreRequiresItsLocalInterval = 354,
    /// <summary>Stable direct-failure message identifier 355.</summary>
    UpperVectorRestoreRequiresAnOwningUse = 355,
    /// <summary>Stable direct-failure message identifier 356.</summary>
    VectorRestoreBeforeABranchRequiresATerminator = 356,
    /// <summary>Stable direct-failure message identifier 357.</summary>
    LocalSourceUseRequiresItsTree = 357,
    /// <summary>Stable direct-failure message identifier 358.</summary>
    MinimalRegisterAllocationRequiresLocalsToRemainUnEnregistered = 358,
    /// <summary>Stable direct-failure message identifier 359.</summary>
    MinimalRegisterAllocationCannotRunWithEnregisteredLocals = 359,
    /// <summary>Stable direct-failure message identifier 360.</summary>
    MinimalLSRAReferencesMustNotBeLocalVariableIntervals = 360,
    /// <summary>Stable direct-failure message identifier 361.</summary>
    UpperVectorSaveRequiresAnAssignedRegister = 361,
    /// <summary>Stable direct-failure message identifier 362.</summary>
    UpperVectorIntervalMustHaveAFirstReference = 362,
    /// <summary>Stable direct-failure message identifier 363.</summary>
    RequiredCopyRegisterAllocationFoundNoRegister = 363,
    /// <summary>Stable direct-failure message identifier 364.</summary>
    MinimalIntervalConstructionCannotRunWithEnregisteredLocals = 364,
    /// <summary>Stable direct-failure message identifier 365.</summary>
    MinimalRegisterResolutionCannotRunWithEnregisteredLocals = 365,
    /// <summary>Stable direct-failure message identifier 366.</summary>
    MinimalResolutionCannotSaveAnEnregisteredLocalVector = 366,
    /// <summary>Stable direct-failure message identifier 367.</summary>
    UpperVectorRestoresRequireEnregisteredLocalResolution = 367,
    /// <summary>Stable direct-failure message identifier 368.</summary>
    ContainedAddressUseRequiresAnAddressMode = 368,
    /// <summary>Stable direct-failure message identifier 369.</summary>
    LateCallArgumentMustHaveALoweredNode = 369,
    /// <summary>Stable direct-failure message identifier 370.</summary>
    LSRADefinitionListEntryHasNoReferencePosition = 370,
    /// <summary>Stable direct-failure message identifier 371.</summary>
    LSRACouldNotMatchAUseWithItsDefinition = 371,
    /// <summary>Stable direct-failure message identifier 372.</summary>
    TrackedLSRALocalsRequireIntervalMappings = 372,
    /// <summary>Stable direct-failure message identifier 373.</summary>
    GetRegTypeByIndexCannotDetermineAMultiRegisterLocalFieldType = 373,
    /// <summary>Stable direct-failure message identifier 374.</summary>
    LSRACannotDetermineAMultiRegisterDefinitionType = 374,
    /// <summary>Stable direct-failure message identifier 375.</summary>
    LSRAUpperVectorLookupEncounteredANonUpperVectorInterval = 375,
    /// <summary>Stable direct-failure message identifier 376.</summary>
    UpperVectorIntervalsRequireARelatedLocalInterval = 376,
    /// <summary>Stable direct-failure message identifier 377.</summary>
    PartiallySpilledVectorRequiresAPrecedingUpperVectorSave = 377,
    /// <summary>Stable direct-failure message identifier 378.</summary>
    RegisterAssignmentRequiresInitializedCodegenState = 378,
    /// <summary>Stable direct-failure message identifier 379.</summary>
    CannotClearAnUnassignedPhysicalRegister = 379,
    /// <summary>Stable direct-failure message identifier 380.</summary>
    CannotUnassignAPhysicalRegisterWithoutAnInterval = 380,
    /// <summary>Stable direct-failure message identifier 381.</summary>
    ActiveIntervalSpillRequiresItsLastReference = 381,
    /// <summary>Stable direct-failure message identifier 382.</summary>
    RestorableRegisterHasNoPreviousInterval = 382,
    /// <summary>Stable direct-failure message identifier 383.</summary>
    UpperVectorIntervalRequiresItsRelatedLocalInterval = 383,
    /// <summary>Stable direct-failure message identifier 384.</summary>
    UpperVectorIntervalMustBeRelatedToALocalInterval = 384,
    /// <summary>Stable direct-failure message identifier 385.</summary>
    SingleDefinitionIntervalMustHaveAFirstReference = 385,
    /// <summary>Stable direct-failure message identifier 386.</summary>
    OnlyLocalVariableIntervalsCanBeLiveOnBlockEntry = 386,
    /// <summary>Stable direct-failure message identifier 387.</summary>
    RegisterKillsRequireInitializedCodegenState = 387,
    /// <summary>Stable direct-failure message identifier 388.</summary>
    NonEmptyKillListMustHaveATailReference = 388,
    /// <summary>Stable direct-failure message identifier 389.</summary>
    MinimalCopyRegisterAssignmentRequiresAnActiveInterval = 389,
    /// <summary>Stable direct-failure message identifier 390.</summary>
    ActiveCopyRegisterIntervalMustRetainItsAssignedRegister = 390,
    /// <summary>Stable direct-failure message identifier 391.</summary>
    IntervalSAssignedRegisterDoesNotMatchItsPhysicalRegister = 391,
    /// <summary>Stable direct-failure message identifier 392.</summary>
    NoSpillUnassignmentRequiresAnActiveInterval = 392,
    /// <summary>Stable direct-failure message identifier 393.</summary>
    MinimalRegisterStateResetCannotRunWithEnregisteredLocals = 393,
    /// <summary>Stable direct-failure message identifier 394.</summary>
    SpillWeightCalculationRequiresAnAssignedInterval = 394,
    /// <summary>Stable direct-failure message identifier 395.</summary>
    SpillWeightCalculationRequiresARecentReference = 395,
    /// <summary>Stable direct-failure message identifier 396.</summary>
    RegisterSelectionHasNotBeenReset = 396,
    /// <summary>Stable direct-failure message identifier 397.</summary>
    MinimalLSRASelectionRequiresLocalVariablesToRemainUnEnregistered = 397,
    /// <summary>Stable direct-failure message identifier 398.</summary>
    MinimalLSRASelectionHasNoCandidateRegisters = 398,
    /// <summary>Stable direct-failure message identifier 399.</summary>
    RequiredReferenceLostAllCandidateRegisters = 399,
    /// <summary>Stable direct-failure message identifier 400.</summary>
    MinimalLSRASelectionDidNotChooseExactlyOneRegister = 400,
    /// <summary>Stable direct-failure message identifier 401.</summary>
    SingleRegisterHeuristicMustSelectExactlyOneRegister = 401,
    /// <summary>Stable direct-failure message identifier 402.</summary>
    RegisterSelectionIsMissingItsRangeEndReference = 402,
    /// <summary>Stable direct-failure message identifier 403.</summary>
    SpillCostSelectionRequiresEachBusyCandidateToHaveAnAssignedInterval = 403,
    /// <summary>Stable direct-failure message identifier 404.</summary>
    CurrentNextIntervalLocationMustHaveAReference = 404,
    /// <summary>Stable direct-failure message identifier 405.</summary>
    CopyOrReloadRequiresAnOwningLIRUse = 405,
    /// <summary>Stable direct-failure message identifier 406.</summary>
    GroupedLSRACopyOrReloadRequiresAMultiRegisterSource = 406,
    /// <summary>Stable direct-failure message identifier 407.</summary>
    LSRACopyOrReloadRegisterIsAlreadyAssigned = 407,
    /// <summary>Stable direct-failure message identifier 408.</summary>
    UnsupportedScalarMathIntrinsic = 408,
    /// <summary>Stable direct-failure message identifier 409.</summary>
    DefUseConflictResolutionIsOnlyValidForTemporaryIntervals = 409,
    /// <summary>Stable direct-failure message identifier 410.</summary>
    ConflictingDefinitionMustBeFollowedByItsUse = 410,
    /// <summary>Stable direct-failure message identifier 411.</summary>
    DefUseConflictReferencesMustBeOrderedReferencesOfOneInterval = 411,
    /// <summary>Stable direct-failure message identifier 412.</summary>
    TemporaryDefinitionMustRetainItsSourceTreeNode = 412,
    /// <summary>Stable direct-failure message identifier 413.</summary>
    FixedUseMustBePresentInTheNextFixedReferenceMap = 413,
    /// <summary>Stable direct-failure message identifier 414.</summary>
    AssignedIntervalMustRetainItsRecentReference = 414,
    /// <summary>Stable direct-failure message identifier 415.</summary>
    CannotSpillAnUnassignedPhysicalRegister = 415,
    /// <summary>Stable direct-failure message identifier 416.</summary>
    PhysicalRegisterWithoutARecentReferenceMustBelongToALocal = 416,
    /// <summary>Stable direct-failure message identifier 417.</summary>
    LocalWithoutARecentReferenceMustBeAParameter = 417,
    /// <summary>Stable direct-failure message identifier 418.</summary>
    KillReferencesDoNotHaveASpillWeight = 418,
    /// <summary>Stable direct-failure message identifier 419.</summary>
    SpilledIntervalMustHaveAFirstReference = 419,
    /// <summary>Stable direct-failure message identifier 420.</summary>
    LSRABlockWeightsAreUnavailableBeforeIntervalConstruction = 420,
    /// <summary>Stable direct-failure message identifier 421.</summary>
    RejectedStackAllocationRequiresAReason = 421,
    /// <summary>Stable direct-failure message identifier 422.</summary>
    ConditionalEscapeCloningRequiresADFSTree = 422,
    /// <summary>Stable direct-failure message identifier 423.</summary>
    ConditionalEscapeCloningRequiresDominators = 423,
    /// <summary>Stable direct-failure message identifier 424.</summary>
    ConditionalEscapeCloningRequiresADefinitionBlock = 424,
    /// <summary>Stable direct-failure message identifier 425.</summary>
    UnexpectedObjectAllocationCandidateKind = 425,
    /// <summary>Stable direct-failure message identifier 426.</summary>
    NewArrayHelperRequiresALengthArgument = 426,
    /// <summary>Stable direct-failure message identifier 427.</summary>
    SelectedTryRegionCouldNotBeCloned = 427,
    /// <summary>Stable direct-failure message identifier 428.</summary>
    EmptyStaticAllocationRequiresOnePredecessor = 428,
    /// <summary>Stable direct-failure message identifier 429.</summary>
    EmptyStaticPredecessorRequiresItsBranchStatement = 429,
    /// <summary>Stable direct-failure message identifier 430.</summary>
    StructFieldStoreMustHaveALayout = 430,
    /// <summary>Stable direct-failure message identifier 431.</summary>
    RetypingStructFieldsRequiresTheirNewLayout = 431,
    /// <summary>Stable direct-failure message identifier 432.</summary>
    UnexpectedParentWhileRetypingAStackObjectUse = 432,
    /// <summary>Stable direct-failure message identifier 433.</summary>
    RetypedStructLocalRequiresItsLayout = 433,
    /// <summary>Stable direct-failure message identifier 434.</summary>
    UNBOXRequiresItsObjectArgument = 434,
    /// <summary>Stable direct-failure message identifier 435.</summary>
    UNBOXRequiresACloneableLocal = 435,
    /// <summary>Stable direct-failure message identifier 436.</summary>
    DelegateInvokeRequiresAThisArgument = 436,
    /// <summary>Stable direct-failure message identifier 437.</summary>
    DelegateInvokeRequiresACloneableReceiver = 437,
    /// <summary>Stable direct-failure message identifier 438.</summary>
    UNBOXTypeTestMustRetainItsSideEffectCall = 438,
    /// <summary>Stable direct-failure message identifier 439.</summary>
    TrackedStructRequiresItsLayout = 439,
    /// <summary>Stable direct-failure message identifier 440.</summary>
    InvalidVariableLocationKind = 440,
    /// <summary>Stable direct-failure message identifier 441.</summary>
    InvalidStackVariableType = 441,
    /// <summary>Stable direct-failure message identifier 442.</summary>
    InvalidRegisterVariableType = 442,
    /// <summary>Stable direct-failure message identifier 443.</summary>
    VariableLiveRangesHaveNotBeenInitialized = 443,
    /// <summary>Stable direct-failure message identifier 444.</summary>
    TrackedLocalMappingHasNotBeenInitialized = 444,
    /// <summary>Stable direct-failure message identifier 445.</summary>
    InvalidRegisterNumber = 445,
    /// <summary>Stable direct-failure message identifier 446.</summary>
    PreciseMapStoreRequiresTheCurrentBlock = 446,
    /// <summary>Stable failure identifier for a missing LSRA register bank.</summary>
    MissingRegisterBank = 447,
    /// <summary>Stable failure identifier for an unexpected LSRA reference kind.</summary>
    UnexpectedReferenceKind = 448,
    /// <summary>The generated ICorJitInfo vtable does not match its API order.</summary>
    ICorJitInfoVTableLayoutMismatch = 449,
    /// <summary>The ICorJitInfo proxy context does not reference a wrapper.</summary>
    ICorJitInfoProxyContextUnavailable = 450,
    /// <summary>Environment.FailFast returned unexpectedly.</summary>
    FailFastReturned = 451,
    /// <summary>A native instruction-set ordinal is unavailable for this target.</summary>
    NativeInstructionSetUnavailable = 452,
    /// <summary>The GC bit stream requires a little-endian 64-bit target.</summary>
    GCBitStreamRequiresALittleEndian64BitTarget = 453,
    /// <summary>The destination is shorter than the encoded bit stream.</summary>
    DestinationIsShorterThanTheEncodedBitStream = 454,
    /// <summary>Emit requires a completed Build and may only be called once.</summary>
    EmitRequiresACompletedBuildAndMayOnlyBeCalledOnce = 456,
    /// <summary>GC info encoding is not implemented for ARM targets.</summary>
    GCInfoEncodingIsNotImplementedForARMTargets = 457,
    /// <summary>Only the AMD64 GC info encoding is implemented.</summary>
    OnlyTheAMD64GCInfoEncodingIsImplemented = 458,
    /// <summary>Every call site needs a corresponding instruction size.</summary>
    EveryCallSiteNeedsACorrespondingInstructionSize = 461,
    /// <summary>ARM64 Edit-and-Continue GC-info metadata is not implemented.</summary>
    ARM64EditAndContinueGCInfoMetadataIsNotImplemented = 462,
    /// <summary>GC information cannot change after Build.</summary>
    GCInformationCannotChangeAfterBuild = 463,
    /// <summary>Register numbers are not defined for this target.</summary>
    RegisterNumbersAreNotDefinedForThisTarget = 465,
    /// <summary>Ambient stack-pointer register number is not defined for this target.</summary>
    AmbientStackPointerRegisterNumberIsNotDefinedForThisTarget = 466,
    /// <summary>Floating-point register numbering is not defined for this target.</summary>
    FloatingPointRegisterNumberingIsNotDefinedForThisTarget = 467,
    /// <summary>Caller-pop conventions are only defined for x86.</summary>
    CallerPopConventionsAreOnlyDefinedForX86 = 468,
    /// <summary>Unseeded CLRRandom.Init requires the Windows native thread ID.</summary>
    UnseededCLRRandomInitRequiresTheWindowsNativeThreadID = 469,
    /// <summary>CLRRandom must be initialized before drawing values.</summary>
    CLRRandomMustBeInitializedBeforeDrawingValues = 470,
    /// <summary>Jump kinds are not supported for this target.</summary>
    JumpKindsAreNotSupportedForThisTarget = 471,
    /// <summary>WebAssembly-only compiler code requires the WebAssembly target.</summary>
    WebAssemblyOnlyCompilerCodeRequiresTheWebAssemblyTarget = 472,
    /// <summary>Unsupported host architecture: {0}.</summary>
    UnsupportedHostArchitectureArgument = 473,
    /// <summary>Required compiler configuration switch &#x27;{0}&#x27; is not set.</summary>
    RequiredCompilerConfigurationSwitchArgumentIsNotSet = 474,
    /// <summary>The debugger method hash is only available in Debug builds.</summary>
    DebuggerMethodHashIsOnlyAvailableInDebugBuilds = 475,
    /// <summary>A non-empty register assignment requires a referent.</summary>
    NonEmptyRegisterAssignmentRequiresAReferent = 476,
    /// <summary>Inline context diagnostic state is only available in Debug builds.</summary>
    InlineContextDiagnosticStateIsOnlyAvailableInDebugBuilds = 477,
    /// <summary>Inline instruction state has not been initialized.</summary>
    InlineInstructionStateHasNotBeenInitialized = 478,
    /// <summary>Async-call diagnostic state is only available in Debug builds.</summary>
    AsyncCallDiagnosticStateIsOnlyAvailableInDebugBuilds = 479,
    /// <summary>Inline-policy data-collection state is only available in Debug builds.</summary>
    InlinePolicyDataCollectionStateIsOnlyAvailableInDebugBuilds = 480,
    /// <summary>Inline stress random state is only available in Debug builds.</summary>
    InlineStressRandomStateIsOnlyAvailableInDebugBuilds = 481,
    /// <summary>Caller shadow-space offsets require Windows AMD64.</summary>
    CallerShadowSpaceOffsetsRequireWindowsAMD64 = 482,
    /// <summary>No ABI classifier is available for this target.</summary>
    NoABIClassifierIsAvailableForThisTarget = 483,
    /// <summary>Trivial pointer-sized struct classification requires x86.</summary>
    TrivialPointerSizedStructClassificationRequiresX86 = 488,
    /// <summary>Floating-point struct lowering is unsupported for this target.</summary>
    FloatingPointStructLoweringIsUnsupportedForThisTarget = 489,
    /// <summary>Swift ABI classification is unsupported for this target.</summary>
    SwiftABIClassificationIsUnsupportedForThisTarget = 490,
    /// <summary>A link register is not available for this target.</summary>
    LinkRegisterIsNotAvailableForThisTarget = 491,
    /// <summary>Long return registers are not available for this target.</summary>
    LongReturnRegistersAreNotAvailableForThisTarget = 492,
    /// <summary>Encoded callee-saved registers are not defined for this target.</summary>
    EncodedCalleeSavedRegistersAreNotDefinedForThisTarget = 493,
    /// <summary>Swift return registers are not supported for this target.</summary>
    SwiftReturnRegistersAreNotSupportedForThisTarget = 494,
    /// <summary>Stack-probe helper arguments are not supported for this target.</summary>
    StackProbeHelperArgumentsAreNotSupportedForThisTarget = 495,
    /// <summary>A zero register is not available for this target.</summary>
    ZeroRegisterIsNotAvailableForThisTarget = 496,
    /// <summary>Masked registers are not supported for this target.</summary>
    MaskedRegistersAreNotSupportedForThisTarget = 497,
    /// <summary>Swift ABI registers are not supported for this target.</summary>
    SwiftABIRegistersAreNotSupportedForThisTarget = 498,
    /// <summary>xarch metadata is unavailable for this architecture.</summary>
    XarchMetadataIsUnavailableForThisArchitecture = 499,
    /// <summary>Target metadata is unavailable for this architecture.</summary>
    TargetMetadataIsUnavailableForThisArchitecture = 500,
    /// <summary>Windows x64 ABI classification requires Windows AMD64.</summary>
    WindowsX64ABIClassificationRequiresWindowsAMD64 = 502,
    /// <summary>The expandable array has not been allocated.</summary>
    ExpandableArrayHasNotBeenAllocated = 503,
    /// <summary>Struct return has no layout</summary>
    StructReturnHasNoLayout = 504,
    /// <summary>Could not find return for call</summary>
    CouldNotFindReturnForCall = 505,
    /// <summary>Struct local has no layout</summary>
    StructLocalHasNoLayout = 506,
    /// <summary>Epoch validation is only available in Debug.</summary>
    EpochValidationIsOnlyAvailableInDebug = 510,
    /// <summary>Basic block LIR membership checks are only available in Debug builds.</summary>
    BasicBlockLIRMembershipChecksAreOnlyAvailableInDebugBuilds = 511,
    /// <summary>Native sizeof(BasicBlock) is not ported.</summary>
    NativeSizeofBasicBlockIsNotPorted = 512,
    /// <summary>Basic block dumps are only available in Debug builds.</summary>
    BasicBlockDumpsAreOnlyAvailableInDebugBuilds = 513,
    /// <summary>Edge likelihood tracking is only available in Debug builds.</summary>
    EdgeLikelihoodTrackingIsOnlyAvailableInDebugBuilds = 514,
    /// <summary>Predecessor shuffling is only available in Debug builds.</summary>
    PredecessorShufflingIsOnlyAvailableInDebugBuilds = 515,
    /// <summary>SSA stress hashing is only available in Debug builds.</summary>
    SSAStressHashingIsOnlyAvailableInDebugBuilds = 516,
    /// <summary>Instruction metadata is not available for the configured target.</summary>
    InstructionMetadataIsNotAvailableForTheConfiguredTarget = 517,
    /// <summary>Wasm SIMD metadata is not available for the configured target.</summary>
    WasmSIMDMetadataIsNotAvailableForTheConfiguredTarget = 518,
    /// <summary>Instruction input size metadata requires xarch.</summary>
    InstructionInputSizeMetadataRequiresXarch = 519,
    /// <summary>Instruction {0} has no input size.</summary>
    InstructionArgumentHasNoInputSize = 520,
    /// <summary>Register masks are unavailable for this target.</summary>
    RegisterMasksAreUnavailableForThisTarget = 521,
    /// <summary>Write-barrier diagnostic state is only available in Debug builds.</summary>
    WriteBarrierDiagnosticStateIsOnlyAvailableInDebugBuilds = 522,
    /// <summary>Register zeroing requires a fixed register set.</summary>
    RegisterZeroingRequiresAFixedRegisterSet = 523,
    /// <summary>Register zeroing is not implemented for this target.</summary>
    RegisterZeroingIsNotImplementedForThisTarget = 524,
    /// <summary>CodeGen.genGetGSCookieTempRegs is not supported for this target.</summary>
    CodeGenGenGetGSCookieTempRegsIsNotSupportedForThisTarget = 525,
    /// <summary>Code-generation diagnostic state is only available in Debug builds.</summary>
    CodeGenerationDiagnosticStateIsOnlyAvailableInDebugBuilds = 526,
    /// <summary>Machine-code generation requires Windows AMD64.</summary>
    MachineCodeGenerationRequiresWindowsAMD64 = 527,
    /// <summary>Wasm funclet validation requires the Wasm target.</summary>
    WasmFuncletValidationRequiresTheWasmTarget = 528,
    /// <summary>Wasm ReadyToRun range validation requires the Wasm target.</summary>
    WasmReadyToRunRangeValidationRequiresTheWasmTarget = 529,
    /// <summary>Frame-pointer register is not available for the configured target.</summary>
    FramePointerRegisterIsNotAvailableForTheConfiguredTarget = 530,
    /// <summary>Stack-pointer register is not available for the configured target.</summary>
    StackPointerRegisterIsNotAvailableForTheConfiguredTarget = 531,
    /// <summary>APX callee-saved register pops are only supported on AMD64.</summary>
    APXCalleeSavedRegisterPopsAreOnlySupportedOnAMD64 = 532,
    /// <summary>ARM32 immediate materialization requires ARM32.</summary>
    ARM32ImmediateMaterializationRequiresARM32 = 533,
    /// <summary>ARM32 saturating increment code generation is not ported.</summary>
    ARM32SaturatingIncrementCodeGenerationIsNotPorted = 534,
    /// <summary>ARM64 prolog pointer authentication is not ported.</summary>
    ARM64PrologPointerAuthenticationIsNotPorted = 536,
    /// <summary>ARM64 epilog pointer authentication is not ported.</summary>
    ARM64EpilogPointerAuthenticationIsNotPorted = 537,
    /// <summary>SIMD constants are not supported by this ARM64 configuration.</summary>
    SIMDConstantsAreNotSupportedByThisARM64Configuration = 538,
    /// <summary>Masked SIMD constants are not supported by this ARM64 configuration.</summary>
    MaskedSIMDConstantsAreNotSupportedByThisARM64Configuration = 539,
    /// <summary>ARM64 frame policy is not supported for this target.</summary>
    ARM64FramePolicyIsNotSupportedForThisTarget = 540,
    /// <summary>ARM64 SIMD12 indirect-load code generation requires SIMD-enabled ARM64.</summary>
    ARM64SIMD12IndirectLoadCodeGenerationRequiresSIMDEnabledARM64 = 541,
    /// <summary>ARM64 SIMD12 indirect stores are unavailable for this target.</summary>
    ARM64SIMD12IndirectStoresAreUnavailableForThisTarget = 542,
    /// <summary>The reserved ARM64 code-generation register requires ARM64.</summary>
    ReservedARM64CodeGenerationRegisterRequiresARM64 = 545,
    /// <summary>Jump compare is missing its second operand.</summary>
    JumpCompareIsMissingItsSecondOperand = 546,
    /// <summary>ARM64 block-relative address recording is not ported.</summary>
    ARM64BlockRelativeAddressRecordingIsNotPorted = 547,
    /// <summary>ARM64 SIMD upper-lane saves are unavailable for this target.</summary>
    ARM64SIMDUpperLaneSavesAreUnavailableForThisTarget = 548,
    /// <summary>ARM64 SIMD upper-lane restores are unavailable for this target.</summary>
    ARM64SIMDUpperLaneRestoresAreUnavailableForThisTarget = 549,
    /// <summary>Missing epilog node.</summary>
    MissingEpilogNode = 550,
    /// <summary>Unsupported JMP indirection.</summary>
    UnsupportedJMPIndirection = 551,
    /// <summary>ARM architecture indirect loads are not supported for this target.</summary>
    ARMArchitectureIndirectLoadsAreNotSupportedForThisTarget = 552,
    /// <summary>Native local-address generation is unavailable for this target.</summary>
    NativeLocalAddressGenerationIsUnavailableForThisTarget = 553,
    /// <summary>ARM code generation was requested for a non-ARM target.</summary>
    ARMCodeGenerationWasRequestedForANonARMTarget = 554,
    /// <summary>SIMD constants are not supported by this ARM configuration.</summary>
    SIMDConstantsAreNotSupportedByThisARMConfiguration = 555,
    /// <summary>Masked SIMD constants are not supported by this ARM configuration.</summary>
    MaskedSIMDConstantsAreNotSupportedByThisARMConfiguration = 556,
    /// <summary>Swift error returns are not supported by this ARM configuration.</summary>
    SwiftErrorReturnsAreNotSupportedByThisARMConfiguration = 557,
    /// <summary>Hardware intrinsics are not supported by this ARM configuration.</summary>
    HardwareIntrinsicsAreNotSupportedByThisARMConfiguration = 558,
    /// <summary>Swift error values are not supported by this ARM configuration.</summary>
    SwiftErrorValuesAreNotSupportedByThisARMConfiguration = 559,
    /// <summary>ARM64 code generation for {0} is not supported on this target.</summary>
    ARM64CodeGenerationForArgumentIsNotSupportedOnThisTarget = 560,
    /// <summary>ARM32 code generation for {0} is not supported on this target.</summary>
    ARM32CodeGenerationForArgumentIsNotSupportedOnThisTarget = 561,
    /// <summary>GT_NULLCHECK is not supported for ARM32; use GT_IND.</summary>
    GTNULLCHECKIsNotSupportedForARM32UseGTIND = 562,
    /// <summary>Unknown-size frame generation requires SIMD-enabled ARM64.</summary>
    UnknownSizeFrameGenerationRequiresSIMDEnabledARM64 = 563,
    /// <summary>Unknown-size frame initialization requires SIMD-enabled ARM64.</summary>
    UnknownSizeFrameInitializationRequiresSIMDEnabledARM64 = 564,
    /// <summary>Wasm throw-helper generation uses a separate target path.</summary>
    WasmThrowHelperGenerationUsesASeparateTargetPath = 565,
    /// <summary>Overflow checking uses a separate target path.</summary>
    OverflowCheckingUsesASeparateTargetPath = 566,
    /// <summary>Overflow checking is not implemented for this target.</summary>
    OverflowCheckingIsNotImplementedForThisTarget = 567,
    /// <summary>Jump instruction generation is not implemented for this target.</summary>
    JumpInstructionGenerationIsNotImplementedForThisTarget = 568,
    /// <summary>Helper call generation is not implemented for this target.</summary>
    HelperCallGenerationIsNotImplementedForThisTarget = 569,
    /// <summary>Async resume address generation requires Windows AMD64.</summary>
    AsyncResumeAddressGenerationRequiresWindowsAMD64 = 570,
    /// <summary>SIMD return values are not supported on this Wasm target.</summary>
    SIMDReturnValuesAreNotSupportedOnThisWasmTarget = 571,
    /// <summary>Nonlocal jump generation is unsupported for this target.</summary>
    NonlocalJumpGenerationIsUnsupportedForThisTarget = 573,
    /// <summary>Function-entry address generation is unsupported for this target.</summary>
    FunctionEntryAddressGenerationIsUnsupportedForThisTarget = 575,
    /// <summary>Patchpoint generation is unsupported on Wasm.</summary>
    PatchpointGenerationIsUnsupportedOnWasm = 576,
    /// <summary>Patchpoint transfer is unsupported for this target.</summary>
    PatchpointTransferIsUnsupportedForThisTarget = 577,
    /// <summary>Locked addition generation outside xarch is not implemented.</summary>
    LockedAdditionGenerationOutsideXarchIsNotImplemented = 578,
    /// <summary>unimplemented on LOONGARCH64 yet</summary>
    UnimplementedOnLOONGARCH64Yet = 579,
    /// <summary>Unexpected operation in locked instruction generation.</summary>
    UnexpectedOperationInLockedInstructionGeneration = 580,
    /// <summary>Locked instruction generation outside xarch is not implemented.</summary>
    LockedInstructionGenerationOutsideXarchIsNotImplemented = 581,
    /// <summary>Compare-exchange generation outside xarch is not implemented.</summary>
    CompareExchangeGenerationOutsideXarchIsNotImplemented = 582,
    /// <summary>Memory barrier generation outside supported targets is not implemented.</summary>
    MemoryBarrierGenerationOutsideSupportedTargetsIsNotImplemented = 583,
    /// <summary>Wasm register bitcast generation is not ported.</summary>
    WasmRegisterBitcastGenerationIsNotPorted = 584,
    /// <summary>Block memory generation is not implemented for this target.</summary>
    BlockMemoryGenerationIsNotImplementedForThisTarget = 585,
    /// <summary>Unrolled memmove is not implemented for this target.</summary>
    UnrolledMemmoveIsNotImplementedForThisTarget = 586,
    /// <summary>Call register argument placement requires a native target.</summary>
    CallRegisterArgumentPlacementRequiresANativeTarget = 590,
    /// <summary>Unix AMD64 varargs are unsupported.</summary>
    UnixAMD64VarargsAreUnsupported = 591,
    /// <summary>Call generation is not implemented for this target.</summary>
    CallGenerationIsNotImplementedForThisTarget = 592,
    /// <summary>Wasm tailcall epilog-register verification is not ported.</summary>
    WasmTailcallEpilogRegisterVerificationIsNotPorted = 594,
    /// <summary>Callee-save pushes require xarch.</summary>
    CalleeSavePushesRequireXarch = 595,
    /// <summary>APX callee-save pushes require AMD64.</summary>
    APXCalleeSavePushesRequireAMD64 = 596,
    /// <summary>Floating callee-save recording requires xarch.</summary>
    FloatingCalleeSaveRecordingRequiresXarch = 597,
    /// <summary>Floating callee-save restoration requires xarch.</summary>
    FloatingCalleeSaveRestorationRequiresXarch = 598,
    /// <summary>EVEX conditional-compare mapping requires AMD64.</summary>
    EVEXConditionalCompareMappingRequiresAMD64 = 599,
    /// <summary>EVEX conditional-compare options require AMD64.</summary>
    EVEXConditionalCompareOptionsRequireAMD64 = 600,
    /// <summary>Conditional compare is only supported on AMD64 and ARM64.</summary>
    ConditionalCompareIsOnlySupportedOnAMD64AndARM64 = 601,
    /// <summary>Register copy generation is not supported on Wasm.</summary>
    RegisterCopyGenerationIsNotSupportedOnWasm = 602,
    /// <summary>Indexed register copies are not supported on Wasm.</summary>
    IndexedRegisterCopiesAreNotSupportedOnWasm = 603,
    /// <summary>Finally-call generation is not supported by this dispatcher.</summary>
    FinallyCallGenerationIsNotSupportedByThisDispatcher = 604,
    /// <summary>Catch-return generation is not supported by this dispatcher.</summary>
    CatchReturnGenerationIsNotSupportedByThisDispatcher = 605,
    /// <summary>AMD64 emitter tests require an AMD64 target.</summary>
    AMD64EmitterTestsRequireAnAMD64Target = 606,
    /// <summary>Callee-save restoration is not supported by this dispatcher.</summary>
    CalleeSaveRestorationIsNotSupportedByThisDispatcher = 607,
    /// <summary>Root epilog generation is not supported by this dispatcher.</summary>
    RootEpilogGenerationIsNotSupportedByThisDispatcher = 609,
    /// <summary>instGen_Return is not used on ARM64.</summary>
    InstGenReturnIsNotUsedOnARM64 = 610,
    /// <summary>Return instruction generation is not implemented on this target.</summary>
    ReturnInstructionGenerationIsNotImplementedOnThisTarget = 611,
    /// <summary>x87 floating-point return generation requires X86.</summary>
    X87FloatingPointReturnGenerationRequiresX86 = 612,
    /// <summary>Floating casts outside xarch are not implemented.</summary>
    FloatingCastsOutsideXarchAreNotImplemented = 613,
    /// <summary>Invalid integral-to-floating cast target.</summary>
    InvalidIntegralToFloatingCastTarget = 614,
    /// <summary>Integral-to-floating casts outside xarch are not implemented.</summary>
    IntegralToFloatingCastsOutsideXarchAreNotImplemented = 615,
    /// <summary>Parameter homing register selection requires AMD64.</summary>
    ParameterHomingRegisterSelectionRequiresAMD64 = 616,
    /// <summary>Prespilled unmapped registers are only supported on ARM.</summary>
    PrespilledUnmappedRegistersAreOnlySupportedOnARM = 617,
    /// <summary>Unwind-region immediate materialization is not implemented for ARM.</summary>
    UnwindRegionImmediateMaterializationIsNotImplementedForARM = 618,
    /// <summary>Immediate materialization is not implemented for this target.</summary>
    ImmediateMaterializationIsNotImplementedForThisTarget = 619,
    /// <summary>Instruction flags for immediate materialization are only supported on ARM.</summary>
    InstructionFlagsForImmediateMaterializationAreOnlySupportedOnARM = 620,
    /// <summary>Frame poisoning is not implemented for Wasm.</summary>
    FramePoisoningIsNotImplementedForWasm = 621,
    /// <summary>Unknown-size frame poisoning requires ARM64.</summary>
    UnknownSizeFramePoisoningRequiresARM64 = 622,
    /// <summary>Funclet prologs require xarch.</summary>
    FuncletPrologsRequireXarch = 623,
    /// <summary>Funclet epilogs require xarch.</summary>
    FuncletEpilogsRequireXarch = 624,
    /// <summary>Funclet frame capture requires xarch.</summary>
    FuncletFrameCaptureRequiresXarch = 625,
    /// <summary>The JIT32 GC encoder requires X86.</summary>
    JIT32GCEncoderRequiresX86 = 626,
    /// <summary>GC-info publication is unsupported for this target.</summary>
    GCInfoPublicationIsUnsupportedForThisTarget = 627,
    /// <summary>GC-info publication requires AMD64.</summary>
    GCInfoPublicationRequiresAMD64 = 628,
    /// <summary>GS-cookie checks are not ported for this target.</summary>
    GSCookieChecksAreNotPortedForThisTarget = 629,
    /// <summary>GS-cookie initialization is not ported for this target.</summary>
    GSCookieInitializationIsNotPortedForThisTarget = 630,
    /// <summary>Late disassembly requires a native callback bridge to preserve decoder-error output.</summary>
    LateDisassemblyRequiresANativeCallbackBridgeToPreserveDecoderErrorOutput = 631,
    /// <summary>ARM architecture intrinsic generation is unavailable for this target.</summary>
    ARMArchitectureIntrinsicGenerationIsUnavailableForThisTarget = 632,
    /// <summary>Ceiling intrinsic generation requires ARM64.</summary>
    CeilingIntrinsicGenerationRequiresARM64 = 633,
    /// <summary>Floor intrinsic generation requires ARM64.</summary>
    FloorIntrinsicGenerationRequiresARM64 = 634,
    /// <summary>Truncate intrinsic generation requires ARM64.</summary>
    TruncateIntrinsicGenerationRequiresARM64 = 635,
    /// <summary>Round intrinsic generation requires ARM64.</summary>
    RoundIntrinsicGenerationRequiresARM64 = 636,
    /// <summary>PopCount intrinsic generation requires ARM64.</summary>
    PopCountIntrinsicGenerationRequiresARM64 = 637,
    /// <summary>TrailingZeroCount intrinsic generation requires ARM64.</summary>
    TrailingZeroCountIntrinsicGenerationRequiresARM64 = 638,
    /// <summary>SaturateToInt8 intrinsic generation requires ARM.</summary>
    SaturateToInt8IntrinsicGenerationRequiresARM = 639,
    /// <summary>SaturateToInt16 intrinsic generation requires ARM.</summary>
    SaturateToInt16IntrinsicGenerationRequiresARM = 640,
    /// <summary>SaturateToUInt8 intrinsic generation requires ARM.</summary>
    SaturateToUInt8IntrinsicGenerationRequiresARM = 641,
    /// <summary>SaturateToUInt16 intrinsic generation requires ARM.</summary>
    SaturateToUInt16IntrinsicGenerationRequiresARM = 642,
    /// <summary>SIMD upper-register restoration is unsupported.</summary>
    SIMDUpperRegisterRestorationIsUnsupported = 643,
    /// <summary>SIMD upper-register saving is unsupported.</summary>
    SIMDUpperRegisterSavingIsUnsupported = 644,
    /// <summary>JMP argument placement requires a native target.</summary>
    JMPArgumentPlacementRequiresANativeTarget = 645,
    /// <summary>SysV AMD64 JMP varargs are not supported.</summary>
    SysVAMD64JMPVarargsAreNotSupported = 646,
    /// <summary>JMP varargs placement is not yet ported for this target.</summary>
    JMPVarargsPlacementIsNotYetPortedForThisTarget = 647,
    /// <summary>Jump table emission is not supported on Wasm.</summary>
    JumpTableEmissionIsNotSupportedOnWasm = 648,
    /// <summary>Stack-pointer adjustment with probing requires xarch.</summary>
    StackPointerAdjustmentWithProbingRequiresXarch = 649,
    /// <summary>Local heap generation uses a separate Wasm path.</summary>
    LocalHeapGenerationUsesASeparateWasmPath = 650,
    /// <summary>Local heap generation is not ported for this target.</summary>
    LocalHeapGenerationIsNotPortedForThisTarget = 651,
    /// <summary>Long unsigned remainder generation requires X86.</summary>
    LongUnsignedRemainderGenerationRequiresX86 = 652,
    /// <summary>Long-to-int cast generation requires X86.</summary>
    LongToIntCastGenerationRequiresX86 = 653,
    /// <summary>Raw-hex output file path is invalid.</summary>
    RawHexOutputFilePathIsInvalid = 654,
    /// <summary>Register-local stores are not supported on Wasm.</summary>
    RegisterLocalStoresAreNotSupportedOnWasm = 655,
    /// <summary>Multi-register SIMD local stores require xarch SIMD support.</summary>
    MultiRegisterSIMDLocalStoresRequireXarchSIMDSupport = 656,
    /// <summary>Multi-register SIMD local stores are unsupported for this xarch ABI.</summary>
    MultiRegisterSIMDLocalStoresAreUnsupportedForThisXarchABI = 657,
    /// <summary>Node instruction generation requires xarch.</summary>
    NodeInstructionGenerationRequiresXarch = 658,
    /// <summary>RISC-V-only node reached non-RISC-V code generation.</summary>
    RISCVOnlyNodeReachedNonRISCVCodeGeneration = 659,
    /// <summary>RISC-V rotate node reached non-RISC-V code generation.</summary>
    RISCVRotateNodeReachedNonRISCVCodeGeneration = 660,
    /// <summary>Hardware intrinsics are unavailable for this target.</summary>
    HardwareIntrinsicsAreUnavailableForThisTarget = 661,
    /// <summary>LoongArch conditional branch reached another target.</summary>
    LoongArchConditionalBranchReachedAnotherTarget = 662,
    /// <summary>RISC-V select node reached another target.</summary>
    RISCVSelectNodeReachedAnotherTarget = 663,
    /// <summary>RISC-V atomic node reached another target.</summary>
    RISCVAtomicNodeReachedAnotherTarget = 664,
    /// <summary>RISC-V arithmetic node reached another target.</summary>
    RISCVArithmeticNodeReachedAnotherTarget = 665,
    /// <summary>Unimplemented node type in code generation.</summary>
    UnimplementedNodeTypeInCodeGeneration = 666,
    /// <summary>SIMD constants are unavailable for this target.</summary>
    SIMDConstantsAreUnavailableForThisTarget = 667,
    /// <summary>Masked constants are unavailable for this target.</summary>
    MaskedConstantsAreUnavailableForThisTarget = 668,
    /// <summary>AMD64 conditional compare reached another target.</summary>
    AMD64ConditionalCompareReachedAnotherTarget = 669,
    /// <summary>Swift error registers are unavailable for this target.</summary>
    SwiftErrorRegistersAreUnavailableForThisTarget = 670,
    /// <summary>OSR callee-save restoration is unsupported for this target.</summary>
    OSRCalleeSaveRestorationIsUnsupportedForThisTarget = 671,
    /// <summary>OSR callee-save recording requires AMD64.</summary>
    OSRCalleeSaveRecordingRequiresAMD64 = 672,
    /// <summary>OSR local homing is not ported for this target.</summary>
    OSRLocalHomingIsNotPortedForThisTarget = 673,
    /// <summary>Profiler leave callbacks are unsupported for this target.</summary>
    ProfilerLeaveCallbacksAreUnsupportedForThisTarget = 674,
    /// <summary>Profiler enter callbacks are unsupported for this target.</summary>
    ProfilerEnterCallbacksAreUnsupportedForThisTarget = 675,
    /// <summary>Unix NativeAOT prologs require CFI unwind support.</summary>
    UnixNativeAOTPrologsRequireCFIUnwindSupport = 676,
    /// <summary>Aligned SIMD stores require AMD64.</summary>
    AlignedSIMDStoresRequireAMD64 = 677,
    /// <summary>Block-based prolog initialization is not supported on Wasm.</summary>
    BlockBasedPrologInitializationIsNotSupportedOnWasm = 678,
    /// <summary>Prolog block initialization requires a supported target.</summary>
    PrologBlockInitializationRequiresASupportedTarget = 679,
    /// <summary>ARM32 stack-allocation register selection is not supported on this target.</summary>
    ARM32StackAllocationRegisterSelectionIsNotSupportedOnThisTarget = 680,
    /// <summary>Frame pointer establishment is not ported for this target.</summary>
    FramePointerEstablishmentIsNotPortedForThisTarget = 681,
    /// <summary>Local frame allocation is not ported for this target.</summary>
    LocalFrameAllocationIsNotPortedForThisTarget = 682,
    /// <summary>Prolog AVX-state clearing requires xarch.</summary>
    PrologAVXStateClearingRequiresXarch = 683,
    /// <summary>Epilog AVX-state clearing requires xarch.</summary>
    EpilogAVXStateClearingRequiresXarch = 684,
    /// <summary>Wasm has no architectural integer zero register.</summary>
    WasmHasNoArchitecturalIntegerZeroRegister = 685,
    /// <summary>Wasm has no architectural floating-point registers.</summary>
    WasmHasNoArchitecturalFloatingPointRegisters = 686,
    /// <summary>Floating-register zeroing is not implemented for this target.</summary>
    FloatingRegisterZeroingIsNotImplementedForThisTarget = 687,
    /// <summary>ARM64 floating-register zeroing is unavailable.</summary>
    ARM64FloatingRegisterZeroingIsUnavailable = 688,
    /// <summary>Wasm frame initialization is unavailable for this target.</summary>
    WasmFrameInitializationIsUnavailableForThisTarget = 689,
    /// <summary>Wasm generic-context reporting is unavailable for this target.</summary>
    WasmGenericContextReportingIsUnavailableForThisTarget = 690,
    /// <summary>ARM32 generic-context storage is unavailable.</summary>
    ARM32GenericContextStorageIsUnavailable = 691,
    /// <summary>ARM64 generic-context storage is unavailable.</summary>
    ARM64GenericContextStorageIsUnavailable = 692,
    /// <summary>LoongArch64 generic-context storage is unavailable.</summary>
    LoongArch64GenericContextStorageIsUnavailable = 693,
    /// <summary>RISC-V64 generic-context storage is unavailable.</summary>
    RISCV64GenericContextStorageIsUnavailable = 694,
    /// <summary>Native generic-context loading is unavailable on Wasm.</summary>
    NativeGenericContextLoadingIsUnavailableOnWasm = 695,
    /// <summary>xarch generic-context storage is unavailable.</summary>
    XarchGenericContextStorageIsUnavailable = 696,
    /// <summary>ARM32 profiling pre-spill information is unavailable.</summary>
    ARM32ProfilingPreSpillInformationIsUnavailable = 697,
    /// <summary>Generic-context reporting is not implemented for this target.</summary>
    GenericContextReportingIsNotImplementedForThisTarget = 698,
    /// <summary>Register argument generation is not ported for this target.</summary>
    RegisterArgumentGenerationIsNotPortedForThisTarget = 699,
    /// <summary>Return-trap generation is not ported for this target.</summary>
    ReturnTrapGenerationIsNotPortedForThisTarget = 700,
    /// <summary>Long return generation is only supported on X86 and ARM.</summary>
    LongReturnGenerationIsOnlySupportedOnX86AndARM = 701,
    /// <summary>Return GC information requires a fixed-register target.</summary>
    ReturnGCInformationRequiresAFixedRegisterTarget = 702,
    /// <summary>Simple return generation is not ported for this target.</summary>
    SimpleReturnGenerationIsNotPortedForThisTarget = 703,
    /// <summary>Clearing the Wasm async continuation is only supported on Wasm.</summary>
    ClearingTheWasmAsyncContinuationIsOnlySupportedOnWasm = 704,
    /// <summary>SIMD split returns require SIMD support on xarch or ARM.</summary>
    SIMDSplitReturnsRequireSIMDSupportOnXarchOrARM = 705,
    /// <summary>SIMD split returns are not ported for this target.</summary>
    SIMDSplitReturnsAreNotPortedForThisTarget = 706,
    /// <summary>Swift error returns are not supported on this target.</summary>
    SwiftErrorReturnsAreNotSupportedOnThisTarget = 707,
    /// <summary>Stack-pointer checking is not ported for this target.</summary>
    StackPointerCheckingIsNotPortedForThisTarget = 708,
    /// <summary>Rich debug-info file path is invalid.</summary>
    RichDebugInfoFilePathIsInvalid = 709,
    /// <summary>Saturating increment generation is not implemented for this target.</summary>
    SaturatingIncrementGenerationIsNotImplementedForThisTarget = 710,
    /// <summary>Bit modification generation requires xarch.</summary>
    BitModificationGenerationRequiresXarch = 711,
    /// <summary>Physical register generation is not implemented for this target.</summary>
    PhysicalRegisterGenerationIsNotImplementedForThisTarget = 712,
    /// <summary>Register spills are not supported on Wasm.</summary>
    RegisterSpillsAreNotSupportedOnWasm = 713,
    /// <summary>Register reloads are not supported on Wasm.</summary>
    RegisterReloadsAreNotSupportedOnWasm = 714,
    /// <summary>ARM32 cannot place this contained stack argument.</summary>
    ARM32CannotPlaceThisContainedStackArgument = 715,
    /// <summary>ARM struct stack argument local has no layout.</summary>
    ARMStructStackArgumentLocalHasNoLayout = 716,
    /// <summary>SIMD in genPutArgStk-----unimplemented/unused on RISCV64 yet----</summary>
    SIMDInGenPutArgStkUnimplementedUnusedOnRISCV64Yet = 717,
    /// <summary>RISC-V struct stack argument local has no layout.</summary>
    RISCVStructStackArgumentLocalHasNoLayout = 718,
    /// <summary>Stack argument generation requires xarch.</summary>
    StackArgumentGenerationRequiresXarch = 719,
    /// <summary>Struct stack argument generation requires xarch.</summary>
    StructStackArgumentGenerationRequiresXarch = 720,
    /// <summary>Partial REP struct copies are unavailable on x86.</summary>
    PartialREPStructCopiesAreUnavailableOnX86 = 721,
    /// <summary>The first incoming stack argument is not implemented for x86.</summary>
    FirstIncomingStackArgumentIsNotImplementedForX86 = 722,
    /// <summary>Incoming stack argument selection is not yet ported for this target.</summary>
    IncomingStackArgumentSelectionIsNotYetPortedForThisTarget = 723,
    /// <summary>x86 stack argument bases require fixed outgoing arguments.</summary>
    X86StackArgumentBasesRequireFixedOutgoingArguments = 724,
    /// <summary>Stack argument base selection requires xarch.</summary>
    StackArgumentBaseSelectionRequiresXarch = 725,
    /// <summary>x86 stack-argument constant materialization is unavailable.</summary>
    X86StackArgumentConstantMaterializationIsUnavailable = 726,
    /// <summary>SIMD12 stack arguments are unavailable for this target.</summary>
    SIMD12StackArgumentsAreUnavailableForThisTarget = 727,
    /// <summary>Stack-level adjustment is not supported on Wasm.</summary>
    StackLevelAdjustmentIsNotSupportedOnWasm = 728,
    /// <summary>Stack-level marking is not available for this target.</summary>
    StackLevelMarkingIsNotAvailableForThisTarget = 729,
    /// <summary>Stack-segment homing is not supported for this target.</summary>
    StackSegmentHomingIsNotSupportedForThisTarget = 730,
    /// <summary>Stack vector argument upper-bit clearing requires SysV AMD64 SIMD.</summary>
    StackVectorArgumentUpperBitClearingRequiresSysVAMD64SIMD = 731,
    /// <summary>Swift stack parameter homing is not supported on this target.</summary>
    SwiftStackParameterHomingIsNotSupportedOnThisTarget = 732,
    /// <summary>{0} is not implemented for the active target.</summary>
    ArgumentIsNotImplementedForTheActiveTarget = 733,
    /// <summary>A struct local on the frame must have a layout.</summary>
    StructLocalOnTheFrameMustHaveALayout = 734,
    /// <summary>JIT32 GC encoder lookup table has not been initialized.</summary>
    JIT32GCEncoderLookupTableHasNotBeenInitialized = 735,
    /// <summary>The pending-argument stack is missing its overflow storage.</summary>
    PendingArgumentStackIsMissingItsOverflowStorage = 736,
    /// <summary>JIT32 GC call argument table is missing.</summary>
    JIT32GCCallArgumentTableIsMissing = 737,
    /// <summary>Condition instruction mapping is not ported for this target.</summary>
    ConditionInstructionMappingIsNotPortedForThisTarget = 738,
    /// <summary>Register masks are not available for targets without a fixed register set.</summary>
    RegisterMasksAreNotAvailableForTargetsWithoutAFixedRegisterSet = 739,
    /// <summary>Too many internal registers were added to a node.</summary>
    TooManyInternalRegistersWereAddedToANode = 740,
    /// <summary>The internal register index is out of range.</summary>
    InternalRegisterIndexIsOutOfRange = 741,
    /// <summary>No internal register is available to extract.</summary>
    NoInternalRegisterIsAvailableToExtract = 742,
    /// <summary>Exactly one internal register was expected.</summary>
    ExactlyOneInternalRegisterWasExpected = 743,
    /// <summary>Internal registers are missing for the specified node.</summary>
    InternalRegistersAreMissingForTheSpecifiedNode = 744,
    /// <summary>Register-mask tracking is not available for this target.</summary>
    RegisterMaskTrackingIsNotAvailableForThisTarget = 745,
    /// <summary>WebAssembly register tracking is not available for this target.</summary>
    WebAssemblyRegisterTrackingIsNotAvailableForThisTarget = 746,
    /// <summary>The WebAssembly register table is not initialized.</summary>
    WebAssemblyRegisterTableIsNotInitialized = 747,
    /// <summary>ARM condition-result generation is not supported on this target.</summary>
    ARMConditionResultGenerationIsNotSupportedOnThisTarget = 748,
    /// <summary>Floating-to-integer casts are not implemented for this target.</summary>
    FloatingToIntegerCastsAreNotImplementedForThisTarget = 749,
    /// <summary>Integral-to-integral casts are not implemented for this target.</summary>
    IntegralToIntegralCastsAreNotImplementedForThisTarget = 750,
    /// <summary>Invalid floating-to-integral cast source.</summary>
    InvalidFloatingToIntegralCastSource = 751,
    /// <summary>Floating-to-integer cast generation outside ARM is not ported.</summary>
    FloatingToIntegerCastGenerationOutsideARMIsNotPorted = 752,
    /// <summary>Split-long cast generation is only implemented for 32-bit ARM.</summary>
    SplitLongCastGenerationIsOnlyImplementedFor32BitARM = 753,
    /// <summary>Flag branch generation has no native implementation for this target.</summary>
    FlagBranchGenerationHasNoNativeImplementationForThisTarget = 754,
    /// <summary>Conditional branch sequences have no native implementation for this target.</summary>
    ConditionalBranchSequencesHaveNoNativeImplementationForThisTarget = 755,
    /// <summary>Flag-result generation has no native implementation for this target.</summary>
    FlagResultGenerationHasNoNativeImplementationForThisTarget = 756,
    /// <summary>Operand-use numbering is only available in Debug builds.</summary>
    OperandUseNumberingIsOnlyAvailableInDebugBuilds = 757,
    /// <summary>Register copies are not supported on Wasm.</summary>
    RegisterCopiesAreNotSupportedOnWasm = 758,
    /// <summary>Indexed register consumption is not supported on Wasm.</summary>
    IndexedRegisterConsumptionIsNotSupportedOnWasm = 759,
    /// <summary>Split-long local stores are not supported on this target.</summary>
    SplitLongLocalStoresAreNotSupportedOnThisTarget = 760,
    /// <summary>Register-local spills are not supported on Wasm.</summary>
    RegisterLocalSpillsAreNotSupportedOnWasm = 761,
    /// <summary>Local-variable spills require a fixed register set.</summary>
    LocalVariableSpillsRequireAFixedRegisterSet = 762,
    /// <summary>Struct stack arguments are not supported on Wasm.</summary>
    StructStackArgumentsAreNotSupportedOnWasm = 763,
    /// <summary>This struct field-list stack-argument path is not supported on this target.</summary>
    ThisStructFieldListStackArgumentPathIsNotSupportedOnThisTarget = 764,
    /// <summary>Target SIMD12 local-stack store recording is not yet ported.</summary>
    TargetSIMD12LocalStackStoreRecordingIsNotYetPorted = 765,
    /// <summary>Target SIMD12 local-stack store recording is only implemented for ARM64.</summary>
    TargetSIMD12LocalStackStoreRecordingIsOnlyImplementedForARM64 = 766,
    /// <summary>Register-local reloads are not supported on Wasm.</summary>
    RegisterLocalReloadsAreNotSupportedOnWasm = 767,
    /// <summary>Indexed register reloads are not supported on Wasm.</summary>
    IndexedRegisterReloadsAreNotSupportedOnWasm = 768,
    /// <summary>WebAssembly register-copy selection is not implemented.</summary>
    WebAssemblyRegisterCopySelectionIsNotImplemented = 769,
    /// <summary>Unsupported target for the exception-object register.</summary>
    UnsupportedTargetForTheExceptionObjectRegister = 770,
    /// <summary>Unsupported target for the exception-object register mask.</summary>
    UnsupportedTargetForTheExceptionObjectRegisterMask = 771,
    /// <summary>LoongArch64 ternary instruction recording is not ported.</summary>
    LoongArch64TernaryInstructionRecordingIsNotPorted = 772,
    /// <summary>This block-store implementation requires LoongArch64 or RISC-V64.</summary>
    ThisBlockStoreImplementationRequiresLoongArch64OrRISCV64 = 773,
    /// <summary>A block-copy destination address mode requires a base.</summary>
    BlockCopyDestinationAddressModeRequiresABase = 774,
    /// <summary>A block-copy source address mode requires a base.</summary>
    BlockCopySourceAddressModeRequiresABase = 775,
    /// <summary>LoongArch64 memory barrier generation requires LoongArch64.</summary>
    LoongArch64MemoryBarrierGenerationRequiresLoongArch64 = 776,
    /// <summary>LoongArch64 varargs are not supported.</summary>
    LoongArch64VarargsAreNotSupported = 777,
    /// <summary>Varargs are not supported on LoongArch64.</summary>
    VarargsAreNotSupportedOnLoongArch64 = 778,
    /// <summary>Immediate materialization uses a separate target implementation.</summary>
    ImmediateMaterializationUsesASeparateTargetImplementation = 779,
    /// <summary>LoongArch64 SIMD12 indirect loads are not ported.</summary>
    LoongArch64SIMD12IndirectLoadsAreNotPorted = 780,
    /// <summary>LoongArch64 SIMD12 indirect stores are not ported.</summary>
    LoongArch64SIMD12IndirectStoresAreNotPorted = 781,
    /// <summary>This local-load implementation requires LoongArch64 or RISC-V64.</summary>
    ThisLocalLoadImplementationRequiresLoongArch64OrRISCV64 = 782,
    /// <summary>LoongArch64 SIMD12 local-store recording is not ported.</summary>
    LoongArch64SIMD12LocalStoreRecordingIsNotPorted = 783,
    /// <summary>LoongArch64 struct local-field loads are not supported.</summary>
    LoongArch64StructLocalFieldLoadsAreNotSupported = 784,
    /// <summary>Unknown intrinsic reached unreachable code.</summary>
    UnknownIntrinsicReachedUnreachableCode = 785,
    /// <summary>Hardware-intrinsic generation is not ported for this target.</summary>
    HardwareIntrinsicGenerationIsNotPortedForThisTarget = 786,
    /// <summary>LoongArch64 hardware-intrinsic generation is not ported.</summary>
    LoongArch64HardwareIntrinsicGenerationIsNotPorted = 787,
    /// <summary>Zero-register null-check generation requires LoongArch64 or RISC-V64.</summary>
    ZeroRegisterNullCheckGenerationRequiresLoongArch64OrRISCV64 = 788,
    /// <summary>LoongArch64 profiling callbacks are unavailable.</summary>
    LoongArch64ProfilingCallbacksAreUnavailable = 789,
    /// <summary>LoongArch64 GT_SWAP generation is not ported.</summary>
    LoongArch64GTSWAPGenerationIsNotPorted = 790,
    /// <summary>LoongArch64 SIMD is unavailable for this configuration.</summary>
    LoongArch64SIMDIsUnavailableForThisConfiguration = 791,
    /// <summary>LoongArch64 SIMD instruction options are not ported.</summary>
    LoongArch64SIMDInstructionOptionsAreNotPorted = 792,
    /// <summary>LoongArch64 indirect load/store recording requires LoongArch64.</summary>
    LoongArch64IndirectLoadStoreRecordingRequiresLoongArch64 = 795,
    /// <summary>LoongArch64 indirect load/store recording requires an active compiler.</summary>
    LoongArch64IndirectLoadStoreRecordingRequiresAnActiveCompiler = 796,
    /// <summary>LoongArch64 contained address is missing its index tree.</summary>
    LoongArch64ContainedAddressIsMissingItsIndexTree = 797,
    /// <summary>LoongArch64 indexed address is missing its base tree.</summary>
    LoongArch64IndexedAddressIsMissingItsBaseTree = 798,
    /// <summary>Unsupported LoongArch64 indirect load/store size.</summary>
    UnsupportedLoongArch64IndirectLoadStoreSize = 799,
    /// <summary>LoongArch64 contained address is missing its base tree.</summary>
    LoongArch64ContainedAddressIsMissingItsBaseTree = 800,
    /// <summary>GT_ROR-----unimplemented/unused on RISCV64 yet----</summary>
    GTRORUnimplementedUnusedOnRISCV64Yet = 801,
    /// <summary>RISC-V ternary instruction recording is not ported.</summary>
    RISCVTernaryInstructionRecordingIsNotPorted = 802,
    /// <summary>RISC-V64 SIMD12 indirect loads are not ported.</summary>
    RISCV64SIMD12IndirectLoadsAreNotPorted = 803,
    /// <summary>RISC-V64 SIMD12 indirect stores are not ported.</summary>
    RISCV64SIMD12IndirectStoresAreNotPorted = 804,
    /// <summary>RISC-V64 struct local-field loads are not supported.</summary>
    RISCV64StructLocalFieldLoadsAreNotSupported = 805,
    /// <summary>RISC-V64 multi-register local stores are not ported.</summary>
    RISCV64MultiRegisterLocalStoresAreNotPorted = 806,
    /// <summary>RISC-V64 relocatable immediate instruction recording is not ported.</summary>
    RISCV64RelocatableImmediateInstructionRecordingIsNotPorted = 807,
    /// <summary>RISC-V SIMD12 local-store recording is not ported.</summary>
    RISCVSIMD12LocalStoreRecordingIsNotPorted = 808,
    /// <summary>RISC-V64 GT_SWAP generation is not ported.</summary>
    RISCV64GTSWAPGenerationIsNotPorted = 809,
    /// <summary>RISC-V64 SIMD intrinsic generation is not ported.</summary>
    RISCV64SIMDIntrinsicGenerationIsNotPorted = 810,
    /// <summary>RISC-V64 SIMD upper-lane saving is not ported.</summary>
    RISCV64SIMDUpperLaneSavingIsNotPorted = 811,
    /// <summary>RISC-V64 SIMD upper-lane restoration is not ported.</summary>
    RISCV64SIMDUpperLaneRestorationIsNotPorted = 812,
    /// <summary>RISC-V64 SIMD12 local stores are not ported.</summary>
    RISCV64SIMD12LocalStoresAreNotPorted = 813,
    /// <summary>RISC-V64 SIMD instruction options are not ported.</summary>
    RISCV64SIMDInstructionOptionsAreNotPorted = 814,
    /// <summary>RISC-V64 SIMD instruction selection is not ported.</summary>
    RISCV64SIMDInstructionSelectionIsNotPorted = 815,
    /// <summary>RISC-V64 SIMD initialization is not ported.</summary>
    RISCV64SIMDInitializationIsNotPorted = 816,
    /// <summary>RISC-V64 SIMD multi-operand initialization is not ported.</summary>
    RISCV64SIMDMultiOperandInitializationIsNotPorted = 817,
    /// <summary>RISC-V64 unary SIMD intrinsic generation is not ported.</summary>
    RISCV64UnarySIMDIntrinsicGenerationIsNotPorted = 818,
    /// <summary>RISC-V64 SIMD widening is not ported.</summary>
    RISCV64SIMDWideningIsNotPorted = 819,
    /// <summary>RISC-V64 SIMD narrowing is not ported.</summary>
    RISCV64SIMDNarrowingIsNotPorted = 820,
    /// <summary>RISC-V64 binary SIMD intrinsic generation is not ported.</summary>
    RISCV64BinarySIMDIntrinsicGenerationIsNotPorted = 821,
    /// <summary>RISC-V64 SIMD get-item intrinsic generation is not ported.</summary>
    RISCV64SIMDGetItemIntrinsicGenerationIsNotPorted = 822,
    /// <summary>RISC-V64 SIMD set-item intrinsic generation is not ported.</summary>
    RISCV64SIMDSetItemIntrinsicGenerationIsNotPorted = 823,
    /// <summary>RISC-V64 SIMD comparison is not ported.</summary>
    RISCV64SIMDComparisonIsNotPorted = 824,
    /// <summary>RISC-V64 dot-product SIMD intrinsic generation is not ported.</summary>
    RISCV64DotProductSIMDIntrinsicGenerationIsNotPorted = 825,
    /// <summary>Wasm internal-register tracking is not ported.</summary>
    WasmInternalRegisterTrackingIsNotPorted = 826,
    /// <summary>Wasm internal-register extraction is not ported.</summary>
    WasmInternalRegisterExtractionIsNotPorted = 827,
    /// <summary>Wasm finally call is missing its return block.</summary>
    WasmFinallyCallIsMissingItsReturnBlock = 829,
    /// <summary>Wasm hardware intrinsic memory node has no address.</summary>
    WasmHardwareIntrinsicMemoryNodeHasNoAddress = 830,
    /// <summary>Unsupported Wasm integer comparison.</summary>
    UnsupportedWasmIntegerComparison = 831,
    /// <summary>Unsupported Wasm floating-point comparison.</summary>
    UnsupportedWasmFloatingPointComparison = 832,
    /// <summary>GT_IND base is unavailable for null-check codegen.</summary>
    GTINDBaseIsUnavailableForNullCheckCodegen = 833,
    /// <summary>GT_STOREIND base is unavailable for null-check codegen.</summary>
    GTSTOREINDBaseIsUnavailableForNullCheckCodegen = 834,
    /// <summary>Call requiring a null check has no this argument.</summary>
    CallRequiringANullCheckHasNoThisArgument = 835,
    /// <summary>Early call argument has no node.</summary>
    EarlyCallArgumentHasNoNode = 836,
    /// <summary>Late call argument has no node.</summary>
    LateCallArgumentHasNoNode = 837,
    /// <summary>Wasm helper {0} has no hard-coded signature.</summary>
    WasmHelperArgumentHasNoHardCodedSignature = 838,
    /// <summary>Invalid WebAssembly value type.</summary>
    InvalidWebAssemblyValueType = 839,
    /// <summary>Wasm {0} is not ported for {1}.</summary>
    WasmArgumentIsNotPortedForArgument = 840,
    /// <summary>Wasm {0} is not ported.</summary>
    WasmArgumentIsNotPorted = 841,
    /// <summary>Wasm local declarations are missing.</summary>
    WasmLocalDeclarationsAreMissing = 842,
    /// <summary>Unsupported Wasm bitcast.</summary>
    UnsupportedWasmBitcast = 843,
    /// <summary>Wasm funclet local declarations are missing.</summary>
    WasmFuncletLocalDeclarationsAreMissing = 844,
    /// <summary>Unexpected Wasm value type: {0}.</summary>
    UnexpectedWasmValueTypeArgument = 846,
    /// <summary>SIMD12 register clearing requires xarch SIMD support.</summary>
    SIMD12RegisterClearingRequiresXarchSIMDSupport = 847,
    /// <summary>Comparison flag reuse requires xarch.</summary>
    ComparisonFlagReuseRequiresXarch = 848,
    /// <summary>Boolean condition generation requires AMD64.</summary>
    BooleanConditionGenerationRequiresAMD64 = 849,
    /// <summary>SIMD constants require SIMD support.</summary>
    SIMDConstantsRequireSIMDSupport = 850,
    /// <summary>Mask constants require masked hardware-intrinsic support.</summary>
    MaskConstantsRequireMaskedHardwareIntrinsicSupport = 851,
    /// <summary>Callee-save restoration requires xarch.</summary>
    CalleeSaveRestorationRequiresXarch = 852,
    /// <summary>Root epilog generation requires xarch.</summary>
    RootEpilogGenerationRequiresXarch = 853,
    /// <summary>An indirect helper call requires an explicit target register on this architecture.</summary>
    IndirectHelperCallRequiresAnExplicitTargetRegisterOnThisArchitecture = 854,
    /// <summary>Address relocation hints require AMD64.</summary>
    AddressRelocationHintsRequireAMD64 = 855,
    /// <summary>SIMD12 indirect loads require SIMD support.</summary>
    SIMD12IndirectLoadsRequireSIMDSupport = 856,
    /// <summary>Finite-check code generation requires xarch.</summary>
    FiniteCheckCodeGenerationRequiresXarch = 857,
    /// <summary>SIMD12 local loads require SIMD support.</summary>
    SIMD12LocalLoadsRequireSIMDSupport = 858,
    /// <summary>Local-store lifetime updates use a separate target implementation.</summary>
    LocalStoreLifetimeUpdatesUseASeparateTargetImplementation = 859,
    /// <summary>SIMD12 local stores require SIMD support.</summary>
    SIMD12LocalStoresRequireSIMDSupport = 860,
    /// <summary>Scalar operation instruction selection requires xarch or ARM.</summary>
    ScalarOperationInstructionSelectionRequiresXarchOrARM = 861,
    /// <summary>Long shift generation requires x86.</summary>
    LongShiftGenerationRequiresX86 = 862,
    /// <summary>AddCodeDsc.Dump is only available in Debug builds.</summary>
    AddCodeDscDumpIsOnlyAvailableInDebugBuilds = 863,
    /// <summary>An item with the same key has already been added.</summary>
    ItemWithTheSameKeyHasAlreadyBeenAdded = 865,
    /// <summary>Varargs register mapping requires Windows AMD64.</summary>
    VarargsRegisterMappingRequiresWindowsAMD64 = 866,
    /// <summary>ARM64 hardware intrinsic support is not enabled.</summary>
    ARM64HardwareIntrinsicSupportIsNotEnabled = 867,
    /// <summary>MD-array morphing requires the Windows AMD64 path.</summary>
    MDArrayMorphingRequiresTheWindowsAMD64Path = 868,
    /// <summary>SIMD assertions are unsupported on this target.</summary>
    SIMDAssertionsAreUnsupportedOnThisTarget = 873,
    /// <summary>Async analysis requires an entry block</summary>
    AsyncAnalysisRequiresAnEntryBlock = 874,
    /// <summary>Async analysis requires tracked locals</summary>
    AsyncAnalysisRequiresTrackedLocals = 875,
    /// <summary>Async call result has no use</summary>
    AsyncCallResultHasNoUse = 876,
    /// <summary>Async call result has no LIR use</summary>
    AsyncCallResultHasNoLIRUse = 877,
    /// <summary>Async call result has no local store</summary>
    AsyncCallResultHasNoLocalStore = 878,
    /// <summary>Async call has no insertion point</summary>
    AsyncCallHasNoInsertionPoint = 879,
    /// <summary>Morphed async helper lost its argument placeholder</summary>
    MorphedAsyncHelperLostItsArgumentPlaceholder = 880,
    /// <summary>Unregistered inline-frame continuation member</summary>
    UnregisteredInlineFrameContinuationMember = 881,
    /// <summary>Missing async execution context</summary>
    MissingAsyncExecutionContext = 882,
    /// <summary>Missing async synchronization context</summary>
    MissingAsyncSynchronizationContext = 883,
    /// <summary>Missing async resumed indicator</summary>
    MissingAsyncResumedIndicator = 884,
    /// <summary>Async awaiter has no signature layout</summary>
    AsyncAwaiterHasNoSignatureLayout = 885,
    /// <summary>Async awaiter has no allocated continuation member</summary>
    AsyncAwaiterHasNoAllocatedContinuationMember = 886,
    /// <summary>Implicit-byref local has no layout</summary>
    ImplicitByrefLocalHasNoLayout = 887,
    /// <summary>Async method has no entry block</summary>
    AsyncMethodHasNoEntryBlock = 888,
    /// <summary>Async liveness has no tracked-local mapping</summary>
    AsyncLivenessHasNoTrackedLocalMapping = 889,
    /// <summary>Unallocated continuation member offset</summary>
    UnallocatedContinuationMemberOffset = 890,
    /// <summary>Continuation member offset missing from final LIR</summary>
    ContinuationMemberOffsetMissingFromFinalLIR = 891,
    /// <summary>Inlined async frame context chain is inconsistent</summary>
    InlinedAsyncFrameContextChainIsInconsistent = 892,
    /// <summary>Inlined async frame is missing a context argument</summary>
    InlinedAsyncFrameIsMissingAContextArgument = 893,
    /// <summary>Inlined frame tail requires a return block</summary>
    InlinedFrameTailRequiresAReturnBlock = 894,
    /// <summary>Inline frame tail member was never registered</summary>
    InlineFrameTailMemberWasNeverRegistered = 895,
    /// <summary>Unrecognized inline-frame context handling</summary>
    UnrecognizedInlineFrameContextHandling = 896,
    /// <summary>An outstanding LIR edge has no use</summary>
    OutstandingLIREdgeHasNoUse = 897,
    /// <summary>Async resumed-def argument must be a local address</summary>
    AsyncResumedDefArgumentMustBeALocalAddress = 898,
    /// <summary>Terminator block is empty</summary>
    TerminatorBlockIsEmpty = 899,
    /// <summary>Async result requires a local definition</summary>
    AsyncResultRequiresALocalDefinition = 900,
    /// <summary>Reusable state lacks its return definition</summary>
    ReusableStateLacksItsReturnDefinition = 901,
    /// <summary>Common resumed def has no call argument</summary>
    CommonResumedDefHasNoCallArgument = 902,
    /// <summary>Shared context handling requires a return block</summary>
    SharedContextHandlingRequiresAReturnBlock = 903,
    /// <summary>Async state has no resumed indicator</summary>
    AsyncStateHasNoResumedIndicator = 904,
    /// <summary>Async state has no execution context</summary>
    AsyncStateHasNoExecutionContext = 905,
    /// <summary>Async state has no synchronization context</summary>
    AsyncStateHasNoSynchronizationContext = 906,
    /// <summary>No async states were created</summary>
    NoAsyncStatesWereCreated = 907,
    /// <summary>Block weights require a DFS tree.</summary>
    BlockWeightsRequireADFSTree = 908,
    /// <summary>Block weights require natural loops.</summary>
    BlockWeightsRequireNaturalLoops = 909,
    /// <summary>Block weights require an entry block.</summary>
    BlockWeightsRequireAnEntryBlock = 910,
    /// <summary>Loop weights require dominators.</summary>
    LoopWeightsRequireDominators = 911,
    /// <summary>CSE diagnostic configuration is only available in Debug builds.</summary>
    CSEDiagnosticConfigurationIsOnlyAvailableInDebugBuilds = 914,
    /// <summary>Loop-hoist statistics are only available in Debug builds.</summary>
    LoopHoistStatisticsAreOnlyAvailableInDebugBuilds = 915,
    /// <summary>The loop-hoist statistics lock is unavailable.</summary>
    LoopHoistStatisticsLockIsUnavailable = 916,
    /// <summary>Copy-propagation definition nodes are only available in Debug builds.</summary>
    CopyPropagationDefinitionNodesAreOnlyAvailableInDebugBuilds = 917,
    /// <summary>Copy propagation lost a live definition.</summary>
    CopyPropagationLostALiveDefinition = 918,
    /// <summary>Copy propagation requires definitions pushed through its stack helper.</summary>
    CopyPropagationRequiresDefinitionsPushedThroughItsStackHelper = 919,
    /// <summary>Copy propagation requires dominators.</summary>
    CopyPropagationRequiresDominators = 920,
    /// <summary>Compiler debug-break state is only available in Debug builds.</summary>
    CompilerDebugBreakStateIsOnlyAvailableInDebugBuilds = 921,
    /// <summary>Deferred profile inconsistency state is only available in Debug builds.</summary>
    DeferredProfileInconsistencyStateIsOnlyAvailableInDebugBuilds = 922,
    /// <summary>Local debug flags are only available in Debug builds.</summary>
    LocalDebugFlagsAreOnlyAvailableInDebugBuilds = 923,
    /// <summary>The do-not-enregister reason is only available in Debug builds.</summary>
    DoNotEnregisterReasonIsOnlyAvailableInDebugBuilds = 924,
    /// <summary>Address-exposed reason is only available in Debug builds.</summary>
    AddressExposedReasonIsOnlyAvailableInDebugBuilds = 925,
    /// <summary>The single-definition disqualification reason is only available in Debug builds.</summary>
    SingleDefinitionDisqualificationReasonIsOnlyAvailableInDebugBuilds = 926,
    /// <summary>Compiler debug data is only available in Debug builds.</summary>
    CompilerDebugDataIsOnlyAvailableInDebugBuilds = 927,
    /// <summary>Tree IDs are only available in Debug builds.</summary>
    TreeIDsAreOnlyAvailableInDebugBuilds = 929,
    /// <summary>Inline size diagnostics are only available in Debug builds.</summary>
    InlineSizeDiagnosticsAreOnlyAvailableInDebugBuilds = 930,
    /// <summary>Basic block dump order is only available in Debug builds.</summary>
    BasicBlockDumpOrderIsOnlyAvailableInDebugBuilds = 931,
    /// <summary>Inline method diagnostics are only available in Debug builds.</summary>
    InlineMethodDiagnosticsAreOnlyAvailableInDebugBuilds = 932,
    /// <summary>EH normalization diagnostics are only available in Debug builds.</summary>
    EHNormalizationDiagnosticsAreOnlyAvailableInDebugBuilds = 933,
    /// <summary>Basic-block creation state is only available in Debug builds.</summary>
    BasicBlockCreationStateIsOnlyAvailableInDebugBuilds = 934,
    /// <summary>Flow-edge creation state is only available in Debug builds.</summary>
    FlowEdgeCreationStateIsOnlyAvailableInDebugBuilds = 935,
    /// <summary>Call-finally range ended prematurely.</summary>
    CallFinallyRangeEndedPrematurely = 936,
    /// <summary>A call-finally pair requires its leave block.</summary>
    CallFinallyPairRequiresItsLeaveBlock = 937,
    /// <summary>Try region ended prematurely.</summary>
    TryRegionEndedPrematurely = 938,
    /// <summary>Handler region ended prematurely.</summary>
    HandlerRegionEndedPrematurely = 939,
    /// <summary>Finally return requires a statement.</summary>
    FinallyReturnRequiresAStatement = 940,
    /// <summary>Finally return targets must be initialized.</summary>
    FinallyReturnTargetsMustBeInitialized = 941,
    /// <summary>Unexpected block kind {0}.</summary>
    UnexpectedBlockKindArgument = 942,
    /// <summary>Filter region ended prematurely.</summary>
    FilterRegionEndedPrematurely = 943,
    /// <summary>lvaAlignFrame is not supported for this target.</summary>
    LvaAlignFrameIsNotSupportedForThisTarget = 944,
    /// <summary>Reserved-register frame checks are unavailable for this target.</summary>
    ReservedRegisterFrameChecksAreUnavailableForThisTarget = 945,
    /// <summary>Pre-spilled register arguments are unavailable for this target.</summary>
    PreSpilledRegisterArgumentsAreUnavailableForThisTarget = 946,
    /// <summary>Finally handler range ended prematurely.</summary>
    FinallyHandlerRangeEndedPrematurely = 947,
    /// <summary>A subsequent clone needs its predecessor.</summary>
    SubsequentCloneNeedsItsPredecessor = 948,
    /// <summary>A finally return requires a statement.</summary>
    FinallyReturnRequiresAStatement2 = 949,
    /// <summary>The recorded operand is no longer present.</summary>
    RecordedOperandIsNoLongerPresent = 950,
    /// <summary>The visited operand is not owned by its parent.</summary>
    VisitedOperandIsNotOwnedByItsParent = 951,
    /// <summary>A block with a last statement must have a first statement.</summary>
    BlockWithALastStatementMustHaveAFirstStatement = 952,
    /// <summary>Filter region has no first block.</summary>
    FilterRegionHasNoFirstBlock = 953,
    /// <summary>Filter region has no last block.</summary>
    FilterRegionHasNoLastBlock = 954,
    /// <summary>Flow graph loop dumping requires natural loops.</summary>
    FlowGraphLoopDumpingRequiresNaturalLoops = 955,
    /// <summary>Native XML flow-graph jump-kind table has no defined entry for this block kind (B350).</summary>
    NativeXMLFlowGraphJumpKindTableHasNoDefinedEntryForThisBlockKindB350 = 956,
    /// <summary>lvaToInitialSPRelativeOffset is not implemented for this target.</summary>
    LvaToInitialSPRelativeOffsetIsNotImplementedForThisTarget = 957,
    /// <summary>GS shadow analysis requires a local table.</summary>
    GSShadowAnalysisRequiresALocalTable = 958,
    /// <summary>GS shadow copies require a local table.</summary>
    GSShadowCopiesRequireALocalTable = 959,
    /// <summary>GS shadow rewriting requires a local table.</summary>
    GSShadowRewritingRequiresALocalTable = 960,
    /// <summary>IJW struct-argument shadow copies require the x86 special-copy helper.</summary>
    IJWStructArgumentShadowCopiesRequireTheX86SpecialCopyHelper = 961,
    /// <summary>GS shadow copies require an entry block.</summary>
    GSShadowCopiesRequireAnEntryBlock = 962,
    /// <summary>Hardware-intrinsic import without hardware-intrinsic and SIMD support.</summary>
    HardwareIntrinsicImportWithoutHardwareIntrinsicAndSIMDSupport = 963,
    /// <summary>ARM64 immediate discovery requires ARM64 hardware intrinsics.</summary>
    ARM64ImmediateDiscoveryRequiresARM64HardwareIntrinsics = 964,
    /// <summary>ARM64 hardware-intrinsic fallback requires ARM64 hardware intrinsics.</summary>
    ARM64HardwareIntrinsicFallbackRequiresARM64HardwareIntrinsics = 965,
    /// <summary>Unreachable ARM64 fallback intrinsic.</summary>
    UnreachableARM64FallbackIntrinsic = 966,
    /// <summary>ARM64 hardware-intrinsic special import requires ARM64 hardware intrinsics.</summary>
    ARM64HardwareIntrinsicSpecialImportRequiresARM64HardwareIntrinsics = 967,
    /// <summary>Unreachable ARM64 SIMD element size.</summary>
    UnreachableARM64SIMDElementSize = 968,
    /// <summary>SIMD creation requires hardware intrinsics.</summary>
    SIMDCreationRequiresHardwareIntrinsics = 969,
    /// <summary>Hardware-intrinsic special import requires hardware intrinsics.</summary>
    HardwareIntrinsicSpecialImportRequiresHardwareIntrinsics = 970,
    /// <summary>Hardware-intrinsic special import outside xarch is not ported.</summary>
    HardwareIntrinsicSpecialImportOutsideXarchIsNotPorted = 971,
    /// <summary>Hardware-intrinsic nonconstant fallback requires hardware intrinsics.</summary>
    HardwareIntrinsicNonconstantFallbackRequiresHardwareIntrinsics = 972,
    /// <summary>Unreachable Wasm hardware-intrinsic nonconstant fallback.</summary>
    UnreachableWasmHardwareIntrinsicNonconstantFallback = 973,
    /// <summary>Hardware-intrinsic nonconstant fallback outside xarch is not ported.</summary>
    HardwareIntrinsicNonconstantFallbackOutsideXarchIsNotPorted = 974,
    /// <summary>Xarch hardware-intrinsic fallback requires xarch hardware intrinsics.</summary>
    XarchHardwareIntrinsicFallbackRequiresXarchHardwareIntrinsics = 975,
    /// <summary>Hardware-intrinsic immediate discovery requires hardware intrinsics.</summary>
    HardwareIntrinsicImmediateDiscoveryRequiresHardwareIntrinsics = 976,
    /// <summary>Hardware-intrinsic immediate discovery outside xarch.</summary>
    HardwareIntrinsicImmediateDiscoveryOutsideXarch = 977,
    /// <summary>Xarch hardware-intrinsic special import requires xarch hardware intrinsics.</summary>
    XarchHardwareIntrinsicSpecialImportRequiresXarchHardwareIntrinsics = 978,
    /// <summary>Wasm hardware-intrinsic special import requires Wasm hardware intrinsics.</summary>
    WasmHardwareIntrinsicSpecialImportRequiresWasmHardwareIntrinsics = 979,
    /// <summary>Cross-platform intrinsic import requires a supported hardware-intrinsic target.</summary>
    CrossPlatformIntrinsicImportRequiresASupportedHardwareIntrinsicTarget = 980,
    /// <summary>Unsupported SIMD narrowing target.</summary>
    UnsupportedSIMDNarrowingTarget = 985,
    /// <summary>Unsupported SIMD shift target.</summary>
    UnsupportedSIMDShiftTarget = 986,
    /// <summary>Saturating SIMD operations require a supported hardware-intrinsic target.</summary>
    SaturatingSIMDOperationsRequireASupportedHardwareIntrinsicTarget = 987,
    /// <summary>Unsupported saturating SIMD target.</summary>
    UnsupportedSaturatingSIMDTarget = 989,
    /// <summary>Hardware intrinsic range checking is not enabled.</summary>
    HardwareIntrinsicRangeCheckingIsNotEnabled = 990,
    /// <summary>Unsupported or unset target architecture.</summary>
    UnsupportedOrUnsetTargetArchitecture = 991,
    /// <summary>Inline candidate call has no import statement.</summary>
    InlineCandidateCallHasNoImportStatement = 992,
    /// <summary>Unable to clone the first Math.Min/Max operand.</summary>
    UnableToCloneTheFirstMathMinMaxOperand = 994,
    /// <summary>Unable to clone the Math.Min/Max result.</summary>
    UnableToCloneTheMathMinMaxResult = 995,
    /// <summary>Unable to clone the second Math.Min/Max operand.</summary>
    UnableToCloneTheSecondMathMinMaxOperand = 996,
    /// <summary>An initialized IV cursor needs a recurrence.</summary>
    InitializedIVCursorNeedsARecurrence = 997,
    /// <summary>Post-use placement needs a use statement.</summary>
    PostUsePlacementNeedsAUseStatement = 998,
    /// <summary>The compiler method hash is only available in Debug builds.</summary>
    CompilerMethodHashIsOnlyAvailableInDebugBuilds = 999,
    /// <summary>Varargs float register mapping requires Windows AMD64.</summary>
    VarargsFloatRegisterMappingRequiresWindowsAMD64 = 1000,
    /// <summary>Loop discovery requires a DFS tree.</summary>
    LoopDiscoveryRequiresADFSTree = 1001,
    /// <summary>A forbidden test expression was hoisted.</summary>
    ForbiddenTestExpressionWasHoisted = 1002,
    /// <summary>Loop hoist violated a node test annotation.</summary>
    LoopHoistViolatedANodeTestAnnotation = 1003,
    /// <summary>The SSA definition block must be assigned before loop-invariance analysis.</summary>
    SSADefinitionBlockMustBeAssignedBeforeLoopInvarianceAnalysis = 1004,
    /// <summary>Loop side-effect analysis requires a block-to-loop map.</summary>
    LoopSideEffectAnalysisRequiresABlockToLoopMap = 1005,
    /// <summary>A field address must carry a field sequence.</summary>
    FieldAddressMustCarryAFieldSequence = 1006,
    /// <summary>Compiler display options are only available in Debug builds.</summary>
    CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds = 1007,
    /// <summary>Loop-alignment diagnostic options are only available in Debug builds.</summary>
    LoopAlignmentDiagnosticOptionsAreOnlyAvailableInDebugBuilds = 1008,
    /// <summary>Patchpoint metadata generation is not implemented for this target.</summary>
    PatchpointMetadataGenerationIsNotImplementedForThisTarget = 1009,
    /// <summary>JIT method halting is only available in Debug builds.</summary>
    JITMethodHaltingIsOnlyAvailableInDebugBuilds = 1010,
    /// <summary>Target prolog unwind padding is not ported.</summary>
    TargetPrologUnwindPaddingIsNotPorted = 1011,
    /// <summary>The mapped phi operand has no definition.</summary>
    MappedPhiOperandHasNoDefinition = 1012,
    /// <summary>A non-entry block in the unique-successor scan has no predecessor.</summary>
    NonEntryBlockInTheUniqueSuccessorScanHasNoPredecessor = 1013,
    /// <summary>The flow graph DFS tree is required.</summary>
    FlowGraphDFSTreeIsRequired = 1014,
    /// <summary>Code generation state is required.</summary>
    CodeGenerationStateIsRequired = 1015,
    /// <summary>Unknown target architecture for FEATURE_PARTIAL_SIMD_CALLEE_SAVE.</summary>
    UnknownTargetArchitectureForFEATUREPARTIALSIMDCALLEESAVE = 1016,
    /// <summary>Compiler.raMarkStkVars outside AMD64/ARM64.</summary>
    CompilerRaMarkStkVarsOutsideAMD64ARM64 = 1017,
    /// <summary>Compiler.dumpRegMask outside AMD64.</summary>
    CompilerDumpRegMaskOutsideAMD64 = 1018,
    /// <summary>getCompileTimeVectorTByteLength is unsupported on ARM32.</summary>
    GetCompileTimeVectorTByteLengthIsUnsupportedOnARM32 = 1019,
    /// <summary>Compile-time Vector&lt;T&gt; length is not supported on this target.</summary>
    CompileTimeVectorTLengthIsNotSupportedOnThisTarget = 1020,
    /// <summary>Hardware intrinsic arguments are not supported on this target.</summary>
    HardwareIntrinsicArgumentsAreNotSupportedOnThisTarget = 1021,
    /// <summary>Scope diagnostics are only available in Debug builds.</summary>
    ScopeDiagnosticsAreOnlyAvailableInDebugBuilds = 1022,
    /// <summary>Two-source SIMD shuffles require WebAssembly hardware-intrinsic support.</summary>
    TwoSourceSIMDShufflesRequireWebAssemblyHardwareIntrinsicSupport = 1023,
    /// <summary>SIMD conversion requires hardware intrinsics.</summary>
    SIMDConversionRequiresHardwareIntrinsics = 1024,
    /// <summary>gtNewSimdCvtNode requires its target-specific implementation.</summary>
    GtNewSimdCvtNodeRequiresItsTargetSpecificImplementation = 1025,
    /// <summary>Native SIMD conversion requires hardware intrinsics.</summary>
    NativeSIMDConversionRequiresHardwareIntrinsics = 1026,
    /// <summary>gtNewSimdCvtNativeNode requires its target-specific implementation.</summary>
    GtNewSimdCvtNativeNodeRequiresItsTargetSpecificImplementation = 1027,
    /// <summary>SIMD ceiling requires hardware intrinsics.</summary>
    SIMDCeilingRequiresHardwareIntrinsics = 1028,
    /// <summary>gtNewSimdCeilNode requires its target-specific implementation.</summary>
    GtNewSimdCeilNodeRequiresItsTargetSpecificImplementation = 1029,
    /// <summary>SIMD floor requires hardware intrinsics.</summary>
    SIMDFloorRequiresHardwareIntrinsics = 1030,
    /// <summary>gtNewSimdFloorNode requires its target-specific implementation.</summary>
    GtNewSimdFloorNodeRequiresItsTargetSpecificImplementation = 1031,
    /// <summary>SIMD rounding requires hardware intrinsics.</summary>
    SIMDRoundingRequiresHardwareIntrinsics = 1032,
    /// <summary>gtNewSimdRoundNode requires its target-specific implementation.</summary>
    GtNewSimdRoundNodeRequiresItsTargetSpecificImplementation = 1033,
    /// <summary>SIMD loads require hardware intrinsics.</summary>
    SIMDLoadsRequireHardwareIntrinsics = 1034,
    /// <summary>Aligned SIMD loads require hardware intrinsics.</summary>
    AlignedSIMDLoadsRequireHardwareIntrinsics = 1035,
    /// <summary>gtNewSimdLoadAlignedNode requires its target-specific implementation.</summary>
    GtNewSimdLoadAlignedNodeRequiresItsTargetSpecificImplementation = 1036,
    /// <summary>Non-temporal SIMD loads require hardware intrinsics.</summary>
    NonTemporalSIMDLoadsRequireHardwareIntrinsics = 1037,
    /// <summary>gtNewSimdLoadNonTemporalNode requires its target-specific implementation.</summary>
    GtNewSimdLoadNonTemporalNodeRequiresItsTargetSpecificImplementation = 1038,
    /// <summary>SIMD stores require hardware intrinsics.</summary>
    SIMDStoresRequireHardwareIntrinsics = 1039,
    /// <summary>Aligned SIMD stores require hardware intrinsics.</summary>
    AlignedSIMDStoresRequireHardwareIntrinsics = 1040,
    /// <summary>gtNewSimdStoreAlignedNode requires its target-specific implementation.</summary>
    GtNewSimdStoreAlignedNodeRequiresItsTargetSpecificImplementation = 1041,
    /// <summary>Non-temporal SIMD stores require hardware intrinsics.</summary>
    NonTemporalSIMDStoresRequireHardwareIntrinsics = 1042,
    /// <summary>gtNewSimdStoreNonTemporalNode requires its target-specific implementation.</summary>
    GtNewSimdStoreNonTemporalNodeRequiresItsTargetSpecificImplementation = 1043,
    /// <summary>SIMD dot products require hardware intrinsics.</summary>
    SIMDDotProductsRequireHardwareIntrinsics = 1044,
    /// <summary>SIMD fused multiply-add requires hardware intrinsics.</summary>
    SIMDFusedMultiplyAddRequiresHardwareIntrinsics = 1045,
    /// <summary>Unreachable Wasm SIMD fused multiply-add.</summary>
    UnreachableWasmSIMDFusedMultiplyAdd = 1046,
    /// <summary>gtNewSimdFmaNode requires its target-specific implementation.</summary>
    GtNewSimdFmaNodeRequiresItsTargetSpecificImplementation = 1047,
    /// <summary>SIMD comparisons require hardware intrinsics.</summary>
    SIMDComparisonsRequireHardwareIntrinsics = 1048,
    /// <summary>SIMD ternary logic requires xarch hardware intrinsics.</summary>
    SIMDTernaryLogicRequiresXarchHardwareIntrinsics = 1049,
    /// <summary>SIMD concatenation requires hardware intrinsics.</summary>
    SIMDConcatenationRequiresHardwareIntrinsics = 1050,
    /// <summary>gtNewSimdConcatNode requires its target-specific implementation.</summary>
    GtNewSimdConcatNodeRequiresItsTargetSpecificImplementation = 1051,
    /// <summary>SIMD zip requires hardware intrinsics.</summary>
    SIMDZipRequiresHardwareIntrinsics = 1052,
    /// <summary>gtNewSimdZipNode requires its target-specific implementation.</summary>
    GtNewSimdZipNodeRequiresItsTargetSpecificImplementation = 1053,
    /// <summary>SIMD unzip requires hardware intrinsics.</summary>
    SIMDUnzipRequiresHardwareIntrinsics = 1054,
    /// <summary>gtNewSimdUnzipNode requires its target-specific implementation.</summary>
    GtNewSimdUnzipNodeRequiresItsTargetSpecificImplementation = 1055,
    /// <summary>SIMD reverse requires hardware intrinsics.</summary>
    SIMDReverseRequiresHardwareIntrinsics = 1056,
    /// <summary>Saturating SIMD narrowing requires hardware intrinsics.</summary>
    SaturatingSIMDNarrowingRequiresHardwareIntrinsics = 1057,
    /// <summary>SIMD indices require hardware intrinsics.</summary>
    SIMDIndicesRequireHardwareIntrinsics = 1058,
    /// <summary>SIMD sequences require hardware intrinsics.</summary>
    SIMDSequencesRequireHardwareIntrinsics = 1059,
    /// <summary>Alternating SIMD sequences require hardware intrinsics.</summary>
    AlternatingSIMDSequencesRequireHardwareIntrinsics = 1060,
    /// <summary>SIMD sums require hardware intrinsics.</summary>
    SIMDSumsRequireHardwareIntrinsics = 1061,
    /// <summary>gtNewSimdSumNode requires its target-specific implementation.</summary>
    GtNewSimdSumNodeRequiresItsTargetSpecificImplementation = 1062,
    /// <summary>SIMD hardware intrinsics are unsupported on this target.</summary>
    SIMDHardwareIntrinsicsAreUnsupportedOnThisTarget = 1063,
    /// <summary>SIMD equality is unsupported on this target.</summary>
    SIMDEqualityIsUnsupportedOnThisTarget = 1064,
    /// <summary>SIMD bitwise operations are unsupported on this target.</summary>
    SIMDBitwiseOperationsAreUnsupportedOnThisTarget = 1065,
    /// <summary>Hardware intrinsics are unsupported on this target.</summary>
    HardwareIntrinsicsAreUnsupportedOnThisTarget = 1066,
    /// <summary>SIMD scalar creation is unsupported on this target.</summary>
    SIMDScalarCreationIsUnsupportedOnThisTarget = 1067,
    /// <summary>SIMD scalar extraction is unsupported on this target.</summary>
    SIMDScalarExtractionIsUnsupportedOnThisTarget = 1068,
    /// <summary>Node test data is only available in Debug builds.</summary>
    NodeTestDataIsOnlyAvailableInDebugBuilds = 1070,
    /// <summary>CoreCLR TLS expansion is unsupported for this target.</summary>
    CoreCLRTLSExpansionIsUnsupportedForThisTarget = 1076,
    /// <summary>Three-opt layout requires the DFS tree.</summary>
    ThreeOptLayoutRequiresTheDFSTree = 1077,
    /// <summary>The layout requires an entry block.</summary>
    LayoutRequiresAnEntryBlock = 1078,
    /// <summary>A call-finally head must have a paired tail.</summary>
    CallFinallyHeadMustHaveAPairedTail = 1079,
    /// <summary>Unknown-size local allocation requires SIMD-enabled ARM64.</summary>
    UnknownSizeLocalAllocationRequiresSIMDEnabledARM64 = 1080,
    /// <summary>Unknown-size temporary allocation requires SIMD-enabled ARM64.</summary>
    UnknownSizeTemporaryAllocationRequiresSIMDEnabledARM64 = 1082,
    /// <summary>Cold unwind information was not initialized.</summary>
    ColdUnwindInformationWasNotInitialized = 1084,
    /// <summary>Target unwind reservation is not ported.</summary>
    TargetUnwindReservationIsNotPorted = 1085,
    /// <summary>Target unwind emission is not ported.</summary>
    TargetUnwindEmissionIsNotPorted = 1086,
    /// <summary>Target unwind recording is not ported.</summary>
    TargetUnwindRecordingIsNotPorted = 1087,
    /// <summary>Target epilog unwind recording is not ported.</summary>
    TargetEpilogUnwindRecordingIsNotPorted = 1088,
    /// <summary>Unmapped VN helper: {0}</summary>
    UnmappedVNHelperArgument = 1090,
    /// <summary>Struct local has no layout.</summary>
    StructLocalHasNoLayout2 = 1091,
    /// <summary>Struct store has no layout.</summary>
    StructStoreHasNoLayout = 1092,
    /// <summary>An SSA store has no defining block.</summary>
    SSAStoreHasNoDefiningBlock = 1093,
    /// <summary>Handle {0} in fgValueNumberAddExceptionSet</summary>
    HandleArgumentInFgValueNumberAddExceptionSet = 1095,
    /// <summary>Unexpected saturating intrinsic: {0}</summary>
    UnexpectedSaturatingIntrinsicArgument = 1096,
    /// <summary>Memory intrinsic has no address operand.</summary>
    MemoryIntrinsicHasNoAddressOperand = 1097,
    /// <summary>Unexpected store operator {0}</summary>
    UnexpectedStoreOperatorArgument = 1098,
    /// <summary>Cast nodes use fgValueNumberCastTree instead of GetVNFuncForNode.</summary>
    CastNodesUseFgValueNumberCastTreeInsteadOfGetVNFuncForNode = 1102,
    /// <summary>Unhandled leaf {0} in fgValueNumberTree.</summary>
    UnhandledLeafArgumentInFgValueNumberTree = 1103,
    /// <summary>ARR_ELEM must be morphed before value numbering.</summary>
    ARRELEMMustBeMorphedBeforeValueNumbering = 1104,
    /// <summary>Wasm block reordering requires a preceding block.</summary>
    WasmBlockReorderingRequiresAPrecedingBlock = 1106,
    /// <summary>A conditional Wasm block requires a final LIR node.</summary>
    ConditionalWasmBlockRequiresAFinalLIRNode = 1107,
    /// <summary>A paired Wasm call-finally block has no following block.</summary>
    PairedWasmCallFinallyBlockHasNoFollowingBlock = 1108,
    /// <summary>A Wasm resume pad has no switch edge.</summary>
    WasmResumePadHasNoSwitchEdge = 1109,
    /// <summary>A Wasm continuation is missing its resume pad or switch edge.</summary>
    WasmContinuationIsMissingItsResumePadOrSwitchEdge = 1110,
    /// <summary>Wasm GC-ref candidates were not computed.</summary>
    WasmGCRefCandidatesWereNotComputed = 1112,
    /// <summary>Wasm try-entry repair requires an entry block.</summary>
    WasmTryEntryRepairRequiresAnEntryBlock = 1113,
    /// <summary>A Wasm try-entry dispatcher was not initialized.</summary>
    WasmTryEntryDispatcherWasNotInitialized = 1114,
    /// <summary>A Wasm side-entry target has no assigned index.</summary>
    WasmSideEntryTargetHasNoAssignedIndex = 1115,
    /// <summary>Lowering is unavailable during Wasm virtual-IP setup.</summary>
    LoweringIsUnavailableDuringWasmVirtualIPSetup = 1116,
    /// <summary>Hardware intrinsic assertion propagation is not enabled.</summary>
    HardwareIntrinsicAssertionPropagationIsNotEnabled = 1117,
    /// <summary>SSA store has no value.</summary>
    SSAStoreHasNoValue = 1119,
    /// <summary>SSA store has no defining block.</summary>
    SSAStoreHasNoDefiningBlock2 = 1120,
    /// <summary>Immutable source returned no data.</summary>
    ImmutableSourceReturnedNoData = 1124,
    /// <summary>GS security blocker diagnostics are only available in Debug builds.</summary>
    GSSecurityBlockerDiagnosticsAreOnlyAvailableInDebugBuilds = 1139,
    /// <summary>xarch register metadata is not defined for the configured target.</summary>
    XarchRegisterMetadataIsNotDefinedForTheConfiguredTarget = 1140,
    /// <summary>JIT stress is only available in Debug builds.</summary>
    JITStressIsOnlyAvailableInDebugBuilds = 1141,
    /// <summary>Async inlining stress is only available in Debug builds.</summary>
    AsyncInliningStressIsOnlyAvailableInDebugBuilds = 1142,
    /// <summary>Inline stress is only available in Debug builds.</summary>
    InlineStressIsOnlyAvailableInDebugBuilds = 1143,
    /// <summary>Local variable names are only available in Debug builds.</summary>
    LocalVariableNamesAreOnlyAvailableInDebugBuilds = 1144,
    /// <summary>Register variable names are only available in Debug builds.</summary>
    RegisterVariableNamesAreOnlyAvailableInDebugBuilds = 1145,
    /// <summary>Verbose output is only available in Debug builds.</summary>
    VerboseOutputIsOnlyAvailableInDebugBuilds = 1146,
    /// <summary>Verbose tree output is only available in Debug builds.</summary>
    VerboseTreeOutputIsOnlyAvailableInDebugBuilds = 1147,
    /// <summary>Verbose SSA output is only available in Debug builds.</summary>
    VerboseSSAOutputIsOnlyAvailableInDebugBuilds = 1148,
    /// <summary>Mask registers are not defined for the configured target.</summary>
    MaskRegistersAreNotDefinedForTheConfiguredTarget = 1149,
    /// <summary>SIMD is not enabled for this target.</summary>
    SIMDIsNotEnabledForThisTarget = 1150,
    /// <summary>EVEX debug support checks are unavailable in Release.</summary>
    EVEXDebugSupportChecksAreUnavailableInRelease = 1151,
    /// <summary>SkipMethod is only available in Debug builds.</summary>
    SkipMethodIsOnlyAvailableInDebugBuilds = 1152,
    /// <summary>VN diagnostics require a value number store.</summary>
    VNDiagnosticsRequireAValueNumberStore = 1153,
    /// <summary>Wasm globals are not available on this target.</summary>
    WasmGlobalsAreNotAvailableOnThisTarget = 1154,
    /// <summary>Class size fallback is only available in Debug builds.</summary>
    ClassSizeFallbackIsOnlyAvailableInDebugBuilds = 1155,
    /// <summary>Normal statics are expected to be handled in the importer</summary>
    NormalStaticsAreExpectedToBeHandledInTheImporter = 1156,
    /// <summary>New target requires floating-to-small-integer saturation support.</summary>
    NewTargetRequiresFloatingToSmallIntegerSaturationSupport = 1159,
    /// <summary>unexpected accessType for R2R field access</summary>
    UnexpectedAccessTypeForR2RFieldAccess = 1163,
    /// <summary>Hardware intrinsic morphing is unsupported for this target.</summary>
    HardwareIntrinsicMorphingIsUnsupportedForThisTarget = 1164,
    /// <summary>Unsupported masked intrinsic conversion target.</summary>
    UnsupportedMaskedIntrinsicConversionTarget = 1165,
    /// <summary>Geometric-sequence intrinsic is unsupported for this target.</summary>
    GeometricSequenceIntrinsicIsUnsupportedForThisTarget = 1166,
    /// <summary>Fast tail-call argument checks are not supported on this platform.</summary>
    FastTailCallArgumentChecksAreNotSupportedOnThisPlatform = 1167,
    /// <summary>Statement-link checks are only available in Debug builds.</summary>
    StatementLinkChecksAreOnlyAvailableInDebugBuilds = 1168,
    /// <summary>Profile checks are only available in Debug builds.</summary>
    ProfileChecksAreOnlyAvailableInDebugBuilds = 1169,
    /// <summary>Profile weight checks are only available in Debug builds.</summary>
    ProfileWeightChecksAreOnlyAvailableInDebugBuilds = 1170,
    /// <summary>Incoming profile checks are only available in Debug builds.</summary>
    IncomingProfileChecksAreOnlyAvailableInDebugBuilds = 1171,
    /// <summary>Outgoing profile checks are only available in Debug builds.</summary>
    OutgoingProfileChecksAreOnlyAvailableInDebugBuilds = 1172,
    /// <summary>Profile edge formatting is only available in Debug builds.</summary>
    ProfileEdgeFormattingIsOnlyAvailableInDebugBuilds = 1173,
    /// <summary>Tree dumps are only available in Debug builds.</summary>
    TreeDumpsAreOnlyAvailableInDebugBuilds = 1174,
    /// <summary>Filter end block is missing.</summary>
    FilterEndBlockIsMissing = 1175,
    /// <summary>Basic block lookup invalidation is only available in Debug builds.</summary>
    BasicBlockLookupInvalidationIsOnlyAvailableInDebugBuilds = 1176,
    /// <summary>Basic block table display is only available in Debug builds.</summary>
    BasicBlockTableDisplayIsOnlyAvailableInDebugBuilds = 1177,
    /// <summary>Indirect-call verification is only available in Debug builds.</summary>
    IndirectCallVerificationIsOnlyAvailableInDebugBuilds = 1178,
    /// <summary>Mask conversion folding is not supported on this target.</summary>
    MaskConversionFoldingIsNotSupportedOnThisTarget = 1179,
    /// <summary>Scalable-vector mask folding requires an ARM64 target.</summary>
    ScalableVectorMaskFoldingRequiresAnARM64Target = 1180,
    /// <summary>SIMD shuffles are unsupported on this target.</summary>
    SIMDShufflesAreUnsupportedOnThisTarget = 1183,
    /// <summary>SIMD constant shuffles are unsupported on this target.</summary>
    SIMDConstantShufflesAreUnsupportedOnThisTarget = 1184,
    /// <summary>SIMD variable shuffles are unsupported on this target.</summary>
    SIMDVariableShufflesAreUnsupportedOnThisTarget = 1185,
    /// <summary>Hardware intrinsic tree cloning is unsupported on this target.</summary>
    HardwareIntrinsicTreeCloningIsUnsupportedOnThisTarget = 1186,
    /// <summary>Invalid integral constant cast destination.</summary>
    InvalidIntegralConstantCastDestination = 1187,
    /// <summary>Invalid long constant cast destination.</summary>
    InvalidLongConstantCastDestination = 1188,
    /// <summary>Invalid floating constant cast destination.</summary>
    InvalidFloatingConstantCastDestination = 1189,
    /// <summary>Scalable mask constants are only supported on ARM64.</summary>
    ScalableMaskConstantsAreOnlySupportedOnARM64 = 1190,
    /// <summary>SIMD absolute value is unsupported on this target.</summary>
    SIMDAbsoluteValueIsUnsupportedOnThisTarget = 1191,
    /// <summary>SIMD square root is unsupported on this target.</summary>
    SIMDSquareRootIsUnsupportedOnThisTarget = 1192,
    /// <summary>SIMD comparison reductions are unsupported on this target.</summary>
    SIMDComparisonReductionsAreUnsupportedOnThisTarget = 1193,
    /// <summary>SIMD conditional select is unsupported on this target.</summary>
    SIMDConditionalSelectIsUnsupportedOnThisTarget = 1194,
    /// <summary>Masked hardware intrinsic conversion is unsupported in this configuration.</summary>
    MaskedHardwareIntrinsicConversionIsUnsupportedInThisConfiguration = 1195,
    /// <summary>Mask-to-vector conversion is unsupported on this target.</summary>
    MaskToVectorConversionIsUnsupportedOnThisTarget = 1196,
    /// <summary>Vector-to-mask conversion is unsupported on this target.</summary>
    VectorToMaskConversionIsUnsupportedOnThisTarget = 1197,
    /// <summary>SIMD get-lower is unsupported on this target.</summary>
    SIMDGetLowerIsUnsupportedOnThisTarget = 1198,
    /// <summary>Wasm vector get-lower support is not ported.</summary>
    WasmVectorGetLowerSupportIsNotPorted = 1199,
    /// <summary>SIMD get-upper is unsupported on this target.</summary>
    SIMDGetUpperIsUnsupportedOnThisTarget = 1200,
    /// <summary>Wasm vector get-upper support is not ported.</summary>
    WasmVectorGetUpperSupportIsNotPorted = 1201,
    /// <summary>SIMD native min/max is unsupported on this target.</summary>
    SIMDNativeMinMaxIsUnsupportedOnThisTarget = 1202,
    /// <summary>SIMD narrowing is unsupported on this target.</summary>
    SIMDNarrowingIsUnsupportedOnThisTarget = 1203,
    /// <summary>SIMD operation is not supported on this target.</summary>
    SIMDOperationIsNotSupportedOnThisTarget = 1204,
    /// <summary>Wasm vector with-lower support is not ported.</summary>
    WasmVectorWithLowerSupportIsNotPorted = 1205,
    /// <summary>Wasm vector with-upper support is not ported.</summary>
    WasmVectorWithUpperSupportIsNotPorted = 1206,
    /// <summary>Invalid scalable constant element size.</summary>
    InvalidScalableConstantElementSize = 1207,
    /// <summary>String constant costs are unsupported on this target.</summary>
    StringConstantCostsAreUnsupportedOnThisTarget = 1208,
    /// <summary>32-bit long constant costs are unsupported on this target.</summary>
    Failure32BitLongConstantCostsAreUnsupportedOnThisTarget = 1209,
    /// <summary>Integer constant costs are unsupported on this target.</summary>
    IntegerConstantCostsAreUnsupportedOnThisTarget = 1210,
    /// <summary>Floating-point constant costs are unsupported on this target.</summary>
    FloatingPointConstantCostsAreUnsupportedOnThisTarget = 1211,
    /// <summary>Cast cost estimation is unsupported on this target.</summary>
    CastCostEstimationIsUnsupportedOnThisTarget = 1212,
    /// <summary>JitTestLabel.Mark importer is only available in Debug builds.</summary>
    JitTestLabelMarkImporterIsOnlyAvailableInDebugBuilds = 1213,
    /// <summary>Importer IL-offset tracking is only available in Debug builds.</summary>
    ImporterILOffsetTrackingIsOnlyAvailableInDebugBuilds = 1214,
    /// <summary>Implicit-byref poisoning is only available in Debug builds.</summary>
    ImplicitByrefPoisoningIsOnlyAvailableInDebugBuilds = 1215,
    /// <summary>Unsupported platform</summary>
    UnsupportedPlatform = 1216,
    /// <summary>SIMD Half operations are unsupported on this target.</summary>
    SIMDHalfOperationsAreUnsupportedOnThisTarget = 1217,
    /// <summary>Unsupported Half hardware intrinsic platform</summary>
    UnsupportedHalfHardwareIntrinsicPlatform = 1218,
    /// <summary>ARM frame addressing is unavailable on this target.</summary>
    ARMFrameAddressingIsUnavailableOnThisTarget = 1219,
    /// <summary>SIMD12 local widening is unsupported on this target.</summary>
    SIMD12LocalWideningIsUnsupportedOnThisTarget = 1220,
    /// <summary>SIMD initialization temps are unsupported on this target.</summary>
    SIMDInitializationTempsAreUnsupportedOnThisTarget = 1221,
    /// <summary>SIMD type classification is unsupported on this target.</summary>
    SIMDTypeClassificationIsUnsupportedOnThisTarget = 1223,
    /// <summary>Unexpected loop comparison: {0}</summary>
    UnexpectedLoopComparisonArgument = 1224,
    /// <summary>Unwind information has not been initialized.</summary>
    UnwindInformationHasNotBeenInitialized = 1225,
    /// <summary>The LIR range does not reach its last node.</summary>
    LIRRangeDoesNotReachItsLastNode = 1227,
    /// <summary>No compiler is active on this thread.</summary>
    NoCompilerIsActiveOnThisThread = 1228,
    /// <summary>Debugger lookups are only available in Debug builds.</summary>
    DebuggerLookupsAreOnlyAvailableInDebugBuilds = 1229,
    /// <summary>GenTree::s_gtNodeSizes native allocation sizes are not ported.</summary>
    GenTreeSGtNodeSizesNativeAllocationSizesAreNotPorted = 1230,
    /// <summary>Native sizeof(GenTreeLclFld) is not ported.</summary>
    NativeSizeofGenTreeLclFldIsNotPorted = 1235,
    /// <summary>Native sizeof(GenTreeCall) is not ported.</summary>
    NativeSizeofGenTreeCallIsNotPorted = 1236,
    /// <summary>genNodeSizeStats collection is not ported.</summary>
    GenNodeSizeStatsCollectionIsNotPorted = 1237,
    /// <summary>genNodeSizeStatsPerFunc collection is not ported.</summary>
    GenNodeSizeStatsPerFuncCollectionIsNotPorted = 1238,
    /// <summary>genTreeNcntHist collection is not ported.</summary>
    GenTreeNcntHistCollectionIsNotPorted = 1239,
    /// <summary>genTreeNsizHist collection is not ported.</summary>
    GenTreeNsizHistCollectionIsNotPorted = 1240,
    /// <summary>Native sizeof(FlowEdge) is not ported.</summary>
    NativeSizeofFlowEdgeIsNotPorted = 1241,
    /// <summary>Integer argument-register indexing outside AMD64/ARM64.</summary>
    IntegerArgumentRegisterIndexingOutsideAMD64ARM64 = 1242,
    /// <summary>Node destruction diagnostics are only available in Debug builds.</summary>
    NodeDestructionDiagnosticsAreOnlyAvailableInDebugBuilds = 1243,
    /// <summary>Unsupported platform for hardware intrinsic range analysis.</summary>
    UnsupportedPlatformForHardwareIntrinsicRangeAnalysis = 1246,
    /// <summary>The local debug reason is only available in Debug builds.</summary>
    LocalDebugReasonIsOnlyAvailableInDebugBuilds = 1249,
    /// <summary>Local register masks require a fixed register set.</summary>
    LocalRegisterMasksRequireAFixedRegisterSet = 1251,
    /// <summary>Unable to allocate the ICorJitInfo timing proxy.</summary>
    UnableToAllocateTheICorJitInfoTimingProxy = 1252,
    /// <summary>The compiler EE handle changed before timing proxy installation.</summary>
    CompilerEEHandleChangedBeforeTimingProxyInstallation = 1253,
    /// <summary>The all-varset operation counter is not initialized.</summary>
    AllVarsetOperationCounterIsNotInitialized = 1254,
    /// <summary>The varset operation counter is not initialized.</summary>
    VarsetOperationCounterIsNotInitialized = 1255,
    /// <summary>Disassembler has not been initialized.</summary>
    DisassemblerHasNotBeenInitialized = 1256,
    /// <summary>CoreDisTools does not support this JIT target.</summary>
    CoreDisToolsDoesNotSupportThisJITTarget = 1258,
    /// <summary>CoreDisTools decoder cleanup requires the FinishDisasm export.</summary>
    CoreDisToolsDecoderCleanupRequiresTheFinishDisasmExport = 1259,
    /// <summary>CoreDisTools decoding requires the DumpInstruction export.</summary>
    CoreDisToolsDecodingRequiresTheDumpInstructionExport = 1260,
    /// <summary>CoreDisTools returned a null buffered output pointer.</summary>
    CoreDisToolsReturnedANullBufferedOutputPointer = 1261,
    /// <summary>CoreDisTools returned invalid buffered output.</summary>
    CoreDisToolsReturnedInvalidBufferedOutput = 1262,
    /// <summary>Late disassembly output is only available in Debug builds.</summary>
    LateDisassemblyOutputIsOnlyAvailableInDebugBuilds = 1263,
    /// <summary>Late disassembly requires the native decoder and symbol callbacks.</summary>
    LateDisassemblyRequiresTheNativeDecoderAndSymbolCallbacks = 1264,
    /// <summary>MSVCDIS does not support this JIT target.</summary>
    MSVCDISDoesNotSupportThisJITTarget = 1265,
    /// <summary>Late disassembly requires DIS::PdisNew and the MSVCDIS C++ callback ABI.</summary>
    LateDisassemblyRequiresDISPdisNewAndTheMSVCDISCCallbackABI = 1266,
    /// <summary>Unknown target for multi-register arguments.</summary>
    UnknownTargetForMultiRegisterArguments = 1267,
    /// <summary>Outgoing EH diagnostics are only available in Debug builds.</summary>
    OutgoingEHDiagnosticsAreOnlyAvailableInDebugBuilds = 1268,
    /// <summary>EH diagnostics require an emitter.</summary>
    EHDiagnosticsRequireAnEmitter = 1269,
    /// <summary>Unknown outgoing EH clause type.</summary>
    UnknownOutgoingEHClauseType = 1270,
    /// <summary>xarch address encoding is not available for this target.</summary>
    XarchAddressEncodingIsNotAvailableForThisTarget = 1271,
    /// <summary>xarch address scale decoding is not available for this target.</summary>
    XarchAddressScaleDecodingIsNotAvailableForThisTarget = 1272,
    /// <summary>xarch address instruction sizing is not available for this target.</summary>
    XarchAddressInstructionSizingIsNotAvailableForThisTarget = 1273,
    /// <summary>DATA16 recording requires xarch.</summary>
    DATA16RecordingRequiresXarch = 1274,
    /// <summary>xarch SIB register encoding is not available for this target.</summary>
    XarchSIBRegisterEncodingIsNotAvailableForThisTarget = 1275,
    /// <summary>ARM64 indexed address/register instruction recording is not ported.</summary>
    ARM64IndexedAddressRegisterInstructionRecordingIsNotPorted = 1276,
    /// <summary>Indexed address/register instruction recording requires xarch.</summary>
    IndexedAddressRegisterInstructionRecordingRequiresXarch = 1277,
    /// <summary>ARM32 address/register instruction options are not ported.</summary>
    ARM32AddressRegisterInstructionOptionsAreNotPorted = 1278,
    /// <summary>ARM64 address/register instruction recording is not ported.</summary>
    ARM64AddressRegisterInstructionRecordingIsNotPorted = 1279,
    /// <summary>Address/register instruction recording requires xarch.</summary>
    AddressRegisterInstructionRecordingRequiresXarch = 1280,
    /// <summary>ARM32 register-register instruction flags are not supported for this target.</summary>
    ARM32RegisterRegisterInstructionFlagsAreNotSupportedForThisTarget = 1282,
    /// <summary>RISC-V64 register-address instruction recording is not ported.</summary>
    RISCV64RegisterAddressInstructionRecordingIsNotPorted = 1283,
    /// <summary>Register/address instruction recording requires xarch.</summary>
    RegisterAddressInstructionRecordingRequiresXarch = 1284,
    /// <summary>Non-destructive binary instruction recording requires xarch.</summary>
    NonDestructiveBinaryInstructionRecordingRequiresXarch = 1285,
    /// <summary>Implicit register-pair destinations are only defined for xarch.</summary>
    ImplicitRegisterPairDestinationsAreOnlyDefinedForXarch = 1286,
    /// <summary>Binary operand recording requires xarch.</summary>
    BinaryOperandRecordingRequiresXarch = 1287,
    /// <summary>Label output requires xarch.</summary>
    LabelOutputRequiresXarch = 1288,
    /// <summary>Call scratch registers are only defined for xarch.</summary>
    CallScratchRegistersAreOnlyDefinedForXarch = 1289,
    /// <summary>No-GC call register mask {0} is not ported for this target.</summary>
    NoGCCallRegisterMaskArgumentIsNotPortedForThisTarget = 1290,
    /// <summary>GC call output requires an active compiler.</summary>
    GCCallOutputRequiresAnActiveCompiler = 1291,
    /// <summary>RISC-V call recording is only supported on RISC-V64.</summary>
    RISCVCallRecordingIsOnlySupportedOnRISCV64 = 1292,
    /// <summary>RISC-V call recording requires an active compiler.</summary>
    RISCVCallRecordingRequiresAnActiveCompiler = 1293,
    /// <summary>RISC-V call recording does not support call kind {0}.</summary>
    RISCVCallRecordingDoesNotSupportCallKindArgument = 1294,
    /// <summary>Unexpected Wasm call type {0}.</summary>
    UnexpectedWasmCallTypeArgument = 1295,
    /// <summary>Call instruction recording is not implemented for this target.</summary>
    CallInstructionRecordingIsNotImplementedForThisTarget = 1296,
    /// <summary>REX.W prefix emission is not implemented for this target.</summary>
    REXWPrefixEmissionIsNotImplementedForThisTarget = 1297,
    /// <summary>Call GC register encoding is not supported for this target.</summary>
    CallGCRegisterEncodingIsNotSupportedForThisTarget = 1298,
    /// <summary>Call GC register encoding is not ported for this target.</summary>
    CallGCRegisterEncodingIsNotPortedForThisTarget = 1299,
    /// <summary>Conditional compare immediates are only supported for x64 and ARM64.</summary>
    ConditionalCompareImmediatesAreOnlySupportedForX64AndARM64 = 1300,
    /// <summary>ARM32 local-variable immediate resolution requires an active compiler.</summary>
    ARM32LocalVariableImmediateResolutionRequiresAnActiveCompiler = 1301,
    /// <summary>Address-mode descriptor allocation requires xarch.</summary>
    AddressModeDescriptorAllocationRequiresXarch = 1302,
    /// <summary>Constant-data chunk lookup requires AMD64 or WebAssembly.</summary>
    ConstantDataChunkLookupRequiresAMD64OrWebAssembly = 1303,
    /// <summary>ARM64 local-pair descriptors are unavailable on this target.</summary>
    ARM64LocalPairDescriptorsAreUnavailableOnThisTarget = 1304,
    /// <summary>RISC-V load-immediate descriptors are unavailable on this target.</summary>
    RISCVLoadImmediateDescriptorsAreUnavailableOnThisTarget = 1305,
    /// <summary>This instruction requires an initialized descriptor with its native layout.</summary>
    ThisInstructionRequiresAnInitializedDescriptorWithItsNativeLayout = 1306,
    /// <summary>Base instruction descriptor size is not yet ported for this target.</summary>
    BaseInstructionDescriptorSizeIsNotYetPortedForThisTarget = 1307,
    /// <summary>Extended instruction descriptor layout is not yet ported.</summary>
    ExtendedInstructionDescriptorLayoutIsNotYetPorted = 1308,
    /// <summary>Instruction descriptor sizes are not yet ported for this target.</summary>
    InstructionDescriptorSizesAreNotYetPortedForThisTarget = 1309,
    /// <summary>The ARM64 call descriptor layout is only supported on ARM64.</summary>
    ARM64CallDescriptorLayoutIsOnlySupportedOnARM64 = 1310,
    /// <summary>The ARM64 call descriptor layout is not established for this ABI.</summary>
    ARM64CallDescriptorLayoutIsNotEstablishedForThisABI = 1311,
    /// <summary>Instruction traversal requires descriptor ownership.</summary>
    InstructionTraversalRequiresDescriptorOwnership = 1312,
    /// <summary>Instruction traversal requires saved descriptor storage.</summary>
    InstructionTraversalRequiresSavedDescriptorStorage = 1313,
    /// <summary>A saved instruction group requires descriptor storage.</summary>
    SavedInstructionGroupRequiresDescriptorStorage = 1314,
    /// <summary>Unwind emitter instruction-size queries require ARM32.</summary>
    UnwindEmitterInstructionSizeQueriesRequireARM32 = 1315,
    /// <summary>Single indirect instruction recording requires xarch.</summary>
    SingleIndirectInstructionRecordingRequiresXarch = 1316,
    /// <summary>Register-memory instruction recording requires xarch.</summary>
    RegisterMemoryInstructionRecordingRequiresXarch = 1317,
    /// <summary>Register-memory-immediate instruction recording requires xarch.</summary>
    RegisterMemoryImmediateInstructionRecordingRequiresXarch = 1318,
    /// <summary>Register-field-immediate instruction recording requires xarch.</summary>
    RegisterFieldImmediateInstructionRecordingRequiresXarch = 1319,
    /// <summary>AVX2 gather instruction recording requires xarch.</summary>
    AVX2GatherInstructionRecordingRequiresXarch = 1320,
    /// <summary>ARM32 immediate memory instruction recording is not ported.</summary>
    ARM32ImmediateMemoryInstructionRecordingIsNotPorted = 1321,
    /// <summary>Immediate memory instruction recording requires xarch.</summary>
    ImmediateMemoryInstructionRecordingRequiresXarch = 1322,
    /// <summary>ARM32 absolute-address instruction recording requires a general register.</summary>
    ARM32AbsoluteAddressInstructionRecordingRequiresAGeneralRegister = 1323,
    /// <summary>ARM32 relocated-address instruction recording is not ported.</summary>
    ARM32RelocatedAddressInstructionRecordingIsNotPorted = 1324,
    /// <summary>Absolute-address instruction recording requires xarch.</summary>
    AbsoluteAddressInstructionRecordingRequiresXarch = 1325,
    /// <summary>Memory-register-immediate recording requires xarch.</summary>
    MemoryRegisterImmediateRecordingRequiresXarch = 1326,
    /// <summary>Instruction input sizes are not available for this target.</summary>
    InstructionInputSizesAreNotAvailableForThisTarget = 1327,
    /// <summary>EVEX displacement compression is not available for this target.</summary>
    EVEXDisplacementCompressionIsNotAvailableForThisTarget = 1328,
    /// <summary>Short jump patching is not supported for this target.</summary>
    ShortJumpPatchingIsNotSupportedForThisTarget = 1329,
    /// <summary>Long jump patching is not supported for this target.</summary>
    LongJumpPatchingIsNotSupportedForThisTarget = 1330,
    /// <summary>The epilog recorder is only supported by the x86 GC encoder.</summary>
    EpilogRecorderIsOnlySupportedByTheX86GCEncoder = 1331,
    /// <summary>Emitter is not initialized.</summary>
    EmitterIsNotInitialized = 1332,
    /// <summary>Execution characteristics outside supported targets are not implemented.</summary>
    ExecutionCharacteristicsOutsideSupportedTargetsAreNotImplemented = 1333,
    /// <summary>xarch execution characteristics are not available for this target.</summary>
    XarchExecutionCharacteristicsAreNotAvailableForThisTarget = 1334,
    /// <summary>Non-debug alignment performance scoring requires late-disassembly placement metadata.</summary>
    NonDebugAlignmentPerformanceScoringRequiresLateDisassemblyPlacementMetadata = 1335,
    /// <summary>Execution-cost estimates are not available for this target.</summary>
    ExecutionCostEstimatesAreNotAvailableForThisTarget = 1336,
    /// <summary>xarch instruction latency data is not available for this target.</summary>
    XarchInstructionLatencyDataIsNotAvailableForThisTarget = 1337,
    /// <summary>xarch instruction throughput data is not available for this target.</summary>
    XarchInstructionThroughputDataIsNotAvailableForThisTarget = 1338,
    /// <summary>Xarch execution memory classification is unavailable on this target.</summary>
    XarchExecutionMemoryClassificationIsUnavailableOnThisTarget = 1339,
    /// <summary>Redundant comparison queries outside xarch are not implemented.</summary>
    RedundantComparisonQueriesOutsideXarchAreNotImplemented = 1340,
    /// <summary>Zero-comparison flag queries outside xarch are not implemented.</summary>
    ZeroComparisonFlagQueriesOutsideXarchAreNotImplemented = 1341,
    /// <summary>Sign-jump flag queries outside xarch are not implemented.</summary>
    SignJumpFlagQueriesOutsideXarchAreNotImplemented = 1342,
    /// <summary>Xarch instruction-flag metadata is not available for this target.</summary>
    XarchInstructionFlagMetadataIsNotAvailableForThisTarget = 1343,
    /// <summary>Unwind emitter fragmentation requires ARM, LoongArch64, or RISC-V64.</summary>
    UnwindEmitterFragmentationRequiresARMLoongArch64OrRISCV64 = 1344,
    /// <summary>GC-delta indentation is not supported for this target.</summary>
    GCDeltaIndentationIsNotSupportedForThisTarget = 1345,
    /// <summary>GC delta diagnostics require an active compiler.</summary>
    GCDeltaDiagnosticsRequireAnActiveCompiler = 1346,
    /// <summary>GC delta diagnostics are only available in Debug builds.</summary>
    GCDeltaDiagnosticsAreOnlyAvailableInDebugBuilds = 1347,
    /// <summary>GC tracking diagnostics are only available in Debug builds.</summary>
    GCTrackingDiagnosticsAreOnlyAvailableInDebugBuilds = 1348,
    /// <summary>GC tracking diagnostics require an active compiler.</summary>
    GCTrackingDiagnosticsRequireAnActiveCompiler = 1349,
    /// <summary>Instruction-group diagnostics require an active compiler.</summary>
    InstructionGroupDiagnosticsRequireAnActiveCompiler = 1350,
    /// <summary>A placeholder instruction group requires placeholder data.</summary>
    PlaceholderInstructionGroupRequiresPlaceholderData = 1351,
    /// <summary>Jump-list diagnostics require descriptor debug information.</summary>
    JumpListDiagnosticsRequireDescriptorDebugInformation = 1352,
    /// <summary>Instruction-group address recording requires xarch, ARM64, RISC-V64, or LoongArch64.</summary>
    InstructionGroupAddressRecordingRequiresXarchARM64RISCV64OrLoongArch64 = 1353,
    /// <summary>Backward instruction validation requires descriptor storage.</summary>
    BackwardInstructionValidationRequiresDescriptorStorage = 1354,
    /// <summary>Immediate-only instruction recording requires xarch.</summary>
    ImmediateOnlyInstructionRecordingRequiresXarch = 1355,
    /// <summary>X86 stack-depth adjustment is not supported for this target.</summary>
    X86StackDepthAdjustmentIsNotSupportedForThisTarget = 1356,
    /// <summary>Register-immediate output requires xarch.</summary>
    RegisterImmediateOutputRequiresXarch = 1357,
    /// <summary>Eight-byte immediate encoding requires AMD64.</summary>
    EightByteImmediateEncodingRequiresAMD64 = 1358,
    /// <summary>Eight-byte immediate output requires AMD64.</summary>
    EightByteImmediateOutputRequiresAMD64 = 1359,
    /// <summary>Immediate output requires xarch.</summary>
    ImmediateOutputRequiresXarch = 1360,
    /// <summary>Indirect-load recording requires xarch.</summary>
    IndirectLoadRecordingRequiresXarch = 1361,
    /// <summary>Indirect-store recording requires xarch.</summary>
    IndirectStoreRecordingRequiresXarch = 1362,
    /// <summary>Contained hardware-intrinsic stores require hardware-intrinsic support.</summary>
    ContainedHardwareIntrinsicStoresRequireHardwareIntrinsicSupport = 1363,
    /// <summary>Emitter GC diagnostic state is only available in Debug builds.</summary>
    EmitterGCDiagnosticStateIsOnlyAvailableInDebugBuilds = 1364,
    /// <summary>Label instruction descriptors are not supported for this target.</summary>
    LabelInstructionDescriptorsAreNotSupportedForThisTarget = 1365,
    /// <summary>LoongArch64 binary instruction recording is unused and not ported.</summary>
    LoongArch64BinaryInstructionRecordingIsUnusedAndNotPorted = 1366,
    /// <summary>Binary operand recording is not supported for this target.</summary>
    BinaryOperandRecordingIsNotSupportedForThisTarget = 1367,
    /// <summary>Ternary instruction recording is not supported for this target.</summary>
    TernaryInstructionRecordingIsNotSupportedForThisTarget = 1368,
    /// <summary>emitDispFrameRef-----unused on LoongArch64.</summary>
    EmitDispFrameRefUnusedOnLoongArch64 = 1369,
    /// <summary>emitDispFrameRef-----unimplemented/unused on RISCV64 yet----</summary>
    EmitDispFrameRefUnimplementedUnusedOnRISCV64Yet = 1370,
    /// <summary>Frame-reference display is not implemented for this target.</summary>
    FrameReferenceDisplayIsNotImplementedForThisTarget = 1371,
    /// <summary>Static xarch reference display requires xarch.</summary>
    StaticXarchReferenceDisplayRequiresXarch = 1372,
    /// <summary>xarch instruction flags are unavailable on this target.</summary>
    XarchInstructionFlagsAreUnavailableOnThisTarget = 1373,
    /// <summary>Instruction update modes outside xarch are not implemented.</summary>
    InstructionUpdateModesOutsideXarchAreNotImplemented = 1374,
    /// <summary>Instruction format selection outside xarch is not implemented.</summary>
    InstructionFormatSelectionOutsideXarchIsNotImplemented = 1375,
    /// <summary>Instruction scheduling metadata outside xarch is not implemented.</summary>
    InstructionSchedulingMetadataOutsideXarchIsNotImplemented = 1376,
    /// <summary>APX instruction classification outside xarch is not implemented.</summary>
    APXInstructionClassificationOutsideXarchIsNotImplemented = 1377,
    /// <summary>Unsupported instruction format target.</summary>
    UnsupportedInstructionFormatTarget = 1378,
    /// <summary>An instruction group must precede the new group.</summary>
    InstructionGroupMustPrecedeTheNewGroup = 1379,
    /// <summary>Instruction groups require an active compiler.</summary>
    InstructionGroupsRequireAnActiveCompiler = 1380,
    /// <summary>xarch instruction tuple information is not available for this target.</summary>
    XarchInstructionTupleInformationIsNotAvailableForThisTarget = 1381,
    /// <summary>xarch instruction tuple data is not available for this target.</summary>
    XarchInstructionTupleDataIsNotAvailableForThisTarget = 1382,
    /// <summary>Signed 20-bit immediates are not supported for this target.</summary>
    Signed20BitImmediatesAreNotSupportedForThisTarget = 1383,
    /// <summary>General-register-or-zero validation is not supported for this target.</summary>
    GeneralRegisterOrZeroValidationIsNotSupportedForThisTarget = 1384,
    /// <summary>Vector-index validation is not supported for this target.</summary>
    VectorIndexValidationIsNotSupportedForThisTarget = 1385,
    /// <summary>Embedded rounding modes are only supported on xarch.</summary>
    EmbeddedRoundingModesAreOnlySupportedOnXarch = 1386,
    /// <summary>EVEX embedded masking is only supported on xarch.</summary>
    EVEXEmbeddedMaskingIsOnlySupportedOnXarch = 1387,
    /// <summary>Instruction output is not implemented for this target.</summary>
    InstructionOutputIsNotImplementedForThisTarget = 1388,
    /// <summary>8-byte APX immediate operands require AMD64.</summary>
    Failure8ByteAPXImmediateOperandsRequireAMD64 = 1389,
    /// <summary>Instruction display is not used on LoongArch64.</summary>
    InstructionDisplayIsNotUsedOnLoongArch64 = 1390,
    /// <summary>Load/store classification is only supported on ARM32 and ARM64.</summary>
    LoadStoreClassificationIsOnlySupportedOnARM32AndARM64 = 1391,
    /// <summary>Instruction sanity checking is not implemented for this target.</summary>
    InstructionSanityCheckingIsNotImplementedForThisTarget = 1392,
    /// <summary>Instruction display is not implemented for this target.</summary>
    InstructionDisplayIsNotImplementedForThisTarget = 1393,
    /// <summary>ARM32 instruction classification is only supported on ARM32.</summary>
    ARM32InstructionClassificationIsOnlySupportedOnARM32 = 1394,
    /// <summary>The current instruction group is required.</summary>
    CurrentInstructionGroupIsRequired = 1395,
    /// <summary>Xarch instruction sizing requires an xarch target.</summary>
    XarchInstructionSizingRequiresAnXarchTarget = 1396,
    /// <summary>WebAssembly local-stack store recording is not implemented.</summary>
    WebAssemblyLocalStackStoreRecordingIsNotImplemented = 1397,
    /// <summary>Target local-stack store recording is not implemented.</summary>
    TargetLocalStackStoreRecordingIsNotImplemented = 1398,
    /// <summary>An unbound LoongArch64 jump requires a basic-block target.</summary>
    UnboundLoongArch64JumpRequiresABasicBlockTarget = 1399,
    /// <summary>A LoongArch64 jump target has no instruction-group cookie.</summary>
    LoongArch64JumpTargetHasNoInstructionGroupCookie = 1400,
    /// <summary>Jump target binding requires an active compiler.</summary>
    JumpTargetBindingRequiresAnActiveCompiler = 1401,
    /// <summary>A bound branch requires an instruction-group target.</summary>
    BoundBranchRequiresAnInstructionGroupTarget = 1402,
    /// <summary>Funclet branch checking requires an active compiler.</summary>
    FuncletBranchCheckingRequiresAnActiveCompiler = 1403,
    /// <summary>Funclet branch validation requires debug information.</summary>
    FuncletBranchValidationRequiresDebugInformation = 1404,
    /// <summary>ARM jump-format classification is not supported for this target.</summary>
    ARMJumpFormatClassificationIsNotSupportedForThisTarget = 1405,
    /// <summary>ARM label-load classification is not supported for this target.</summary>
    ARMLabelLoadClassificationIsNotSupportedForThisTarget = 1406,
    /// <summary>Unconditional jump classification is not supported for this target.</summary>
    UnconditionalJumpClassificationIsNotSupportedForThisTarget = 1407,
    /// <summary>RISC-V jump classification is not supported for this target.</summary>
    RISCVJumpClassificationIsNotSupportedForThisTarget = 1408,
    /// <summary>Compare-jump classification is not supported for this target.</summary>
    CompareJumpClassificationIsNotSupportedForThisTarget = 1409,
    /// <summary>RISC-V compare-jump classification is not supported for this target.</summary>
    RISCVCompareJumpClassificationIsNotSupportedForThisTarget = 1410,
    /// <summary>Medium-jump selection is not supported for this target.</summary>
    MediumJumpSelectionIsNotSupportedForThisTarget = 1411,
    /// <summary>ARM64 constant-load classification is not supported for this target.</summary>
    ARM64ConstantLoadClassificationIsNotSupportedForThisTarget = 1412,
    /// <summary>Wasm has no short-jump instruction form.</summary>
    WasmHasNoShortJumpInstructionForm = 1413,
    /// <summary>Non-xarch short-jump selection is not ported.</summary>
    NonXarchShortJumpSelectionIsNotPorted = 1414,
    /// <summary>Short-jump selection is implemented by the LoongArch64 binder.</summary>
    ShortJumpSelectionIsImplementedByTheLoongArch64Binder = 1415,
    /// <summary>Jump distance binding requires an active compiler.</summary>
    JumpDistanceBindingRequiresAnActiveCompiler = 1416,
    /// <summary>Unknown ARM jump instruction.</summary>
    UnknownARMJumpInstruction = 1417,
    /// <summary>Unknown ARM64 jump instruction.</summary>
    UnknownARM64JumpInstruction = 1418,
    /// <summary>Unknown RISC-V jump instruction.</summary>
    UnknownRISCVJumpInstruction = 1419,
    /// <summary>Jump distance binding is not supported for this target.</summary>
    JumpDistanceBindingIsNotSupportedForThisTarget = 1420,
    /// <summary>Short-jump size update is not supported for this target.</summary>
    ShortJumpSizeUpdateIsNotSupportedForThisTarget = 1421,
    /// <summary>Reverse jump-kind mapping is not supported for this target.</summary>
    ReverseJumpKindMappingIsNotSupportedForThisTarget = 1422,
    /// <summary>ARM jump-kind mapping is not supported for this target.</summary>
    ARMJumpKindMappingIsNotSupportedForThisTarget = 1423,
    /// <summary>Basic-block label address recording is not supported for this target.</summary>
    BasicBlockLabelAddressRecordingIsNotSupportedForThisTarget = 1424,
    /// <summary>Data-label address recording is not supported for this target.</summary>
    DataLabelAddressRecordingIsNotSupportedForThisTarget = 1425,
    /// <summary>Conditional register branches are not supported for this target.</summary>
    ConditionalRegisterBranchesAreNotSupportedForThisTarget = 1426,
    /// <summary>Label jump instruction recording is not supported for this target.</summary>
    LabelJumpInstructionRecordingIsNotSupportedForThisTarget = 1427,
    /// <summary>RISC-V jump recording is not supported for this target.</summary>
    RISCVJumpRecordingIsNotSupportedForThisTarget = 1428,
    /// <summary>RISC-V jump recording requires an active compiler.</summary>
    RISCVJumpRecordingRequiresAnActiveCompiler = 1429,
    /// <summary>RISC-V jump recording requires a current basic block.</summary>
    RISCVJumpRecordingRequiresACurrentBasicBlock = 1430,
    /// <summary>Xarch short-jump selection is not supported for this target.</summary>
    XarchShortJumpSelectionIsNotSupportedForThisTarget = 1431,
    /// <summary>Jump-kind instruction mapping is not supported for this target.</summary>
    JumpKindInstructionMappingIsNotSupportedForThisTarget = 1432,
    /// <summary>Jump-to-next-instruction removal is only available for xarch.</summary>
    JumpToNextInstructionRemovalIsOnlyAvailableForXarch = 1433,
    /// <summary>Zero-operand instruction recording is unsupported for this target ({0}).</summary>
    ZeroOperandInstructionRecordingIsUnsupportedForThisTargetArgument = 1434,
    /// <summary>LoongArch64 emitter operations require LoongArch64.</summary>
    LoongArch64EmitterOperationsRequireLoongArch64 = 1435,
    /// <summary>Local store recording requires xarch.</summary>
    LocalStoreRecordingRequiresXarch = 1436,
    /// <summary>An aligned group requires a final alignment descriptor.</summary>
    AlignedGroupRequiresAFinalAlignmentDescriptor = 1437,
    /// <summary>Loop alignment is unsupported on this target.</summary>
    LoopAlignmentIsUnsupportedOnThisTarget = 1438,
    /// <summary>Memory-barrier coalescing is not supported for this target.</summary>
    MemoryBarrierCoalescingIsNotSupportedForThisTarget = 1439,
    /// <summary>ARM64 static-field immediate instruction recording is not ported.</summary>
    ARM64StaticFieldImmediateInstructionRecordingIsNotPorted = 1440,
    /// <summary>ARM32 static-field immediate instruction recording is not ported.</summary>
    ARM32StaticFieldImmediateInstructionRecordingIsNotPorted = 1441,
    /// <summary>Static-field immediate instruction recording requires xarch.</summary>
    StaticFieldImmediateInstructionRecordingRequiresXarch = 1442,
    /// <summary>Static-field register-immediate instruction recording requires xarch.</summary>
    StaticFieldRegisterImmediateInstructionRecordingRequiresXarch = 1443,
    /// <summary>Register-stack-immediate instruction recording requires xarch.</summary>
    RegisterStackImmediateInstructionRecordingRequiresXarch = 1444,
    /// <summary>Memory operand format mapping requires xarch.</summary>
    MemoryOperandFormatMappingRequiresXarch = 1445,
    /// <summary>Memory operand descriptor initialization requires xarch.</summary>
    MemoryOperandDescriptorInitializationRequiresXarch = 1446,
    /// <summary>Instruction memory format mapping requires xarch.</summary>
    InstructionMemoryFormatMappingRequiresXarch = 1447,
    /// <summary>Address scaling is only supported on xarch.</summary>
    AddressScalingIsOnlySupportedOnXarch = 1448,
    /// <summary>ARM64 static-field instruction recording is not ported.</summary>
    ARM64StaticFieldInstructionRecordingIsNotPorted = 1449,
    /// <summary>ARM32 static-field instruction recording is not ported.</summary>
    ARM32StaticFieldInstructionRecordingIsNotPorted = 1450,
    /// <summary>Single static-field instruction recording requires xarch.</summary>
    SingleStaticFieldInstructionRecordingRequiresXarch = 1451,
    /// <summary>Unary memory instruction recording requires xarch.</summary>
    UnaryMemoryInstructionRecordingRequiresXarch = 1452,
    /// <summary>Two-register memory instruction recording requires xarch.</summary>
    TwoRegisterMemoryInstructionRecordingRequiresXarch = 1453,
    /// <summary>Two-register memory-immediate recording requires xarch.</summary>
    TwoRegisterMemoryImmediateRecordingRequiresXarch = 1454,
    /// <summary>Register-address-register instruction recording requires xarch.</summary>
    RegisterAddressRegisterInstructionRecordingRequiresXarch = 1455,
    /// <summary>ARM32 four-register instruction options are not ported.</summary>
    ARM32FourRegisterInstructionOptionsAreNotPorted = 1456,
    /// <summary>Four-register instruction recording requires xarch.</summary>
    FourRegisterInstructionRecordingRequiresXarch = 1457,
    /// <summary>Address/two-register instruction recording requires xarch.</summary>
    AddressTwoRegisterInstructionRecordingRequiresXarch = 1458,
    /// <summary>Register-register instruction sizing requires xarch.</summary>
    RegisterRegisterInstructionSizingRequiresXarch = 1459,
    /// <summary>Register-immediate instruction sizing requires xarch.</summary>
    RegisterImmediateInstructionSizingRequiresXarch = 1460,
    /// <summary>Two-register static-field instruction recording requires xarch.</summary>
    TwoRegisterStaticFieldInstructionRecordingRequiresXarch = 1461,
    /// <summary>Two-register stack instruction recording requires xarch.</summary>
    TwoRegisterStackInstructionRecordingRequiresXarch = 1462,
    /// <summary>Two-register field-immediate recording requires xarch.</summary>
    TwoRegisterFieldImmediateRecordingRequiresXarch = 1463,
    /// <summary>Two-register stack-immediate recording requires xarch.</summary>
    TwoRegisterStackImmediateRecordingRequiresXarch = 1464,
    /// <summary>Register-field-register instruction recording requires xarch.</summary>
    RegisterFieldRegisterInstructionRecordingRequiresXarch = 1465,
    /// <summary>Register-stack-register instruction recording requires xarch.</summary>
    RegisterStackRegisterInstructionRecordingRequiresXarch = 1466,
    /// <summary>Two-register-immediate instruction recording requires xarch.</summary>
    TwoRegisterImmediateInstructionRecordingRequiresXarch = 1467,
    /// <summary>Illegal instruction within RISC-V two-register-immediate instruction recording.</summary>
    IllegalInstructionWithinRISCVTwoRegisterImmediateInstructionRecording = 1468,
    /// <summary>Illegal instruction within RISC-V register-two-immediate instruction recording.</summary>
    IllegalInstructionWithinRISCVRegisterTwoImmediateInstructionRecording = 1469,
    /// <summary>ARM32 three-register instruction options are not ported.</summary>
    ARM32ThreeRegisterInstructionOptionsAreNotPorted = 1470,
    /// <summary>Illegal instruction within RISC-V three-register instruction recording.</summary>
    IllegalInstructionWithinRISCVThreeRegisterInstructionRecording = 1471,
    /// <summary>Three-register instruction recording requires xarch.</summary>
    ThreeRegisterInstructionRecordingRequiresXarch = 1472,
    /// <summary>Three-register-immediate instruction recording requires xarch.</summary>
    ThreeRegisterImmediateInstructionRecordingRequiresXarch = 1473,
    /// <summary>Three-operand IMUL selection requires x86 architecture support.</summary>
    ThreeOperandIMULSelectionRequiresX86ArchitectureSupport = 1474,
    /// <summary>Three-operand IMUL register layout is not available for this target.</summary>
    ThreeOperandIMULRegisterLayoutIsNotAvailableForThisTarget = 1475,
    /// <summary>Data-size prefix output requires xarch.</summary>
    DataSizePrefixOutputRequiresXarch = 1476,
    /// <summary>NOP output requires xarch.</summary>
    NOPOutputRequiresXarch = 1477,
    /// <summary>Loop-alignment output requires xarch.</summary>
    LoopAlignmentOutputRequiresXarch = 1478,
    /// <summary>An alignment descriptor requires a following loop group.</summary>
    AlignmentDescriptorRequiresAFollowingLoopGroup = 1479,
    /// <summary>Target register instruction recording is not implemented.</summary>
    TargetRegisterInstructionRecordingIsNotImplemented = 1480,
    /// <summary>Target two-register-immediate instruction recording is not implemented.</summary>
    TargetTwoRegisterImmediateInstructionRecordingIsNotImplemented = 1481,
    /// <summary>Target register-immediate instruction recording is not implemented.</summary>
    TargetRegisterImmediateInstructionRecordingIsNotImplemented = 1482,
    /// <summary>Address constant materialization is not implemented for this target.</summary>
    AddressConstantMaterializationIsNotImplementedForThisTarget = 1483,
    /// <summary>RISC-V64 address constant materialization is not ported.</summary>
    RISCV64AddressConstantMaterializationIsNotPorted = 1484,
    /// <summary>Last-instruction call tracking is not supported for this target.</summary>
    LastInstructionCallTrackingIsNotSupportedForThisTarget = 1485,
    /// <summary>Pre-epilog nop insertion is not supported for this target.</summary>
    PreEpilogNopInsertionIsNotSupportedForThisTarget = 1486,
    /// <summary>Instruction recording outside AMD64 is not ported.</summary>
    InstructionRecordingOutsideAMD64IsNotPorted = 1487,
    /// <summary>Exit sequence recording is not implemented for this target.</summary>
    ExitSequenceRecordingIsNotImplementedForThisTarget = 1488,
    /// <summary>General-register classification is not supported for this target.</summary>
    GeneralRegisterClassificationIsNotSupportedForThisTarget = 1489,
    /// <summary>ARM32 double-register validation is not available for this target.</summary>
    ARM32DoubleRegisterValidationIsNotAvailableForThisTarget = 1490,
    /// <summary>EVEX broadcast options are not available for this target.</summary>
    EVEXBroadcastOptionsAreNotAvailableForThisTarget = 1491,
    /// <summary>EVEX embedded rounding is not available for this target.</summary>
    EVEXEmbeddedRoundingIsNotAvailableForThisTarget = 1492,
    /// <summary>EVEX NDD options are not available for this target.</summary>
    EVEXNDDOptionsAreNotAvailableForThisTarget = 1493,
    /// <summary>EVEX NF options are not available for this target.</summary>
    EVEXNFOptionsAreNotAvailableForThisTarget = 1494,
    /// <summary>APX PPX options are not available for this target.</summary>
    APXPPXOptionsAreNotAvailableForThisTarget = 1495,
    /// <summary>EVEX DFV options require AMD64.</summary>
    EVEXDFVOptionsRequireAMD64 = 1496,
    /// <summary>Instruction encodability checks require xarch.</summary>
    InstructionEncodabilityChecksRequireXarch = 1497,
    /// <summary>VEX output requires xarch.</summary>
    VEXOutputRequiresXarch = 1498,
    /// <summary>xarch register/register sizing is not available for this target.</summary>
    XarchRegisterRegisterSizingIsNotAvailableForThisTarget = 1499,
    /// <summary>ARM32 register-immediate instruction options are not ported.</summary>
    ARM32RegisterImmediateInstructionOptionsAreNotPorted = 1500,
    /// <summary>ARM32 register-immediate recording does not accept a target handle.</summary>
    ARM32RegisterImmediateRecordingDoesNotAcceptATargetHandle = 1501,
    /// <summary>Register-immediate instruction recording requires xarch.</summary>
    RegisterImmediateInstructionRecordingRequiresXarch = 1502,
    /// <summary>ARM32 register-immediate instruction flags are not supported for this target.</summary>
    ARM32RegisterImmediateInstructionFlagsAreNotSupportedForThisTarget = 1503,
    /// <summary>ARM32 register-register instruction options are not ported.</summary>
    ARM32RegisterRegisterInstructionOptionsAreNotPorted = 1504,
    /// <summary>RISC-V register-register instruction options are not implemented.</summary>
    RISCVRegisterRegisterInstructionOptionsAreNotImplemented = 1505,
    /// <summary>Register-register instruction recording requires xarch.</summary>
    RegisterRegisterInstructionRecordingRequiresXarch = 1506,
    /// <summary>APX NDD move recording requires xarch.</summary>
    APXNDDMoveRecordingRequiresXarch = 1507,
    /// <summary>Register move recording requires xarch.</summary>
    RegisterMoveRecordingRequiresXarch = 1508,
    /// <summary>ARM32 register move flags are not supported for this target.</summary>
    ARM32RegisterMoveFlagsAreNotSupportedForThisTarget = 1509,
    /// <summary>Move sign-extension optimization requires xarch.</summary>
    MoveSignExtensionOptimizationRequiresXarch = 1510,
    /// <summary>Move elision is not implemented for this target.</summary>
    MoveElisionIsNotImplementedForThisTarget = 1511,
    /// <summary>Register display requires an active compiler.</summary>
    RegisterDisplayRequiresAnActiveCompiler = 1512,
    /// <summary>Invalid ARM64 stack register size {0}.</summary>
    InvalidARM64StackRegisterSizeArgument = 1513,
    /// <summary>Invalid ARM64 zero register size {0}.</summary>
    InvalidARM64ZeroRegisterSizeArgument = 1514,
    /// <summary>Invalid ARM64 general register size {0}.</summary>
    InvalidARM64GeneralRegisterSizeArgument = 1515,
    /// <summary>Invalid ARM64 register {0}.</summary>
    InvalidARM64RegisterArgument = 1516,
    /// <summary>Invalid ARM64 vector register size {0}.</summary>
    InvalidARM64VectorRegisterSizeArgument = 1517,
    /// <summary>Emitter register names outside xarch are not implemented.</summary>
    EmitterRegisterNamesOutsideXarchAreNotImplemented = 1518,
    /// <summary>Vector register names are only supported on ARM64.</summary>
    VectorRegisterNamesAreOnlySupportedOnARM64 = 1519,
    /// <summary>Floating register names are only supported on ARM32.</summary>
    FloatingRegisterNamesAreOnlySupportedOnARM32 = 1520,
    /// <summary>Floating register display requires an active compiler.</summary>
    FloatingRegisterDisplayRequiresAnActiveCompiler = 1521,
    /// <summary>XMM register names are only supported on xarch.</summary>
    XMMRegisterNamesAreOnlySupportedOnXarch = 1522,
    /// <summary>YMM register names are only supported on xarch.</summary>
    YMMRegisterNamesAreOnlySupportedOnXarch = 1523,
    /// <summary>ZMM register names are only supported on xarch.</summary>
    ZMMRegisterNamesAreOnlySupportedOnXarch = 1524,
    /// <summary>xarch register encoding is not available for this target.</summary>
    XarchRegisterEncodingIsNotAvailableForThisTarget = 1525,
    /// <summary>REX.X output requires AMD64.</summary>
    REXXOutputRequiresAMD64 = 1526,
    /// <summary>REX.B output requires AMD64.</summary>
    REXBOutputRequiresAMD64 = 1527,
    /// <summary>REX output requires AMD64.</summary>
    REXOutputRequiresAMD64 = 1528,
    /// <summary>Register output requires xarch.</summary>
    RegisterOutputRequiresXarch = 1529,
    /// <summary>APX register output requires AMD64.</summary>
    APXRegisterOutputRequiresAMD64 = 1530,
    /// <summary>Register-pair output requires xarch.</summary>
    RegisterPairOutputRequiresXarch = 1531,
    /// <summary>Eight-byte register-pair output requires AMD64.</summary>
    EightByteRegisterPairOutputRequiresAMD64 = 1532,
    /// <summary>Three-register output requires xarch.</summary>
    ThreeRegisterOutputRequiresXarch = 1533,
    /// <summary>Eight-byte three-register output requires AMD64.</summary>
    EightByteThreeRegisterOutputRequiresAMD64 = 1534,
    /// <summary>xarch prefix output is not available for this target.</summary>
    XarchPrefixOutputIsNotAvailableForThisTarget = 1535,
    /// <summary>REX2 output requires AMD64.</summary>
    REX2OutputRequiresAMD64 = 1536,
    /// <summary>CCMP and CTEST output requires AMD64.</summary>
    CCMPAndCTESTOutputRequiresAMD64 = 1537,
    /// <summary>Invalid CCMP or CTEST condition.</summary>
    InvalidCCMPOrCTESTCondition = 1538,
    /// <summary>EVEX output requires xarch.</summary>
    EVEXOutputRequiresXarch = 1539,
    /// <summary>Invalid EVEX embedded rounding mode.</summary>
    InvalidEVEXEmbeddedRoundingMode = 1540,
    /// <summary>REX.R output requires AMD64.</summary>
    REXROutputRequiresAMD64 = 1541,
    /// <summary>EVEX R-prime output requires AMD64.</summary>
    EVEXRPrimeOutputRequiresAMD64 = 1542,
    /// <summary>xarch GC register classification is not available for this target.</summary>
    XarchGCRegisterClassificationIsNotAvailableForThisTarget = 1543,
    /// <summary>xarch GC register output is not available for this target.</summary>
    XarchGCRegisterOutputIsNotAvailableForThisTarget = 1544,
    /// <summary>Peephole iteration requires a last instruction.</summary>
    PeepholeIterationRequiresALastInstruction = 1545,
    /// <summary>Instruction write classification requires xarch.</summary>
    InstructionWriteClassificationRequiresXarch = 1546,
    /// <summary>Read-modify-write instruction recording requires xarch.</summary>
    ReadModifyWriteInstructionRecordingRequiresXarch = 1547,
    /// <summary>Unary read-modify-write instruction recording requires xarch.</summary>
    UnaryReadModifyWriteInstructionRecordingRequiresXarch = 1548,
    /// <summary>Indexed-address instruction recording requires xarch.</summary>
    IndexedAddressInstructionRecordingRequiresXarch = 1549,
    /// <summary>Base register-immediate instruction recording requires xarch.</summary>
    BaseRegisterImmediateInstructionRecordingRequiresXarch = 1550,
    /// <summary>Xarch shift instruction classification is not supported for this target.</summary>
    XarchShiftInstructionClassificationIsNotSupportedForThisTarget = 1551,
    /// <summary>Fixed-size prolog jumps are not available for WebAssembly.</summary>
    FixedSizePrologJumpsAreNotAvailableForWebAssembly = 1552,
    /// <summary>SIMD constant emission requires SIMD support.</summary>
    SIMDConstantEmissionRequiresSIMDSupport = 1553,
    /// <summary>Compressed SIMD constant loads require SIMD support on xarch.</summary>
    CompressedSIMDConstantLoadsRequireSIMDSupportOnXarch = 1554,
    /// <summary>SIMD mask constant emission requires masked hardware intrinsic support.</summary>
    SIMDMaskConstantEmissionRequiresMaskedHardwareIntrinsicSupport = 1555,
    /// <summary>Two-register base-address instruction recording requires AMD64.</summary>
    TwoRegisterBaseAddressInstructionRecordingRequiresAMD64 = 1556,
    /// <summary>SIMD register-immediate recording requires xarch.</summary>
    SIMDRegisterImmediateRecordingRequiresXarch = 1557,
    /// <summary>SIMD memory instruction recording requires xarch.</summary>
    SIMDMemoryInstructionRecordingRequiresXarch = 1558,
    /// <summary>SIMD stack instruction recording requires xarch.</summary>
    SIMDStackInstructionRecordingRequiresXarch = 1559,
    /// <summary>SIMD address-immediate recording requires xarch.</summary>
    SIMDAddressImmediateRecordingRequiresXarch = 1560,
    /// <summary>SIMD field-immediate recording requires xarch.</summary>
    SIMDFieldImmediateRecordingRequiresXarch = 1561,
    /// <summary>SIMD stack-immediate recording requires xarch.</summary>
    SIMDStackImmediateRecordingRequiresXarch = 1562,
    /// <summary>SIMD multioperand recording requires xarch.</summary>
    SIMDMultioperandRecordingRequiresXarch = 1563,
    /// <summary>SIMD multioperand recording requires hardware intrinsics.</summary>
    SIMDMultioperandRecordingRequiresHardwareIntrinsics = 1564,
    /// <summary>AVX-512 blend classification is not implemented for this target.</summary>
    AVX512BlendClassificationIsNotImplementedForThisTarget = 1565,
    /// <summary>x86 AVX-512 blend classification without hardware intrinsics is not ported.</summary>
    X86AVX512BlendClassificationWithoutHardwareIntrinsicsIsNotPorted = 1566,
    /// <summary>AVX blend classification is not implemented for this target.</summary>
    AVXBlendClassificationIsNotImplementedForThisTarget = 1567,
    /// <summary>x86 AVX blend classification without hardware intrinsics is not ported.</summary>
    X86AVXBlendClassificationWithoutHardwareIntrinsicsIsNotPorted = 1568,
    /// <summary>SSE4.1 blend classification is not implemented for this target.</summary>
    SSE41BlendClassificationIsNotImplementedForThisTarget = 1569,
    /// <summary>SSE4.1 blend classification without hardware intrinsics is not ported.</summary>
    SSE41BlendClassificationWithoutHardwareIntrinsicsIsNotPorted = 1570,
    /// <summary>SIMD register instruction recording requires xarch.</summary>
    SIMDRegisterInstructionRecordingRequiresXarch = 1571,
    /// <summary>SIMD register-immediate instruction recording requires xarch.</summary>
    SIMDRegisterImmediateInstructionRecordingRequiresXarch = 1572,
    /// <summary>LoongArch64 jump recording requires an active compiler.</summary>
    LoongArch64JumpRecordingRequiresAnActiveCompiler = 1573,
    /// <summary>LoongArch64 jump recording requires a current basic block.</summary>
    LoongArch64JumpRecordingRequiresACurrentBasicBlock = 1574,
    /// <summary>Target conditional-branch recording is not implemented.</summary>
    TargetConditionalBranchRecordingIsNotImplemented = 1575,
    /// <summary>LoongArch64 conditional-branch recording requires LoongArch64.</summary>
    LoongArch64ConditionalBranchRecordingRequiresLoongArch64 = 1576,
    /// <summary>Relocated-address instruction recording requires an active compiler.</summary>
    RelocatedAddressInstructionRecordingRequiresAnActiveCompiler = 1577,
    /// <summary>Address range checks require an active compiler.</summary>
    AddressRangeChecksRequireAnActiveCompiler = 1578,
    /// <summary>Stack-depth tracking is disabled for this target.</summary>
    StackDepthTrackingIsDisabledForThisTarget = 1579,
    /// <summary>A current instruction group is required.</summary>
    CurrentInstructionGroupIsRequired2 = 1580,
    /// <summary>Move-instruction classification is not supported for this target.</summary>
    MoveInstructionClassificationIsNotSupportedForThisTarget = 1581,
    /// <summary>MOVSXD side-effect classification is only supported on 64-bit xarch.</summary>
    MOVSXDSideEffectClassificationIsOnlySupportedOn64BitXarch = 1582,
    /// <summary>Xarch stack-move optimization is not supported for this target.</summary>
    XarchStackMoveOptimizationIsNotSupportedForThisTarget = 1583,
    /// <summary>ARM64 stack instruction recording is not ported.</summary>
    ARM64StackInstructionRecordingIsNotPorted = 1584,
    /// <summary>ARM32 stack instruction recording is not ported.</summary>
    ARM32StackInstructionRecordingIsNotPorted = 1585,
    /// <summary>Stack instruction recording requires xarch.</summary>
    StackInstructionRecordingRequiresXarch = 1586,
    /// <summary>ARM64 stack-immediate instruction recording is not ported.</summary>
    ARM64StackImmediateInstructionRecordingIsNotPorted = 1587,
    /// <summary>ARM32 stack-immediate instruction recording is not ported.</summary>
    ARM32StackImmediateInstructionRecordingIsNotPorted = 1588,
    /// <summary>Stack-immediate instruction recording requires xarch.</summary>
    StackImmediateInstructionRecordingRequiresXarch = 1589,
    /// <summary>Static-field instruction sizing requires AMD64.</summary>
    StaticFieldInstructionSizingRequiresAMD64 = 1590,
    /// <summary>ARM32 static-field register instruction recording is not ported.</summary>
    ARM32StaticFieldRegisterInstructionRecordingIsNotPorted = 1591,
    /// <summary>Static-field register stores require xarch.</summary>
    StaticFieldRegisterStoresRequireXarch = 1592,
    /// <summary>ARM32 static-field instruction options are not ported.</summary>
    ARM32StaticFieldInstructionOptionsAreNotPorted = 1593,
    /// <summary>JitDataOffset static fields are not ported.</summary>
    JitDataOffsetStaticFieldsAreNotPorted = 1594,
    /// <summary>Thread-Local-Storage static fields are not ported.</summary>
    ThreadLocalStorageStaticFieldsAreNotPorted = 1595,
    /// <summary>Normal static-field instruction recording is not ported.</summary>
    NormalStaticFieldInstructionRecordingIsNotPorted = 1596,
    /// <summary>ARM32 static-field instruction recording requires a general register.</summary>
    ARM32StaticFieldInstructionRecordingRequiresAGeneralRegister = 1597,
    /// <summary>Static-field register instruction recording requires xarch.</summary>
    StaticFieldRegisterInstructionRecordingRequiresXarch = 1598,
    /// <summary>SIMD static-field instruction recording requires xarch.</summary>
    SIMDStaticFieldInstructionRecordingRequiresXarch = 1599,
    /// <summary>Native emitter layout reporting requires the target-specific pointer, register-mask and descriptor layouts.</summary>
    NativeEmitterLayoutReportingRequiresTheTargetSpecificPointerRegisterMaskAndDescriptorLayouts = 1600,
    /// <summary>emitterStats outside xarch, arm, and arm64 is not ported.</summary>
    EmitterStatsOutsideXarchArmAndArm64IsNotPorted = 1601,
    /// <summary>Target local-stack instruction recording is not implemented.</summary>
    TargetLocalStackInstructionRecordingIsNotImplemented = 1603,
    /// <summary>LoongArch64 embedded-data recording requires a LoongArch64 target.</summary>
    LoongArch64EmbeddedDataRecordingRequiresALoongArch64Target = 1604,
    /// <summary>LoongArch64 embedded-data recording requires an active compiler.</summary>
    LoongArch64EmbeddedDataRecordingRequiresAnActiveCompiler = 1605,
    /// <summary>RISC-V embedded-data recording requires a RISC-V64 target.</summary>
    RISCVEmbeddedDataRecordingRequiresARISCV64Target = 1606,
    /// <summary>Instruction byte display is not implemented for the active target.</summary>
    InstructionByteDisplayIsNotImplementedForTheActiveTarget = 1607,
    /// <summary>Target instruction store classification is not implemented.</summary>
    TargetInstructionStoreClassificationIsNotImplemented = 1608,
    /// <summary>Wasm store classification is unreachable for instruction {0}.</summary>
    WasmStoreClassificationIsUnreachableForInstructionArgument = 1609,
    /// <summary>Single-register instruction recording requires xarch.</summary>
    SingleRegisterInstructionRecordingRequiresXarch = 1610,
    /// <summary>Base unary instruction recording requires xarch.</summary>
    BaseUnaryInstructionRecordingRequiresXarch = 1611,
    /// <summary>APX EVEX.zu instruction recording requires AMD64.</summary>
    APXEVEXZuInstructionRecordingRequiresAMD64 = 1612,
    /// <summary>Unknown-size frame recording requires SIMD-enabled ARM64.</summary>
    UnknownSizeFrameRecordingRequiresSIMDEnabledARM64 = 1613,
    /// <summary>The ARM64 frame count cannot be encoded by a single move.</summary>
    ARM64FrameCountCannotBeEncodedByASingleMove = 1614,
    /// <summary>Unsupported ARM64 unknown-size-frame immediate instruction.</summary>
    UnsupportedARM64UnknownSizeFrameImmediateInstruction = 1615,
    /// <summary>Unsupported ARM64 unknown-size-frame register-pair instruction.</summary>
    UnsupportedARM64UnknownSizeFrameRegisterPairInstruction = 1616,
    /// <summary>Varargs shadow-slot homing requires xarch.</summary>
    VarargsShadowSlotHomingRequiresXarch = 1617,
    /// <summary>ARM64 emitter operations are not supported for this target.</summary>
    ARM64EmitterOperationsAreNotSupportedForThisTarget = 1618,
    /// <summary>ARM32 emitter operations are not supported for this target.</summary>
    ARM32EmitterOperationsAreNotSupportedForThisTarget = 1619,
    /// <summary>RISC-V64 emitter operations are not supported for this target.</summary>
    RISCV64EmitterOperationsAreNotSupportedForThisTarget = 1620,
    /// <summary>ARM32 local-frame-pointer descriptor flags are only supported on ARM32.</summary>
    ARM32LocalFramePointerDescriptorFlagsAreOnlySupportedOnARM32 = 1621,
    /// <summary>Local-variable instruction descriptors are not supported for this target.</summary>
    LocalVariableInstructionDescriptorsAreNotSupportedForThisTarget = 1622,
    /// <summary>The call descriptor layout is not ported for this target.</summary>
    CallDescriptorLayoutIsNotPortedForThisTarget = 1623,
    /// <summary>Instruction descriptor constants are not supported for this target.</summary>
    InstructionDescriptorConstantsAreNotSupportedForThisTarget = 1624,
    /// <summary>Constant descriptor size is not ported for this target.</summary>
    ConstantDescriptorSizeIsNotPortedForThisTarget = 1625,
    /// <summary>Displacement descriptor size is not ported for this target.</summary>
    DisplacementDescriptorSizeIsNotPortedForThisTarget = 1626,
    /// <summary>Constant/displacement descriptor size is not ported for this target.</summary>
    ConstantDisplacementDescriptorSizeIsNotPortedForThisTarget = 1627,
    /// <summary>Address-mode descriptor size is not ported for this target.</summary>
    AddressModeDescriptorSizeIsNotPortedForThisTarget = 1628,
    /// <summary>Constant/address-mode descriptor size is not ported for this target.</summary>
    ConstantAddressModeDescriptorSizeIsNotPortedForThisTarget = 1629,
    /// <summary>Instruction formats are not supported for this target.</summary>
    InstructionFormatsAreNotSupportedForThisTarget = 1630,
    /// <summary>Explicit instruction sizes are not supported for this target.</summary>
    ExplicitInstructionSizesAreNotSupportedForThisTarget = 1631,
    /// <summary>Instruction encoding storage is not supported for this target.</summary>
    InstructionEncodingStorageIsNotSupportedForThisTarget = 1632,
    /// <summary>LoongArch64 jump-offset storage is not supported for this target.</summary>
    LoongArch64JumpOffsetStorageIsNotSupportedForThisTarget = 1633,
    /// <summary>Target-specific instruction size is not supported for this target.</summary>
    TargetSpecificInstructionSizeIsNotSupportedForThisTarget = 1634,
    /// <summary>Register-pointer call descriptors are not supported for this target.</summary>
    RegisterPointerCallDescriptorsAreNotSupportedForThisTarget = 1635,
    /// <summary>ARM relocation descriptor size is not supported for this target.</summary>
    ARMRelocationDescriptorSizeIsNotSupportedForThisTarget = 1636,
    /// <summary>ARM64 empty-alignment descriptors are not supported for this target.</summary>
    ARM64EmptyAlignmentDescriptorsAreNotSupportedForThisTarget = 1637,
    /// <summary>Previous-instruction sizes are only supported on xarch.</summary>
    PreviousInstructionSizesAreOnlySupportedOnXarch = 1638,
    /// <summary>WebAssembly instruction descriptors are not supported for this target.</summary>
    WebAssemblyInstructionDescriptorsAreNotSupportedForThisTarget = 1639,
    /// <summary>Align descriptor size is not yet ported for this target.</summary>
    AlignDescriptorSizeIsNotYetPortedForThisTarget = 1640,
    /// <summary>Xarch alignment padding is unsupported on this target.</summary>
    XarchAlignmentPaddingIsUnsupportedOnThisTarget = 1641,
    /// <summary>Jump descriptor size is not yet ported for this target.</summary>
    JumpDescriptorSizeIsNotYetPortedForThisTarget = 1642,
    /// <summary>xarch opcode table is not available for this target.</summary>
    XarchOpcodeTableIsNotAvailableForThisTarget = 1644,
    /// <summary>Absolute xarch register numbering is not available for this target.</summary>
    AbsoluteXarchRegisterNumberingIsNotAvailableForThisTarget = 1645,
    /// <summary>Instruction-group debug data is only available in Debug builds.</summary>
    InstructionGroupDebugDataIsOnlyAvailableInDebugBuilds = 1646,
    /// <summary>Contained ARM32 indirect has no base register.</summary>
    ContainedARM32IndirectHasNoBaseRegister = 1647,
    /// <summary>Thumb branch output is only supported on ARM32.</summary>
    ThumbBranchOutputIsOnlySupportedOnARM32 = 1648,
    /// <summary>Thumb CBZ/CBNZ output requires a jump descriptor.</summary>
    ThumbCBZCBNZOutputRequiresAJumpDescriptor = 1649,
    /// <summary>Unknown Thumb short-branch format.</summary>
    UnknownThumbShortBranchFormat = 1650,
    /// <summary>ARM32 call recording is only supported on ARM32.</summary>
    ARM32CallRecordingIsOnlySupportedOnARM32 = 1651,
    /// <summary>ARM32 conditional-register branches are not available for this target.</summary>
    ARM32ConditionalRegisterBranchesAreNotAvailableForThisTarget = 1652,
    /// <summary>ARM32 data-label address recording is not available for this target.</summary>
    ARM32DataLabelAddressRecordingIsNotAvailableForThisTarget = 1653,
    /// <summary>Instruction display requires descriptor debug information.</summary>
    InstructionDisplayRequiresDescriptorDebugInformation = 1654,
    /// <summary>An ARM32 label table contains a null block.</summary>
    ARM32LabelTableContainsANullBlock = 1655,
    /// <summary>A bound ARM32 label table contains an unbound block.</summary>
    BoundARM32LabelTableContainsAnUnboundBlock = 1656,
    /// <summary>A bound ARM32 label instruction has no jump descriptor.</summary>
    BoundARM32LabelInstructionHasNoJumpDescriptor = 1657,
    /// <summary>A bound ARM32 label instruction has no instruction group.</summary>
    BoundARM32LabelInstructionHasNoInstructionGroup = 1658,
    /// <summary>An unbound ARM32 label instruction has no jump descriptor.</summary>
    UnboundARM32LabelInstructionHasNoJumpDescriptor = 1659,
    /// <summary>An unbound ARM32 label instruction has no basic block.</summary>
    UnboundARM32LabelInstructionHasNoBasicBlock = 1660,
    /// <summary>A bound ARM32 branch has no jump descriptor.</summary>
    BoundARM32BranchHasNoJumpDescriptor = 1661,
    /// <summary>A bound ARM32 branch has no instruction group.</summary>
    BoundARM32BranchHasNoInstructionGroup = 1662,
    /// <summary>An unbound ARM32 branch has no jump descriptor.</summary>
    UnboundARM32BranchHasNoJumpDescriptor = 1663,
    /// <summary>An unbound ARM32 branch has no basic block.</summary>
    UnboundARM32BranchHasNoBasicBlock = 1664,
    /// <summary>The spill temporary must exist.</summary>
    SpillTemporaryMustExist = 1665,
    /// <summary>Instruction debug information must exist.</summary>
    InstructionDebugInformationMustExist = 1666,
    /// <summary>An ARM32 jump requires a bound target group.</summary>
    ARM32JumpRequiresABoundTargetGroup = 1667,
    /// <summary>Unknown ARM32 branch format.</summary>
    UnknownARM32BranchFormat = 1668,
    /// <summary>Unknown ARM32 label format.</summary>
    UnknownARM32LabelFormat = 1669,
    /// <summary>ARM32 memory-barrier coalescing is not available for this target.</summary>
    ARM32MemoryBarrierCoalescingIsNotAvailableForThisTarget = 1670,
    /// <summary>ARM32 multioperand recording is not available for this target.</summary>
    ARM32MultioperandRecordingIsNotAvailableForThisTarget = 1671,
    /// <summary>ARM64 call recording is only supported on ARM64.</summary>
    ARM64CallRecordingIsOnlySupportedOnARM64 = 1672,
    /// <summary>ARM64 register-immediate instruction recording requires ARM64.</summary>
    ARM64RegisterImmediateInstructionRecordingRequiresARM64 = 1675,
    /// <summary>Unexpected ARM64 label-address instruction {0}.</summary>
    UnexpectedARM64LabelAddressInstructionArgument = 1676,
    /// <summary>ARM64 relocatable-address recording is only supported on ARM64.</summary>
    ARM64RelocatableAddressRecordingIsOnlySupportedOnARM64 = 1677,
    /// <summary>LoongArch64 call recording is only supported on LoongArch64.</summary>
    LoongArch64CallRecordingIsOnlySupportedOnLoongArch64 = 1678,
    /// <summary>LoongArch64 call recording requires an active compiler.</summary>
    LoongArch64CallRecordingRequiresAnActiveCompiler = 1679,
    /// <summary>LoongArch64 call output requires an active compiler.</summary>
    LoongArch64CallOutputRequiresAnActiveCompiler = 1680,
    /// <summary>LoongArch64 disassembly requires an active compiler.</summary>
    LoongArch64DisassemblyRequiresAnActiveCompiler = 1681,
    /// <summary>LoongArch64 instruction classification requires LoongArch64.</summary>
    LoongArch64InstructionClassificationRequiresLoongArch64 = 1682,
    /// <summary>LoongArch64 GC-register classification requires LoongArch64.</summary>
    LoongArch64GCRegisterClassificationRequiresLoongArch64 = 1683,
    /// <summary>LoongArch64 stack-store classification requires LoongArch64.</summary>
    LoongArch64StackStoreClassificationRequiresLoongArch64 = 1684,
    /// <summary>LoongArch64 SIMD instruction encoding requires FEATURE_SIMD.</summary>
    LoongArch64SIMDInstructionEncodingRequiresFEATURESIMD = 1685,
    /// <summary>LoongArch64 instruction output requires an active compiler.</summary>
    LoongArch64InstructionOutputRequiresAnActiveCompiler = 1686,
    /// <summary>The LoongArch64 spill temporary must exist.</summary>
    LoongArch64SpillTemporaryMustExist = 1687,
    /// <summary>LoongArch64 instruction output requires debug descriptor information.</summary>
    LoongArch64InstructionOutputRequiresDebugDescriptorInformation = 1688,
    /// <summary>An unbound LoongArch64 label requires a basic-block target.</summary>
    UnboundLoongArch64LabelRequiresABasicBlockTarget = 1689,
    /// <summary>A LoongArch64 label has no instruction-group cookie.</summary>
    LoongArch64LabelHasNoInstructionGroupCookie = 1690,
    /// <summary>A bound LoongArch64 label requires an instruction group.</summary>
    BoundLoongArch64LabelRequiresAnInstructionGroup = 1691,
    /// <summary>LoongArch64 label recording requires an active compiler.</summary>
    LoongArch64LabelRecordingRequiresAnActiveCompiler = 1692,
    /// <summary>LoongArch64 instruction-group label recording requires an active compiler.</summary>
    LoongArch64InstructionGroupLabelRecordingRequiresAnActiveCompiler = 1693,
    /// <summary>LoongArch64 stack stores require an active compiler.</summary>
    LoongArch64StackStoresRequireAnActiveCompiler = 1694,
    /// <summary>LoongArch64 stack loads require an active compiler.</summary>
    LoongArch64StackLoadsRequireAnActiveCompiler = 1695,
    /// <summary>LoongArch64 instruction validation is not available for this target.</summary>
    LoongArch64InstructionValidationIsNotAvailableForThisTarget = 1696,
    /// <summary>LoongArch64 vector validation is not available without SIMD support.</summary>
    LoongArch64VectorValidationIsNotAvailableWithoutSIMDSupport = 1697,
    /// <summary>LoongArch64 jump binding requires an active compiler.</summary>
    LoongArch64JumpBindingRequiresAnActiveCompiler = 1698,
    /// <summary>A LoongArch64 jump must be associated with an instruction group.</summary>
    LoongArch64JumpMustBeAssociatedWithAnInstructionGroup = 1699,
    /// <summary>A LoongArch64 jump group is missing from the group list.</summary>
    LoongArch64JumpGroupIsMissingFromTheGroupList = 1700,
    /// <summary>A bound LoongArch64 jump requires a target group.</summary>
    BoundLoongArch64JumpRequiresATargetGroup = 1701,
    /// <summary>LoongArch64 jump-kind mapping is not supported for this target.</summary>
    LoongArch64JumpKindMappingIsNotSupportedForThisTarget = 1702,
    /// <summary>emitInsToJumpKind-----unimplemented on LOONGARCH64 yet----</summary>
    EmitInsToJumpKindUnimplementedOnLOONGARCH64Yet = 1703,
    /// <summary>LoongArch64 register jump recording is not supported for this target.</summary>
    LoongArch64RegisterJumpRecordingIsNotSupportedForThisTarget = 1704,
    /// <summary>emitIns_J_R-----unimplemented/unused on LOONGARCH64 yet----</summary>
    EmitInsJRUnimplementedUnusedOnLOONGARCH64Yet = 1705,
    /// <summary>LoongArch64 immediate materialization requires LoongArch64.</summary>
    LoongArch64ImmediateMaterializationRequiresLoongArch64 = 1706,
    /// <summary>A bound RISC-V branch requires an instruction group.</summary>
    BoundRISCVBranchRequiresAnInstructionGroup = 1707,
    /// <summary>An unbound RISC-V branch requires a basic block.</summary>
    UnboundRISCVBranchRequiresABasicBlock = 1708,
    /// <summary>RISC-V disassembly requires an active compiler.</summary>
    RISCVDisassemblyRequiresAnActiveCompiler = 1709,
    /// <summary>RISC-V branch disassembly requires an instruction descriptor.</summary>
    RISCVBranchDisassemblyRequiresAnInstructionDescriptor = 1710,
    /// <summary>Unsupported RISC-V system instruction disassembly.</summary>
    UnsupportedRISCVSystemInstructionDisassembly = 1711,
    /// <summary>A bound RISC-V jump requires an instruction group.</summary>
    BoundRISCVJumpRequiresAnInstructionGroup = 1712,
    /// <summary>Unsupported RISC-V floating-point min/max instruction.</summary>
    UnsupportedRISCVFloatingPointMinMaxInstruction = 1713,
    /// <summary>Unsupported RISC-V floating-point conversion instruction.</summary>
    UnsupportedRISCVFloatingPointConversionInstruction = 1714,
    /// <summary>Unsupported RISC-V floating-point register operation.</summary>
    UnsupportedRISCVFloatingPointRegisterOperation = 1715,
    /// <summary>Unsupported RISC-V floating-point compare instruction.</summary>
    UnsupportedRISCVFloatingPointCompareInstruction = 1716,
    /// <summary>Unsupported RISC-V floating-point disassembly opcode.</summary>
    UnsupportedRISCVFloatingPointDisassemblyOpcode = 1717,
    /// <summary>RISC-V instruction disassembly requires debug descriptor info.</summary>
    RISCVInstructionDisassemblyRequiresDebugDescriptorInfo = 1718,
    /// <summary>RISC-V64 emission requires an active compiler.</summary>
    RISCV64EmissionRequiresAnActiveCompiler = 1719,
    /// <summary>Indirect load/store recording requires an active compiler.</summary>
    IndirectLoadStoreRecordingRequiresAnActiveCompiler = 1720,
    /// <summary>Contained RISC-V address mode is missing its base register.</summary>
    ContainedRISCVAddressModeIsMissingItsBaseRegister = 1721,
    /// <summary>Contained RISC-V address mode is missing its base tree.</summary>
    ContainedRISCVAddressModeIsMissingItsBaseTree = 1722,
    /// <summary>Invalid compressed register number.</summary>
    InvalidCompressedRegisterNumber = 1723,
    /// <summary>Invalid compressed arithmetic instruction.</summary>
    InvalidCompressedArithmeticInstruction = 1724,
    /// <summary>An unbound RISC-V label requires a basic block.</summary>
    UnboundRISCVLabelRequiresABasicBlock = 1725,
    /// <summary>The RISC-V basic block has no instruction-group cookie.</summary>
    RISCVBasicBlockHasNoInstructionGroupCookie = 1726,
    /// <summary>A bound RISC-V label requires an instruction group.</summary>
    BoundRISCVLabelRequiresAnInstructionGroup = 1727,
    /// <summary>A RISC-V jump requires a target instruction group.</summary>
    RISCVJumpRequiresATargetInstructionGroup = 1728,
    /// <summary>Method is too large.</summary>
    MethodIsTooLarge = 1729,
    /// <summary>RISC-V call output requires an active compiler.</summary>
    RISCVCallOutputRequiresAnActiveCompiler = 1730,
    /// <summary>RISC-V instruction output requires an active compiler.</summary>
    RISCVInstructionOutputRequiresAnActiveCompiler = 1731,
    /// <summary>The RISC-V spill temporary must exist.</summary>
    RISCVSpillTemporaryMustExist = 1732,
    /// <summary>RISC-V instruction output requires debug descriptor info.</summary>
    RISCVInstructionOutputRequiresDebugDescriptorInfo = 1733,
    /// <summary>RISC-V label instructions require debug descriptor info.</summary>
    RISCVLabelInstructionsRequireDebugDescriptorInfo = 1734,
    /// <summary>Illegal instruction within RISC-V two-register instruction recording.</summary>
    IllegalInstructionWithinRISCVTwoRegisterInstructionRecording = 1735,
    /// <summary>A Wasm v128 immediate must contain at least 16 bytes.</summary>
    WasmV128ImmediateMustContainAtLeast16Bytes = 1736,
    /// <summary>Unexpected Wasm SIMD element size.</summary>
    UnexpectedWasmSIMDElementSize = 1737,
    /// <summary>Unexpected Wasm lane immediate descriptor.</summary>
    UnexpectedWasmLaneImmediateDescriptor = 1738,
    /// <summary>Unexpected Wasm instruction display format {0}.</summary>
    UnexpectedWasmInstructionDisplayFormatArgument = 1739,
    /// <summary>Wasm jump target has no emitter label.</summary>
    WasmJumpTargetHasNoEmitterLabel = 1740,
    /// <summary>Wasm function start location is missing.</summary>
    WasmFunctionStartLocationIsMissing = 1741,
    /// <summary>Wasm function end location is missing.</summary>
    WasmFunctionEndLocationIsMissing = 1742,
    /// <summary>Unexpected Wasm instruction output format {0}.</summary>
    UnexpectedWasmInstructionOutputFormatArgument = 1743,
    /// <summary>WebAssembly emitter operations require WebAssembly.</summary>
    WebAssemblyEmitterOperationsRequireWebAssembly = 1744,
    /// <summary>Unexpected Wasm instruction descriptor format {0}.</summary>
    UnexpectedWasmInstructionDescriptorFormatArgument = 1745,
    /// <summary>Address output is not implemented for this target.</summary>
    AddressOutputIsNotImplementedForThisTarget = 1746,
    /// <summary>Absolute accumulator address output is unavailable on AMD64.</summary>
    AbsoluteAccumulatorAddressOutputIsUnavailableOnAMD64 = 1747,
    /// <summary>Stack output is not implemented for this target.</summary>
    StackOutputIsNotImplementedForThisTarget = 1748,
    /// <summary>Unexpected GC reference stack instruction format {0} for {1}.</summary>
    UnexpectedGCReferenceStackInstructionFormatArgumentForArgument = 1749,
    /// <summary>SIMD stack stores are not enabled for this target.</summary>
    SIMDStackStoresAreNotEnabledForThisTarget = 1750,
    /// <summary>Static output is not implemented for this target.</summary>
    StaticOutputIsNotImplementedForThisTarget = 1751,
    /// <summary>An initialized compiler is required for static output.</summary>
    InitializedCompilerIsRequiredForStaticOutput = 1752,
    /// <summary>Unexpected GC reference static instruction format {0} for {1}.</summary>
    UnexpectedGCReferenceStaticInstructionFormatArgumentForArgument = 1753,
    /// <summary>Xarch instruction recording requires xarch.</summary>
    XarchInstructionRecordingRequiresXarch = 1754,
    /// <summary>SafeCvt does not support destination type {0}.</summary>
    SafeCvtDoesNotSupportDestinationTypeArgument = 1756,
    /// <summary>Critical edge probe was not split</summary>
    CriticalEdgeProbeWasNotSplit = 1757,
    /// <summary>JitSynthesisExceptionWeight requires at least one value.</summary>
    JitSynthesisExceptionWeightRequiresAtLeastOneValue = 1758,
    /// <summary>Wasm SCC analysis requires a DFS tree.</summary>
    WasmSCCAnalysisRequiresADFSTree = 1759,
    /// <summary>Wasm SCC analysis requires postorder traits.</summary>
    WasmSCCAnalysisRequiresPostorderTraits = 1760,
    /// <summary>A call-finally pair tail requires its call-finally block.</summary>
    CallFinallyPairTailRequiresItsCallFinallyBlock = 1761,
    /// <summary>A used add-code descriptor has no destination block.</summary>
    UsedAddCodeDescriptorHasNoDestinationBlock = 1762,
    /// <summary>A call-finally pair requires a following block.</summary>
    CallFinallyPairRequiresAFollowingBlock = 1763,
    /// <summary>An unconditional Wasm branch requires a target.</summary>
    UnconditionalWasmBranchRequiresATarget = 1764,
    /// <summary>A conditional Wasm branch requires a true target.</summary>
    ConditionalWasmBranchRequiresATrueTarget = 1765,
    /// <summary>A conditional Wasm branch requires a false target.</summary>
    ConditionalWasmBranchRequiresAFalseTarget = 1766,
    /// <summary>An EH filter entry requires a filter block.</summary>
    EHFilterEntryRequiresAFilterBlock = 1767,
    /// <summary>Wasm DFS requires a method entry block.</summary>
    WasmDFSRequiresAMethodEntryBlock = 1768,
    /// <summary>An irreducible SCC requires a dispatch block.</summary>
    IrreducibleSCCRequiresADispatchBlock = 1769,
    /// <summary>GC filter lifetimes require Windows AMD64.</summary>
    GCFilterLifetimesRequireWindowsAMD64 = 1770,
    /// <summary>Modern GC encoding requires Windows AMD64.</summary>
    ModernGCEncodingRequiresWindowsAMD64 = 1771,
    /// <summary>JIT32 GC table dumping requires the JIT32 GC encoder.</summary>
    JIT32GCTableDumpingRequiresTheJIT32GCEncoder = 1772,
    /// <summary>Invalid GC dump register.</summary>
    InvalidGCDumpRegister = 1773,
    /// <summary>Invalid GC dump callee-saved register.</summary>
    InvalidGCDumpCalleeSavedRegister = 1774,
    /// <summary>Modern GC table encoding requires Windows AMD64.</summary>
    ModernGCTableEncodingRequiresWindowsAMD64 = 1775,
    /// <summary>Morphing Vararg call not yet implemented on non Windows targets.</summary>
    MorphingVarargCallNotYetImplementedOnNonWindowsTargets = 1776,
    /// <summary>Dont-extend state is only supported on xarch targets.</summary>
    DontExtendStateIsOnlySupportedOnXarchTargets = 1777,
    /// <summary>GenTree debug data is only available in Debug builds.</summary>
    GenTreeDebugDataIsOnlyAvailableInDebugBuilds = 1778,
    /// <summary>Xarch embedded-masking compatibility is unsupported on this target.</summary>
    XarchEmbeddedMaskingCompatibilityIsUnsupportedOnThisTarget = 1779,
    /// <summary>Embedded hardware-intrinsic masking requires hardware-intrinsic support.</summary>
    EmbeddedHardwareIntrinsicMaskingRequiresHardwareIntrinsicSupport = 1780,
    /// <summary>Node-bashing statistics are not enabled.</summary>
    NodeBashingStatisticsAreNotEnabled = 1781,
    /// <summary>GenTree operator counts are not enabled.</summary>
    GenTreeOperatorCountsAreNotEnabled = 1782,
    /// <summary>GenTree::s_gtTrueSizes native logical sizes are not ported.</summary>
    GenTreeSGtTrueSizesNativeLogicalSizesAreNotPorted = 1783,
    /// <summary>Unexpected scalable vector constant kind.</summary>
    UnexpectedScalableVectorConstantKind = 1784,
    /// <summary>GenTree construction requires an active compiler in Debug builds.</summary>
    GenTreeConstructionRequiresAnActiveCompilerInDebugBuilds = 1785,
    /// <summary>Containment diagnostics are only available in Debug builds.</summary>
    ContainmentDiagnosticsAreOnlyAvailableInDebugBuilds = 1786,
    /// <summary>LIR diagnostics are only available in Debug builds.</summary>
    LIRDiagnosticsAreOnlyAvailableInDebugBuilds = 1787,
    /// <summary>Tree flag diagnostics are only available in Debug builds.</summary>
    TreeFlagDiagnosticsAreOnlyAvailableInDebugBuilds = 1788,
    /// <summary>Morph stress cloning is only available in Debug builds.</summary>
    MorphStressCloningIsOnlyAvailableInDebugBuilds = 1789,
    /// <summary>Replace a long constant at its owning use rather than changing its managed runtime type</summary>
    ReplaceALongConstantAtItsOwningUseRatherThanChangingItsManagedRuntimeType = 1790,
    /// <summary>ReadyToRun allocation entry points are unsupported.</summary>
    ReadyToRunAllocationEntryPointsAreUnsupported = 1791,
    /// <summary>ReadyToRun field lookups are unsupported.</summary>
    ReadyToRunFieldLookupsAreUnsupported = 1792,
    /// <summary>ReadyToRun function-pointer entry points are unsupported.</summary>
    ReadyToRunFunctionPointerEntryPointsAreUnsupported = 1793,
    /// <summary>RMW hardware-intrinsic operand selection is unsupported on this target.</summary>
    RMWHardwareIntrinsicOperandSelectionIsUnsupportedOnThisTarget = 1794,
    /// <summary>Embedded-broadcast compatibility is only supported on xarch targets.</summary>
    EmbeddedBroadcastCompatibilityIsOnlySupportedOnXarchTargets = 1795,
    /// <summary>Code generation is not initialized.</summary>
    CodeGenerationIsNotInitialized = 1796,
    /// <summary>Unexpected embedded-rounding intrinsic.</summary>
    UnexpectedEmbeddedRoundingIntrinsic = 1797,
    /// <summary>Hardware intrinsic immediate operands are only available on WebAssembly.</summary>
    HardwareIntrinsicImmediateOperandsAreOnlyAvailableOnWebAssembly = 1798,
    /// <summary>Address-base fit checks are unsupported on this target.</summary>
    AddressBaseFitChecksAreUnsupportedOnThisTarget = 1799,
    /// <summary>ReadyToRun intrinsic entry points are unsupported.</summary>
    ReadyToRunIntrinsicEntryPointsAreUnsupported = 1800,
    /// <summary>Hardware-intrinsic user calls are unsupported on this target.</summary>
    HardwareIntrinsicUserCallsAreUnsupportedOnThisTarget = 1801,
    /// <summary>Additional intrinsic registers are unsupported on this target.</summary>
    AdditionalIntrinsicRegistersAreUnsupportedOnThisTarget = 1802,
    /// <summary>Masked hardware intrinsics are not enabled for this target.</summary>
    MaskedHardwareIntrinsicsAreNotEnabledForThisTarget = 1803,
    /// <summary>Hardware intrinsic nodes are unsupported on this target.</summary>
    HardwareIntrinsicNodesAreUnsupportedOnThisTarget = 1804,
    /// <summary>Comparison operand sizes are only supported on xarch targets.</summary>
    ComparisonOperandSizesAreOnlySupportedOnXarchTargets = 1805,
    /// <summary>Long-multiply shape checks are only supported on non-Wasm 32-bit targets.</summary>
    LongMultiplyShapeChecksAreOnlySupportedOnNonWasm32BitTargets = 1806,
    /// <summary>GT_CCMP is only supported on x64 and ARM64.</summary>
    GTCCMPIsOnlySupportedOnX64AndARM64 = 1807,
    /// <summary>Argument load sizes are only supported on xarch targets.</summary>
    ArgumentLoadSizesAreOnlySupportedOnXarchTargets = 1808,
    /// <summary>Argument padding is only supported on the Unix x86 ABI.</summary>
    ArgumentPaddingIsOnlySupportedOnTheUnixX86ABI = 1809,
    /// <summary>RMW status is not supported on this target.</summary>
    RMWStatusIsNotSupportedOnThisTarget = 1810,
    /// <summary>The visited operand does not belong to its user.</summary>
    VisitedOperandDoesNotBelongToItsUser = 1811,
    /// <summary>Hardware intrinsic constant evaluation is unsupported on this target.</summary>
    HardwareIntrinsicConstantEvaluationIsUnsupportedOnThisTarget = 1812,
    /// <summary>ABI return-register masks are unsupported on this target.</summary>
    ABIReturnRegisterMasksAreUnsupportedOnThisTarget = 1813,
    /// <summary>Statement debug data is only available in Debug builds.</summary>
    StatementDebugDataIsOnlyAvailableInDebugBuilds = 1814,
    /// <summary>Scalable vectors are only supported on ARM64.</summary>
    ScalableVectorsAreOnlySupportedOnARM64 = 1815,
    /// <summary>Tree operation diagnostics are only available in Debug builds.</summary>
    TreeOperationDiagnosticsAreOnlyAvailableInDebugBuilds = 1816,
    /// <summary>Operator struct type metadata is only available in Debug or node-size measurement builds.</summary>
    OperatorStructTypeMetadataIsOnlyAvailableInDebugOrNodeSizeMeasurementBuilds = 1817,
    /// <summary>The iterator is not initialized.</summary>
    IteratorIsNotInitialized = 1818,
    /// <summary>JIT logging is only available in Debug builds.</summary>
    JITLoggingIsOnlyAvailableInDebugBuilds = 1819,
    /// <summary>Unexpected ARM64 immediate intrinsic {0}.</summary>
    UnexpectedARM64ImmediateIntrinsicArgument = 1820,
    /// <summary>ARM64 intrinsic immediates are unavailable for this target.</summary>
    ARM64IntrinsicImmediatesAreUnavailableForThisTarget = 1821,
    /// <summary>Hardware-intrinsic containment is not ported for this target.</summary>
    HardwareIntrinsicContainmentIsNotPortedForThisTarget = 1822,
    /// <summary>Floating-point comparison intrinsics require an xarch target.</summary>
    FloatingPointComparisonIntrinsicsRequireAnXarchTarget = 1823,
    /// <summary>Immediate bounds are only defined for xarch intrinsics.</summary>
    ImmediateBoundsAreOnlyDefinedForXarchIntrinsics = 1824,
    /// <summary>Xarch intrinsic immediates are unavailable for this target.</summary>
    XarchIntrinsicImmediatesAreUnavailableForThisTarget = 1825,
    /// <summary>Scalar-input intrinsic variants are only available on ARM64.</summary>
    ScalarInputIntrinsicVariantsAreOnlyAvailableOnARM64 = 1826,
    /// <summary>Tuple-derived intrinsic base types are only available on ARM64.</summary>
    TupleDerivedIntrinsicBaseTypesAreOnlyAvailableOnARM64 = 1827,
    /// <summary>Unexpected ARM64 scalar-input variant intrinsic.</summary>
    UnexpectedARM64ScalarInputVariantIntrinsic = 1828,
    /// <summary>Packed SIMD immediate bounds are only available on WebAssembly.</summary>
    PackedSIMDImmediateBoundsAreOnlyAvailableOnWebAssembly = 1829,
    /// <summary>Packed SIMD immediate operands are only available on WebAssembly.</summary>
    PackedSIMDImmediateOperandsAreOnlyAvailableOnWebAssembly = 1830,
    /// <summary>Table-driven hardware intrinsics are only supported on xarch.</summary>
    TableDrivenHardwareIntrinsicsAreOnlySupportedOnXarch = 1831,
    /// <summary>Hardware-intrinsic metadata is unsupported on this target.</summary>
    HardwareIntrinsicMetadataIsUnsupportedOnThisTarget = 1832,
    /// <summary>The value is not a hardware intrinsic identity.</summary>
    ValueIsNotAHardwareIntrinsicIdentity = 1833,
    /// <summary>Hardware intrinsic &#x27;{0}&#x27; is not present in the configured target&#x27;s native metadata table.</summary>
    HardwareIntrinsicArgumentIsNotPresentInTheConfiguredTargetSNativeMetadataTable = 1834,
    /// <summary>Hardware-intrinsic SIMD size lookup is unsupported on this target.</summary>
    HardwareIntrinsicSIMDSizeLookupIsUnsupportedOnThisTarget = 1836,
    /// <summary>Floating-point intrinsic costs are only defined for xarch.</summary>
    FloatingPointIntrinsicCostsAreOnlyDefinedForXarch = 1837,
    /// <summary>Integer intrinsic costs are only defined for xarch.</summary>
    IntegerIntrinsicCostsAreOnlyDefinedForXarch = 1838,
    /// <summary>Unexpected ternary-logic operation: {0}.</summary>
    UnexpectedTernaryLogicOperationArgument = 1839,
    /// <summary>Invalid constant ternary-logic operation.</summary>
    InvalidConstantTernaryLogicOperation = 1840,
    /// <summary>Invalid three-input ternary operation.</summary>
    InvalidThreeInputTernaryOperation = 1841,
    /// <summary>Invalid first ternary-logic usage.</summary>
    InvalidFirstTernaryLogicUsage = 1842,
    /// <summary>Invalid second ternary-logic usage.</summary>
    InvalidSecondTernaryLogicUsage = 1843,
    /// <summary>Invalid conditional ternary-logic usage.</summary>
    InvalidConditionalTernaryLogicUsage = 1844,
    /// <summary>ARM64 hardware intrinsics are unavailable for this target.</summary>
    ARM64HardwareIntrinsicsAreUnavailableForThisTarget = 1845,
    /// <summary>ARM64 emitIns_R_L recording is not ported.</summary>
    ARM64EmitInsRLRecordingIsNotPorted = 1846,
    /// <summary>Hardware intrinsics are disabled.</summary>
    HardwareIntrinsicsAreDisabled = 1847,
    /// <summary>AVX family generation requires xarch.</summary>
    AVXFamilyGenerationRequiresXarch = 1848,
    /// <summary>FMA generation requires xarch.</summary>
    FMAGenerationRequiresXarch = 1849,
    /// <summary>PermuteVar2x generation requires xarch.</summary>
    PermuteVar2xGenerationRequiresXarch = 1850,
    /// <summary>Base hardware intrinsics require xarch support.</summary>
    BaseHardwareIntrinsicsRequireXarchSupport = 1851,
    /// <summary>Hardware-intrinsic code generation requires xarch support.</summary>
    HardwareIntrinsicCodeGenerationRequiresXarchSupport = 1852,
    /// <summary>X86 base intrinsics require xarch support.</summary>
    X86BaseIntrinsicsRequireXarchSupport = 1853,
    /// <summary>Full inline policy is only available in Debug builds.</summary>
    FullInlinePolicyIsOnlyAvailableInDebugBuilds = 1854,
    /// <summary>Random inline policy is only available in Debug builds.</summary>
    RandomInlinePolicyIsOnlyAvailableInDebugBuilds = 1855,
    /// <summary>Replay inline policy is only available in Debug builds.</summary>
    ReplayInlinePolicyIsOnlyAvailableInDebugBuilds = 1856,
    /// <summary>Replay file is not open.</summary>
    ReplayFileIsNotOpen = 1857,
    /// <summary>Size inline policy is only available in Debug builds.</summary>
    SizeInlinePolicyIsOnlyAvailableInDebugBuilds = 1858,
    /// <summary>ARM64 condition mapping is not available for this target.</summary>
    ARM64ConditionMappingIsNotAvailableForThisTarget = 1859,
    /// <summary>ARM32 immediate validation is not supported for this target.</summary>
    ARM32ImmediateValidationIsNotSupportedForThisTarget = 1860,
    /// <summary>ARM32 stack-pointer immediate validation is not supported for this target.</summary>
    ARM32StackPointerImmediateValidationIsNotSupportedForThisTarget = 1861,
    /// <summary>ARM32 zero materialization is not supported for this target.</summary>
    ARM32ZeroMaterializationIsNotSupportedForThisTarget = 1862,
    /// <summary>ARM32 relocation materialization is not supported for this target.</summary>
    ARM32RelocationMaterializationIsNotSupportedForThisTarget = 1863,
    /// <summary>ARM32 data-label materialization is not supported for this target.</summary>
    ARM32DataLabelMaterializationIsNotSupportedForThisTarget = 1864,
    /// <summary>ARM immediate validation is not available for the configured target.</summary>
    ARMImmediateValidationIsNotAvailableForTheConfiguredTarget = 1865,
    /// <summary>Branch target range validation is not supported for this target.</summary>
    BranchTargetRangeValidationIsNotSupportedForThisTarget = 1866,
    /// <summary>Integer extension instruction selection is not implemented on this target.</summary>
    IntegerExtensionInstructionSelectionIsNotImplementedOnThisTarget = 1867,
    /// <summary>Masked-register extension requires masked hardware intrinsics.</summary>
    MaskedRegisterExtensionRequiresMaskedHardwareIntrinsics = 1868,
    /// <summary>Masked-register extension is not implemented for this target.</summary>
    MaskedRegisterExtensionIsNotImplementedForThisTarget = 1869,
    /// <summary>Floating-point extension instruction selection is not implemented on this target.</summary>
    FloatingPointExtensionInstructionSelectionIsNotImplementedOnThisTarget = 1870,
    /// <summary>Invalid integer-to-floating conversion.</summary>
    InvalidIntegerToFloatingConversion = 1871,
    /// <summary>Invalid unsigned-to-floating conversion.</summary>
    InvalidUnsignedToFloatingConversion = 1872,
    /// <summary>long to float</summary>
    LongToFloat = 1873,
    /// <summary>long to double</summary>
    LongToDouble = 1874,
    /// <summary>Invalid long-to-floating conversion.</summary>
    InvalidLongToFloatingConversion = 1875,
    /// <summary>float to long</summary>
    FloatToLong = 1876,
    /// <summary>Invalid float conversion.</summary>
    InvalidFloatConversion = 1877,
    /// <summary>double to long</summary>
    DoubleToLong = 1878,
    /// <summary>Invalid double conversion.</summary>
    InvalidDoubleConversion = 1879,
    /// <summary>Floating conversion instruction selection requires xarch or ARM.</summary>
    FloatingConversionInstructionSelectionRequiresXarchOrARM = 1880,
    /// <summary>inst_RV_SH - unknown target</summary>
    InstRVSHUnknownTarget = 1881,
    /// <summary>No instruction-name table is defined for the configured target.</summary>
    NoInstructionNameTableIsDefinedForTheConfiguredTarget = 1882,
    /// <summary>WebAssembly SIMD loads require SIMD support.</summary>
    WebAssemblySIMDLoadsRequireSIMDSupport = 1883,
    /// <summary>Integer load selection requires a supported target.</summary>
    IntegerLoadSelectionRequiresASupportedTarget = 1884,
    /// <summary>Masked-register loads require masked hardware intrinsics.</summary>
    MaskedRegisterLoadsRequireMaskedHardwareIntrinsics = 1885,
    /// <summary>Masked-register loads are not implemented for this target.</summary>
    MaskedRegisterLoadsAreNotImplementedForThisTarget = 1886,
    /// <summary>Floating-point load selection requires a supported target.</summary>
    FloatingPointLoadSelectionRequiresASupportedTarget = 1887,
    /// <summary>Invalid floating math operator.</summary>
    InvalidFloatingMathOperator = 1888,
    /// <summary>Floating-point math instruction selection is not implemented for this target.</summary>
    FloatingPointMathInstructionSelectionIsNotImplementedForThisTarget = 1889,
    /// <summary>Instruction operand classification requires xarch.</summary>
    InstructionOperandClassificationRequiresXarch = 1890,
    /// <summary>Hardware instruction operands require hardware intrinsics.</summary>
    HardwareInstructionOperandsRequireHardwareIntrinsics = 1891,
    /// <summary>Vector constant operands require SIMD support.</summary>
    VectorConstantOperandsRequireSIMDSupport = 1892,
    /// <summary>Emitter vector-constant operands are not implemented outside xarch.</summary>
    EmitterVectorConstantOperandsAreNotImplementedOutsideXarch = 1893,
    /// <summary>Mask constant operands require masked hardware intrinsics.</summary>
    MaskConstantOperandsRequireMaskedHardwareIntrinsics = 1894,
    /// <summary>Single-operand instruction generation requires xarch.</summary>
    SingleOperandInstructionGenerationRequiresXarch = 1895,
    /// <summary>Register/operand instruction generation requires xarch.</summary>
    RegisterOperandInstructionGenerationRequiresXarch = 1896,
    /// <summary>Register/register/immediate instruction generation requires xarch.</summary>
    RegisterRegisterImmediateInstructionGenerationRequiresXarch = 1897,
    /// <summary>Register/memory/immediate instruction generation requires xarch.</summary>
    RegisterMemoryImmediateInstructionGenerationRequiresXarch = 1898,
    /// <summary>SIMD register/operand/immediate generation requires xarch.</summary>
    SIMDRegisterOperandImmediateGenerationRequiresXarch = 1899,
    /// <summary>Embedded broadcast instruction options require xarch hardware intrinsics.</summary>
    EmbeddedBroadcastInstructionOptionsRequireXarchHardwareIntrinsics = 1900,
    /// <summary>Three-operand register/memory instruction generation requires xarch.</summary>
    ThreeOperandRegisterMemoryInstructionGenerationRequiresXarch = 1901,
    /// <summary>inst_RV_RV_RV is not implemented for this target.</summary>
    InstRVRVRVIsNotImplementedForThisTarget = 1902,
    /// <summary>Stack variable reference requires a local variable or store.</summary>
    StackVariableReferenceRequiresALocalVariableOrStore = 1903,
    /// <summary>Integer copy instruction selection is not implemented on this target.</summary>
    IntegerCopyInstructionSelectionIsNotImplementedOnThisTarget = 1904,
    /// <summary>Masked-register copies require masked hardware intrinsics.</summary>
    MaskedRegisterCopiesRequireMaskedHardwareIntrinsics = 1905,
    /// <summary>Masked-register copies are not implemented for this target.</summary>
    MaskedRegisterCopiesAreNotImplementedForThisTarget = 1906,
    /// <summary>Floating-point copy instruction selection is not implemented on this target.</summary>
    FloatingPointCopyInstructionSelectionIsNotImplementedOnThisTarget = 1907,
    /// <summary>Masked-register to integer copies are not implemented for this target.</summary>
    MaskedRegisterToIntegerCopiesAreNotImplementedForThisTarget = 1908,
    /// <summary>Floating-point to integer copies are not implemented on this target.</summary>
    FloatingPointToIntegerCopiesAreNotImplementedOnThisTarget = 1909,
    /// <summary>Integer to floating-point copies are not implemented on this target.</summary>
    IntegerToFloatingPointCopiesAreNotImplementedOnThisTarget = 1910,
    /// <summary>Register move destination type is not implemented.</summary>
    RegisterMoveDestinationTypeIsNotImplemented = 1911,
    /// <summary>LoongArch64 inst_RV is unused and not implemented.</summary>
    LoongArch64InstRVIsUnusedAndNotImplemented = 1912,
    /// <summary>RISC-V inst_RV is unused and not implemented.</summary>
    RISCVInstRVIsUnusedAndNotImplemented = 1913,
    /// <summary>Set-condition generation is not implemented on this target.</summary>
    SetConditionGenerationIsNotImplementedOnThisTarget = 1914,
    /// <summary>An eight-byte floating register store requires a 64-bit target.</summary>
    EightByteFloatingRegisterStoreRequiresA64BitTarget = 1915,
    /// <summary>Masked-register stores require masked hardware intrinsics.</summary>
    MaskedRegisterStoresRequireMaskedHardwareIntrinsics = 1916,
    /// <summary>An eight-byte integer register store requires a 64-bit target.</summary>
    EightByteIntegerRegisterStoreRequiresA64BitTarget = 1917,
    /// <summary>WebAssembly SIMD stores require SIMD support.</summary>
    WebAssemblySIMDStoresRequireSIMDSupport = 1918,
    /// <summary>Integer store selection requires a supported target.</summary>
    IntegerStoreSelectionRequiresASupportedTarget = 1919,
    /// <summary>Masked-register stores are not implemented for this target.</summary>
    MaskedRegisterStoresAreNotImplementedForThisTarget = 1920,
    /// <summary>Floating-point store selection requires a supported target.</summary>
    FloatingPointStoreSelectionRequiresASupportedTarget = 1921,
    /// <summary>Xarch floating-point stack stores are not supported for this target.</summary>
    XarchFloatingPointStackStoresAreNotSupportedForThisTarget = 1922,
    /// <summary>{0} is not supported for this target.</summary>
    ArgumentIsNotSupportedForThisTarget = 1923,
    /// <summary>SIMD Unimplemented yet LOONGARCH</summary>
    SIMDUnimplementedYetLOONGARCH = 1924,
    /// <summary>Exactly one target architecture must be selected.</summary>
    ExactlyOneTargetArchitectureMustBeSelected = 1925,
    /// <summary>Target bitness does not match the selected architecture.</summary>
    TargetBitnessDoesNotMatchTheSelectedArchitecture = 1926,
    /// <summary>When UNIX_AMD64_ABI is enabled, TARGET_AMD64 must be enabled.</summary>
    WhenUNIXAMD64ABIIsEnabledTARGETAMD64MustBeEnabled = 1927,
    /// <summary>When UNIX_X86_ABI is enabled, TARGET_X86 must be enabled.</summary>
    WhenUNIXX86ABIIsEnabledTARGETX86MustBeEnabled = 1928,
    /// <summary>IL instruction dumping is only available in Debug builds.</summary>
    ILInstructionDumpingIsOnlyAvailableInDebugBuilds = 1929,
    /// <summary>JitTls scopes are only available in Debug builds.</summary>
    JitTlsScopesAreOnlyAvailableInDebugBuilds = 1930,
    /// <summary>Importer debug state is only available in Debug builds.</summary>
    ImporterDebugStateIsOnlyAvailableInDebugBuilds = 1931,
    /// <summary>Importer debug state requires an active JitTls scope.</summary>
    ImporterDebugStateRequiresAnActiveJitTlsScope = 1932,
    /// <summary>JitTls.LogEnv is only available in Debug builds.</summary>
    JitTlsLogEnvIsOnlyAvailableInDebugBuilds = 1933,
    /// <summary>Non-pointer GC register marking without a fixed register set is not implemented.</summary>
    NonPointerGCRegisterMarkingWithoutAFixedRegisterSetIsNotImplemented = 1934,
    /// <summary>Small primitive keys of type {0} are not supported.</summary>
    SmallPrimitiveKeysOfTypeArgumentAreNotSupported = 1937,
    /// <summary>Large primitive keys of size {0} are not supported.</summary>
    LargePrimitiveKeysOfSizeArgumentAreNotSupported = 1938,
    /// <summary>Pointer keys of type {0} are not supported.</summary>
    PointerKeysOfTypeArgumentAreNotSupported = 1939,
    /// <summary>The value reference is uninitialized.</summary>
    ValueReferenceIsUninitialized = 1940,
    /// <summary>The hash table must be allocated before removing keys.</summary>
    HashTableMustBeAllocatedBeforeRemovingKeys = 1941,
    /// <summary>The hash-table behavior returned from its no-memory handler.</summary>
    HashTableBehaviorReturnedFromItsNoMemoryHandler = 1942,
    /// <summary>Iterator traits are not defined for {0}.</summary>
    IteratorTraitsAreNotDefinedForArgument = 1944,
    /// <summary>The list iterators are not part of the same sequence.</summary>
    ListIteratorsAreNotPartOfTheSameSequence = 1945,
    /// <summary>The end iterator cannot be dereferenced.</summary>
    EndIteratorCannotBeDereferenced = 1946,
    /// <summary>The iterator refers to an erased node.</summary>
    IteratorRefersToAnErasedNode = 1947,
    /// <summary>The list is empty.</summary>
    ListIsEmpty = 1948,
    /// <summary>The end iterator is not reachable from the start iterator.</summary>
    EndIteratorIsNotReachableFromTheStartIterator = 1949,
    /// <summary>Cannot remove an element from an empty list.</summary>
    CannotRemoveAnElementFromAnEmptyList = 1950,
    /// <summary>The end iterator does not belong to this list.</summary>
    EndIteratorDoesNotBelongToThisList = 1951,
    /// <summary>The iterator does not refer to a live node in this list.</summary>
    IteratorDoesNotReferToALiveNodeInThisList = 1952,
    /// <summary>List node transfer requires a shared allocation scope.</summary>
    ListNodeTransferRequiresASharedAllocationScope = 1953,
    /// <summary>Vector iterators must refer to the same allocation.</summary>
    VectorIteratorsMustReferToTheSameAllocation = 1954,
    /// <summary>The vector is empty.</summary>
    VectorIsEmpty = 1955,
    /// <summary>Cannot remove an element from an empty vector.</summary>
    CannotRemoveAnElementFromAnEmptyVector = 1956,
    /// <summary>The first iterator is not initialized.</summary>
    FirstIteratorIsNotInitialized = 1957,
    /// <summary>The range iterators must refer to the same vector.</summary>
    RangeIteratorsMustReferToTheSameVector = 1958,
    /// <summary>The end iterator precedes the start iterator.</summary>
    EndIteratorPrecedesTheStartIterator = 1959,
    /// <summary>The vector changed during enumeration.</summary>
    VectorChangedDuringEnumeration = 1960,
    /// <summary>The iterator does not belong to this vector.</summary>
    IteratorDoesNotBelongToThisVector = 1961,
    /// <summary>The vector iterator was invalidated by reallocation.</summary>
    VectorIteratorWasInvalidatedByReallocation = 1962,
    /// <summary>Class layout names are only available in Debug builds.</summary>
    ClassLayoutNamesAreOnlyAvailableInDebugBuilds = 1964,
    /// <summary>Value consumption state is only available in Debug builds.</summary>
    ValueConsumptionStateIsOnlyAvailableInDebugBuilds = 1965,
    /// <summary>Consumption state is only available in Debug builds.</summary>
    ConsumptionStateIsOnlyAvailableInDebugBuilds = 1966,
    /// <summary>LIR validation is only available in Debug builds.</summary>
    LIRValidationIsOnlyAvailableInDebugBuilds = 1967,
    /// <summary>Tree-range flag traversal is only available in Debug builds.</summary>
    TreeRangeFlagTraversalIsOnlyAvailableInDebugBuilds = 1968,
    /// <summary>Early tree liveness requires the non-SSA early policy.</summary>
    EarlyTreeLivenessRequiresTheNonSSAEarlyPolicy = 1969,
    /// <summary>LIR liveness orchestration requires a non-early LIR policy.</summary>
    LIRLivenessOrchestrationRequiresANonEarlyLIRPolicy = 1970,
    /// <summary>SSA tree liveness requires the non-LIR SSA policy.</summary>
    SSATreeLivenessRequiresTheNonLIRSSAPolicy = 1971,
    /// <summary>Stack argument lowering is not supported for Wasm.</summary>
    StackArgumentLoweringIsNotSupportedForWasm = 1972,
    /// <summary>Stack argument lowering is not ported for this target.</summary>
    StackArgumentLoweringIsNotPortedForThisTarget = 1973,
    /// <summary>Multiply containment is not ported for this target.</summary>
    MultiplyContainmentIsNotPortedForThisTarget = 1975,
    /// <summary>Multiply lowering is not ported for this target.</summary>
    MultiplyLoweringIsNotPortedForThisTarget = 1976,
    /// <summary>Unexpected ARM64 immediate intrinsic operand count.</summary>
    UnexpectedARM64ImmediateIntrinsicOperandCount = 1977,
    /// <summary>Unexpected ARM64 containment intrinsic: {0}.</summary>
    UnexpectedARM64ContainmentIntrinsicArgument = 1978,
    /// <summary>ARM64 comparison local could not be cloned.</summary>
    ARM64ComparisonLocalCouldNotBeCloned = 1979,
    /// <summary>Unexpected ARM64 vector comparison base type: {0}.</summary>
    UnexpectedARM64VectorComparisonBaseTypeArgument = 1980,
    /// <summary>ARM64 dot-product local could not be cloned.</summary>
    ARM64DotProductLocalCouldNotBeCloned = 1981,
    /// <summary>Expected an array metadata node.</summary>
    ExpectedAnArrayMetadataNode = 1983,
    /// <summary>Binary containment is not ported for this target.</summary>
    BinaryContainmentIsNotPortedForThisTarget = 1984,
    /// <summary>Binary arithmetic lowering is not ported for this target.</summary>
    BinaryArithmeticLoweringIsNotPortedForThisTarget = 1987,
    /// <summary>Block-store address containment is not ported for this target.</summary>
    BlockStoreAddressContainmentIsNotPortedForThisTarget = 1989,
    /// <summary>Bounds-check containment outside xarch, ARM64, LoongArch64 and RISC-V64 is not ported.</summary>
    BoundsCheckContainmentOutsideXarchARM64LoongArch64AndRISCV64IsNotPorted = 1990,
    /// <summary>Unbalanced indirect call data flow.</summary>
    UnbalancedIndirectCallDataFlow = 1991,
    /// <summary>Call operand containment is not ported for this target.</summary>
    CallOperandContainmentIsNotPortedForThisTarget = 1992,
    /// <summary>Tail-call helper target argument is missing.</summary>
    TailCallHelperTargetArgumentIsMissing = 1993,
    /// <summary>Tail-call helper target placement is missing.</summary>
    TailCallHelperTargetPlacementIsMissing = 1994,
    /// <summary>Tail-call helper flags argument is missing.</summary>
    TailCallHelperFlagsArgumentIsMissing = 1995,
    /// <summary>Tail-call helper flags placement is missing.</summary>
    TailCallHelperFlagsPlacementIsMissing = 1996,
    /// <summary>Tail-call helper size argument is missing.</summary>
    TailCallHelperSizeArgumentIsMissing = 1997,
    /// <summary>Tail-call helper size placement is missing.</summary>
    TailCallHelperSizePlacementIsMissing = 1998,
    /// <summary>Tail-call helper incoming size argument is missing.</summary>
    TailCallHelperIncomingSizeArgumentIsMissing = 1999,
    /// <summary>Unexpected floating conversion destination.</summary>
    UnexpectedFloatingConversionDestination = 2000,
    /// <summary>Cast lowering is not ported for this target.</summary>
    CastLoweringIsNotPortedForThisTarget = 2001,
    /// <summary>Cast containment is not ported for this target.</summary>
    CastContainmentIsNotPortedForThisTarget = 2002,
    /// <summary>Comparison containment is not ported for this target.</summary>
    ComparisonContainmentIsNotPortedForThisTarget = 2003,
    /// <summary>The target-specific LowerJTrue override is not ported.</summary>
    TargetSpecificLowerJTrueOverrideIsNotPorted = 2004,
    /// <summary>Unexpected condition type.</summary>
    UnexpectedConditionType = 2005,
    /// <summary>Conditional compare lowering is only supported for x64 and ARM64.</summary>
    ConditionalCompareLoweringIsOnlySupportedForX64AndARM64 = 2006,
    /// <summary>Conditional compare containment is not ported for this target.</summary>
    ConditionalCompareContainmentIsNotPortedForThisTarget = 2007,
    /// <summary>Immediate containment is not ported for this target.</summary>
    ImmediateContainmentIsNotPortedForThisTarget = 2008,
    /// <summary>Lowering.LowerCFGCall dispatcher register is not ported for this target.</summary>
    LoweringLowerCFGCallDispatcherRegisterIsNotPortedForThisTarget = 2009,
    /// <summary>Long-to-long no-op cast decomposition is not ported.</summary>
    LongToLongNoOpCastDecompositionIsNotPorted = 2010,
    /// <summary>Cast decomposition is not ported.</summary>
    CastDecompositionIsNotPorted = 2011,
    /// <summary>Hardware-intrinsic long decomposition is not available.</summary>
    HardwareIntrinsicLongDecompositionIsNotAvailable = 2012,
    /// <summary>Unexpected GT_HWINTRINSIC node in long decomposition.</summary>
    UnexpectedGTHWINTRINSICNodeInLongDecomposition = 2013,
    /// <summary>Long negation decomposition is not ported for this target.</summary>
    LongNegationDecompositionIsNotPortedForThisTarget = 2014,
    /// <summary>Unexpected long shift operator.</summary>
    UnexpectedLongShiftOperator = 2015,
    /// <summary>Interlocked operations on TYP_LONG are not ported.</summary>
    InterlockedOperationsOnTYPLONGAreNotPorted = 2016,
    /// <summary>Illegal TYP_LONG node in Decomposition.</summary>
    IllegalTYPLONGNodeInDecomposition = 2017,
    /// <summary>GetHiOper called for invalid oper</summary>
    GetHiOperCalledForInvalidOper = 2018,
    /// <summary>GetLoOper called for invalid oper</summary>
    GetLoOperCalledForInvalidOper = 2019,
    /// <summary>Division containment is not ported for this target.</summary>
    DivisionContainmentIsNotPortedForThisTarget = 2020,
    /// <summary>Fast tailcall lowering is not available for this target.</summary>
    FastTailcallLoweringIsNotAvailableForThisTarget = 2021,
    /// <summary>Stack argument must precede the tail call.</summary>
    StackArgumentMustPrecedeTheTailCall = 2022,
    /// <summary>Fast tailcall lowering requires a current block.</summary>
    FastTailcallLoweringRequiresACurrentBlock = 2023,
    /// <summary>Stack argument has no operand.</summary>
    StackArgumentHasNoOperand = 2024,
    /// <summary>Tail-call argument use extends beyond the call.</summary>
    TailCallArgumentUseExtendsBeyondTheCall = 2025,
    /// <summary>Struct parameter has no layout.</summary>
    StructParameterHasNoLayout = 2026,
    /// <summary>Unexpected broadcast size: {0}.</summary>
    UnexpectedBroadcastSizeArgument = 2027,
    /// <summary>Unexpected broadcast type: {0}.</summary>
    UnexpectedBroadcastTypeArgument = 2028,
    /// <summary>PTEST local could not be cloned.</summary>
    PTESTLocalCouldNotBeCloned = 2029,
    /// <summary>Unexpected vector comparison base type: {0}.</summary>
    UnexpectedVectorComparisonBaseTypeArgument = 2030,
    /// <summary>EVEX mask local could not be cloned.</summary>
    EVEXMaskLocalCouldNotBeCloned = 2032,
    /// <summary>Conditional-select mask local could not be cloned.</summary>
    ConditionalSelectMaskLocalCouldNotBeCloned = 2033,
    /// <summary>Hardware condition-code lowering is not available for this target.</summary>
    HardwareConditionCodeLoweringIsNotAvailableForThisTarget = 2034,
    /// <summary>SIMD lowering is unsupported on this target.</summary>
    SIMDLoweringIsUnsupportedOnThisTarget = 2035,
    /// <summary>Unhandled binary immediate hardware intrinsic: {0}.</summary>
    UnhandledBinaryImmediateHardwareIntrinsicArgument = 2036,
    /// <summary>Unhandled binary helper hardware intrinsic: {0}.</summary>
    UnhandledBinaryHelperHardwareIntrinsicArgument = 2037,
    /// <summary>Unexpected binary hardware intrinsic category: {0}.</summary>
    UnexpectedBinaryHardwareIntrinsicCategoryArgument = 2038,
    /// <summary>Unexpected hardware intrinsic operand count: {0}.</summary>
    UnexpectedHardwareIntrinsicOperandCountArgument = 2039,
    /// <summary>LoongArch64 hardware-intrinsic containment is not ported.</summary>
    LoongArch64HardwareIntrinsicContainmentIsNotPorted = 2040,
    /// <summary>Unexpected quaternary hardware intrinsic category: {0}.</summary>
    UnexpectedQuaternaryHardwareIntrinsicCategoryArgument = 2041,
    /// <summary>Ternary logic control {0:X2} was not normalized before containment.</summary>
    TernaryLogicControlArgumentWasNotNormalizedBeforeContainment = 2042,
    /// <summary>Unhandled quaternary immediate hardware intrinsic: {0}.</summary>
    UnhandledQuaternaryImmediateHardwareIntrinsicArgument = 2043,
    /// <summary>Invalid permutation element size.</summary>
    InvalidPermutationElementSize = 2044,
    /// <summary>Unhandled ternary hardware intrinsic: {0}.</summary>
    UnhandledTernaryHardwareIntrinsicArgument = 2045,
    /// <summary>Unhandled ternary immediate hardware intrinsic: {0}.</summary>
    UnhandledTernaryImmediateHardwareIntrinsicArgument = 2046,
    /// <summary>Unexpected ternary hardware intrinsic category: {0}.</summary>
    UnexpectedTernaryHardwareIntrinsicCategoryArgument = 2047,
    /// <summary>Unexpected unary hardware intrinsic category: {0}.</summary>
    UnexpectedUnaryHardwareIntrinsicCategoryArgument = 2048,
    /// <summary>Vector Create lowering is not ported for this target.</summary>
    VectorCreateLoweringIsNotPortedForThisTarget = 2049,
    /// <summary>Vector Dot multiply/sum lowering is not ported for this target.</summary>
    VectorDotMultiplySumLoweringIsNotPortedForThisTarget = 2050,
    /// <summary>RISC-V64 hardware-intrinsic Dot lowering is not ported.</summary>
    RISCV64HardwareIntrinsicDotLoweringIsNotPorted = 2051,
    /// <summary>LoongArch64 hardware-intrinsic Dot lowering is not ported.</summary>
    LoongArch64HardwareIntrinsicDotLoweringIsNotPorted = 2052,
    /// <summary>Vector Dot lowering is not ported for this target.</summary>
    VectorDotLoweringIsNotPortedForThisTarget = 2053,
    /// <summary>Vector GetElement lowering is not ported for this target.</summary>
    VectorGetElementLoweringIsNotPortedForThisTarget = 2054,
    /// <summary>Fused multiply lowering is not ported for this target.</summary>
    FusedMultiplyLoweringIsNotPortedForThisTarget = 2055,
    /// <summary>Hardware-intrinsic lowering is not available.</summary>
    HardwareIntrinsicLoweringIsNotAvailable = 2057,
    /// <summary>LoongArch64 hardware-intrinsic lowering is not ported.</summary>
    LoongArch64HardwareIntrinsicLoweringIsNotPorted = 2058,
    /// <summary>RISC-V64 hardware-intrinsic lowering is not ported.</summary>
    RISCV64HardwareIntrinsicLoweringIsNotPorted = 2059,
    /// <summary>Hardware-intrinsic lowering is not ported for this target.</summary>
    HardwareIntrinsicLoweringIsNotPortedForThisTarget = 2060,
    /// <summary>Unexpected tuple type: {0}.</summary>
    UnexpectedTupleTypeArgument = 2061,
    /// <summary>Unexpected intrinsic category: {0}.</summary>
    UnexpectedIntrinsicCategoryArgument = 2062,
    /// <summary>Vector ToScalar lowering is not ported for this target.</summary>
    VectorToScalarLoweringIsNotPortedForThisTarget = 2063,
    /// <summary>Vector WithElement lowering is not ported for this target.</summary>
    VectorWithElementLoweringIsNotPortedForThisTarget = 2064,
    /// <summary>A closed store tree range must include its root.</summary>
    ClosedStoreTreeRangeMustIncludeItsRoot = 2065,
    /// <summary>Store coalescing requires code generation state.</summary>
    StoreCoalescingRequiresCodeGenerationState = 2066,
    /// <summary>RISC-V SIMD indirection containment is not ported.</summary>
    RISCVSIMDIndirectionContainmentIsNotPorted = 2067,
    /// <summary>LoongArch SIMD indirection containment is not ported.</summary>
    LoongArchSIMDIndirectionContainmentIsNotPorted = 2068,
    /// <summary>Foldable Wasm address is missing its base.</summary>
    FoldableWasmAddressIsMissingItsBase = 2069,
    /// <summary>Indirection containment is not ported for this target.</summary>
    IndirectionContainmentIsNotPortedForThisTarget = 2070,
    /// <summary>Unused indirection lowering is not ported for this target.</summary>
    UnusedIndirectionLoweringIsNotPortedForThisTarget = 2071,
    /// <summary>Scalar intrinsic containment is not ported for this target.</summary>
    ScalarIntrinsicContainmentIsNotPortedForThisTarget = 2072,
    /// <summary>Only unary casts and binary operations can be folded in LIR.</summary>
    OnlyUnaryCastsAndBinaryOperationsCanBeFoldedInLIR = 2073,
    /// <summary>Nonlocal jump containment is not ported for this target.</summary>
    NonlocalJumpContainmentIsNotPortedForThisTarget = 2074,
    /// <summary>Local-store lowering is not ported for this target.</summary>
    LocalStoreLoweringIsNotPortedForThisTarget = 2075,
    /// <summary>Local-store containment is not ported for this target.</summary>
    LocalStoreContainmentIsNotPortedForThisTarget = 2076,
    /// <summary>Unsupported memory comparison load width.</summary>
    UnsupportedMemoryComparisonLoadWidth = 2077,
    /// <summary>Indirect P/Invoke target is not in LIR.</summary>
    IndirectPInvokeTargetIsNotInLIR = 2078,
    /// <summary>Relative P/Invoke target lookup is unsupported by the native contract.</summary>
    RelativePInvokeTargetLookupIsUnsupportedByTheNativeContract = 2079,
    /// <summary>Invalid P/Invoke target access type.</summary>
    InvalidPInvokeTargetAccessType = 2080,
    /// <summary>Swift error handling is not supported for this target.</summary>
    SwiftErrorHandlingIsNotSupportedForThisTarget = 2081,
    /// <summary>Swift call has no error-register consumer.</summary>
    SwiftCallHasNoErrorRegisterConsumer = 2082,
    /// <summary>Reached block start while searching for tail-call arguments.</summary>
    ReachedBlockStartWhileSearchingForTailCallArguments = 2083,
    /// <summary>RISC-V64 SIMD lowering was dispatched for another target.</summary>
    RISCV64SIMDLoweringWasDispatchedForAnotherTarget = 2085,
    /// <summary>RISC-V64 SIMD lowering is not ported.</summary>
    RISCV64SIMDLoweringIsNotPorted = 2086,
    /// <summary>RISC-V64 SIMD containment was dispatched for another target.</summary>
    RISCV64SIMDContainmentWasDispatchedForAnotherTarget = 2087,
    /// <summary>RISC-V64 SIMD containment is not ported.</summary>
    RISCV64SIMDContainmentIsNotPorted = 2088,
    /// <summary>RISC-V64 hardware-intrinsic containment was dispatched incorrectly.</summary>
    RISCV64HardwareIntrinsicContainmentWasDispatchedIncorrectly = 2093,
    /// <summary>RISC-V64 hardware-intrinsic containment is not ported.</summary>
    RISCV64HardwareIntrinsicContainmentIsNotPorted = 2094,
    /// <summary>RISC-V conditional select requires Zicond.</summary>
    RISCVConditionalSelectRequiresZicond = 2095,
    /// <summary>Select node containment is unavailable for the current target.</summary>
    SelectNodeContainmentIsUnavailableForThisTarget = 2096,
    /// <summary>Shift containment is not ported for this target.</summary>
    ShiftContainmentIsNotPortedForThisTarget = 2097,
    /// <summary>Integral division should be lowered to helper calls for this target.</summary>
    IntegralDivisionShouldBeLoweredToHelperCallsForThisTarget = 2098,
    /// <summary>64-bit signed magic division requires a 64-bit target.</summary>
    Failure64BitSignedMagicDivisionRequiresA64BitTarget = 2099,
    /// <summary>Indirect-store lowering is not ported for this target.</summary>
    IndirectStoreLoweringIsNotPortedForThisTarget = 2100,
    /// <summary>Unexpected tuple type for a narrowing conversion.</summary>
    UnexpectedTupleTypeForANarrowingConversion = 2101,
    /// <summary>Indirect-store containment is not ported for this target.</summary>
    IndirectStoreContainmentIsNotPortedForThisTarget = 2102,
    /// <summary>HFA struct-call lowering is not supported for this target.</summary>
    HFAStructCallLoweringIsNotSupportedForThisTarget = 2103,
    /// <summary>Hardware-intrinsic struct-call lowering is not available.</summary>
    HardwareIntrinsicStructCallLoweringIsNotAvailable = 2104,
    /// <summary>The range to lower must not be empty.</summary>
    RangeToLowerMustNotBeEmpty = 2105,
    /// <summary>Hardware-intrinsic lowering requires hardware intrinsics to be enabled.</summary>
    HardwareIntrinsicLoweringRequiresHardwareIntrinsicsToBeEnabled = 2106,
    /// <summary>Unsigned integer division should be lowered to helper calls for this target.</summary>
    UnsignedIntegerDivisionShouldBeLoweredToHelperCallsForThisTarget = 2107,
    /// <summary>Native-width division must use the 32-bit path on this target.</summary>
    NativeWidthDivisionMustUseThe32BitPathOnThisTarget = 2108,
    /// <summary>Wasm stackification was dispatched for another target.</summary>
    WasmStackificationWasDispatchedForAnotherTarget = 2109,
    /// <summary>A Wasm stackification root has no preceding node.</summary>
    WasmStackificationRootHasNoPrecedingNode = 2110,
    /// <summary>A Wasm stackification use has no preceding node.</summary>
    WasmStackificationUseHasNoPrecedingNode = 2111,
    /// <summary>Wasm stackification did not process a node.</summary>
    WasmStackificationDidNotProcessANode = 2112,
    /// <summary>A Wasm stackifier move requires an exclusive range endpoint.</summary>
    WasmStackifierMoveRequiresAnExclusiveRangeEndpoint = 2113,
    /// <summary>A stackifier temporary disappeared while in use.</summary>
    StackifierTemporaryDisappearedWhileInUse = 2114,
    /// <summary>A root use has no owning node.</summary>
    RootUseHasNoOwningNode = 2115,
    /// <summary>A root use has no operand edge.</summary>
    RootUseHasNoOperandEdge = 2116,
    /// <summary>Wasm hardware-intrinsic lowering is not available.</summary>
    WasmHardwareIntrinsicLoweringIsNotAvailable = 2117,
    /// <summary>The interval index is only available in Debug builds.</summary>
    IntervalIndexIsOnlyAvailableInDebugBuilds = 2118,
    /// <summary>LSRA allocation table initial register set on this target.</summary>
    LSRAAllocationTableInitialRegisterSetOnThisTarget = 2119,
    /// <summary>Upper-vector intervals must retain their related local.</summary>
    UpperVectorIntervalsMustRetainTheirRelatedLocal = 2120,
    /// <summary>Invalid LSRA statistic.</summary>
    InvalidLSRAStatistic = 2121,
    /// <summary>LSRA statistics require initialized block information.</summary>
    LSRAStatisticsRequireInitializedBlockInformation = 2122,
    /// <summary>LSRA statistics are not enabled.</summary>
    LSRAStatisticsAreNotEnabled = 2123,
    /// <summary>LSRA statistic block number is outside the block table.</summary>
    LSRAStatisticBlockNumberIsOutsideTheBlockTable = 2124,
    /// <summary>LSRA statistics do not cover a pre-resolution block.</summary>
    LSRAStatisticsDoNotCoverAPreResolutionBlock = 2125,
    /// <summary>A pre-resolution block has no LSRA statistics.</summary>
    PreResolutionBlockHasNoLSRAStatistics = 2126,
    /// <summary>Stress spilling requires an assigned register.</summary>
    StressSpillingRequiresAnAssignedRegister = 2127,
    /// <summary>An upper-vector save requires a matching restore.</summary>
    UpperVectorSaveRequiresAMatchingRestore = 2128,
    /// <summary>A live this interval requires an incoming map.</summary>
    LiveThisIntervalRequiresAnIncomingMap = 2129,
    /// <summary>Unsupported full LSRA allocation event: {0}.</summary>
    UnsupportedFullLSRAAllocationEventArgument = 2131,
    /// <summary>A consecutive upper-vector restore requires a related interval.</summary>
    ConsecutiveUpperVectorRestoreRequiresARelatedInterval = 2132,
    /// <summary>A consecutive upper-vector restore must precede a use.</summary>
    ConsecutiveUpperVectorRestoreMustPrecedeAUse = 2133,
    /// <summary>A consecutive register sequence ended prematurely.</summary>
    ConsecutiveRegisterSequenceEndedPrematurely = 2134,
    /// <summary>A consecutive sequence requires additional references.</summary>
    ConsecutiveSequenceRequiresAdditionalReferences = 2135,
    /// <summary>A consecutive sequence ended prematurely.</summary>
    ConsecutiveSequenceEndedPrematurely = 2136,
    /// <summary>An active consecutive interval has no register.</summary>
    ActiveConsecutiveIntervalHasNoRegister = 2137,
    /// <summary>Consecutive register reference is missing its successor.</summary>
    ConsecutiveRegisterReferenceIsMissingItsSuccessor = 2138,
    /// <summary>ARM64 RMW operand position exceeds three.</summary>
    ARM64RMWOperandPositionExceedsThree = 2140,
    /// <summary>Embedded FMA allocation requires block sequence.</summary>
    EmbeddedFMAAllocationRequiresBlockSequence = 2141,
    /// <summary>ARM64 intrinsic {0} has unhandled immediate requirements.</summary>
    ARM64IntrinsicArgumentHasUnhandledImmediateRequirements = 2142,
    /// <summary>Unhandled ARM64 low-vector intrinsic {0}.</summary>
    UnhandledARM64LowVectorIntrinsicArgument = 2143,
    /// <summary>ARM64 intrinsic {0} has unhandled consecutive register requirements.</summary>
    ARM64IntrinsicArgumentHasUnhandledConsecutiveRegisterRequirements = 2144,
    /// <summary>Unexpected non-contained {0} in ARM64 LSRA.</summary>
    UnexpectedNonContainedArgumentInARM64LSRA = 2145,
    /// <summary>Unexpected ARM64 scalable vector constant kind.</summary>
    UnexpectedARM64ScalableVectorConstantKind = 2146,
    /// <summary>ARM64 integer remainder must be lowered and floating remainder is not implemented.</summary>
    ARM64IntegerRemainderMustBeLoweredAndFloatingRemainderIsNotImplemented = 2147,
    /// <summary>ARM32 store reference building requires initialized CodeGen.</summary>
    ARM32StoreReferenceBuildingRequiresInitializedCodeGen = 2148,
    /// <summary>Contained ARM32 block destination requires a base address.</summary>
    ContainedARM32BlockDestinationRequiresABaseAddress = 2151,
    /// <summary>Contained ARM32 block source requires a base address.</summary>
    ContainedARM32BlockSourceRequiresABaseAddress = 2152,
    /// <summary>ARM32 REG_SWIFT_ERROR is not defined.</summary>
    ARM32REGSWIFTERRORIsNotDefined = 2153,
    /// <summary>An x86 byte block store requires an internal integer definition.</summary>
    X86ByteBlockStoreRequiresAnInternalIntegerDefinition = 2155,
    /// <summary>Unsupported ARM64 initialization block kind {0}.</summary>
    UnsupportedARM64InitializationBlockKindArgument = 2156,
    /// <summary>Unsupported ARM64 copy block kind {0}.</summary>
    UnsupportedARM64CopyBlockKindArgument = 2157,
    /// <summary>Contained ARM64 block destination requires a base address.</summary>
    ContainedARM64BlockDestinationRequiresABaseAddress = 2158,
    /// <summary>Contained ARM64 block source requires a base address.</summary>
    ContainedARM64BlockSourceRequiresABaseAddress = 2159,
    /// <summary>Unsupported LoongArch64 initialization block kind {0}.</summary>
    UnsupportedLoongArch64InitializationBlockKindArgument = 2160,
    /// <summary>Unsupported RISC-V initialization block kind {0}.</summary>
    UnsupportedRISCVInitializationBlockKindArgument = 2161,
    /// <summary>Unsupported LoongArch64 copy block kind {0}.</summary>
    UnsupportedLoongArch64CopyBlockKindArgument = 2162,
    /// <summary>Unsupported RISC-V copy block kind {0}.</summary>
    UnsupportedRISCVCopyBlockKindArgument = 2163,
    /// <summary>LinearScan.buildBlockStore on this target is not implemented.</summary>
    LinearScanBuildBlockStoreOnThisTargetIsNotImplemented = 2164,
    /// <summary>A partial x86 field store requires an internal register.</summary>
    PartialX86FieldStoreRequiresAnInternalRegister = 2165,
    /// <summary>LinearScan.buildPutArgStk on this target is not implemented.</summary>
    LinearScanBuildPutArgStkOnThisTargetIsNotImplemented = 2166,
    /// <summary>LSRA call reference building is not implemented on this target.</summary>
    LSRACallReferenceBuildingIsNotImplementedOnThisTarget = 2167,
    /// <summary>The write-barrier register contract is not available on this target.</summary>
    WriteBarrierRegisterContractIsNotAvailableOnThisTarget = 2169,
    /// <summary>Optimized write barriers require initialized CodeGen.</summary>
    OptimizedWriteBarriersRequireInitializedCodeGen = 2170,
    /// <summary>Swift error register needs an argument use at the call.</summary>
    SwiftErrorRegisterNeedsAnArgumentUseAtTheCall = 2171,
    /// <summary>LSRA Swift error lifetime is not implemented on this target.</summary>
    LSRASwiftErrorLifetimeIsNotImplementedOnThisTarget = 2172,
    /// <summary>LSRA async continuation register is not defined on this target.</summary>
    LSRAAsyncContinuationRegisterIsNotDefinedOnThisTarget = 2173,
    /// <summary>Linear-scan allocation diagnostic state is only available in Debug builds.</summary>
    LinearScanAllocationDiagnosticStateIsOnlyAvailableInDebugBuilds = 2174,
    /// <summary>Resolution validation requires an incoming map.</summary>
    ResolutionValidationRequiresAnIncomingMap = 2177,
    /// <summary>Resolution validation requires an outgoing map.</summary>
    ResolutionValidationRequiresAnOutgoingMap = 2178,
    /// <summary>Ready resolution target requires a source interval.</summary>
    ReadyResolutionTargetRequiresASourceInterval = 2181,
    /// <summary>Double resolution requires a pending source.</summary>
    DoubleResolutionRequiresAPendingSource = 2182,
    /// <summary>Floating resolution requires a source interval.</summary>
    FloatingResolutionRequiresASourceInterval = 2183,
    /// <summary>Double resolution requires a source interval.</summary>
    DoubleResolutionRequiresASourceInterval = 2184,
    /// <summary>An LSRA-added local must have an owning use.</summary>
    LSRAAddedLocalMustHaveAnOwningUse = 2185,
    /// <summary>Final allocation verification with enregistered locals is not available.</summary>
    FinalAllocationVerificationWithEnregisteredLocalsIsNotAvailable = 2186,
    /// <summary>Resolution blocks require enregistered locals.</summary>
    ResolutionBlocksRequireEnregisteredLocals = 2187,
    /// <summary>Resolution moves require enregistered locals.</summary>
    ResolutionMovesRequireEnregisteredLocals = 2188,
    /// <summary>An allocation reference must refer to an interval.</summary>
    AllocationReferenceMustReferToAnInterval = 2189,
    /// <summary>A reload requires an assigned register.</summary>
    ReloadRequiresAnAssignedRegister = 2190,
    /// <summary>An assigned interval requires a register record.</summary>
    AssignedIntervalRequiresARegisterRecord = 2191,
    /// <summary>A register move requires a previous assignment.</summary>
    RegisterMoveRequiresAPreviousAssignment = 2192,
    /// <summary>A register move requires a destination register.</summary>
    RegisterMoveRequiresADestinationRegister = 2193,
    /// <summary>A copy requires a destination register.</summary>
    CopyRequiresADestinationRegister = 2194,
    /// <summary>A register assignment requires a register record.</summary>
    RegisterAssignmentRequiresARegisterRecord = 2195,
    /// <summary>Unsupported final-allocation reference type: {0}.</summary>
    UnsupportedFinalAllocationReferenceTypeArgument = 2196,
    /// <summary>A copy requires a home register.</summary>
    CopyRequiresAHomeRegister = 2197,
    /// <summary>Upper-vector references require a related interval.</summary>
    UpperVectorReferencesRequireARelatedInterval = 2198,
    /// <summary>Frame-pointer base register mask is not ported.</summary>
    FramePointerBaseRegisterMaskIsNotPorted = 2199,
    /// <summary>Reserved frame-offset register is not ported.</summary>
    ReservedFrameOffsetRegisterIsNotPorted = 2200,
    /// <summary>Saved localloc stack-pointer register is not ported.</summary>
    SavedLocallocStackPointerRegisterIsNotPorted = 2201,
    /// <summary>Tracked locals require an index-to-local mapping.</summary>
    TrackedLocalsRequireAnIndexToLocalMapping = 2203,
    /// <summary>Tracked locals require a variable-to-register map.</summary>
    TrackedLocalsRequireAVariableToRegisterMap = 2204,
    /// <summary>No incoming register mapping exists for V{0:D2}.</summary>
    NoIncomingRegisterMappingExistsForVArgument = 2205,
    /// <summary>A fixed use requires its preceding register reference.</summary>
    FixedUseRequiresItsPrecedingRegisterReference = 2206,
    /// <summary>Variable-location predecessors require LSRA block information.</summary>
    VariableLocationPredecessorsRequireLSRABlockInformation = 2207,
    /// <summary>Resolution blocks require a split-edge mapping.</summary>
    ResolutionBlocksRequireASplitEdgeMapping = 2208,
    /// <summary>No split-edge mapping exists for BB{0:D2}.</summary>
    NoSplitEdgeMappingExistsForBBArgument = 2209,
    /// <summary>Optimized write barriers are unsupported on this target.</summary>
    OptimizedWriteBarriersAreUnsupportedOnThisTarget = 2210,
    /// <summary>Unrecognized double-alignment policy.</summary>
    UnrecognizedDoubleAlignmentPolicy = 2211,
    /// <summary>Upper-vector local intervals are unsupported on this target.</summary>
    UpperVectorLocalIntervalsAreUnsupportedOnThisTarget = 2212,
    /// <summary>Last-use verification requires tracked local mapping.</summary>
    LastUseVerificationRequiresTrackedLocalMapping = 2213,
    /// <summary>Parameter preference stress requires initialized codegen state.</summary>
    ParameterPreferenceStressRequiresInitializedCodegenState = 2214,
    /// <summary>Unexpected local resolution reference: {0}.</summary>
    UnexpectedLocalResolutionReferenceArgument = 2215,
    /// <summary>ARM32 multi-reg parameter assignment is not ported.</summary>
    ARM32MultiRegParameterAssignmentIsNotPorted = 2216,
    /// <summary>Final allocation verification requires an outgoing map.</summary>
    FinalAllocationVerificationRequiresAnOutgoingMap = 2219,
    /// <summary>Final allocation verification requires an incoming map.</summary>
    FinalAllocationVerificationRequiresAnIncomingMap = 2220,
    /// <summary>A verified allocation reference requires an interval.</summary>
    VerifiedAllocationReferenceRequiresAnInterval = 2221,
    /// <summary>A definition requires an assigned register.</summary>
    DefinitionRequiresAnAssignedRegister = 2222,
    /// <summary>A moved interval requires an assigned register.</summary>
    MovedIntervalRequiresAnAssignedRegister = 2223,
    /// <summary>A move requires a destination register.</summary>
    MoveRequiresADestinationRegister = 2224,
    /// <summary>A copy requires an assigned register.</summary>
    CopyRequiresAnAssignedRegister = 2225,
    /// <summary>An interval requires an assigned register.</summary>
    IntervalRequiresAnAssignedRegister = 2226,
    /// <summary>Unsupported final-allocation reference: {0}.</summary>
    UnsupportedFinalAllocationReferenceArgument = 2227,
    /// <summary>A copied interval requires a register.</summary>
    CopiedIntervalRequiresARegister = 2228,
    /// <summary>Upper-vector references require a local.</summary>
    UpperVectorReferencesRequireALocal = 2229,
    /// <summary>A resolution block requires an incoming map.</summary>
    ResolutionBlockRequiresAnIncomingMap = 2230,
    /// <summary>A resolution block requires an outgoing map.</summary>
    ResolutionBlockRequiresAnOutgoingMap = 2231,
    /// <summary>The 32-bit long return-register contract is not available on this target.</summary>
    Failure32BitLongReturnRegisterContractIsNotAvailableOnThisTarget = 2232,
    /// <summary>The return-register contract is not available on this target.</summary>
    ReturnRegisterContractIsNotAvailableOnThisTarget = 2233,
    /// <summary>LoongArch64 hardware-intrinsic LSRA is not ported.</summary>
    LoongArch64HardwareIntrinsicLSRAIsNotPorted = 2235,
    /// <summary>LinearScan.buildIndir on this target is not implemented.</summary>
    LinearScanBuildIndirOnThisTargetIsNotImplemented = 2236,
    /// <summary>LinearScan.buildLclHeap on this target is not implemented.</summary>
    LinearScanBuildLclHeapOnThisTargetIsNotImplemented = 2238,
    /// <summary>Register-candidate locals require LSRA interval mappings.</summary>
    RegisterCandidateLocalsRequireLSRAIntervalMappings = 2239,
    /// <summary>A register-candidate local has no LSRA interval.</summary>
    RegisterCandidateLocalHasNoLSRAInterval = 2240,
    /// <summary>Unsupported minimal LSRA dump event: {0}.</summary>
    UnsupportedMinimalLSRADumpEventArgument = 2241,
    /// <summary>Unexpected non-contained {0} in LSRA node building.</summary>
    UnexpectedNonContainedArgumentInLSRANodeBuilding = 2242,
    /// <summary>LinearScan target is not supported.</summary>
    LinearScanTargetIsNotSupported = 2243,
    /// <summary>A live interval must have a first reference.</summary>
    LiveIntervalMustHaveAFirstReference = 2244,
    /// <summary>Tracked local {0} has no LSRA interval.</summary>
    TrackedLocalArgumentHasNoLSRAInterval = 2245,
    /// <summary>LSRA cannot define a register for {0}.</summary>
    LSRACannotDefineARegisterForArgument = 2246,
    /// <summary>ARM64 multi-register HW intrinsic has unsupported SIMD size {0}.</summary>
    ARM64MultiRegisterHWIntrinsicHasUnsupportedSIMDSizeArgument = 2247,
    /// <summary>Multi-register HW intrinsic register typing is not implemented for this target.</summary>
    MultiRegisterHWIntrinsicRegisterTypingIsNotImplementedForThisTarget = 2248,
    /// <summary>No upper-vector interval exists for tracked local {0}.</summary>
    NoUpperVectorIntervalExistsForTrackedLocalArgument = 2250,
    /// <summary>A definition-list entry must retain its defining node.</summary>
    DefinitionListEntryMustRetainItsDefiningNode = 2252,
    /// <summary>An assigned ARM32 double must retain its first register.</summary>
    AssignedARM32DoubleMustRetainItsFirstRegister = 2253,
    /// <summary>A double-register spill requires an occupied half.</summary>
    DoubleRegisterSpillRequiresAnOccupiedHalf = 2255,
    /// <summary>A spilled interval requires a following reference.</summary>
    SpilledIntervalRequiresAFollowingReference = 2257,
    /// <summary>A spilled interval must retain its assigned register.</summary>
    SpilledIntervalMustRetainItsAssignedRegister = 2258,
    /// <summary>Insufficient consecutive register candidates.</summary>
    InsufficientConsecutiveRegisterCandidates = 2259,
    /// <summary>Unexpected LSRA register-selection heuristic.</summary>
    UnexpectedLSRARegisterSelectionHeuristic = 2260,
    /// <summary>Register-selection debug data is only available in Debug builds.</summary>
    RegisterSelectionDebugDataIsOnlyAvailableInDebugBuilds = 2262,
    /// <summary>LSRA selector heuristic mapping is incomplete.</summary>
    LSRASelectorHeuristicMappingIsIncomplete = 2263,
    /// <summary>Invalid lsraOrdering value.</summary>
    InvalidLsraOrderingValue = 2264,
    /// <summary>Unknown RISC-V intrinsic {0}.</summary>
    UnknownRISCVIntrinsicArgument = 2265,
    /// <summary>RISC-V64 SIMD LSRA is not ported.</summary>
    RISCV64SIMDLSRAIsNotPorted = 2267,
    /// <summary>RISC-V64 hardware-intrinsic LSRA is not ported.</summary>
    RISCV64HardwareIntrinsicLSRAIsNotPorted = 2269,
    /// <summary>LSRA shift/rotate reference building is not implemented outside xarch.</summary>
    LSRAShiftRotateReferenceBuildingIsNotImplementedOutsideXarch = 2270,
    /// <summary>LSRA division reference building is not implemented outside xarch.</summary>
    LSRADivisionReferenceBuildingIsNotImplementedOutsideXarch = 2271,
    /// <summary>LSRA multiplication reference building is not implemented outside xarch.</summary>
    LSRAMultiplicationReferenceBuildingIsNotImplementedOutsideXarch = 2272,
    /// <summary>LinearScan.buildCast on this target is not implemented.</summary>
    LinearScanBuildCastOnThisTargetIsNotImplemented = 2273,
    /// <summary>Unsupported ARM64 scalar intrinsic {0}.</summary>
    UnsupportedARM64ScalarIntrinsicArgument = 2274,
    /// <summary>LinearScan.buildIntrinsic on this target is not implemented.</summary>
    LinearScanBuildIntrinsicOnThisTargetIsNotImplemented = 2275,
    /// <summary>LinearScan.buildSelect on this target is not implemented.</summary>
    LinearScanBuildSelectOnThisTargetIsNotImplemented = 2276,
    /// <summary>Unsupported LSRA register stress limit: 0x{0:X}.</summary>
    UnsupportedLSRARegisterStressLimit0xArgument = 2277,
    /// <summary>Unsupported ARM64 LSRA stress register type.</summary>
    UnsupportedARM64LSRAStressRegisterType = 2278,
    /// <summary>LSRA stress register limiting on this target is not ported.</summary>
    LSRAStressRegisterLimitingOnThisTargetIsNotPorted = 2280,
    /// <summary>Unsupported LSRA register type: {0}.</summary>
    UnsupportedLSRARegisterTypeArgument = 2282,
    /// <summary>The entry block has no LSRA statistics.</summary>
    EntryBlockHasNoLSRAStatistics = 2284,
    /// <summary>Consecutive-register allocation is not implemented.</summary>
    ConsecutiveRegisterAllocationIsNotImplemented = 2286,
    /// <summary>Partial SIMD callee saves are not supported on this target.</summary>
    PartialSIMDCalleeSavesAreNotSupportedOnThisTarget = 2287,
    /// <summary>RefPosition debug data is only available in Debug builds.</summary>
    RefPositionDebugDataIsOnlyAvailableInDebugBuilds = 2288,
    /// <summary>Consecutive-register diagnostics are only available in Debug builds.</summary>
    ConsecutiveRegisterDiagnosticsAreOnlyAvailableInDebugBuilds = 2289,
    /// <summary>Missing enumerator allocation block.</summary>
    MissingEnumeratorAllocationBlock = 2290,
    /// <summary>Missing enumerator definition.</summary>
    MissingEnumeratorDefinition = 2291,
    /// <summary>Conditional escape requires dominators.</summary>
    ConditionalEscapeRequiresDominators = 2292,
    /// <summary>Conditional allocation&#x27;s definition has no dominator.</summary>
    ConditionalAllocationSDefinitionHasNoDominator = 2293,
    /// <summary>Missing enumerator allocation tree.</summary>
    MissingEnumeratorAllocationTree = 2294,
    /// <summary>Conditional escape requires DFS.</summary>
    ConditionalEscapeRequiresDFS = 2295,
    /// <summary>Missing allocation block.</summary>
    MissingAllocationBlock = 2296,
    /// <summary>Missing cloned block.</summary>
    MissingClonedBlock = 2297,
    /// <summary>Missing tracked-local map.</summary>
    MissingTrackedLocalMap = 2298,
    /// <summary>Missing connection graph.</summary>
    MissingConnectionGraph = 2299,
    /// <summary>Missing clone for enumerator appearance.</summary>
    MissingCloneForEnumeratorAppearance = 2300,
    /// <summary>Missing cloned statement.</summary>
    MissingClonedStatement = 2301,
    /// <summary>Missing first cloned block.</summary>
    MissingFirstClonedBlock = 2302,
    /// <summary>Missing allocation tree.</summary>
    MissingAllocationTree = 2303,
    /// <summary>Missing guard in cloned enumerator region.</summary>
    MissingGuardInClonedEnumeratorRegion = 2304,
    /// <summary>Missing guard statement.</summary>
    MissingGuardStatement = 2305,
    /// <summary>Missing ancestor in object-allocation analysis.</summary>
    MissingAncestorInObjectAllocationAnalysis = 2306,
    /// <summary>Unexpected type for stack-pointer retyping.</summary>
    UnexpectedTypeForStackPointerRetyping = 2307,
    /// <summary>A CSE SSA use needs a local tree.</summary>
    CSESSAUseNeedsALocalTree = 2308,
    /// <summary>CSE heuristic diagnostics are only available in Debug builds.</summary>
    CSEHeuristicDiagnosticsAreOnlyAvailableInDebugBuilds = 2309,
    /// <summary>CSE_HeuristicCommon.CanConsiderTree target intrinsic categories</summary>
    CSEHeuristicCommonCanConsiderTreeTargetIntrinsicCategories = 2310,
    /// <summary>Tracked local map is not available.</summary>
    TrackedLocalMapIsNotAvailable = 2311,
    /// <summary>RL CSE update candidate was previously consumed.</summary>
    RLCSEUpdateCandidateWasPreviouslyConsumed = 2312,
    /// <summary>RL CSE update sequence exceeds the native reward capacity.</summary>
    RLCSEUpdateSequenceExceedsTheNativeRewardCapacity = 2313,
    /// <summary>RLHook CSE candidate is missing its descriptor.</summary>
    RLHookCSECandidateIsMissingItsDescriptor = 2314,
    /// <summary>RLHook CSE feature names have not been initialized.</summary>
    RLHookCSEFeatureNamesHaveNotBeenInitialized = 2315,
    /// <summary>Random CSE permutation is missing a descriptor.</summary>
    RandomCSEPermutationIsMissingADescriptor = 2316,
    /// <summary>Replay CSE candidate is missing its descriptor.</summary>
    ReplayCSECandidateIsMissingItsDescriptor = 2317,
    /// <summary>Location was not initialized.</summary>
    LocationWasNotInitialized = 2318,
    /// <summary>Decomposed address was used more times than planned.</summary>
    DecomposedAddressWasUsedMoreTimesThanPlanned = 2319,
    /// <summary>A block store requires an address.</summary>
    BlockStoreRequiresAnAddress = 2320,
    /// <summary>A block read requires an address.</summary>
    BlockReadRequiresAnAddress = 2321,
    /// <summary>A struct local requires a layout.</summary>
    StructLocalRequiresALayout = 2322,
    /// <summary>A replacement must belong to a struct local.</summary>
    ReplacementMustBelongToAStructLocal = 2323,
    /// <summary>A replacement must belong to a promoted aggregate.</summary>
    ReplacementMustBelongToAPromotedAggregate = 2324,
    /// <summary>A promoted retbuffer definition must have an exact size.</summary>
    PromotedRetbufferDefinitionMustHaveAnExactSize = 2325,
    /// <summary>Physical promotion liveness has not initialized this block.</summary>
    PhysicalPromotionLivenessHasNotInitializedThisBlock = 2326,
    /// <summary>The call must define its retbuffer local.</summary>
    CallMustDefineItsRetbufferLocal = 2327,
    /// <summary>Physical promotion requires the current DFS tree.</summary>
    PhysicalPromotionRequiresTheCurrentDFSTree = 2328,
    /// <summary>Physical promotion has not started a block.</summary>
    PhysicalPromotionHasNotStartedABlock = 2329,
    /// <summary>Physical promotion has not started a statement.</summary>
    PhysicalPromotionHasNotStartedAStatement = 2330,
    /// <summary>Physical promotion has not prepared readbacks.</summary>
    PhysicalPromotionHasNotPreparedReadbacks = 2331,
    /// <summary>A non-first statement must have a predecessor.</summary>
    NonFirstStatementMustHaveAPredecessor = 2332,
    /// <summary>A struct argument needs a layout.</summary>
    StructArgumentNeedsALayout = 2333,
    /// <summary>An optimized retbuffer call needs a retbuffer argument.</summary>
    OptimizedRetbufferCallNeedsARetbufferArgument = 2334,
    /// <summary>A struct local needs a layout.</summary>
    StructLocalNeedsALayout = 2335,
    /// <summary>A struct use needs a layout.</summary>
    StructUseNeedsALayout = 2336,
    /// <summary>A dying promoted local needs aggregate information.</summary>
    DyingPromotedLocalNeedsAggregateInformation = 2337,
    /// <summary>Struct access requires layout.</summary>
    StructAccessRequiresLayout = 2338,
    /// <summary>Candidate requires struct layout.</summary>
    CandidateRequiresStructLayout = 2339,
    /// <summary>Spilled reduction operand has no definition.</summary>
    SpilledReductionOperandHasNoDefinition = 2343,
    /// <summary>Failed to clone reduction operand.</summary>
    FailedToCloneReductionOperand = 2344,
    /// <summary>Spilled vector operand has no definition.</summary>
    SpilledVectorOperandHasNoDefinition = 2345,
    /// <summary>Failed to clone vector operand.</summary>
    FailedToCloneVectorOperand = 2346,
    /// <summary>Target-specific hardware immediate rationalization is not ported.</summary>
    TargetSpecificHardwareImmediateRationalizationIsNotPorted = 2347,
    /// <summary>Unexpected mask intrinsic.</summary>
    UnexpectedMaskIntrinsic = 2348,
    /// <summary>Target-specific most-significant-bit rationalization is not ported.</summary>
    TargetSpecificMostSignificantBitRationalizationIsNotPorted = 2349,
    /// <summary>Wasm register allocation has no current block.</summary>
    WasmRegisterAllocationHasNoCurrentBlock = 2350,
    /// <summary>Wasm indirections require a base address.</summary>
    WasmIndirectionsRequireABaseAddress = 2351,
    /// <summary>Wasm struct stores require a local layout.</summary>
    WasmStructStoresRequireALocalLayout = 2352,
    /// <summary>Wasm local stack stores require an active lowering phase.</summary>
    WasmLocalStackStoresRequireAnActiveLoweringPhase = 2353,
    /// <summary>Local V{0} has no tracked-register index.</summary>
    LocalVArgumentHasNoTrackedRegisterIndex = 2354,
    /// <summary>A Wasm parameter register target has no register mapping.</summary>
    WasmParameterRegisterTargetHasNoRegisterMapping = 2355,
    /// <summary>A Wasm temporary register bank is not initialized.</summary>
    WasmTemporaryRegisterBankIsNotInitialized = 2356,
    /// <summary>A collected Wasm register reference is missing.</summary>
    CollectedWasmRegisterReferenceIsMissing = 2357,
    /// <summary>Wasm register allocation requires code generation.</summary>
    WasmRegisterAllocationRequiresCodeGeneration = 2358,
    /// <summary>Wasm funclet allocation state is not initialized.</summary>
    WasmFuncletAllocationStateIsNotInitialized = 2359,
    /// <summary>Callee-saved register masks are unavailable for this target.</summary>
    CalleeSavedRegisterMasksAreUnavailableForThisTarget = 2360,
    /// <summary>Floating-point stack spills require x86.</summary>
    FloatingPointStackSpillsRequireX86 = 2361,
    /// <summary>Live register masks require a fixed register set.</summary>
    LiveRegisterMasksRequireAFixedRegisterSet = 2362,
    /// <summary>OSR integer callee-saved masks require AMD64.</summary>
    OSRIntegerCalleeSavedMasksRequireAMD64 = 2363,
    /// <summary>SIMD spill temps are unsupported for this target.</summary>
    SIMDSpillTempsAreUnsupportedForThisTarget = 2364,
    /// <summary>Tree spills require a fixed register set.</summary>
    TreeSpillsRequireAFixedRegisterSet = 2365,
    /// <summary>Scalar evolution requires value numbering.</summary>
    ScalarEvolutionRequiresValueNumbering = 2366,
    /// <summary>Scalar evolution requires a loop.</summary>
    ScalarEvolutionRequiresALoop = 2367,
    /// <summary>Unexpected operator {0}.</summary>
    UnexpectedOperatorArgument = 2368,
    /// <summary>Unexpected exit operation {0}.</summary>
    UnexpectedExitOperationArgument = 2369,
    /// <summary>Unexpected relational operation {0}.</summary>
    UnexpectedRelationalOperationArgument = 2370,
    /// <summary>An exiting block must have a condition.</summary>
    ExitingBlockMustHaveACondition = 2371,
    /// <summary>Unexpected relational result {0}.</summary>
    UnexpectedRelationalResultArgument = 2372,
    /// <summary>Unexpected binary scalar evolution operation {0}.</summary>
    UnexpectedBinaryScalarEvolutionOperationArgument = 2373,
    /// <summary>Unexpected scalar evolution operation {0}.</summary>
    UnexpectedScalarEvolutionOperationArgument = 2374,
    /// <summary>Unexpected operation {0}.</summary>
    UnexpectedOperationArgument = 2375,
    /// <summary>Unexpected binary operation {0}.</summary>
    UnexpectedBinaryOperationArgument = 2376,
    /// <summary>Scalar-evolution loop data is unavailable.</summary>
    ScalarEvolutionLoopDataIsUnavailable = 2377,
    /// <summary>Scalar-evolution debug data is only available in Debug builds.</summary>
    ScalarEvolutionDebugDataIsOnlyAvailableInDebugBuilds = 2378,
    /// <summary>Live range diagnostics are only available in Debug builds.</summary>
    LiveRangeDiagnosticsAreOnlyAvailableInDebugBuilds = 2379,
    /// <summary>Expected first node to precede end node.</summary>
    ExpectedFirstNodeToPrecedeEndNode = 2380,
    /// <summary>ARM64 scalable SIMD constants are not supported on this target.</summary>
    ARM64ScalableSIMDConstantsAreNotSupportedOnThisTarget = 2381,
    /// <summary>Unexpected ARM64 scalable constant base type.</summary>
    UnexpectedARM64ScalableConstantBaseType = 2382,
    /// <summary>SIMD local alignment for this target is not implemented.</summary>
    SIMDLocalAlignmentForThisTargetIsNotImplemented = 2383,
    /// <summary>Unsupported ARM64 SIMD element type.</summary>
    UnsupportedARM64SIMDElementType = 2385,
    /// <summary>Unsupported ARM64 mask element type.</summary>
    UnsupportedARM64MaskElementType = 2386,
    /// <summary>Mask conversions are only supported on xarch and ARM64.</summary>
    MaskConversionsAreOnlySupportedOnXarchAndARM64 = 2387,
    /// <summary>Mask evaluation is only supported on xarch and ARM64.</summary>
    MaskEvaluationIsOnlySupportedOnXarchAndARM64 = 2388,
    /// <summary>SIMD upper-lane saves require xarch SIMD support.</summary>
    SIMDUpperLaneSavesRequireXarchSIMDSupport = 2389,
    /// <summary>SIMD upper-lane restores require xarch SIMD support.</summary>
    SIMDUpperLaneRestoresRequireXarchSIMDSupport = 2390,
    /// <summary>x86 SIMD12 stack arguments are unsupported for this target.</summary>
    X86SIMD12StackArgumentsAreUnsupportedForThisTarget = 2391,
    /// <summary>SIMD12 indirect stores require xarch SIMD support.</summary>
    SIMD12IndirectStoresRequireXarchSIMDSupport = 2392,
    /// <summary>No default native hash is defined for {0}.</summary>
    NoDefaultNativeHashIsDefinedForArgument = 2393,
    /// <summary>The hash table could not reinsert an occupied bucket.</summary>
    HashTableCouldNotReinsertAnOccupiedBucket = 2394,
    /// <summary>The hash table has no free bucket.</summary>
    HashTableHasNoFreeBucket = 2395,
    /// <summary>SSA definition blocks are not finalized.</summary>
    SSADefinitionBlocksAreNotFinalized = 2397,
    /// <summary>SSA frontiers are not finalized.</summary>
    SSAFrontiersAreNotFinalized = 2398,
    /// <summary>SSA DFS tree is not available.</summary>
    SSADFSTreeIsNotAvailable = 2399,
    /// <summary>Phi argument has no reaching definition.</summary>
    PhiArgumentHasNoReachingDefinition = 2400,
    /// <summary>SSA use has no reaching definition.</summary>
    SSAUseHasNoReachingDefinition = 2401,
    /// <summary>SSA definition has no statement.</summary>
    SSADefinitionHasNoStatement = 2402,
    /// <summary>SSA statement has no matching definition.</summary>
    SSAStatementHasNoMatchingDefinition = 2403,
    /// <summary>SSA statement use has no tree.</summary>
    SSAStatementUseHasNoTree = 2404,
    /// <summary>SSA definition has no tree.</summary>
    SSADefinitionHasNoTree = 2405,
    /// <summary>SSA insertion requires value numbering.</summary>
    SSAInsertionRequiresValueNumbering = 2406,
    /// <summary>SSA use has no tree.</summary>
    SSAUseHasNoTree = 2407,
    /// <summary>Cannot pop an empty SSA rename stack.</summary>
    CannotPopAnEmptySSARenameStack = 2408,
    /// <summary>The active SSA rename stack is empty.</summary>
    ActiveSSARenameStackIsEmpty = 2409,
    /// <summary>The memory SSA rename stack is empty.</summary>
    MemorySSARenameStackIsEmpty = 2410,
    /// <summary>Invalid target architecture.</summary>
    InvalidTargetArchitecture = 2411,
    /// <summary>Register successor is not supported for this target.</summary>
    RegisterSuccessorIsNotSupportedForThisTarget = 2412,
    /// <summary>Register predecessor is not supported for this target.</summary>
    RegisterPredecessorIsNotSupportedForThisTarget = 2413,
    /// <summary>Register order is unavailable for this target.</summary>
    RegisterOrderIsUnavailableForThisTarget = 2414,
    /// <summary>Floating-point register order is unavailable for this target.</summary>
    FloatingPointRegisterOrderIsUnavailableForThisTarget = 2415,
    /// <summary>Mask-register allocation order is unavailable for this target.</summary>
    MaskRegisterAllocationOrderIsUnavailableForThisTarget = 2416,
    /// <summary>Fixed register masks are not supported on this target.</summary>
    FixedRegisterMasksAreNotSupportedOnThisTarget = 2417,
    /// <summary>Swift calling conventions are not supported for this target.</summary>
    SwiftCallingConventionsAreNotSupportedForThisTarget = 2418,
    /// <summary>{0}</summary>
    Argument = 2419,
    /// <summary>Register mask &#x27;EAX&#x27; is not available for the configured target.</summary>
    RegisterMaskEAXIsNotAvailableForTheConfiguredTarget = 2420,
    /// <summary>Register mask &#x27;ECX&#x27; is not available for the configured target.</summary>
    RegisterMaskECXIsNotAvailableForTheConfiguredTarget = 2421,
    /// <summary>Register mask &#x27;EDX&#x27; is not available for the configured target.</summary>
    RegisterMaskEDXIsNotAvailableForTheConfiguredTarget = 2422,
    /// <summary>Register mask &#x27;EBX&#x27; is not available for the configured target.</summary>
    RegisterMaskEBXIsNotAvailableForTheConfiguredTarget = 2423,
    /// <summary>Register mask &#x27;ESP&#x27; is not available for the configured target.</summary>
    RegisterMaskESPIsNotAvailableForTheConfiguredTarget = 2424,
    /// <summary>Register mask &#x27;EBP&#x27; is not available for the configured target.</summary>
    RegisterMaskEBPIsNotAvailableForTheConfiguredTarget = 2425,
    /// <summary>Register mask &#x27;ESI&#x27; is not available for the configured target.</summary>
    RegisterMaskESIIsNotAvailableForTheConfiguredTarget = 2426,
    /// <summary>Register mask &#x27;EDI&#x27; is not available for the configured target.</summary>
    RegisterMaskEDIIsNotAvailableForTheConfiguredTarget = 2427,
    /// <summary>Register mask &#x27;RAX&#x27; is not available for the configured target.</summary>
    RegisterMaskRAXIsNotAvailableForTheConfiguredTarget = 2428,
    /// <summary>Register mask &#x27;RCX&#x27; is not available for the configured target.</summary>
    RegisterMaskRCXIsNotAvailableForTheConfiguredTarget = 2429,
    /// <summary>Register mask &#x27;RDX&#x27; is not available for the configured target.</summary>
    RegisterMaskRDXIsNotAvailableForTheConfiguredTarget = 2430,
    /// <summary>Register mask &#x27;RBX&#x27; is not available for the configured target.</summary>
    RegisterMaskRBXIsNotAvailableForTheConfiguredTarget = 2431,
    /// <summary>Register mask &#x27;RSP&#x27; is not available for the configured target.</summary>
    RegisterMaskRSPIsNotAvailableForTheConfiguredTarget = 2432,
    /// <summary>Register mask &#x27;RBP&#x27; is not available for the configured target.</summary>
    RegisterMaskRBPIsNotAvailableForTheConfiguredTarget = 2433,
    /// <summary>Register mask &#x27;RSI&#x27; is not available for the configured target.</summary>
    RegisterMaskRSIIsNotAvailableForTheConfiguredTarget = 2434,
    /// <summary>Register mask &#x27;RDI&#x27; is not available for the configured target.</summary>
    RegisterMaskRDIIsNotAvailableForTheConfiguredTarget = 2435,
    /// <summary>Register mask &#x27;XMM0&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM0IsNotAvailableForTheConfiguredTarget = 2436,
    /// <summary>Register mask &#x27;XMM1&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM1IsNotAvailableForTheConfiguredTarget = 2437,
    /// <summary>Register mask &#x27;XMM2&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM2IsNotAvailableForTheConfiguredTarget = 2438,
    /// <summary>Register mask &#x27;XMM3&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM3IsNotAvailableForTheConfiguredTarget = 2439,
    /// <summary>Register mask &#x27;XMM4&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM4IsNotAvailableForTheConfiguredTarget = 2440,
    /// <summary>Register mask &#x27;XMM5&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM5IsNotAvailableForTheConfiguredTarget = 2441,
    /// <summary>Register mask &#x27;XMM6&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM6IsNotAvailableForTheConfiguredTarget = 2442,
    /// <summary>Register mask &#x27;XMM7&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM7IsNotAvailableForTheConfiguredTarget = 2443,
    /// <summary>Register mask &#x27;K0&#x27; is not available for the configured target.</summary>
    RegisterMaskK0IsNotAvailableForTheConfiguredTarget = 2444,
    /// <summary>Register mask &#x27;K1&#x27; is not available for the configured target.</summary>
    RegisterMaskK1IsNotAvailableForTheConfiguredTarget = 2445,
    /// <summary>Register mask &#x27;K2&#x27; is not available for the configured target.</summary>
    RegisterMaskK2IsNotAvailableForTheConfiguredTarget = 2446,
    /// <summary>Register mask &#x27;K3&#x27; is not available for the configured target.</summary>
    RegisterMaskK3IsNotAvailableForTheConfiguredTarget = 2447,
    /// <summary>Register mask &#x27;K4&#x27; is not available for the configured target.</summary>
    RegisterMaskK4IsNotAvailableForTheConfiguredTarget = 2448,
    /// <summary>Register mask &#x27;K5&#x27; is not available for the configured target.</summary>
    RegisterMaskK5IsNotAvailableForTheConfiguredTarget = 2449,
    /// <summary>Register mask &#x27;K6&#x27; is not available for the configured target.</summary>
    RegisterMaskK6IsNotAvailableForTheConfiguredTarget = 2450,
    /// <summary>Register mask &#x27;K7&#x27; is not available for the configured target.</summary>
    RegisterMaskK7IsNotAvailableForTheConfiguredTarget = 2451,
    /// <summary>Register mask &#x27;STK&#x27; is not available for the configured target.</summary>
    RegisterMaskSTKIsNotAvailableForTheConfiguredTarget = 2452,
    /// <summary>Register mask &#x27;R8&#x27; is not available for the configured target.</summary>
    RegisterMaskR8IsNotAvailableForTheConfiguredTarget = 2453,
    /// <summary>Register mask &#x27;R9&#x27; is not available for the configured target.</summary>
    RegisterMaskR9IsNotAvailableForTheConfiguredTarget = 2454,
    /// <summary>Register mask &#x27;R10&#x27; is not available for the configured target.</summary>
    RegisterMaskR10IsNotAvailableForTheConfiguredTarget = 2455,
    /// <summary>Register mask &#x27;R11&#x27; is not available for the configured target.</summary>
    RegisterMaskR11IsNotAvailableForTheConfiguredTarget = 2456,
    /// <summary>Register mask &#x27;R12&#x27; is not available for the configured target.</summary>
    RegisterMaskR12IsNotAvailableForTheConfiguredTarget = 2457,
    /// <summary>Register mask &#x27;R13&#x27; is not available for the configured target.</summary>
    RegisterMaskR13IsNotAvailableForTheConfiguredTarget = 2458,
    /// <summary>Register mask &#x27;R14&#x27; is not available for the configured target.</summary>
    RegisterMaskR14IsNotAvailableForTheConfiguredTarget = 2459,
    /// <summary>Register mask &#x27;R15&#x27; is not available for the configured target.</summary>
    RegisterMaskR15IsNotAvailableForTheConfiguredTarget = 2460,
    /// <summary>Register mask &#x27;R16&#x27; is not available for the configured target.</summary>
    RegisterMaskR16IsNotAvailableForTheConfiguredTarget = 2461,
    /// <summary>Register mask &#x27;R17&#x27; is not available for the configured target.</summary>
    RegisterMaskR17IsNotAvailableForTheConfiguredTarget = 2462,
    /// <summary>Register mask &#x27;R18&#x27; is not available for the configured target.</summary>
    RegisterMaskR18IsNotAvailableForTheConfiguredTarget = 2463,
    /// <summary>Register mask &#x27;R19&#x27; is not available for the configured target.</summary>
    RegisterMaskR19IsNotAvailableForTheConfiguredTarget = 2464,
    /// <summary>Register mask &#x27;R20&#x27; is not available for the configured target.</summary>
    RegisterMaskR20IsNotAvailableForTheConfiguredTarget = 2465,
    /// <summary>Register mask &#x27;R21&#x27; is not available for the configured target.</summary>
    RegisterMaskR21IsNotAvailableForTheConfiguredTarget = 2466,
    /// <summary>Register mask &#x27;R22&#x27; is not available for the configured target.</summary>
    RegisterMaskR22IsNotAvailableForTheConfiguredTarget = 2467,
    /// <summary>Register mask &#x27;R23&#x27; is not available for the configured target.</summary>
    RegisterMaskR23IsNotAvailableForTheConfiguredTarget = 2468,
    /// <summary>Register mask &#x27;R24&#x27; is not available for the configured target.</summary>
    RegisterMaskR24IsNotAvailableForTheConfiguredTarget = 2469,
    /// <summary>Register mask &#x27;R25&#x27; is not available for the configured target.</summary>
    RegisterMaskR25IsNotAvailableForTheConfiguredTarget = 2470,
    /// <summary>Register mask &#x27;R26&#x27; is not available for the configured target.</summary>
    RegisterMaskR26IsNotAvailableForTheConfiguredTarget = 2471,
    /// <summary>Register mask &#x27;R27&#x27; is not available for the configured target.</summary>
    RegisterMaskR27IsNotAvailableForTheConfiguredTarget = 2472,
    /// <summary>Register mask &#x27;R28&#x27; is not available for the configured target.</summary>
    RegisterMaskR28IsNotAvailableForTheConfiguredTarget = 2473,
    /// <summary>Register mask &#x27;R29&#x27; is not available for the configured target.</summary>
    RegisterMaskR29IsNotAvailableForTheConfiguredTarget = 2474,
    /// <summary>Register mask &#x27;R30&#x27; is not available for the configured target.</summary>
    RegisterMaskR30IsNotAvailableForTheConfiguredTarget = 2475,
    /// <summary>Register mask &#x27;R31&#x27; is not available for the configured target.</summary>
    RegisterMaskR31IsNotAvailableForTheConfiguredTarget = 2476,
    /// <summary>Register mask &#x27;XMM8&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM8IsNotAvailableForTheConfiguredTarget = 2477,
    /// <summary>Register mask &#x27;XMM9&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM9IsNotAvailableForTheConfiguredTarget = 2478,
    /// <summary>Register mask &#x27;XMM10&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM10IsNotAvailableForTheConfiguredTarget = 2479,
    /// <summary>Register mask &#x27;XMM11&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM11IsNotAvailableForTheConfiguredTarget = 2480,
    /// <summary>Register mask &#x27;XMM12&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM12IsNotAvailableForTheConfiguredTarget = 2481,
    /// <summary>Register mask &#x27;XMM13&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM13IsNotAvailableForTheConfiguredTarget = 2482,
    /// <summary>Register mask &#x27;XMM14&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM14IsNotAvailableForTheConfiguredTarget = 2483,
    /// <summary>Register mask &#x27;XMM15&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM15IsNotAvailableForTheConfiguredTarget = 2484,
    /// <summary>Register mask &#x27;XMM16&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM16IsNotAvailableForTheConfiguredTarget = 2485,
    /// <summary>Register mask &#x27;XMM17&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM17IsNotAvailableForTheConfiguredTarget = 2486,
    /// <summary>Register mask &#x27;XMM18&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM18IsNotAvailableForTheConfiguredTarget = 2487,
    /// <summary>Register mask &#x27;XMM19&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM19IsNotAvailableForTheConfiguredTarget = 2488,
    /// <summary>Register mask &#x27;XMM20&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM20IsNotAvailableForTheConfiguredTarget = 2489,
    /// <summary>Register mask &#x27;XMM21&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM21IsNotAvailableForTheConfiguredTarget = 2490,
    /// <summary>Register mask &#x27;XMM22&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM22IsNotAvailableForTheConfiguredTarget = 2491,
    /// <summary>Register mask &#x27;XMM23&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM23IsNotAvailableForTheConfiguredTarget = 2492,
    /// <summary>Register mask &#x27;XMM24&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM24IsNotAvailableForTheConfiguredTarget = 2493,
    /// <summary>Register mask &#x27;XMM25&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM25IsNotAvailableForTheConfiguredTarget = 2494,
    /// <summary>Register mask &#x27;XMM26&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM26IsNotAvailableForTheConfiguredTarget = 2495,
    /// <summary>Register mask &#x27;XMM27&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM27IsNotAvailableForTheConfiguredTarget = 2496,
    /// <summary>Register mask &#x27;XMM28&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM28IsNotAvailableForTheConfiguredTarget = 2497,
    /// <summary>Register mask &#x27;XMM29&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM29IsNotAvailableForTheConfiguredTarget = 2498,
    /// <summary>Register mask &#x27;XMM30&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM30IsNotAvailableForTheConfiguredTarget = 2499,
    /// <summary>Register mask &#x27;XMM31&#x27; is not available for the configured target.</summary>
    RegisterMaskXMM31IsNotAvailableForTheConfiguredTarget = 2500,
    /// <summary>Register mask &#x27;R0&#x27; is not available for the configured target.</summary>
    RegisterMaskR0IsNotAvailableForTheConfiguredTarget = 2501,
    /// <summary>Register mask &#x27;R1&#x27; is not available for the configured target.</summary>
    RegisterMaskR1IsNotAvailableForTheConfiguredTarget = 2502,
    /// <summary>Register mask &#x27;R2&#x27; is not available for the configured target.</summary>
    RegisterMaskR2IsNotAvailableForTheConfiguredTarget = 2503,
    /// <summary>Register mask &#x27;R3&#x27; is not available for the configured target.</summary>
    RegisterMaskR3IsNotAvailableForTheConfiguredTarget = 2504,
    /// <summary>Register mask &#x27;R4&#x27; is not available for the configured target.</summary>
    RegisterMaskR4IsNotAvailableForTheConfiguredTarget = 2505,
    /// <summary>Register mask &#x27;R5&#x27; is not available for the configured target.</summary>
    RegisterMaskR5IsNotAvailableForTheConfiguredTarget = 2506,
    /// <summary>Register mask &#x27;R6&#x27; is not available for the configured target.</summary>
    RegisterMaskR6IsNotAvailableForTheConfiguredTarget = 2507,
    /// <summary>Register mask &#x27;R7&#x27; is not available for the configured target.</summary>
    RegisterMaskR7IsNotAvailableForTheConfiguredTarget = 2508,
    /// <summary>Register mask &#x27;SP&#x27; is not available for the configured target.</summary>
    RegisterMaskSPIsNotAvailableForTheConfiguredTarget = 2509,
    /// <summary>Register mask &#x27;LR&#x27; is not available for the configured target.</summary>
    RegisterMaskLRIsNotAvailableForTheConfiguredTarget = 2510,
    /// <summary>Register mask &#x27;PC&#x27; is not available for the configured target.</summary>
    RegisterMaskPCIsNotAvailableForTheConfiguredTarget = 2511,
    /// <summary>Register mask &#x27;FP&#x27; is not available for the configured target.</summary>
    RegisterMaskFPIsNotAvailableForTheConfiguredTarget = 2512,
    /// <summary>Register mask &#x27;F0&#x27; is not available for the configured target.</summary>
    RegisterMaskF0IsNotAvailableForTheConfiguredTarget = 2513,
    /// <summary>Register mask &#x27;F1&#x27; is not available for the configured target.</summary>
    RegisterMaskF1IsNotAvailableForTheConfiguredTarget = 2514,
    /// <summary>Register mask &#x27;F2&#x27; is not available for the configured target.</summary>
    RegisterMaskF2IsNotAvailableForTheConfiguredTarget = 2515,
    /// <summary>Register mask &#x27;F3&#x27; is not available for the configured target.</summary>
    RegisterMaskF3IsNotAvailableForTheConfiguredTarget = 2516,
    /// <summary>Register mask &#x27;F4&#x27; is not available for the configured target.</summary>
    RegisterMaskF4IsNotAvailableForTheConfiguredTarget = 2517,
    /// <summary>Register mask &#x27;F5&#x27; is not available for the configured target.</summary>
    RegisterMaskF5IsNotAvailableForTheConfiguredTarget = 2518,
    /// <summary>Register mask &#x27;F6&#x27; is not available for the configured target.</summary>
    RegisterMaskF6IsNotAvailableForTheConfiguredTarget = 2519,
    /// <summary>Register mask &#x27;F7&#x27; is not available for the configured target.</summary>
    RegisterMaskF7IsNotAvailableForTheConfiguredTarget = 2520,
    /// <summary>Register mask &#x27;F8&#x27; is not available for the configured target.</summary>
    RegisterMaskF8IsNotAvailableForTheConfiguredTarget = 2521,
    /// <summary>Register mask &#x27;F9&#x27; is not available for the configured target.</summary>
    RegisterMaskF9IsNotAvailableForTheConfiguredTarget = 2522,
    /// <summary>Register mask &#x27;F10&#x27; is not available for the configured target.</summary>
    RegisterMaskF10IsNotAvailableForTheConfiguredTarget = 2523,
    /// <summary>Register mask &#x27;F11&#x27; is not available for the configured target.</summary>
    RegisterMaskF11IsNotAvailableForTheConfiguredTarget = 2524,
    /// <summary>Register mask &#x27;F12&#x27; is not available for the configured target.</summary>
    RegisterMaskF12IsNotAvailableForTheConfiguredTarget = 2525,
    /// <summary>Register mask &#x27;F13&#x27; is not available for the configured target.</summary>
    RegisterMaskF13IsNotAvailableForTheConfiguredTarget = 2526,
    /// <summary>Register mask &#x27;F14&#x27; is not available for the configured target.</summary>
    RegisterMaskF14IsNotAvailableForTheConfiguredTarget = 2527,
    /// <summary>Register mask &#x27;F15&#x27; is not available for the configured target.</summary>
    RegisterMaskF15IsNotAvailableForTheConfiguredTarget = 2528,
    /// <summary>Register mask &#x27;F16&#x27; is not available for the configured target.</summary>
    RegisterMaskF16IsNotAvailableForTheConfiguredTarget = 2529,
    /// <summary>Register mask &#x27;F17&#x27; is not available for the configured target.</summary>
    RegisterMaskF17IsNotAvailableForTheConfiguredTarget = 2530,
    /// <summary>Register mask &#x27;F18&#x27; is not available for the configured target.</summary>
    RegisterMaskF18IsNotAvailableForTheConfiguredTarget = 2531,
    /// <summary>Register mask &#x27;F19&#x27; is not available for the configured target.</summary>
    RegisterMaskF19IsNotAvailableForTheConfiguredTarget = 2532,
    /// <summary>Register mask &#x27;F20&#x27; is not available for the configured target.</summary>
    RegisterMaskF20IsNotAvailableForTheConfiguredTarget = 2533,
    /// <summary>Register mask &#x27;F21&#x27; is not available for the configured target.</summary>
    RegisterMaskF21IsNotAvailableForTheConfiguredTarget = 2534,
    /// <summary>Register mask &#x27;F22&#x27; is not available for the configured target.</summary>
    RegisterMaskF22IsNotAvailableForTheConfiguredTarget = 2535,
    /// <summary>Register mask &#x27;F23&#x27; is not available for the configured target.</summary>
    RegisterMaskF23IsNotAvailableForTheConfiguredTarget = 2536,
    /// <summary>Register mask &#x27;F24&#x27; is not available for the configured target.</summary>
    RegisterMaskF24IsNotAvailableForTheConfiguredTarget = 2537,
    /// <summary>Register mask &#x27;F25&#x27; is not available for the configured target.</summary>
    RegisterMaskF25IsNotAvailableForTheConfiguredTarget = 2538,
    /// <summary>Register mask &#x27;F26&#x27; is not available for the configured target.</summary>
    RegisterMaskF26IsNotAvailableForTheConfiguredTarget = 2539,
    /// <summary>Register mask &#x27;F27&#x27; is not available for the configured target.</summary>
    RegisterMaskF27IsNotAvailableForTheConfiguredTarget = 2540,
    /// <summary>Register mask &#x27;F28&#x27; is not available for the configured target.</summary>
    RegisterMaskF28IsNotAvailableForTheConfiguredTarget = 2541,
    /// <summary>Register mask &#x27;F29&#x27; is not available for the configured target.</summary>
    RegisterMaskF29IsNotAvailableForTheConfiguredTarget = 2542,
    /// <summary>Register mask &#x27;F30&#x27; is not available for the configured target.</summary>
    RegisterMaskF30IsNotAvailableForTheConfiguredTarget = 2543,
    /// <summary>Register mask &#x27;F31&#x27; is not available for the configured target.</summary>
    RegisterMaskF31IsNotAvailableForTheConfiguredTarget = 2544,
    /// <summary>Register mask &#x27;IP0&#x27; is not available for the configured target.</summary>
    RegisterMaskIP0IsNotAvailableForTheConfiguredTarget = 2545,
    /// <summary>Register mask &#x27;IP1&#x27; is not available for the configured target.</summary>
    RegisterMaskIP1IsNotAvailableForTheConfiguredTarget = 2546,
    /// <summary>Register mask &#x27;PR&#x27; is not available for the configured target.</summary>
    RegisterMaskPRIsNotAvailableForTheConfiguredTarget = 2547,
    /// <summary>Register mask &#x27;ZR&#x27; is not available for the configured target.</summary>
    RegisterMaskZRIsNotAvailableForTheConfiguredTarget = 2548,
    /// <summary>Register mask &#x27;V0&#x27; is not available for the configured target.</summary>
    RegisterMaskV0IsNotAvailableForTheConfiguredTarget = 2549,
    /// <summary>Register mask &#x27;V1&#x27; is not available for the configured target.</summary>
    RegisterMaskV1IsNotAvailableForTheConfiguredTarget = 2550,
    /// <summary>Register mask &#x27;V2&#x27; is not available for the configured target.</summary>
    RegisterMaskV2IsNotAvailableForTheConfiguredTarget = 2551,
    /// <summary>Register mask &#x27;V3&#x27; is not available for the configured target.</summary>
    RegisterMaskV3IsNotAvailableForTheConfiguredTarget = 2552,
    /// <summary>Register mask &#x27;V4&#x27; is not available for the configured target.</summary>
    RegisterMaskV4IsNotAvailableForTheConfiguredTarget = 2553,
    /// <summary>Register mask &#x27;V5&#x27; is not available for the configured target.</summary>
    RegisterMaskV5IsNotAvailableForTheConfiguredTarget = 2554,
    /// <summary>Register mask &#x27;V6&#x27; is not available for the configured target.</summary>
    RegisterMaskV6IsNotAvailableForTheConfiguredTarget = 2555,
    /// <summary>Register mask &#x27;V7&#x27; is not available for the configured target.</summary>
    RegisterMaskV7IsNotAvailableForTheConfiguredTarget = 2556,
    /// <summary>Register mask &#x27;V8&#x27; is not available for the configured target.</summary>
    RegisterMaskV8IsNotAvailableForTheConfiguredTarget = 2557,
    /// <summary>Register mask &#x27;V9&#x27; is not available for the configured target.</summary>
    RegisterMaskV9IsNotAvailableForTheConfiguredTarget = 2558,
    /// <summary>Register mask &#x27;V10&#x27; is not available for the configured target.</summary>
    RegisterMaskV10IsNotAvailableForTheConfiguredTarget = 2559,
    /// <summary>Register mask &#x27;V11&#x27; is not available for the configured target.</summary>
    RegisterMaskV11IsNotAvailableForTheConfiguredTarget = 2560,
    /// <summary>Register mask &#x27;V12&#x27; is not available for the configured target.</summary>
    RegisterMaskV12IsNotAvailableForTheConfiguredTarget = 2561,
    /// <summary>Register mask &#x27;V13&#x27; is not available for the configured target.</summary>
    RegisterMaskV13IsNotAvailableForTheConfiguredTarget = 2562,
    /// <summary>Register mask &#x27;V14&#x27; is not available for the configured target.</summary>
    RegisterMaskV14IsNotAvailableForTheConfiguredTarget = 2563,
    /// <summary>Register mask &#x27;V15&#x27; is not available for the configured target.</summary>
    RegisterMaskV15IsNotAvailableForTheConfiguredTarget = 2564,
    /// <summary>Register mask &#x27;V16&#x27; is not available for the configured target.</summary>
    RegisterMaskV16IsNotAvailableForTheConfiguredTarget = 2565,
    /// <summary>Register mask &#x27;V17&#x27; is not available for the configured target.</summary>
    RegisterMaskV17IsNotAvailableForTheConfiguredTarget = 2566,
    /// <summary>Register mask &#x27;V18&#x27; is not available for the configured target.</summary>
    RegisterMaskV18IsNotAvailableForTheConfiguredTarget = 2567,
    /// <summary>Register mask &#x27;V19&#x27; is not available for the configured target.</summary>
    RegisterMaskV19IsNotAvailableForTheConfiguredTarget = 2568,
    /// <summary>Register mask &#x27;V20&#x27; is not available for the configured target.</summary>
    RegisterMaskV20IsNotAvailableForTheConfiguredTarget = 2569,
    /// <summary>Register mask &#x27;V21&#x27; is not available for the configured target.</summary>
    RegisterMaskV21IsNotAvailableForTheConfiguredTarget = 2570,
    /// <summary>Register mask &#x27;V22&#x27; is not available for the configured target.</summary>
    RegisterMaskV22IsNotAvailableForTheConfiguredTarget = 2571,
    /// <summary>Register mask &#x27;V23&#x27; is not available for the configured target.</summary>
    RegisterMaskV23IsNotAvailableForTheConfiguredTarget = 2572,
    /// <summary>Register mask &#x27;V24&#x27; is not available for the configured target.</summary>
    RegisterMaskV24IsNotAvailableForTheConfiguredTarget = 2573,
    /// <summary>Register mask &#x27;V25&#x27; is not available for the configured target.</summary>
    RegisterMaskV25IsNotAvailableForTheConfiguredTarget = 2574,
    /// <summary>Register mask &#x27;V26&#x27; is not available for the configured target.</summary>
    RegisterMaskV26IsNotAvailableForTheConfiguredTarget = 2575,
    /// <summary>Register mask &#x27;V27&#x27; is not available for the configured target.</summary>
    RegisterMaskV27IsNotAvailableForTheConfiguredTarget = 2576,
    /// <summary>Register mask &#x27;V28&#x27; is not available for the configured target.</summary>
    RegisterMaskV28IsNotAvailableForTheConfiguredTarget = 2577,
    /// <summary>Register mask &#x27;V29&#x27; is not available for the configured target.</summary>
    RegisterMaskV29IsNotAvailableForTheConfiguredTarget = 2578,
    /// <summary>Register mask &#x27;V30&#x27; is not available for the configured target.</summary>
    RegisterMaskV30IsNotAvailableForTheConfiguredTarget = 2579,
    /// <summary>Register mask &#x27;V31&#x27; is not available for the configured target.</summary>
    RegisterMaskV31IsNotAvailableForTheConfiguredTarget = 2580,
    /// <summary>Register mask &#x27;P0&#x27; is not available for the configured target.</summary>
    RegisterMaskP0IsNotAvailableForTheConfiguredTarget = 2581,
    /// <summary>Register mask &#x27;P1&#x27; is not available for the configured target.</summary>
    RegisterMaskP1IsNotAvailableForTheConfiguredTarget = 2582,
    /// <summary>Register mask &#x27;P2&#x27; is not available for the configured target.</summary>
    RegisterMaskP2IsNotAvailableForTheConfiguredTarget = 2583,
    /// <summary>Register mask &#x27;P3&#x27; is not available for the configured target.</summary>
    RegisterMaskP3IsNotAvailableForTheConfiguredTarget = 2584,
    /// <summary>Register mask &#x27;P4&#x27; is not available for the configured target.</summary>
    RegisterMaskP4IsNotAvailableForTheConfiguredTarget = 2585,
    /// <summary>Register mask &#x27;P5&#x27; is not available for the configured target.</summary>
    RegisterMaskP5IsNotAvailableForTheConfiguredTarget = 2586,
    /// <summary>Register mask &#x27;P6&#x27; is not available for the configured target.</summary>
    RegisterMaskP6IsNotAvailableForTheConfiguredTarget = 2587,
    /// <summary>Register mask &#x27;P7&#x27; is not available for the configured target.</summary>
    RegisterMaskP7IsNotAvailableForTheConfiguredTarget = 2588,
    /// <summary>Register mask &#x27;P8&#x27; is not available for the configured target.</summary>
    RegisterMaskP8IsNotAvailableForTheConfiguredTarget = 2589,
    /// <summary>Register mask &#x27;P9&#x27; is not available for the configured target.</summary>
    RegisterMaskP9IsNotAvailableForTheConfiguredTarget = 2590,
    /// <summary>Register mask &#x27;P10&#x27; is not available for the configured target.</summary>
    RegisterMaskP10IsNotAvailableForTheConfiguredTarget = 2591,
    /// <summary>Register mask &#x27;P11&#x27; is not available for the configured target.</summary>
    RegisterMaskP11IsNotAvailableForTheConfiguredTarget = 2592,
    /// <summary>Register mask &#x27;P12&#x27; is not available for the configured target.</summary>
    RegisterMaskP12IsNotAvailableForTheConfiguredTarget = 2593,
    /// <summary>Register mask &#x27;P13&#x27; is not available for the configured target.</summary>
    RegisterMaskP13IsNotAvailableForTheConfiguredTarget = 2594,
    /// <summary>Register mask &#x27;P14&#x27; is not available for the configured target.</summary>
    RegisterMaskP14IsNotAvailableForTheConfiguredTarget = 2595,
    /// <summary>Register mask &#x27;P15&#x27; is not available for the configured target.</summary>
    RegisterMaskP15IsNotAvailableForTheConfiguredTarget = 2596,
    /// <summary>Register mask &#x27;FFR&#x27; is not available for the configured target.</summary>
    RegisterMaskFFRIsNotAvailableForTheConfiguredTarget = 2597,
    /// <summary>Register mask &#x27;RA&#x27; is not available for the configured target.</summary>
    RegisterMaskRAIsNotAvailableForTheConfiguredTarget = 2598,
    /// <summary>Register mask &#x27;TP&#x27; is not available for the configured target.</summary>
    RegisterMaskTPIsNotAvailableForTheConfiguredTarget = 2599,
    /// <summary>Register mask &#x27;A0&#x27; is not available for the configured target.</summary>
    RegisterMaskA0IsNotAvailableForTheConfiguredTarget = 2600,
    /// <summary>Register mask &#x27;A1&#x27; is not available for the configured target.</summary>
    RegisterMaskA1IsNotAvailableForTheConfiguredTarget = 2601,
    /// <summary>Register mask &#x27;A2&#x27; is not available for the configured target.</summary>
    RegisterMaskA2IsNotAvailableForTheConfiguredTarget = 2602,
    /// <summary>Register mask &#x27;A3&#x27; is not available for the configured target.</summary>
    RegisterMaskA3IsNotAvailableForTheConfiguredTarget = 2603,
    /// <summary>Register mask &#x27;A4&#x27; is not available for the configured target.</summary>
    RegisterMaskA4IsNotAvailableForTheConfiguredTarget = 2604,
    /// <summary>Register mask &#x27;A5&#x27; is not available for the configured target.</summary>
    RegisterMaskA5IsNotAvailableForTheConfiguredTarget = 2605,
    /// <summary>Register mask &#x27;A6&#x27; is not available for the configured target.</summary>
    RegisterMaskA6IsNotAvailableForTheConfiguredTarget = 2606,
    /// <summary>Register mask &#x27;A7&#x27; is not available for the configured target.</summary>
    RegisterMaskA7IsNotAvailableForTheConfiguredTarget = 2607,
    /// <summary>Register mask &#x27;T0&#x27; is not available for the configured target.</summary>
    RegisterMaskT0IsNotAvailableForTheConfiguredTarget = 2608,
    /// <summary>Register mask &#x27;T1&#x27; is not available for the configured target.</summary>
    RegisterMaskT1IsNotAvailableForTheConfiguredTarget = 2609,
    /// <summary>Register mask &#x27;T2&#x27; is not available for the configured target.</summary>
    RegisterMaskT2IsNotAvailableForTheConfiguredTarget = 2610,
    /// <summary>Register mask &#x27;T3&#x27; is not available for the configured target.</summary>
    RegisterMaskT3IsNotAvailableForTheConfiguredTarget = 2611,
    /// <summary>Register mask &#x27;T4&#x27; is not available for the configured target.</summary>
    RegisterMaskT4IsNotAvailableForTheConfiguredTarget = 2612,
    /// <summary>Register mask &#x27;T5&#x27; is not available for the configured target.</summary>
    RegisterMaskT5IsNotAvailableForTheConfiguredTarget = 2613,
    /// <summary>Register mask &#x27;T6&#x27; is not available for the configured target.</summary>
    RegisterMaskT6IsNotAvailableForTheConfiguredTarget = 2614,
    /// <summary>Register mask &#x27;T7&#x27; is not available for the configured target.</summary>
    RegisterMaskT7IsNotAvailableForTheConfiguredTarget = 2615,
    /// <summary>Register mask &#x27;T8&#x27; is not available for the configured target.</summary>
    RegisterMaskT8IsNotAvailableForTheConfiguredTarget = 2616,
    /// <summary>Register mask &#x27;X0&#x27; is not available for the configured target.</summary>
    RegisterMaskX0IsNotAvailableForTheConfiguredTarget = 2617,
    /// <summary>Register mask &#x27;S0&#x27; is not available for the configured target.</summary>
    RegisterMaskS0IsNotAvailableForTheConfiguredTarget = 2618,
    /// <summary>Register mask &#x27;S1&#x27; is not available for the configured target.</summary>
    RegisterMaskS1IsNotAvailableForTheConfiguredTarget = 2619,
    /// <summary>Register mask &#x27;S2&#x27; is not available for the configured target.</summary>
    RegisterMaskS2IsNotAvailableForTheConfiguredTarget = 2620,
    /// <summary>Register mask &#x27;S3&#x27; is not available for the configured target.</summary>
    RegisterMaskS3IsNotAvailableForTheConfiguredTarget = 2621,
    /// <summary>Register mask &#x27;S4&#x27; is not available for the configured target.</summary>
    RegisterMaskS4IsNotAvailableForTheConfiguredTarget = 2622,
    /// <summary>Register mask &#x27;S5&#x27; is not available for the configured target.</summary>
    RegisterMaskS5IsNotAvailableForTheConfiguredTarget = 2623,
    /// <summary>Register mask &#x27;S6&#x27; is not available for the configured target.</summary>
    RegisterMaskS6IsNotAvailableForTheConfiguredTarget = 2624,
    /// <summary>Register mask &#x27;S7&#x27; is not available for the configured target.</summary>
    RegisterMaskS7IsNotAvailableForTheConfiguredTarget = 2625,
    /// <summary>Register mask &#x27;S8&#x27; is not available for the configured target.</summary>
    RegisterMaskS8IsNotAvailableForTheConfiguredTarget = 2626,
    /// <summary>Register mask &#x27;GP&#x27; is not available for the configured target.</summary>
    RegisterMaskGPIsNotAvailableForTheConfiguredTarget = 2627,
    /// <summary>Register mask &#x27;S9&#x27; is not available for the configured target.</summary>
    RegisterMaskS9IsNotAvailableForTheConfiguredTarget = 2628,
    /// <summary>Register mask &#x27;S10&#x27; is not available for the configured target.</summary>
    RegisterMaskS10IsNotAvailableForTheConfiguredTarget = 2629,
    /// <summary>Register mask &#x27;S11&#x27; is not available for the configured target.</summary>
    RegisterMaskS11IsNotAvailableForTheConfiguredTarget = 2630,
    /// <summary>Register mask &#x27;ZERO&#x27; is not available for the configured target.</summary>
    RegisterMaskZEROIsNotAvailableForTheConfiguredTarget = 2631,
    /// <summary>Register mask &#x27;FT0&#x27; is not available for the configured target.</summary>
    RegisterMaskFT0IsNotAvailableForTheConfiguredTarget = 2632,
    /// <summary>Register mask &#x27;FT1&#x27; is not available for the configured target.</summary>
    RegisterMaskFT1IsNotAvailableForTheConfiguredTarget = 2633,
    /// <summary>Register mask &#x27;FT2&#x27; is not available for the configured target.</summary>
    RegisterMaskFT2IsNotAvailableForTheConfiguredTarget = 2634,
    /// <summary>Register mask &#x27;FT3&#x27; is not available for the configured target.</summary>
    RegisterMaskFT3IsNotAvailableForTheConfiguredTarget = 2635,
    /// <summary>Register mask &#x27;FT4&#x27; is not available for the configured target.</summary>
    RegisterMaskFT4IsNotAvailableForTheConfiguredTarget = 2636,
    /// <summary>Register mask &#x27;FT5&#x27; is not available for the configured target.</summary>
    RegisterMaskFT5IsNotAvailableForTheConfiguredTarget = 2637,
    /// <summary>Register mask &#x27;FT6&#x27; is not available for the configured target.</summary>
    RegisterMaskFT6IsNotAvailableForTheConfiguredTarget = 2638,
    /// <summary>Register mask &#x27;FT7&#x27; is not available for the configured target.</summary>
    RegisterMaskFT7IsNotAvailableForTheConfiguredTarget = 2639,
    /// <summary>Register mask &#x27;FS0&#x27; is not available for the configured target.</summary>
    RegisterMaskFS0IsNotAvailableForTheConfiguredTarget = 2640,
    /// <summary>Register mask &#x27;FS1&#x27; is not available for the configured target.</summary>
    RegisterMaskFS1IsNotAvailableForTheConfiguredTarget = 2641,
    /// <summary>Register mask &#x27;FA0&#x27; is not available for the configured target.</summary>
    RegisterMaskFA0IsNotAvailableForTheConfiguredTarget = 2642,
    /// <summary>Register mask &#x27;FA1&#x27; is not available for the configured target.</summary>
    RegisterMaskFA1IsNotAvailableForTheConfiguredTarget = 2643,
    /// <summary>Register mask &#x27;FA2&#x27; is not available for the configured target.</summary>
    RegisterMaskFA2IsNotAvailableForTheConfiguredTarget = 2644,
    /// <summary>Register mask &#x27;FA3&#x27; is not available for the configured target.</summary>
    RegisterMaskFA3IsNotAvailableForTheConfiguredTarget = 2645,
    /// <summary>Register mask &#x27;FA4&#x27; is not available for the configured target.</summary>
    RegisterMaskFA4IsNotAvailableForTheConfiguredTarget = 2646,
    /// <summary>Register mask &#x27;FA5&#x27; is not available for the configured target.</summary>
    RegisterMaskFA5IsNotAvailableForTheConfiguredTarget = 2647,
    /// <summary>Register mask &#x27;FA6&#x27; is not available for the configured target.</summary>
    RegisterMaskFA6IsNotAvailableForTheConfiguredTarget = 2648,
    /// <summary>Register mask &#x27;FA7&#x27; is not available for the configured target.</summary>
    RegisterMaskFA7IsNotAvailableForTheConfiguredTarget = 2649,
    /// <summary>Register mask &#x27;FS2&#x27; is not available for the configured target.</summary>
    RegisterMaskFS2IsNotAvailableForTheConfiguredTarget = 2650,
    /// <summary>Register mask &#x27;FS3&#x27; is not available for the configured target.</summary>
    RegisterMaskFS3IsNotAvailableForTheConfiguredTarget = 2651,
    /// <summary>Register mask &#x27;FS4&#x27; is not available for the configured target.</summary>
    RegisterMaskFS4IsNotAvailableForTheConfiguredTarget = 2652,
    /// <summary>Register mask &#x27;FS5&#x27; is not available for the configured target.</summary>
    RegisterMaskFS5IsNotAvailableForTheConfiguredTarget = 2653,
    /// <summary>Register mask &#x27;FS6&#x27; is not available for the configured target.</summary>
    RegisterMaskFS6IsNotAvailableForTheConfiguredTarget = 2654,
    /// <summary>Register mask &#x27;FS7&#x27; is not available for the configured target.</summary>
    RegisterMaskFS7IsNotAvailableForTheConfiguredTarget = 2655,
    /// <summary>Register mask &#x27;FS8&#x27; is not available for the configured target.</summary>
    RegisterMaskFS8IsNotAvailableForTheConfiguredTarget = 2656,
    /// <summary>Register mask &#x27;FS9&#x27; is not available for the configured target.</summary>
    RegisterMaskFS9IsNotAvailableForTheConfiguredTarget = 2657,
    /// <summary>Register mask &#x27;FS10&#x27; is not available for the configured target.</summary>
    RegisterMaskFS10IsNotAvailableForTheConfiguredTarget = 2658,
    /// <summary>Register mask &#x27;FS11&#x27; is not available for the configured target.</summary>
    RegisterMaskFS11IsNotAvailableForTheConfiguredTarget = 2659,
    /// <summary>Register mask &#x27;FT8&#x27; is not available for the configured target.</summary>
    RegisterMaskFT8IsNotAvailableForTheConfiguredTarget = 2660,
    /// <summary>Register mask &#x27;FT9&#x27; is not available for the configured target.</summary>
    RegisterMaskFT9IsNotAvailableForTheConfiguredTarget = 2661,
    /// <summary>Register mask &#x27;FT10&#x27; is not available for the configured target.</summary>
    RegisterMaskFT10IsNotAvailableForTheConfiguredTarget = 2662,
    /// <summary>Register mask &#x27;FT11&#x27; is not available for the configured target.</summary>
    RegisterMaskFT11IsNotAvailableForTheConfiguredTarget = 2663,
    /// <summary>NYI: WASM64</summary>
    NYIWASM64 = 2664,
    /// <summary>Floating register names are only available in Debug builds.</summary>
    FloatingRegisterNamesAreOnlyAvailableInDebugBuilds = 2665,
    /// <summary>Unsupported target architecture.</summary>
    UnsupportedTargetArchitecture = 2666,
    /// <summary>WebAssembly register names require the WebAssembly target.</summary>
    WebAssemblyRegisterNamesRequireTheWebAssemblyTarget = 2667,
    /// <summary>Profiler entry register is unavailable for this ABI.</summary>
    ProfilerEntryRegisterIsUnavailableForThisABI = 2668,
    /// <summary>Profiler call target is unavailable for this ABI.</summary>
    ProfilerCallTargetIsUnavailableForThisABI = 2669,
    /// <summary>The AMD64 ABI has no fifth integer argument register.</summary>
    AMD64ABIHasNoFifthIntegerArgumentRegister = 2670,
    /// <summary>The AMD64 ABI has no sixth integer argument register.</summary>
    AMD64ABIHasNoSixthIntegerArgumentRegister = 2671,
    /// <summary>The AMD64 ABI has no fifth floating-point argument register.</summary>
    AMD64ABIHasNoFifthFloatingPointArgumentRegister = 2672,
    /// <summary>The AMD64 ABI has no sixth floating-point argument register.</summary>
    AMD64ABIHasNoSixthFloatingPointArgumentRegister = 2673,
    /// <summary>The AMD64 ABI has no seventh floating-point argument register.</summary>
    AMD64ABIHasNoSeventhFloatingPointArgumentRegister = 2674,
    /// <summary>The AMD64 ABI has no eighth floating-point argument register.</summary>
    AMD64ABIHasNoEighthFloatingPointArgumentRegister = 2675,
    /// <summary>Swift ABI error register is unavailable for this ABI.</summary>
    SwiftABIErrorRegisterIsUnavailableForThisABI = 2676,
    /// <summary>Swift ABI self register is unavailable for this ABI.</summary>
    SwiftABISelfRegisterIsUnavailableForThisABI = 2677,
    /// <summary>Swift ABI return registers are unavailable for this ABI.</summary>
    SwiftABIReturnRegistersAreUnavailableForThisABI = 2678,
    /// <summary>Swift ABI return buffer is unavailable for this ABI.</summary>
    SwiftABIReturnBufferIsUnavailableForThisABI = 2679,
    /// <summary>WebAssembly register operations require the WebAssembly target.</summary>
    WebAssemblyRegisterOperationsRequireTheWebAssemblyTarget = 2680,
    /// <summary>The target DWARF register mapping is not ported.</summary>
    TargetDWARFRegisterMappingIsNotPorted = 2681,
    /// <summary>Unexpected ARM DWARF register.</summary>
    UnexpectedARMDWARFRegister = 2682,
    /// <summary>The target CFI register policy is not ported.</summary>
    TargetCFIRegisterPolicyIsNotPorted = 2683,
    /// <summary>Push unwind recording is not implemented for this target.</summary>
    PushUnwindRecordingIsNotImplementedForThisTarget = 2684,
    /// <summary>Paired-push unwind recording is not implemented for this target.</summary>
    PairedPushUnwindRecordingIsNotImplementedForThisTarget = 2685,
    /// <summary>Pointer-authentication unwind recording is not implemented for this target.</summary>
    PointerAuthenticationUnwindRecordingIsNotImplementedForThisTarget = 2686,
    /// <summary>Return unwind recording is not implemented for this target.</summary>
    ReturnUnwindRecordingIsNotImplementedForThisTarget = 2687,
    /// <summary>Stack unwind recording is not implemented for this target.</summary>
    StackUnwindRecordingIsNotImplementedForThisTarget = 2688,
    /// <summary>Frame-register unwind recording is not implemented for this target.</summary>
    FrameRegisterUnwindRecordingIsNotImplementedForThisTarget = 2689,
    /// <summary>Register-save unwind recording is not implemented for this target.</summary>
    RegisterSaveUnwindRecordingIsNotImplementedForThisTarget = 2690,
    /// <summary>Unwind padding is not implemented for this target.</summary>
    UnwindPaddingIsNotImplementedForThisTarget = 2691,
    /// <summary>Register-pair unwind recording is not implemented for this target.</summary>
    RegisterPairUnwindRecordingIsNotImplementedForThisTarget = 2692,
    /// <summary>Preindexed register-pair unwind recording is not implemented for this target.</summary>
    PreindexedRegisterPairUnwindRecordingIsNotImplementedForThisTarget = 2693,
    /// <summary>Preindexed register unwind recording is not implemented for this target.</summary>
    PreindexedRegisterUnwindRecordingIsNotImplementedForThisTarget = 2694,
    /// <summary>Sized unwind padding requires ARM32.</summary>
    SizedUnwindPaddingRequiresARM32 = 2695,
    /// <summary>Xarch/Wasm unwind EE publication requires xarch or Wasm.</summary>
    XarchWasmUnwindEEPublicationRequiresXarchOrWasm = 2696,
    /// <summary>Unwind EE publication for this target is not ported.</summary>
    UnwindEEPublicationForThisTargetIsNotPorted = 2697,
    /// <summary>Unwind opcode-size decoding for this target is not ported.</summary>
    UnwindOpcodeSizeDecodingForThisTargetIsNotPorted = 2698,
    /// <summary>Unwind diagnostic decoding for this target is not ported.</summary>
    UnwindDiagnosticDecodingForThisTargetIsNotPorted = 2699,
    /// <summary>Unwind NOP diagnostics are only available in Debug builds.</summary>
    UnwindNOPDiagnosticsAreOnlyAvailableInDebugBuilds = 2700,
    /// <summary>Unwind initialization state is only available in Debug builds.</summary>
    UnwindInitializationStateIsOnlyAvailableInDebugBuilds = 2701,
    /// <summary>Unwind storage diagnostics for this target are not ported.</summary>
    UnwindStorageDiagnosticsForThisTargetAreNotPorted = 2702,
    /// <summary>Unwind instruction-size queries require ARM32.</summary>
    UnwindInstructionSizeQueriesRequireARM32 = 2703,
    /// <summary>Fragmented unwind storage for this target is not ported.</summary>
    FragmentedUnwindStorageForThisTargetIsNotPorted = 2704,
    /// <summary>Unwind prolog diagnostics for this target are not ported.</summary>
    UnwindPrologDiagnosticsForThisTargetAreNotPorted = 2706,
    /// <summary>Unwind epilog diagnostics for this target are not ported.</summary>
    UnwindEpilogDiagnosticsForThisTargetAreNotPorted = 2707,
    /// <summary>Compiler-free unwind epilog diagnostics require LoongArch64.</summary>
    CompilerFreeUnwindEpilogDiagnosticsRequireLoongArch64 = 2708,
    /// <summary>Unwind epilog-scope diagnostics for this target are not ported.</summary>
    UnwindEpilogScopeDiagnosticsForThisTargetAreNotPorted = 2709,
    /// <summary>Numbered unwind fragment diagnostics require RISC-V64.</summary>
    NumberedUnwindFragmentDiagnosticsRequireRISCV64 = 2710,
    /// <summary>An emitter is required to reserve unwind information.</summary>
    EmitterIsRequiredToReserveUnwindInformation = 2711,
    /// <summary>The prolog unwind codes have not been recorded.</summary>
    PrologUnwindCodesHaveNotBeenRecorded = 2712,
    /// <summary>An emitter is required to publish unwind information.</summary>
    EmitterIsRequiredToPublishUnwindInformation = 2713,
    /// <summary>An emitter is required to resolve unwind locations.</summary>
    EmitterIsRequiredToResolveUnwindLocations = 2714,
    /// <summary>The prolog unwind codes have not been reserved.</summary>
    PrologUnwindCodesHaveNotBeenReserved = 2715,
    /// <summary>ARM32 unwind diagnostics are only available in Debug builds.</summary>
    ARM32UnwindDiagnosticsAreOnlyAvailableInDebugBuilds = 2716,
    /// <summary>ARM32 unwind diagnostics require ARM32.</summary>
    ARM32UnwindDiagnosticsRequireARM32 = 2717,
    /// <summary>UnwindCodesBase::GetCodes storage is not ported.</summary>
    UnwindCodesBaseGetCodesStorageIsNotPorted = 2718,
    /// <summary>unwindSaveRegPair is not implemented for LoongArch64.</summary>
    UnwindSaveRegPairIsNotImplementedForLoongArch64 = 2719,
    /// <summary>unwindPush is unreachable; use one of the unwindSaveReg* functions instead.</summary>
    UnwindPushIsUnreachableUseOneOfTheUnwindSaveRegFunctionsInstead = 2720,
    /// <summary>unwindSaveRegPair-----unused on RISCV64 yet----</summary>
    UnwindSaveRegPairUnusedOnRISCV64Yet = 2721,
    /// <summary>RISC-V unwind diagnostics are only available in Debug builds.</summary>
    RISCVUnwindDiagnosticsAreOnlyAvailableInDebugBuilds = 2722,
    /// <summary>RISC-V unwind diagnostics require RISC-V64.</summary>
    RISCVUnwindDiagnosticsRequireRISCV64 = 2723,
    /// <summary>Wasm unwind start location is missing.</summary>
    WasmUnwindStartLocationIsMissing = 2724,
    /// <summary>Wasm unwind end location is missing.</summary>
    WasmUnwindEndLocationIsMissing = 2725,
    /// <summary>Invalid floating-point JIT configuration value.</summary>
    InvalidFloatingPointJITConfigurationValue = 2730,
    /// <summary>Configuration arrays are only available in Debug builds.</summary>
    ConfigurationArraysAreOnlyAvailableInDebugBuilds = 2731,
    /// <summary>ConfigIntArray requires a configuration string.</summary>
    ConfigIntArrayRequiresAConfigurationString = 2732,
    /// <summary>Register range formatting is not supported for this target.</summary>
    RegisterRangeFormattingIsNotSupportedForThisTarget = 2734,
    /// <summary>Fixed-width 32-byte and 64-byte vectors are only supported on xarch.</summary>
    FixedWidth32ByteAnd64ByteVectorsAreOnlySupportedOnXarch = 2757,
    /// <summary>Fixed-width 32-byte vectors are only supported on xarch.</summary>
    FixedWidth32ByteVectorsAreOnlySupportedOnXarch = 2759,
    /// <summary>Fixed-width 64-byte vectors are only supported on xarch.</summary>
    FixedWidth64ByteVectorsAreOnlySupportedOnXarch = 2760,
    /// <summary>Value number diagnostics are only available in Debug builds.</summary>
    ValueNumberDiagnosticsAreOnlyAvailableInDebugBuilds = 2762,
    /// <summary>Unexpected scalable SIMD constant kind.</summary>
    UnexpectedScalableSIMDConstantKind = 2763,
    /// <summary>Unexpected VN constant type.</summary>
    UnexpectedVNConstantType = 2764,
    /// <summary>Hardware intrinsics are not enabled for this target.</summary>
    HardwareIntrinsicsAreNotEnabledForThisTarget = 2768,
    /// <summary>Unsupported double math intrinsic: {0}</summary>
    UnsupportedDoubleMathIntrinsicArgument = 2769,
    /// <summary>Unsupported float math intrinsic: {0}</summary>
    UnsupportedFloatMathIntrinsicArgument = 2770,
    /// <summary>Unsupported integral math source: {0}</summary>
    UnsupportedIntegralMathSourceArgument = 2771,
    /// <summary>Unsupported bit-counting intrinsic: {0}</summary>
    UnsupportedBitCountingIntrinsicArgument = 2772,
    /// <summary>Unsupported unary math intrinsic: {0}</summary>
    UnsupportedUnaryMathIntrinsicArgument = 2773,
    /// <summary>Unsupported binary double intrinsic: {0}</summary>
    UnsupportedBinaryDoubleIntrinsicArgument = 2774,
    /// <summary>Unsupported binary float intrinsic: {0}</summary>
    UnsupportedBinaryFloatIntrinsicArgument = 2775,
    /// <summary>Unsupported binary math intrinsic: {0}</summary>
    UnsupportedBinaryMathIntrinsicArgument = 2778,
    /// <summary>Native min/max evaluation is only supported on RISC-V and Wasm.</summary>
    NativeMinMaxEvaluationIsOnlySupportedOnRISCVAndWasm = 2779,
    /// <summary>Most-significant-bit extraction is not supported for this target.</summary>
    MostSignificantBitExtractionIsNotSupportedForThisTarget = 2780,
    /// <summary>Unrecognized map value type.</summary>
    UnrecognizedMapValueType = 2782,
    /// <summary>Scalable mask values are only supported on ARM64 with masked intrinsics enabled.</summary>
    ScalableMaskValuesAreOnlySupportedOnARM64WithMaskedIntrinsicsEnabled = 2790,
    /// <summary>SIMD types are not enabled for this target.</summary>
    SIMDTypesAreNotEnabledForThisTarget = 2791,
    /// <summary>The ARM64 scalable-vector type is not supported on xarch.</summary>
    ARM64ScalableVectorTypeIsNotSupportedOnXarch = 2792,
    /// <summary>Fixed-width 32-byte and 64-byte vectors are not supported on ARM64.</summary>
    FixedWidth32ByteAnd64ByteVectorsAreNotSupportedOnARM64 = 2793,
    /// <summary>Architecture-specific SIMD types are not supported for this target.</summary>
    ArchitectureSpecificSIMDTypesAreNotSupportedForThisTarget = 2794,
    /// <summary>Native variable type ordinal {0} is invalid.</summary>
    NativeVariableTypeOrdinalArgumentIsInvalid = 2795,
    /// <summary>ConfigMethodRange is only available in Debug builds.</summary>
    ConfigMethodRangeIsOnlyAvailableInDebugBuilds = 2796,
    /// <summary>Async continuation variable list has not been created</summary>
    AsyncContinuationVariableListHasNotBeenCreated = 2808,
    /// <summary>Async suspension point list has not been created</summary>
    AsyncSuspensionPointListHasNotBeenCreated = 2809,
    /// <summary>Inlined async frame has no context handling</summary>
    InlinedAsyncFrameHasNoContextHandling = 2810,
    /// <summary>Wasm control-flow intervals are not available for diagnostics.</summary>
    WasmControlFlowIntervalsAreNotAvailableForDiagnostics = 2811,
    /// <summary>Unsupported hardware intrinsic platform</summary>
    UnsupportedHardwareIntrinsicPlatform = 2812,
    /// <summary>Hardware intrinsic lookup ranges are unavailable for this target.</summary>
    HardwareIntrinsicLookupRangesAreUnavailableForThisTarget = 2813,
    /// <summary>Hardware-intrinsic tables are unavailable for this target.</summary>
    HardwareIntrinsicTablesAreUnavailableForThisTarget = 2814,
    /// <summary>Missing enumerator appearances.</summary>
    MissingEnumeratorAppearances = 2815,
    /// <summary>Missing enumerator appearance list.</summary>
    MissingEnumeratorAppearanceList = 2816,
    /// <summary>Missing allocation-temp appearances.</summary>
    MissingAllocationTempAppearances = 2817,
    /// <summary>Missing use-temp appearances.</summary>
    MissingUseTempAppearances = 2818,
    /// <summary>Missing enumerator appearance map.</summary>
    MissingEnumeratorAppearanceMap = 2819,
    /// <summary>Missing blocks to clone.</summary>
    MissingBlocksToClone = 2820,
    /// <summary>Clone extent was not computed.</summary>
    CloneExtentWasNotComputed = 2821,
    /// <summary>CSE heuristic metrics have not been initialized.</summary>
    CSEHeuristicMetricsHaveNotBeenInitialized = 2822,
    /// <summary>CSE heuristic metrics are only available in Debug builds.</summary>
    CSEHeuristicMetricsAreOnlyAvailableInDebugBuilds = 2823,
    /// <summary>CSE local weights have not been captured.</summary>
    CSELocalWeightsHaveNotBeenCaptured = 2824,
    /// <summary>CSE heuristic likelihoods have not been initialized.</summary>
    CSEHeuristicLikelihoodsHaveNotBeenInitialized = 2825,
    /// <summary>RL CSE feature metrics have not been initialized.</summary>
    RLCSEFeatureMetricsHaveNotBeenInitialized = 2826,
    /// <summary>RL CSE base likelihoods have not been initialized.</summary>
    RLCSEBaseLikelihoodsHaveNotBeenInitialized = 2827,
    /// <summary>RL CSE likelihoods have not been initialized.</summary>
    RLCSELikelihoodsHaveNotBeenInitialized = 2828,
    /// <summary>A JIT configuration value is only available in Debug builds.</summary>
    JitConfigValueIsOnlyAvailableInDebugBuilds = 2829,
    /// <summary>Native instruction ordinals are unsupported on this target.</summary>
    NativeInstructionOrdinalsAreUnsupportedOnThisTarget = 2830,
    /// <summary>An instruction has no native ordinal on this target.</summary>
    NativeInstructionOrdinalIsUnavailableForInstruction = 2831,
    /// <summary>A native instruction ordinal is outside the configured table.</summary>
    NativeInstructionOrdinalIsOutOfRange = 2832,
    /// <summary>An instruction set is unavailable for this target.</summary>
    InstructionSetIsUnavailableForThisTarget = 2833,

    /// <summary>A value number has an unsupported vector storage type.</summary>
    UnsupportedValueNumberVectorStorageType = 2834,

    /// <summary>A value number has an unsupported scalar storage type.</summary>
    UnsupportedValueNumberScalarStorageType = 2835,

    /// <summary>The hash table enumerator has no current node.</summary>
    JitHashTableEnumeratorHasNoCurrentNode = 2836,

    /// <summary>The value number attribute table has an unexpected length.</summary>
    ValueNumberAttributeTableHasUnexpectedLength = 2837,
}

public sealed class FatalJitException : SystemException
{
    public FatalJitException() : this(message: null, innerException: null)
    {
    }

    public FatalJitException(string? message) : this(message, innerException: null)
    {
    }

    public FatalJitException(string? message, Exception? innerException)
        : this(CORJIT_INTERNALERROR, message, innerException)
    {
    }

    public FatalJitException(CorJitResult result, string? message = null, Exception? innerException = null)
        : this(result, FatalJitErrorCode.Unspecified, message, innerException)
    {
    }

    internal FatalJitException(
        CorJitResult result,
        FatalJitErrorCode errorCode,
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        HResult = FATAL_JIT_EXCEPTION;
        Result = result;
        ErrorCode = errorCode;
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(FatalJitErrorCode errorCode)
    {
        throw new FatalJitException(
            CORJIT_INTERNALERROR,
            errorCode,
            GetOptionalFailureMessage(errorCode),
            innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException<T0>(FatalJitErrorCode errorCode, T0 argument0)
    {
        throw new FatalJitException(
            CORJIT_INTERNALERROR,
            errorCode,
            GetOptionalFailureMessage(errorCode, argument0),
            innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
    {
        throw new FatalJitException(
            CORJIT_INTERNALERROR,
            errorCode,
            GetOptionalFailureMessage(errorCode, argument0, argument1),
            innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(CorJitResult result, FatalJitErrorCode errorCode)
    {
        throw new FatalJitException(
            result,
            errorCode,
            GetOptionalFailureMessage(errorCode),
            innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(CorJitResult result)
    {
        throw new FatalJitException(
            result,
            FatalJitErrorCode.Unspecified,
            message: null,
            innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException<T0>(
        CorJitResult result,
        FatalJitErrorCode errorCode,
        T0 argument0)
    {
        throw new FatalJitException(
            result,
            errorCode,
            GetOptionalFailureMessage(errorCode, argument0),
            innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException<T0, T1>(
        CorJitResult result,
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
    {
        throw new FatalJitException(
            result,
            errorCode,
            GetOptionalFailureMessage(errorCode, argument0, argument1),
            innerException: null);
    }

    internal static string? GetOptionalArgumentName(FatalJitArgument argument)
    {
        if ((uint)argument >= (uint)FatalJitArgument.Count)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR);
            return default;
        }

        if (!Configuration.DETAILED_FATAL_ERROR_MESSAGES)
        {
            return null;
        }

        switch (argument)
        {
            case FatalJitArgument.Base:
            {
                return "base";
            }

            case FatalJitArgument.Buffer:
            {
                return "buffer";
            }

            case FatalJitArgument.Bytes:
            {
                return "bytes";
            }

            case FatalJitArgument.ExpectedAttributes:
            {
                return "expectedAttributes";
            }

            case FatalJitArgument.Field:
            {
                return "field";
            }

            case FatalJitArgument.Id:
            {
                return "id";
            }

            case FatalJitArgument.Index:
            {
                return "index";
            }

            case FatalJitArgument.Key:
            {
                return "key";
            }

            case FatalJitArgument.Oper:
            {
                return "oper";
            }

            case FatalJitArgument.Ordinal:
            {
                return "ordinal";
            }

            case FatalJitArgument.PCallSites:
            {
                return "pCallSites";
            }

            case FatalJitArgument.PCallSiteSizes:
            {
                return "pCallSiteSizes";
            }

            case FatalJitArgument.PCorJitInfo:
            {
                return "pCorJitInfo";
            }

            case FatalJitArgument.PMethodInfo:
            {
                return "pMethodInfo";
            }

            case FatalJitArgument.Precision:
            {
                return "precision";
            }

            case FatalJitArgument.Status:
            {
                return "status";
            }

            case FatalJitArgument.StructLayout:
            {
                return "structLayout";
            }

            case FatalJitArgument.TableIndex:
            {
                return "tableIndex";
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR);
                return default;
            }
        }
    }

    internal static string? GetOptionalFailureMessage(FatalJitErrorCode errorCode)
    {
        return Configuration.DETAILED_FATAL_ERROR_MESSAGES
            ? GetStaticFailureMessage(errorCode) ?? GetStableFailureId(errorCode)
            : GetStableFailureId(errorCode);
    }

    internal static string? GetOptionalFailureMessage<T0>(
        FatalJitErrorCode errorCode,
        T0 argument0)
    {
        if (!Configuration.DETAILED_FATAL_ERROR_MESSAGES)
        {
            return GetStableFailureId(errorCode);
        }

        var message = GetStaticFailureFormat(errorCode);
        return message is null
            ? GetStableFailureId(errorCode)
            : string.Format(CultureInfo.CurrentCulture, message, argument0);
    }

    internal static string? GetOptionalFailureMessage<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
    {
        if (!Configuration.DETAILED_FATAL_ERROR_MESSAGES)
        {
            return GetStableFailureId(errorCode);
        }

        var message = GetStaticFailureFormat(errorCode);
        return message is null
            ? GetStableFailureId(errorCode)
            : string.Format(CultureInfo.CurrentCulture, message, argument0, argument1);
    }

    private static string? GetStableFailureId(FatalJitErrorCode errorCode)
    {
        return errorCode == FatalJitErrorCode.Unspecified
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"RJS-{(int)errorCode:D4}");
    }

    private static string? GetStaticFailureFormat(FatalJitErrorCode errorCode)
    {
        return errorCode switch
        {
            FatalJitErrorCode.UnsupportedWriteBarrierForm => "No write-barrier helper exists for {0}.",
            FatalJitErrorCode.UnexpectedDataSectionType => "Unexpected data-section type {0}.",
            FatalJitErrorCode.UnexpectedDataSectionElementSize => "Unexpected data-section element size {0}.",
            FatalJitErrorCode.InvalidJumpKind => "Invalid jump kind {0}.",
            FatalJitErrorCode.MissingReverseJumpMapping => "Jump kind {0} has no reverse mapping for this target.",
            FatalJitErrorCode.UnsupportedJumpKind => "Jump kind {0} is not supported for this target.",
            FatalJitErrorCode.UnsupportedInitializationBlockKind => "Unsupported initialization block kind {0}.",
            FatalJitErrorCode.UnsupportedCopyBlockKind => "Unsupported copy block kind {0}.",
            FatalJitErrorCode.UnsupportedStackArgumentKind => "Unsupported stack argument kind {0}.",
            FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds => "JIT config value '{0}' is only available in Debug builds.",
            FatalJitErrorCode.NativeInstructionOrdinalIsUnavailableForInstruction => "Instruction '{0}' is unsupported on this target.",
            FatalJitErrorCode.InstructionSetIsUnavailableForThisTarget => "Instruction set '{0}' is unavailable for this target.",
            FatalJitErrorCode.MissingRegisterBank => "LSRA has no register bank for {0}.",
            FatalJitErrorCode.UnexpectedReferenceKind => "Unexpected reference kind during register resolution: {0}.",
            _ => GetStaticFailureMessage(errorCode),
        };
    }

    internal static string? GetStaticFailureMessage(FatalJitErrorCode errorCode)
    {
        var message = errorCode switch
        {
            FatalJitErrorCode.UnsupportedValueNumberVectorStorageType => "Value number has an unsupported vector storage type.",
            FatalJitErrorCode.UnsupportedValueNumberScalarStorageType => "Value number has an unsupported scalar storage type.",
            FatalJitErrorCode.JitHashTableEnumeratorHasNoCurrentNode => "The hash table enumerator has no current node.",
            FatalJitErrorCode.ValueNumberAttributeTableHasUnexpectedLength => "Value number attribute table has an unexpected length.",
            FatalJitErrorCode.SelectNodeContainmentIsUnavailableForThisTarget => Configuration.TARGET_ARM
                ? "GT_SELECT nodes are not supported on ARM32."
                : Configuration.TARGET_LOONGARCH64
                    ? "GT_SELECT nodes are not supported on LoongArch64."
                    : "Select containment is not ported for this target.",
            FatalJitErrorCode.BoundJumpMissingTarget => "A bound jump requires an instruction-group target.",
            FatalJitErrorCode.GatherWithStackMemoryFormat => "A gather cannot use a stack instruction format.",
            FatalJitErrorCode.GatherWithStaticMemoryFormat => "A gather cannot use a static-memory instruction format.",
            FatalJitErrorCode.JumpTargetMissingEmitterLabelCookie => "A jump target requires an emitter label cookie.",
            FatalJitErrorCode.JumpInstructionGroupMissingFromList => "A jump's instruction group is absent from the group list.",
            FatalJitErrorCode.LabelInstructionMissingBoundTargetGroup => "A label instruction requires a bound target group.",
            FatalJitErrorCode.RelativeLabelTableMissingBlock => "A relative-label table requires a block for every entry.",
            FatalJitErrorCode.RelativeLabelTableMissingEmittedBlockLabel => "A relative-label table requires an emitted block label.",
            FatalJitErrorCode.RelativeLabelTableMissingFirstBasicBlock => "A relative-label table requires the first basic block.",
            FatalJitErrorCode.RelativeLabelTableMissingFirstEmittedLabel => "A relative-label table requires the first emitted label.",
            FatalJitErrorCode.UnresolvedRemovableJumpTarget => "A removable jump requires a resolved basic-block label cookie.",
            FatalJitErrorCode.SavedJumpMissingInstructionGroup => "A saved jump must belong to an instruction group.",
            FatalJitErrorCode.SavedJumpRequiresInstructionGroup => "A saved jump requires an instruction group.",
            FatalJitErrorCode.AddressModeDisplayMissingCompiler => "Address-mode display requires an active compiler.",
            FatalJitErrorCode.AbsoluteLabelTableMissingBlock => "An absolute-label table requires a block for every entry.",
            FatalJitErrorCode.AbsoluteLabelTableMissingEmittedBlockLabel => "An absolute-label table requires an emitted block label.",
            FatalJitErrorCode.AlignmentDescriptorMissingContainingGroup => "An alignment descriptor requires a containing group.",
            FatalJitErrorCode.AlignmentDescriptorMissingLoopHeadGroup => "An alignment descriptor requires a loop-head group.",
            FatalJitErrorCode.AlignmentDescriptorMissingLoopHeadPredecessor => "An alignment descriptor requires a loop-head predecessor.",
            FatalJitErrorCode.StackOutputMissingCompiler => "An initialized compiler is required for stack output.",
            FatalJitErrorCode.UnboundJumpMissingBasicBlockTarget => "An unbound jump requires a basic-block target.",
            FatalJitErrorCode.AsyncDiagnosticsMissingCodeLocation => "Async diagnostics require a valid code location.",
            FatalJitErrorCode.BoundLabelDescriptorMissingTargetGroup => "Bound label descriptor has no target group.",
            FatalJitErrorCode.CodeSizeComputationMissingCompiler => "Code-size computation requires an active compiler.",
            FatalJitErrorCode.DataSectionDiagnosticsMissingCompiler => "Data-section diagnostics require an active compiler.",
            FatalJitErrorCode.DataSectionOutputMissingChunksOrSections => "Data-section output requires allocated chunks and nonempty sections.",
            FatalJitErrorCode.DataSectionOutputMissingCompiler => "Data-section output requires an active compiler.",
            FatalJitErrorCode.FinalInstructionEmissionMissingCompiler => "Final instruction emission requires an active compiler.",
            FatalJitErrorCode.FrameReferenceDisplayMissingCompiler => "Frame reference display requires an active compiler.",
            FatalJitErrorCode.HandleDisplayMissingCompiler => "Handle display requires an active compiler.",
            FatalJitErrorCode.InstructionDisplayMissingCompiler => "Instruction display requires an active compiler.",
            FatalJitErrorCode.InstructionGroupLabelsMissingCompiler => "Instruction group labels require an active compiler.",
            FatalJitErrorCode.InstructionIssueMissingCompiler => "Instruction issue requires an active compiler.",
            FatalJitErrorCode.InvalidEvexRoundingMode => "Invalid EVEX rounding mode.",
            FatalJitErrorCode.InvalidMixedMemoryTupleFormat => "Invalid instruction format for a mixed memory tuple.",
            FatalJitErrorCode.InvalidInstructionTupleType => "Invalid instruction tuple type.",
            FatalJitErrorCode.InvalidMoveSideEffectClassification => "Invalid move instruction for side-effect classification.",
            FatalJitErrorCode.InvalidDisplayOperandRegister => "Invalid operand register.",
            FatalJitErrorCode.LabelOutputRequiresJumpDescriptor => "Label output requires a jump descriptor.",
            FatalJitErrorCode.LabelOutputMissingCompiler => "Label output requires an active compiler.",
            FatalJitErrorCode.LabelTableDiagnosticsMissingBlock => "Label-table diagnostics require a block for every entry.",
            FatalJitErrorCode.LabelTableDiagnosticsMissingEmittedBlockLabel => "Label-table diagnostics require an emitted block label.",
            FatalJitErrorCode.LabelTableDiagnosticsMissingFirstBasicBlock => "Label-table diagnostics require the first basic block.",
            FatalJitErrorCode.LabelTableDiagnosticsMissingFirstEmittedLabel => "Label-table diagnostics require the first emitted label.",
            FatalJitErrorCode.LocalAddressEmissionMissingCompiler => "Local address emission requires an active compiler.",
            FatalJitErrorCode.LoopAlignmentMissingCompiler => "Loop alignment requires an active compiler.",
            FatalJitErrorCode.LoopAlignmentMissingDescriptor => "Loop alignment requires an alignment descriptor.",
            FatalJitErrorCode.LoopAlignmentAdjustmentMissingCompiler => "Loop-alignment adjustment requires an active compiler.",
            FatalJitErrorCode.LoopAlignmentPaddingMissingCompiler => "Loop-alignment padding requires an active compiler.",
            FatalJitErrorCode.LoopSizeCalculationMissingCompiler => "Loop-size calculation requires an active compiler.",
            FatalJitErrorCode.MoveLabelMissingDescriptor => "Move label requires a label descriptor.",
            FatalJitErrorCode.StaticReferenceDisplayMissingCompiler => "Static reference display requires an active compiler.",
            FatalJitErrorCode.UnboundLabelDescriptorMissingTargetBlock => "Unbound label descriptor has no target block.",
            FatalJitErrorCode.UnexpectedApxImmediateInstructionFormat => "Unexpected APX immediate instruction format.",
            FatalJitErrorCode.UnexpectedApxImmediateOperandSize => "Unexpected APX immediate operand size.",
            FatalJitErrorCode.UnexpectedImmediateOnlyInstruction => "Unexpected immediate-only instruction.",
            FatalJitErrorCode.UnexpectedMemoryOperandFormat => "Unexpected memory operand format.",
            FatalJitErrorCode.UnexpectedMemoryOperandTupleType => "Unexpected memory operand tuple type.",
            FatalJitErrorCode.UnexpectedStackImmediateSize => "Unexpected stack immediate size.",
            FatalJitErrorCode.UnexpectedStackOperandSize => "Unexpected stack operand size.",
            FatalJitErrorCode.UnexpectedStaticImmediateSize => "Unexpected static immediate size.",
            FatalJitErrorCode.UnexpectedStaticOperandSize => "Unexpected static operand size.",
            FatalJitErrorCode.UnexpectedThreeRegisterStackStoreFormat => "Unexpected three-register stack-store format.",
            FatalJitErrorCode.UnexpectedThreeRegisterStaticStoreFormat => "Unexpected three-register static-store format.",
            FatalJitErrorCode.UnknownInstructionOutputFormat => "Unknown instruction-output format.",
            FatalJitErrorCode.UnsupportedRexWxInstruction => "Unsupported REX.WX instruction.",
            FatalJitErrorCode.EmitterInitializationBeforeCodegenStart => "emitBegCG must precede emitter initialization.",
            FatalJitErrorCode.InvalidABIClassifierKind => "Invalid ABI classifier kind.",
            FatalJitErrorCode.BlockStatementMustHaveACloneableRoot => "A block statement must have a cloneable root.",
            FatalJitErrorCode.EHToVMClauseOrderHasNotBeenInitialized => "EH-to-VM clause order has not been initialized.",
            FatalJitErrorCode.EHTryEndHasNoSuccessor => "EH try end has no successor.",
            FatalJitErrorCode.EHHandlerEndHasNoSuccessor => "EH handler end has no successor.",
            FatalJitErrorCode.EHFilterHasNoStartBlock => "EH filter has no start block.",
            FatalJitErrorCode.VMToEHClauseOrderHasNotBeenInitialized => "VM-to-EH clause order has not been initialized.",
            FatalJitErrorCode.UnknownEHHandlerType => "Unknown EH handler type.",
            FatalJitErrorCode.IPMappingHasNoPredecessorAtADuplicateNativeOffset => "IP mapping has no predecessor at a duplicate native offset.",
            FatalJitErrorCode.MultiRegisterReturnsIntoSIMDRegistersAreUnsupportedOnWindowsAMD64 => "Multi-register returns into SIMD registers are unsupported on Windows AMD64.",
            FatalJitErrorCode.Failure32BitLongShiftReachedA64BitTarget => "32-bit long shift reached a 64-bit target.",
            FatalJitErrorCode.SwiftErrorReturnsAreUnavailableForThisTarget => "Swift error returns are unavailable for this target.",
            FatalJitErrorCode.X86LongMultiplyNodeReachedAnotherTarget => "The x86 long multiply node reached another target.",
            FatalJitErrorCode.SwiftErrorValuesAreUnavailableForThisTarget => "Swift error values are unavailable for this target.",
            FatalJitErrorCode.Failure32BitLongNodeReachedA64BitTarget => "32-bit long node reached a 64-bit target.",
            FatalJitErrorCode.InsertionIntoAHomedParameterRegisterIsNotSupportedOnThisTarget => "Insertion into a homed parameter register is not supported on this target.",
            FatalJitErrorCode.PushStructArgumentsAreOnlySupportedOnX86 => "Push struct arguments are only supported on x86.",
            FatalJitErrorCode.FixedRegisterTableIsNotInitialized => "The fixed-register table is not initialized.",
            FatalJitErrorCode.UnexpectedBlockKindAtTheEndOfCodeGeneration => "Unexpected block kind at the end of code generation.",
            FatalJitErrorCode.InvalidScalarMemmoveWidth => "Invalid scalar memmove width.",
            FatalJitErrorCode.MissingJMPEpilogNode => "Missing JMP epilog node.",
            FatalJitErrorCode.InvalidShiftOperator => "Invalid shift operator.",
            FatalJitErrorCode.MDArrayTemporaryMustBeAnIntOrObjectReference => "An MD-array temporary must be an int or object reference.",
            FatalJitErrorCode.BoundsCheckCoalescingRequiresValueNumbering => "Bounds-check coalescing requires value numbering.",
            FatalJitErrorCode.UnexpectedBlockKindForCodeSizeEstimation => "Unexpected block kind for code-size estimation.",
            FatalJitErrorCode.InvalidGPRSize => "Invalid GPR size.",
            FatalJitErrorCode.CloningATryRegionRequiresABlockMap => "Cloning a try region requires a block map.",
            FatalJitErrorCode.CloningATryRegionRequiresAnInsertionPoint => "Cloning a try region requires an insertion point.",
            FatalJitErrorCode.CallfinallyContinuationRequiresAPrecedingBlock => "A callfinally continuation requires a preceding block.",
            FatalJitErrorCode.CloningATryRegionRequiresItsBlocks => "Cloning a try region requires its blocks.",
            FatalJitErrorCode.FinallyReturnRequiresItsSuccessorTable => "A finally return requires its successor table.",
            FatalJitErrorCode.ClonedBlockRequiresItsBlockMapEntry => "A cloned block requires its block-map entry.",
            FatalJitErrorCode.ForwardSubstitutionBlockHasABrokenStatementList => "A forward-substitution block has a broken statement list.",
            FatalJitErrorCode.NonFinalStatementMustHaveASuccessor => "A non-final statement must have a successor.",
            FatalJitErrorCode.InsertedLocalSegmentIsNotConnected => "The inserted local segment is not connected.",
            FatalJitErrorCode.CollectedForwardSubstitutionUseLostItsOwner => "A collected forward-substitution use lost its owner.",
            FatalJitErrorCode.CheapForwardSubstitutionAddressCouldNotBeCloned => "A cheap forward-substitution address could not be cloned.",
            FatalJitErrorCode.FinalForwardSubstitutionUseLostItsOwner => "The final forward-substitution use lost its owner.",
            FatalJitErrorCode.ForwardSubstitutionCandidateNeedsAFollowingStatement => "A forward-substitution candidate needs a following statement.",
            FatalJitErrorCode.LastForwardSubstitutionUseLostItsOwner => "The last forward-substitution use lost its owner.",
            FatalJitErrorCode.ForwardedStoreMustHaveALocalsList => "A forwarded store must have a locals list.",
            FatalJitErrorCode.SWITCHShouldBeACallRatherThanAnInlinedPoll => "SWITCH should be a call rather than an inlined poll.",
            FatalJitErrorCode.UnknownBlockTypeForUpdatingPredecessorLists => "Unknown block type for updating predecessor lists.",
            FatalJitErrorCode.NonFirstStatementNeedsAPredecessor => "A non-first statement needs a predecessor.",
            FatalJitErrorCode.UnexpectedScalarComparison => "Unexpected scalar comparison.",
            FatalJitErrorCode.UnexpectedSpecialComparison => "Unexpected special comparison.",
            FatalJitErrorCode.UnexpectedMaskedMemoryIntrinsic => "Unexpected masked memory intrinsic.",
            FatalJitErrorCode.UnsupportedSIMDConversionTarget => "Unsupported SIMD conversion target.",
            FatalJitErrorCode.MissingInlineCandidateInformation => "Missing inline candidate information.",
            FatalJitErrorCode.IVOptimizationRequiresDominators => "IV optimization requires dominators.",
            FatalJitErrorCode.ConditionalLoopExitMustHaveATerminalStatement => "A conditional loop exit must have a terminal statement.",
            FatalJitErrorCode.CountedLoopMustHaveAPreheader => "The counted loop must have a preheader.",
            FatalJitErrorCode.LoopOccurrencesMustBeInitializedBeforeVisiting => "Loop occurrences must be initialized before visiting.",
            FatalJitErrorCode.StrengthReductionRequiresDominators => "Strength reduction requires dominators.",
            FatalJitErrorCode.ActiveIVCursorNeedsARecurrence => "An active IV cursor needs a recurrence.",
            FatalJitErrorCode.RephrasingAnIVRequiresConstantSteps => "Rephrasing an IV requires constant steps.",
            FatalJitErrorCode.Only32And64BitIVsCanBeRephrased => "Only 32- and 64-bit IVs can be rephrased.",
            FatalJitErrorCode.GCPointerStrengthReductionRequiresValueNumbering => "GC-pointer strength reduction requires value numbering.",
            FatalJitErrorCode.ConstantIVStepMustMaterialize => "A constant IV step must materialize.",
            FatalJitErrorCode.ReplacementCursorNeedsItsRecurrence => "A replacement cursor needs its recurrence.",
            FatalJitErrorCode.StrengthReducedUseWasLostFromItsStatement => "The strength-reduced use was lost from its statement.",
            FatalJitErrorCode.InductionVariableUseIsNotOwnedByItsStatement => "An induction variable use is not owned by its statement.",
            FatalJitErrorCode.IVWideningRequiresNaturalLoops => "IV widening requires natural loops.",
            FatalJitErrorCode.IVWideningRequiresAnEntryBlock => "IV widening requires an entry block.",
            FatalJitErrorCode.NonPreheaderIVInitializerNeedsItsSSADefinition => "A non-preheader IV initializer needs its SSA definition.",
            FatalJitErrorCode.LoopCloningVisitorRequiresACurrentBlock => "Loop-cloning visitor requires a current block.",
            FatalJitErrorCode.LoopCloningVisitorRequiresACurrentStatement => "Loop-cloning visitor requires a current statement.",
            FatalJitErrorCode.ArrayCandidateHasNoUseBlock => "Array candidate has no use block.",
            FatalJitErrorCode.UnknownLoopCloningCandidate => "Unknown loop-cloning candidate.",
            FatalJitErrorCode.UnrecognizedLoopCloningExitComparison => "Unrecognized loop-cloning exit comparison.",
            FatalJitErrorCode.LoopCloningRequiresDiscoveredNaturalLoops => "Loop cloning requires discovered natural loops.",
            FatalJitErrorCode.SpanCandidateHasNoUseBlock => "Span candidate has no use block.",
            FatalJitErrorCode.SpanCandidateHasNoBoundsCheck => "Span candidate has no bounds check.",
            FatalJitErrorCode.UnknownLoopCloningOptimization => "Unknown loop-cloning optimization.",
            FatalJitErrorCode.ClonedHoistExpressionHasFewerOperandsThanItsSource => "Cloned hoist expression has fewer operands than its source.",
            FatalJitErrorCode.LoopHoistingRequiresACloneOfItsInvariantExpression => "Loop hoisting requires a clone of its invariant expression.",
            FatalJitErrorCode.LoopHoistingRequiresLoopDiscovery => "Loop hoisting requires loop discovery.",
            FatalJitErrorCode.LoopHoistingRequiresLoopSideEffectAnalysis => "Loop hoisting requires loop side-effect analysis.",
            FatalJitErrorCode.LoopHoistingRequiresADFSTree => "Loop hoisting requires a DFS tree.",
            FatalJitErrorCode.LoopHoistingRequiresDominators => "Loop hoisting requires dominators.",
            FatalJitErrorCode.LoopHoistingRequiresAnSSADefinitionBlock => "Loop hoisting requires an SSA definition block.",
            FatalJitErrorCode.LoopUnrollingRequiresDiscoveredNaturalLoops => "Loop unrolling requires discovered natural loops.",
            FatalJitErrorCode.AnalyzedLoopMustHaveATestBlock => "An analyzed loop must have a test block.",
            FatalJitErrorCode.AnalyzedLoopMustHaveATestTree => "An analyzed loop must have a test tree.",
            FatalJitErrorCode.AnalyzedLoopMustHaveAnIncrement => "An analyzed loop must have an increment.",
            FatalJitErrorCode.BadPreconditionInCompilerOptUnrollLoops => "Bad precondition in Compiler::optUnrollLoops().",
            FatalJitErrorCode.UnexpectedLoopIncrementOperator => "Unexpected loop increment operator.",
            FatalJitErrorCode.LoopTryRegionPassedFeasibilityButCouldNotBeCloned => "A loop try region passed feasibility but could not be cloned.",
            FatalJitErrorCode.UnexpectedLoopIteratorType => "Unexpected loop iterator type.",
            FatalJitErrorCode.UnexpectedLoopIteratorOperator => "Unexpected loop iterator operator.",
            FatalJitErrorCode.UnexpectedLoopTestOperator => "Unexpected loop test operator.",
            FatalJitErrorCode.RangeCheckCloningRequiresValueNumbering => "Range-check cloning requires value numbering.",
            FatalJitErrorCode.FinalRangeCheckIsNotReachableFromTheFirst => "The final range check is not reachable from the first.",
            FatalJitErrorCode.RangeCheckCloningExhaustedTheNativeHashTableSizes => "Range-check cloning exhausted the native hash table sizes.",
            FatalJitErrorCode.ClonedFastPathLostItsPredecessor => "The cloned fast path lost its predecessor.",
            FatalJitErrorCode.BoundsCheckToRemoveIsNotInItsOriginalStatement => "The bounds check to remove is not in its original statement.",
            FatalJitErrorCode.FastPathMustContainAStatement => "The fast path must contain a statement.",
            FatalJitErrorCode.FirstBoundsCheckIndexCannotBeCloned => "The first bounds-check index cannot be cloned.",
            FatalJitErrorCode.FirstBoundsCheckLengthCannotBeCloned => "The first bounds-check length cannot be cloned.",
            FatalJitErrorCode.ClonedBoundsCheckIndexCannotBeClonedAgain => "The cloned bounds-check index cannot be cloned again.",
            FatalJitErrorCode.LowerBoundIndexCannotBeCloned => "The lower-bound index cannot be cloned.",
            FatalJitErrorCode.SplitFastPathMustHaveASuccessor => "The split fast path must have a successor.",
            FatalJitErrorCode.LowerBoundBlockMustHaveATest => "The lower-bound block must have a test.",
            FatalJitErrorCode.UpperBoundBlockMustHaveATest => "The upper-bound block must have a test.",
            FatalJitErrorCode.RuntimeLookupHelperRequiresASignatureArgument => "A runtime lookup helper requires a signature argument.",
            FatalJitErrorCode.RuntimeLookupSignatureIsNotRegistered => "Runtime lookup signature is not registered.",
            FatalJitErrorCode.ExpandableRuntimeLookupRequiresIndirectionsAndANullCheck => "An expandable runtime lookup requires indirections and a null check.",
            FatalJitErrorCode.CannotCloneRuntimeLookupResultLocal => "Cannot clone runtime lookup result local.",
            FatalJitErrorCode.RuntimeLookupHelperRequiresAContextArgument => "A runtime lookup helper requires a context argument.",
            FatalJitErrorCode.CannotCloneRuntimeLookupContext => "Cannot clone runtime lookup context.",
            FatalJitErrorCode.CannotCloneIndirectRuntimeLookupOffset => "Cannot clone indirect runtime lookup offset.",
            FatalJitErrorCode.MissingIndirectRuntimeLookupOffset => "Missing indirect runtime lookup offset.",
            FatalJitErrorCode.CannotCloneDictionarySizeCheckBase => "Cannot clone dictionary size-check base.",
            FatalJitErrorCode.CannotCloneRuntimeLookupSlot => "Cannot clone runtime lookup slot.",
            FatalJitErrorCode.DynamicRuntimeLookupRequiresANonzeroFinalOffset => "A dynamic runtime lookup requires a nonzero final offset.",
            FatalJitErrorCode.UnsupportedSIMDConversionWidth => "Unsupported SIMD conversion width.",
            FatalJitErrorCode.UnsupportedSIMDConversionSource => "Unsupported SIMD conversion source.",
            FatalJitErrorCode.UnsupportedSIMDIndexType => "Unsupported SIMD index type.",
            FatalJitErrorCode.UnsupportedSIMDZipElementWidth => "Unsupported SIMD zip element width.",
            FatalJitErrorCode.UnsupportedSIMDUnzipElementWidth => "Unsupported SIMD unzip element width.",
            FatalJitErrorCode.UnsupportedSaturatingNarrowingType => "Unsupported saturating narrowing type.",
            FatalJitErrorCode.OnlySIMDORAndXOROperationsAreSupported => "Only SIMD OR and XOR operations are supported.",
            FatalJitErrorCode.StaticInitializationCallHasNoOwningUse => "Static initialization call has no owning use.",
            FatalJitErrorCode.NativeAOTTLSExpansionIsNotSupportedForThisTarget => "NativeAOT TLS expansion is not supported for this target.",
            FatalJitErrorCode.CoreCLRTLSExpansionIsNotSupportedForThisTarget => "CoreCLR TLS expansion is not supported for this target.",
            FatalJitErrorCode.ThreadStaticHelperRequiresATypeIndex => "Thread static helper requires a type index.",
            FatalJitErrorCode.CannotCloneAThreadStaticExpression => "Cannot clone a thread static expression.",
            FatalJitErrorCode.CannotCloneReadUtf8DestinationLength => "Cannot clone ReadUtf8 destination length.",
            FatalJitErrorCode.CannotCloneReadUtf8DestinationPointer => "Cannot clone ReadUtf8 destination pointer.",
            FatalJitErrorCode.CannotCloneReadUtf8ByteLength => "Cannot clone ReadUtf8 byte length.",
            FatalJitErrorCode.LoopMemoryDependenceRequiresAValueNumberStore => "Loop memory dependence requires a value number store.",
            FatalJitErrorCode.LoopEntryGuardRequiresACondition => "A loop entry guard requires a condition.",
            FatalJitErrorCode.InvalidCompilerPhase => "Invalid compiler phase.",
            FatalJitErrorCode.UnknownIPMappingKind => "Unknown IP mapping kind.",
            FatalJitErrorCode.InvalidInstructionInputSize => "Invalid instruction input size.",
            FatalJitErrorCode.InvalidInstructionFormatForAPXNDD => "Invalid instruction format for APX NDD.",
            FatalJitErrorCode.JumpRemovalRequiresAFinalInstructionGroup => "Jump removal requires a final instruction group.",
            FatalJitErrorCode.AlignmentAdjustmentRequiresAFinalInstructionGroup => "Alignment adjustment requires a final instruction group.",
            FatalJitErrorCode.InvalidDisplayOperandSize => "Invalid display operand size.",
            FatalJitErrorCode.NonInterruptibleGroupHasNoInstructions => "Non-interruptible group has no instructions.",
            FatalJitErrorCode.InvalidStackSlotBase => "Invalid stack slot base.",
            FatalJitErrorCode.InvalidGCSlotFlags => "Invalid GC slot flags.",
            FatalJitErrorCode.FilterHasNoStartingBlock => "Filter has no starting block.",
            FatalJitErrorCode.UnknownGenericContextParameterType => "Unknown generic context parameter type.",
            FatalJitErrorCode.GCStructLocalHasNoLayout => "GC struct local has no layout.",
            FatalJitErrorCode.StackSlotMapWasNotInitialized => "Stack slot map was not initialized.",
            FatalJitErrorCode.LiveStackSlotWasNotAssigned => "Live stack slot was not assigned.",
            FatalJitErrorCode.RegisterSlotMapWasNotInitialized => "Register slot map was not initialized.",
            FatalJitErrorCode.LiveRegisterSlotWasNotAssigned => "Live register slot was not assigned.",
            FatalJitErrorCode.OutgoingArgumentTerminatorIsMissing => "Outgoing argument terminator is missing.",
            FatalJitErrorCode.GTSLLIUWScaleIndexHandlingIsOnlySupportedOnRISCV64 => "GT_SLLI_UW scale-index handling is only supported on RISC-V64.",
            FatalJitErrorCode.InvalidNodeTypeForGetRegTypeByIndex => "Invalid node type for GetRegTypeByIndex.",
            FatalJitErrorCode.InvalidNodeTypeForGetRegSpillFlagByIdx => "Invalid node type for GetRegSpillFlagByIdx.",
            FatalJitErrorCode.InvalidNodeTypeForIndexedRegisterSpillFlags => "Invalid node type for indexed register spill flags.",
            FatalJitErrorCode.UnsupportedCallKindInVzeroupperClassification => "Unsupported call kind in vzeroupper classification.",
            FatalJitErrorCode.ARM64ConditionalSelectNodesAreOnlySupportedOnARM64 => "ARM64 conditional-select nodes are only supported on ARM64.",
            FatalJitErrorCode.ARM64BitfieldNodesAreOnlySupportedOnARM64 => "ARM64 bitfield nodes are only supported on ARM64.",
            FatalJitErrorCode.SIMDConstantsAreUnsupportedOnThisTarget => "SIMD constants are unsupported on this target.",
            FatalJitErrorCode.ScalableSIMDConstantsAreOnlySupportedOnARM64 => "Scalable SIMD constants are only supported on ARM64.",
            FatalJitErrorCode.ThisVectorOperationRequiresFixedWidthConstantStorage => "This vector operation requires fixed-width constant storage.",
            FatalJitErrorCode.UnexpectedSIMDConstantType => "Unexpected SIMD constant type.",
            FatalJitErrorCode.UnexpectedScalarBroadcastInstruction => "Unexpected scalar broadcast instruction.",
            FatalJitErrorCode.UnexpectedInlineDecision => "Unexpected InlineDecision.",
            FatalJitErrorCode.InvalidInlineObservation => "Invalid inline observation.",
            FatalJitErrorCode.InvalidIntegralToFloatingConversion => "Invalid integral-to-floating conversion.",
            FatalJitErrorCode.InvalidFloatingConversionSourceType => "Invalid floating conversion source type.",
            FatalJitErrorCode.InvalidSignExtensionSize => "Invalid sign-extension size.",
            FatalJitErrorCode.UnexpectedInstructionWithPseudoName => "Unexpected instruction with pseudo name.",
            FatalJitErrorCode.NoPointInMeasuringDEBUGCode => "No point in measuring DEBUG code.",
            FatalJitErrorCode.CycleCountersOnlyHookedUpOnX86X64 => "Cycle counters only hooked up on x86/x64.",
            FatalJitErrorCode.EHCodeOffsetRequiresAnEmitter => "EH code offset requires an emitter.",
            FatalJitErrorCode.GCCallDescriptorIsRequiredBeforeAllocatingItsArgumentTable => "A GC call descriptor is required before allocating its argument table.",
            FatalJitErrorCode.LCArrayToGenTreeDoesNotSupportMDArraysInThePinnedNativeImplementation => "LC_Array.ToGenTree does not support MD arrays in the pinned native implementation.",
            FatalJitErrorCode.CannotMaterializeAnInvalidLoopCloningExpression => "Cannot materialize an invalid loop-cloning expression.",
            FatalJitErrorCode.UnknownLoopCloningIdentifierType => "Unknown loop-cloning identifier type.",
            FatalJitErrorCode.CannotMaterializeAnInvalidLoopCloningIdentifier => "Cannot materialize an invalid loop-cloning identifier.",
            FatalJitErrorCode.ConditionCodeLoweringRequiresAFlagProducingHardwareIntrinsic => "Condition-code lowering requires a flag-producing hardware intrinsic.",
            FatalJitErrorCode.VectorElementExtractionRequiresAnArithmeticBaseType => "Vector element extraction requires an arithmetic base type.",
            FatalJitErrorCode.FusedMultiplyLoweringRequiresAnFMAIntrinsic => "Fused multiply lowering requires an FMA intrinsic.",
            FatalJitErrorCode.UnexpectedFloatingComparisonIntrinsic => "Unexpected floating comparison intrinsic.",
            FatalJitErrorCode.UnexpectedIntegerComparisonIntrinsic => "Unexpected integer comparison intrinsic.",
            FatalJitErrorCode.UnexpectedScalarComparisonIntrinsic => "Unexpected scalar comparison intrinsic.",
            FatalJitErrorCode.UnsupportedSIMDMemoryComparisonOperation => "Unsupported SIMD memory comparison operation.",
            FatalJitErrorCode.SwiftErrorReturnsAreNotSupportedForThisTarget => "Swift error returns are not supported for this target.",
            FatalJitErrorCode.EntryAllocationRequiresBlockInformation => "Entry allocation requires block information.",
            FatalJitErrorCode.EntryAllocationRequiresAFirstBlock => "Entry allocation requires a first block.",
            FatalJitErrorCode.StackEntryDefinitionRequiresANextReference => "A stack entry definition requires a next reference.",
            FatalJitErrorCode.NextStackEntryReferenceRequiresATree => "The next stack entry reference requires a tree.",
            FatalJitErrorCode.UpperVectorReferencesRequireARelatedLocal => "Upper-vector references require a related local.",
            FatalJitErrorCode.UpperVectorSaveRequiresAnAssignedInterval => "Upper-vector save requires an assigned interval.",
            FatalJitErrorCode.SpecialPutargRequiresASourceLocal => "Special putarg requires a source local.",
            FatalJitErrorCode.ExposedUseRequiresAnIncomingLocalMap => "Exposed use requires an incoming local map.",
            FatalJitErrorCode.PreassignedLocalRequiresALastReference => "A preassigned local requires a last reference.",
            FatalJitErrorCode.ConflictingPreassignedLocalRequiresANextReference => "A conflicting preassigned local requires a next reference.",
            FatalJitErrorCode.LSRABlockSequencingCanOnlyBeInitializedOnce => "LSRA block sequencing can only be initialized once.",
            FatalJitErrorCode.LSRABlockSequenceWasAlreadyInitialized => "LSRA block sequence was already initialized.",
            FatalJitErrorCode.LSRABlockMetadataRequiresComputedPredecessorLists => "LSRA block metadata requires computed predecessor lists.",
            FatalJitErrorCode.LSRABlockSequenceDoesNotCoverTheCompilerFlowGraph => "The LSRA block sequence does not cover the compiler flow graph.",
            FatalJitErrorCode.LSRABlockSequenceDoesNotCoverEveryCompilerBlock => "The LSRA block sequence does not cover every compiler block.",
            FatalJitErrorCode.LSRARequiresANonEmptyCompilerFlowGraph => "LSRA requires a non-empty compiler flow graph.",
            FatalJitErrorCode.LSRABlockTraversalRequiresAnInitializedBlockSequence => "LSRA block traversal requires an initialized block sequence.",
            FatalJitErrorCode.LSRABlockVisitationRequiresInitializedBlockTraits => "LSRA block visitation requires initialized block traits.",
            FatalJitErrorCode.CallReferenceBuildingRequiresInitializedCodegenState => "Call reference building requires initialized codegen state.",
            FatalJitErrorCode.WindowsX64VarargsRequiresAnXMM0XMM3ArgumentRegister => "Windows x64 varargs requires an XMM0-XMM3 argument register.",
            FatalJitErrorCode.LSRARequiresInitializedCodegenState => "LSRA requires initialized codegen state.",
            FatalJitErrorCode.LSRAInternalDefinitionStateContainsAnEmptyEntry => "LSRA internal definition state contains an empty entry.",
            FatalJitErrorCode.LSRADefinitionListEntryMustRetainItsReference => "An LSRA definition-list entry must retain its reference.",
            FatalJitErrorCode.UpperVectorSavesRequireTheCurrentBasicBlock => "Upper-vector saves require the current basic block.",
            FatalJitErrorCode.LSRADefinitionListEntryMustRetainItsDefiningNode => "An LSRA definition-list entry must retain its defining node.",
            FatalJitErrorCode.LSRAStructTemporariesMustBeLocalVariablesOrCalls => "LSRA struct temporaries must be local variables or calls.",
            FatalJitErrorCode.InvalidLSRAEdgeResolutionType => "Invalid LSRA edge resolution type.",
            FatalJitErrorCode.LSRAEdgeResolutionRequiresAllocatedLocalMapsAndBlockSequence => "LSRA edge resolution requires allocated local maps and block sequence.",
            FatalJitErrorCode.SplitEdgeRequiresAUniqueOriginalPredecessor => "Split edge requires a unique original predecessor.",
            FatalJitErrorCode.SplitEdgeRequiresASingleSuccessor => "A split edge requires a single successor.",
            FatalJitErrorCode.SplitEdgeRequiresASinglePredecessor => "A split edge requires a single predecessor.",
            FatalJitErrorCode.CriticalEdgeRequiresAnOutgoingRegisterMap => "Critical edge requires an outgoing register map.",
            FatalJitErrorCode.SwitchEdgeRequiresItsSwitchTable => "A switch edge requires its switch table.",
            FatalJitErrorCode.SwitchEdgeResolutionRequiresCodegenState => "Switch edge resolution requires codegen state.",
            FatalJitErrorCode.ConditionalEdgeRequiresItsBranch => "A conditional edge requires its branch.",
            FatalJitErrorCode.CriticalEdgeResolutionRequiresTheSharedMap => "Critical edge resolution requires the shared map.",
            FatalJitErrorCode.CriticalEdgeRequiresASuccessorRegisterMap => "Critical edge requires a successor register map.",
            FatalJitErrorCode.BranchResolutionRequiresATerminator => "Branch resolution requires a terminator.",
            FatalJitErrorCode.SwapResolutionRequiresATerminator => "Swap resolution requires a terminator.",
            FatalJitErrorCode.ResolutionRequiresAPredecessorOutgoingMap => "Resolution requires a predecessor outgoing map.",
            FatalJitErrorCode.ResolutionRequiresASuccessorBlock => "Resolution requires a successor block.",
            FatalJitErrorCode.ResolutionRequiresASuccessorRegisterMap => "Resolution requires a successor register map.",
            FatalJitErrorCode.SplitResolutionRequiresASuccessor => "Split resolution requires a successor.",
            FatalJitErrorCode.CriticalResolutionRequiresASuccessor => "Critical resolution requires a successor.",
            FatalJitErrorCode.UnknownLSRAEdgeResolutionType => "Unknown LSRA edge-resolution type.",
            FatalJitErrorCode.ReadyResolutionTargetRequiresAnInterval => "Ready resolution target requires an interval.",
            FatalJitErrorCode.SwapResolutionRequiresTheDisplacedInterval => "Swap resolution requires the displaced interval.",
            FatalJitErrorCode.SwapResolutionRequiresTheSourceInterval => "Swap resolution requires the source interval.",
            FatalJitErrorCode.CycleSpillRequiresADisplacedInterval => "Cycle spill requires a displaced interval.",
            FatalJitErrorCode.CycleResolutionRequiresASourceInterval => "Cycle resolution requires a source interval.",
            FatalJitErrorCode.CycleScratchMoveRequiresADisplacedInterval => "Cycle scratch move requires a displaced interval.",
            FatalJitErrorCode.StackResolutionRequiresAPendingInterval => "Stack resolution requires a pending interval.",
            FatalJitErrorCode.EdgeResolutionRequiresThePredecessorSRegisterMap => "Edge resolution requires the predecessor's register map.",
            FatalJitErrorCode.EdgeResolutionRequiresTheSuccessorSRegisterMap => "Edge resolution requires the successor's register map.",
            FatalJitErrorCode.SharedCriticalResolutionRequiresARegisterMap => "Shared critical resolution requires a register map.",
            FatalJitErrorCode.LSRAFrameTypeWasNotSelected => "LSRA frame type was not selected.",
            FatalJitErrorCode.IntrinsicReferenceRequiresAnInitializedBlockSequence => "An intrinsic reference requires an initialized block sequence.",
            FatalJitErrorCode.LSRAIntervalRequiresAConcreteRegisterType => "LSRA interval requires a concrete register type.",
            FatalJitErrorCode.WriteBarrierKillSetConstructionRequiresInitializedCodeGen => "Write-barrier kill-set construction requires initialized CodeGen.",
            FatalJitErrorCode.CallKillSetConstructionRequiresInitializedCodeGen => "Call kill-set construction requires initialized CodeGen.",
            FatalJitErrorCode.VirtualStubCallsRequireInitializedRegisterMetadata => "Virtual-stub calls require initialized register metadata.",
            FatalJitErrorCode.LocalBlockLocationsRequireEnregisteredLocals => "Local block locations require enregistered locals.",
            FatalJitErrorCode.BlockLocationsRequireAnOutgoingVariableMap => "Block locations require an outgoing variable map.",
            FatalJitErrorCode.BlockLocationsRequireInitializedLSRABlockInformation => "Block locations require initialized LSRA block information.",
            FatalJitErrorCode.BlockLocationsRequireAnIncomingVariableMap => "Block locations require an incoming variable map.",
            FatalJitErrorCode.BlockLocationsRequireAPredecessorOutgoingMap => "Block locations require a predecessor outgoing map.",
            FatalJitErrorCode.ResolutionOfAPredecessorSpilledRegisterRequiresANextReference => "Resolution of a predecessor-spilled register requires a next reference.",
            FatalJitErrorCode.OutOfOrderIntervalRequiresANextReference => "An out-of-order interval requires a next reference.",
            FatalJitErrorCode.AssignedIntervalRequiresItsRegisterRecord => "An assigned interval requires its register record.",
            FatalJitErrorCode.LocalIntervalConstructionRequiresEnregisteredLocals => "Local interval construction requires enregistered locals.",
            FatalJitErrorCode.IntervalConstructionRequiresInitializedCodegenState => "Interval construction requires initialized codegen state.",
            FatalJitErrorCode.TailcallBlockMustContainItsJumpOrCall => "A tailcall block must contain its jump or call.",
            FatalJitErrorCode.WriteThroughHeuristicsRequireBlockSequencing => "Write-through heuristics require block sequencing.",
            FatalJitErrorCode.TrackedLocalMappingIsRequiredForIntervalConstruction => "Tracked local mapping is required for interval construction.",
            FatalJitErrorCode.LocalRegisterResolutionRequiresMatchingBlockNodeOwnershipAndEnregisteredLocals => "Local register resolution requires matching block/node ownership and enregistered locals.",
            FatalJitErrorCode.ContainedLocalResolutionRequiresAnIncomingVariableMap => "Contained local resolution requires an incoming variable map.",
            FatalJitErrorCode.ContainedLocalUseRequiresItsTreeNode => "A contained local use requires its tree node.",
            FatalJitErrorCode.ReloadResolutionRequiresAnIncomingVariableMap => "Reload resolution requires an incoming variable map.",
            FatalJitErrorCode.CopiedOrMovedLocalReferenceRequiresItsTreeNode => "A copied or moved local reference requires its tree node.",
            FatalJitErrorCode.CopiedLocalReferenceRequiresItsOwningBlock => "A copied local reference requires its owning block.",
            FatalJitErrorCode.LocalRegisterResolutionRequiresEnregisteredLocals => "Local register resolution requires enregistered locals.",
            FatalJitErrorCode.LocalRegisterResolutionRequiresAnEntryBlock => "Local register resolution requires an entry block.",
            FatalJitErrorCode.LocalRegisterResolutionRequiresAnEntryVariableMap => "Local register resolution requires an entry variable map.",
            FatalJitErrorCode.LocalRegisterResolutionRequiresABlockReference => "Local register resolution requires a block reference.",
            FatalJitErrorCode.LocalRegisterResolutionRequiresAnOrderedBlockReference => "Local register resolution requires an ordered block reference.",
            FatalJitErrorCode.UpperVectorSavesRequireAKillNode => "Upper-vector saves require a kill node.",
            FatalJitErrorCode.UpperVectorIntervalRequiresItsLocal => "An upper-vector interval requires its local.",
            FatalJitErrorCode.UpperVectorSaveRequiresItsInterval => "An upper-vector save requires its interval.",
            FatalJitErrorCode.UpperVectorRestoresRequireALocal => "Upper-vector restores require a local.",
            FatalJitErrorCode.SpilledOrMovedTemporaryRequiresItsNextReference => "A spilled or moved temporary requires its next reference.",
            FatalJitErrorCode.SavedTemporaryRequiresALaterUse => "A saved temporary requires a later use.",
            FatalJitErrorCode.RegisterParameterRequiresAnInitialReference => "A register parameter requires an initial reference.",
            FatalJitErrorCode.UpperVectorSaveRequiresItsLocalInterval => "An upper-vector save requires its local interval.",
            FatalJitErrorCode.UpperVectorRestoreRequiresItsLocalInterval => "An upper-vector restore requires its local interval.",
            FatalJitErrorCode.UpperVectorRestoreRequiresAnOwningUse => "An upper-vector restore requires an owning use.",
            FatalJitErrorCode.VectorRestoreBeforeABranchRequiresATerminator => "A vector restore before a branch requires a terminator.",
            FatalJitErrorCode.LocalSourceUseRequiresItsTree => "A local source use requires its tree.",
            FatalJitErrorCode.MinimalRegisterAllocationRequiresLocalsToRemainUnEnregistered => "Minimal register allocation requires locals to remain un-enregistered.",
            FatalJitErrorCode.MinimalRegisterAllocationCannotRunWithEnregisteredLocals => "Minimal register allocation cannot run with enregistered locals.",
            FatalJitErrorCode.MinimalLSRAReferencesMustNotBeLocalVariableIntervals => "Minimal LSRA references must not be local-variable intervals.",
            FatalJitErrorCode.UpperVectorSaveRequiresAnAssignedRegister => "An upper-vector save requires an assigned register.",
            FatalJitErrorCode.UpperVectorIntervalMustHaveAFirstReference => "An upper-vector interval must have a first reference.",
            FatalJitErrorCode.RequiredCopyRegisterAllocationFoundNoRegister => "A required copy-register allocation found no register.",
            FatalJitErrorCode.MinimalIntervalConstructionCannotRunWithEnregisteredLocals => "Minimal interval construction cannot run with enregistered locals.",
            FatalJitErrorCode.MinimalRegisterResolutionCannotRunWithEnregisteredLocals => "Minimal register resolution cannot run with enregistered locals.",
            FatalJitErrorCode.MinimalResolutionCannotSaveAnEnregisteredLocalVector => "Minimal resolution cannot save an enregistered local vector.",
            FatalJitErrorCode.UpperVectorRestoresRequireEnregisteredLocalResolution => "Upper-vector restores require enregistered-local resolution.",
            FatalJitErrorCode.ContainedAddressUseRequiresAnAddressMode => "A contained address use requires an address mode.",
            FatalJitErrorCode.LateCallArgumentMustHaveALoweredNode => "A late call argument must have a lowered node.",
            FatalJitErrorCode.LSRADefinitionListEntryHasNoReferencePosition => "LSRA definition list entry has no reference position.",
            FatalJitErrorCode.LSRACouldNotMatchAUseWithItsDefinition => "LSRA could not match a use with its definition.",
            FatalJitErrorCode.TrackedLSRALocalsRequireIntervalMappings => "Tracked LSRA locals require interval mappings.",
            FatalJitErrorCode.GetRegTypeByIndexCannotDetermineAMultiRegisterLocalFieldType => "GetRegTypeByIndex cannot determine a multi-register local field type.",
            FatalJitErrorCode.LSRACannotDetermineAMultiRegisterDefinitionType => "LSRA cannot determine a multi-register definition type.",
            FatalJitErrorCode.LSRAUpperVectorLookupEncounteredANonUpperVectorInterval => "LSRA upper-vector lookup encountered a non-upper-vector interval.",
            FatalJitErrorCode.UpperVectorIntervalsRequireARelatedLocalInterval => "Upper-vector intervals require a related local interval.",
            FatalJitErrorCode.PartiallySpilledVectorRequiresAPrecedingUpperVectorSave => "A partially spilled vector requires a preceding upper-vector save.",
            FatalJitErrorCode.RegisterAssignmentRequiresInitializedCodegenState => "Register assignment requires initialized codegen state.",
            FatalJitErrorCode.CannotClearAnUnassignedPhysicalRegister => "Cannot clear an unassigned physical register.",
            FatalJitErrorCode.CannotUnassignAPhysicalRegisterWithoutAnInterval => "Cannot unassign a physical register without an interval.",
            FatalJitErrorCode.ActiveIntervalSpillRequiresItsLastReference => "An active interval spill requires its last reference.",
            FatalJitErrorCode.RestorableRegisterHasNoPreviousInterval => "A restorable register has no previous interval.",
            FatalJitErrorCode.UpperVectorIntervalRequiresItsRelatedLocalInterval => "An upper-vector interval requires its related local interval.",
            FatalJitErrorCode.UpperVectorIntervalMustBeRelatedToALocalInterval => "An upper-vector interval must be related to a local interval.",
            FatalJitErrorCode.SingleDefinitionIntervalMustHaveAFirstReference => "A single-definition interval must have a first reference.",
            FatalJitErrorCode.OnlyLocalVariableIntervalsCanBeLiveOnBlockEntry => "Only local-variable intervals can be live on block entry.",
            FatalJitErrorCode.RegisterKillsRequireInitializedCodegenState => "Register kills require initialized codegen state.",
            FatalJitErrorCode.NonEmptyKillListMustHaveATailReference => "A non-empty kill list must have a tail reference.",
            FatalJitErrorCode.MinimalCopyRegisterAssignmentRequiresAnActiveInterval => "A minimal copy-register assignment requires an active interval.",
            FatalJitErrorCode.ActiveCopyRegisterIntervalMustRetainItsAssignedRegister => "An active copy-register interval must retain its assigned register.",
            FatalJitErrorCode.IntervalSAssignedRegisterDoesNotMatchItsPhysicalRegister => "The interval's assigned register does not match its physical register.",
            FatalJitErrorCode.NoSpillUnassignmentRequiresAnActiveInterval => "A no-spill unassignment requires an active interval.",
            FatalJitErrorCode.MinimalRegisterStateResetCannotRunWithEnregisteredLocals => "Minimal register-state reset cannot run with enregistered locals.",
            FatalJitErrorCode.SpillWeightCalculationRequiresAnAssignedInterval => "Spill-weight calculation requires an assigned interval.",
            FatalJitErrorCode.SpillWeightCalculationRequiresARecentReference => "Spill-weight calculation requires a recent reference.",
            FatalJitErrorCode.RegisterSelectionHasNotBeenReset => "Register selection has not been reset.",
            FatalJitErrorCode.MinimalLSRASelectionRequiresLocalVariablesToRemainUnEnregistered => "Minimal LSRA selection requires local variables to remain un-enregistered.",
            FatalJitErrorCode.MinimalLSRASelectionHasNoCandidateRegisters => "Minimal LSRA selection has no candidate registers.",
            FatalJitErrorCode.RequiredReferenceLostAllCandidateRegisters => "A required reference lost all candidate registers.",
            FatalJitErrorCode.MinimalLSRASelectionDidNotChooseExactlyOneRegister => "Minimal LSRA selection did not choose exactly one register.",
            FatalJitErrorCode.SingleRegisterHeuristicMustSelectExactlyOneRegister => "A single-register heuristic must select exactly one register.",
            FatalJitErrorCode.RegisterSelectionIsMissingItsRangeEndReference => "Register selection is missing its range-end reference.",
            FatalJitErrorCode.SpillCostSelectionRequiresEachBusyCandidateToHaveAnAssignedInterval => "Spill-cost selection requires each busy candidate to have an assigned interval.",
            FatalJitErrorCode.CurrentNextIntervalLocationMustHaveAReference => "A current next-interval location must have a reference.",
            FatalJitErrorCode.CopyOrReloadRequiresAnOwningLIRUse => "Copy or reload requires an owning LIR use.",
            FatalJitErrorCode.GroupedLSRACopyOrReloadRequiresAMultiRegisterSource => "A grouped LSRA copy or reload requires a multi-register source.",
            FatalJitErrorCode.LSRACopyOrReloadRegisterIsAlreadyAssigned => "The LSRA copy or reload register is already assigned.",
            FatalJitErrorCode.UnsupportedScalarMathIntrinsic => "Unsupported scalar math intrinsic.",
            FatalJitErrorCode.DefUseConflictResolutionIsOnlyValidForTemporaryIntervals => "Def-use conflict resolution is only valid for temporary intervals.",
            FatalJitErrorCode.ConflictingDefinitionMustBeFollowedByItsUse => "A conflicting definition must be followed by its use.",
            FatalJitErrorCode.DefUseConflictReferencesMustBeOrderedReferencesOfOneInterval => "Def-use conflict references must be ordered references of one interval.",
            FatalJitErrorCode.TemporaryDefinitionMustRetainItsSourceTreeNode => "A temporary definition must retain its source tree node.",
            FatalJitErrorCode.FixedUseMustBePresentInTheNextFixedReferenceMap => "A fixed use must be present in the next-fixed-reference map.",
            FatalJitErrorCode.AssignedIntervalMustRetainItsRecentReference => "An assigned interval must retain its recent reference.",
            FatalJitErrorCode.CannotSpillAnUnassignedPhysicalRegister => "Cannot spill an unassigned physical register.",
            FatalJitErrorCode.PhysicalRegisterWithoutARecentReferenceMustBelongToALocal => "A physical register without a recent reference must belong to a local.",
            FatalJitErrorCode.LocalWithoutARecentReferenceMustBeAParameter => "A local without a recent reference must be a parameter.",
            FatalJitErrorCode.KillReferencesDoNotHaveASpillWeight => "Kill references do not have a spill weight.",
            FatalJitErrorCode.SpilledIntervalMustHaveAFirstReference => "A spilled interval must have a first reference.",
            FatalJitErrorCode.LSRABlockWeightsAreUnavailableBeforeIntervalConstruction => "LSRA block weights are unavailable before interval construction.",
            FatalJitErrorCode.RejectedStackAllocationRequiresAReason => "A rejected stack allocation requires a reason.",
            FatalJitErrorCode.ConditionalEscapeCloningRequiresADFSTree => "Conditional escape cloning requires a DFS tree.",
            FatalJitErrorCode.ConditionalEscapeCloningRequiresDominators => "Conditional escape cloning requires dominators.",
            FatalJitErrorCode.ConditionalEscapeCloningRequiresADefinitionBlock => "Conditional escape cloning requires a definition block.",
            FatalJitErrorCode.UnexpectedObjectAllocationCandidateKind => "Unexpected object allocation candidate kind.",
            FatalJitErrorCode.NewArrayHelperRequiresALengthArgument => "A new-array helper requires a length argument.",
            FatalJitErrorCode.SelectedTryRegionCouldNotBeCloned => "The selected try region could not be cloned.",
            FatalJitErrorCode.EmptyStaticAllocationRequiresOnePredecessor => "An empty-static allocation requires one predecessor.",
            FatalJitErrorCode.EmptyStaticPredecessorRequiresItsBranchStatement => "An empty-static predecessor requires its branch statement.",
            FatalJitErrorCode.StructFieldStoreMustHaveALayout => "Struct field store must have a layout.",
            FatalJitErrorCode.RetypingStructFieldsRequiresTheirNewLayout => "Retyping struct fields requires their new layout.",
            FatalJitErrorCode.UnexpectedParentWhileRetypingAStackObjectUse => "Unexpected parent while retyping a stack-object use.",
            FatalJitErrorCode.RetypedStructLocalRequiresItsLayout => "Retyped struct local requires its layout.",
            FatalJitErrorCode.UNBOXRequiresItsObjectArgument => "UNBOX requires its object argument.",
            FatalJitErrorCode.UNBOXRequiresACloneableLocal => "UNBOX requires a cloneable local.",
            FatalJitErrorCode.DelegateInvokeRequiresAThisArgument => "Delegate invoke requires a this argument.",
            FatalJitErrorCode.DelegateInvokeRequiresACloneableReceiver => "Delegate invoke requires a cloneable receiver.",
            FatalJitErrorCode.UNBOXTypeTestMustRetainItsSideEffectCall => "UNBOX type test must retain its side-effect call.",
            FatalJitErrorCode.TrackedStructRequiresItsLayout => "Tracked struct requires its layout.",
            FatalJitErrorCode.InvalidVariableLocationKind => "Invalid variable location kind.",
            FatalJitErrorCode.InvalidStackVariableType => "Invalid stack variable type.",
            FatalJitErrorCode.InvalidRegisterVariableType => "Invalid register variable type.",
            FatalJitErrorCode.VariableLiveRangesHaveNotBeenInitialized => "Variable live ranges have not been initialized.",
            FatalJitErrorCode.TrackedLocalMappingHasNotBeenInitialized => "Tracked local mapping has not been initialized.",
            FatalJitErrorCode.InvalidRegisterNumber => "Invalid register number.",
            FatalJitErrorCode.PreciseMapStoreRequiresTheCurrentBlock => "A precise map store requires the current block.",
            FatalJitErrorCode.MissingRegisterBank => "LSRA has no register bank for this register type.",
            FatalJitErrorCode.UnexpectedReferenceKind => "Unexpected reference kind during register resolution.",
            FatalJitErrorCode.ICorJitInfoVTableLayoutMismatch => "ICorJitInfo vtable layout does not match the generated API order.",
            FatalJitErrorCode.ICorJitInfoProxyContextUnavailable => "ICorJitInfo proxy context is no longer valid.",
            FatalJitErrorCode.FailFastReturned => "Environment.FailFast returned unexpectedly.",
            FatalJitErrorCode.NativeInstructionSetUnavailable => "Native instruction-set ordinal '{0}' is unavailable for this target.",
            FatalJitErrorCode.NativeInstructionOrdinalsAreUnsupportedOnThisTarget => "Instruction ordinals are unsupported on this target.",
            FatalJitErrorCode.NativeInstructionOrdinalIsOutOfRange => "The native instruction ordinal is outside the configured table.",
            FatalJitErrorCode.GCBitStreamRequiresALittleEndian64BitTarget => "The GC bit stream requires a little-endian 64-bit target.",
            FatalJitErrorCode.DestinationIsShorterThanTheEncodedBitStream => "The destination is shorter than the encoded bit stream.",
            FatalJitErrorCode.EmitRequiresACompletedBuildAndMayOnlyBeCalledOnce => "Emit requires a completed Build and may only be called once.",
            FatalJitErrorCode.GCInfoEncodingIsNotImplementedForARMTargets => "GC info encoding is not implemented for ARM targets.",
            FatalJitErrorCode.OnlyTheAMD64GCInfoEncodingIsImplemented => "Only the AMD64 GC info encoding is implemented.",
            FatalJitErrorCode.EveryCallSiteNeedsACorrespondingInstructionSize => "Every call site needs a corresponding instruction size.",
            FatalJitErrorCode.ARM64EditAndContinueGCInfoMetadataIsNotImplemented => "ARM64 Edit-and-Continue GC-info metadata is not implemented.",
            FatalJitErrorCode.GCInformationCannotChangeAfterBuild => "GC information cannot change after Build.",
            FatalJitErrorCode.RegisterNumbersAreNotDefinedForThisTarget => "Register numbers are not defined for this target.",
            FatalJitErrorCode.AmbientStackPointerRegisterNumberIsNotDefinedForThisTarget => "Ambient stack-pointer register number is not defined for this target.",
            FatalJitErrorCode.FloatingPointRegisterNumberingIsNotDefinedForThisTarget => "Floating-point register numbering is not defined for this target.",
            FatalJitErrorCode.CallerPopConventionsAreOnlyDefinedForX86 => "Caller-pop conventions are only defined for x86.",
            FatalJitErrorCode.UnseededCLRRandomInitRequiresTheWindowsNativeThreadID => "Unseeded CLRRandom.Init requires the Windows native thread ID.",
            FatalJitErrorCode.CLRRandomMustBeInitializedBeforeDrawingValues => "CLRRandom must be initialized before drawing values.",
            FatalJitErrorCode.JumpKindsAreNotSupportedForThisTarget => "Jump kinds are not supported for this target.",
            FatalJitErrorCode.WebAssemblyOnlyCompilerCodeRequiresTheWebAssemblyTarget => "WebAssembly-only compiler code requires the WebAssembly target.",
            FatalJitErrorCode.UnsupportedHostArchitectureArgument => "Unsupported host architecture: {0}.",
            FatalJitErrorCode.RequiredCompilerConfigurationSwitchArgumentIsNotSet => "Required compiler configuration switch '{0}' is not set.",
            FatalJitErrorCode.DebuggerMethodHashIsOnlyAvailableInDebugBuilds => "The debugger method hash is only available in Debug builds.",
            FatalJitErrorCode.NonEmptyRegisterAssignmentRequiresAReferent => "A non-empty register assignment requires a referent.",
            FatalJitErrorCode.InlineContextDiagnosticStateIsOnlyAvailableInDebugBuilds => "Inline context diagnostic state is only available in Debug builds.",
            FatalJitErrorCode.InlineInstructionStateHasNotBeenInitialized => "Inline instruction state has not been initialized.",
            FatalJitErrorCode.AsyncCallDiagnosticStateIsOnlyAvailableInDebugBuilds => "Async-call diagnostic state is only available in Debug builds.",
            FatalJitErrorCode.InlinePolicyDataCollectionStateIsOnlyAvailableInDebugBuilds => "Inline-policy data-collection state is only available in Debug builds.",
            FatalJitErrorCode.InlineStressRandomStateIsOnlyAvailableInDebugBuilds => "Inline stress random state is only available in Debug builds.",
            FatalJitErrorCode.CallerShadowSpaceOffsetsRequireWindowsAMD64 => "Caller shadow-space offsets require Windows AMD64.",
            FatalJitErrorCode.NoABIClassifierIsAvailableForThisTarget => "No ABI classifier is available for this target.",
            FatalJitErrorCode.TrivialPointerSizedStructClassificationRequiresX86 => "Trivial pointer-sized struct classification requires x86.",
            FatalJitErrorCode.FloatingPointStructLoweringIsUnsupportedForThisTarget => "Floating-point struct lowering is unsupported for this target.",
            FatalJitErrorCode.SwiftABIClassificationIsUnsupportedForThisTarget => "Swift ABI classification is unsupported for this target.",
            FatalJitErrorCode.LinkRegisterIsNotAvailableForThisTarget => "A link register is not available for this target.",
            FatalJitErrorCode.LongReturnRegistersAreNotAvailableForThisTarget => "Long return registers are not available for this target.",
            FatalJitErrorCode.EncodedCalleeSavedRegistersAreNotDefinedForThisTarget => "Encoded callee-saved registers are not defined for this target.",
            FatalJitErrorCode.SwiftReturnRegistersAreNotSupportedForThisTarget => "Swift return registers are not supported for this target.",
            FatalJitErrorCode.StackProbeHelperArgumentsAreNotSupportedForThisTarget => "Stack-probe helper arguments are not supported for this target.",
            FatalJitErrorCode.ZeroRegisterIsNotAvailableForThisTarget => "A zero register is not available for this target.",
            FatalJitErrorCode.MaskedRegistersAreNotSupportedForThisTarget => "Masked registers are not supported for this target.",
            FatalJitErrorCode.SwiftABIRegistersAreNotSupportedForThisTarget => "Swift ABI registers are not supported for this target.",
            FatalJitErrorCode.XarchMetadataIsUnavailableForThisArchitecture => "xarch metadata is unavailable for this architecture.",
            FatalJitErrorCode.TargetMetadataIsUnavailableForThisArchitecture => "Target metadata is unavailable for this architecture.",
            FatalJitErrorCode.WindowsX64ABIClassificationRequiresWindowsAMD64 => "Windows x64 ABI classification requires Windows AMD64.",
            FatalJitErrorCode.ExpandableArrayHasNotBeenAllocated => "The expandable array has not been allocated.",
            FatalJitErrorCode.StructReturnHasNoLayout => "Struct return has no layout",
            FatalJitErrorCode.CouldNotFindReturnForCall => "Could not find return for call",
            FatalJitErrorCode.StructLocalHasNoLayout => "Struct local has no layout",
            FatalJitErrorCode.EpochValidationIsOnlyAvailableInDebug => "Epoch validation is only available in Debug.",
            FatalJitErrorCode.BasicBlockLIRMembershipChecksAreOnlyAvailableInDebugBuilds => "Basic block LIR membership checks are only available in Debug builds.",
            FatalJitErrorCode.NativeSizeofBasicBlockIsNotPorted => "Native sizeof(BasicBlock) is not ported.",
            FatalJitErrorCode.BasicBlockDumpsAreOnlyAvailableInDebugBuilds => "Basic block dumps are only available in Debug builds.",
            FatalJitErrorCode.EdgeLikelihoodTrackingIsOnlyAvailableInDebugBuilds => "Edge likelihood tracking is only available in Debug builds.",
            FatalJitErrorCode.PredecessorShufflingIsOnlyAvailableInDebugBuilds => "Predecessor shuffling is only available in Debug builds.",
            FatalJitErrorCode.SSAStressHashingIsOnlyAvailableInDebugBuilds => "SSA stress hashing is only available in Debug builds.",
            FatalJitErrorCode.InstructionMetadataIsNotAvailableForTheConfiguredTarget => "Instruction metadata is not available for the configured target.",
            FatalJitErrorCode.WasmSIMDMetadataIsNotAvailableForTheConfiguredTarget => "Wasm SIMD metadata is not available for the configured target.",
            FatalJitErrorCode.InstructionInputSizeMetadataRequiresXarch => "Instruction input size metadata requires xarch.",
            FatalJitErrorCode.InstructionArgumentHasNoInputSize => "Instruction {0} has no input size.",
            FatalJitErrorCode.RegisterMasksAreUnavailableForThisTarget => "Register masks are unavailable for this target.",
            FatalJitErrorCode.WriteBarrierDiagnosticStateIsOnlyAvailableInDebugBuilds => "Write-barrier diagnostic state is only available in Debug builds.",
            FatalJitErrorCode.RegisterZeroingRequiresAFixedRegisterSet => "Register zeroing requires a fixed register set.",
            FatalJitErrorCode.RegisterZeroingIsNotImplementedForThisTarget => "Register zeroing is not implemented for this target.",
            FatalJitErrorCode.CodeGenGenGetGSCookieTempRegsIsNotSupportedForThisTarget => "CodeGen.genGetGSCookieTempRegs is not supported for this target.",
            FatalJitErrorCode.CodeGenerationDiagnosticStateIsOnlyAvailableInDebugBuilds => "Code-generation diagnostic state is only available in Debug builds.",
            FatalJitErrorCode.MachineCodeGenerationRequiresWindowsAMD64 => "Machine-code generation requires Windows AMD64.",
            FatalJitErrorCode.WasmFuncletValidationRequiresTheWasmTarget => "Wasm funclet validation requires the Wasm target.",
            FatalJitErrorCode.WasmReadyToRunRangeValidationRequiresTheWasmTarget => "Wasm ReadyToRun range validation requires the Wasm target.",
            FatalJitErrorCode.FramePointerRegisterIsNotAvailableForTheConfiguredTarget => "Frame-pointer register is not available for the configured target.",
            FatalJitErrorCode.StackPointerRegisterIsNotAvailableForTheConfiguredTarget => "Stack-pointer register is not available for the configured target.",
            FatalJitErrorCode.APXCalleeSavedRegisterPopsAreOnlySupportedOnAMD64 => "APX callee-saved register pops are only supported on AMD64.",
            FatalJitErrorCode.ARM32ImmediateMaterializationRequiresARM32 => "ARM32 immediate materialization requires ARM32.",
            FatalJitErrorCode.ARM32SaturatingIncrementCodeGenerationIsNotPorted => "ARM32 saturating increment code generation is not ported.",
            FatalJitErrorCode.ARM64PrologPointerAuthenticationIsNotPorted => "ARM64 prolog pointer authentication is not ported.",
            FatalJitErrorCode.ARM64EpilogPointerAuthenticationIsNotPorted => "ARM64 epilog pointer authentication is not ported.",
            FatalJitErrorCode.SIMDConstantsAreNotSupportedByThisARM64Configuration => "SIMD constants are not supported by this ARM64 configuration.",
            FatalJitErrorCode.MaskedSIMDConstantsAreNotSupportedByThisARM64Configuration => "Masked SIMD constants are not supported by this ARM64 configuration.",
            FatalJitErrorCode.ARM64FramePolicyIsNotSupportedForThisTarget => "ARM64 frame policy is not supported for this target.",
            FatalJitErrorCode.ARM64SIMD12IndirectLoadCodeGenerationRequiresSIMDEnabledARM64 => "ARM64 SIMD12 indirect-load code generation requires SIMD-enabled ARM64.",
            FatalJitErrorCode.ARM64SIMD12IndirectStoresAreUnavailableForThisTarget => "ARM64 SIMD12 indirect stores are unavailable for this target.",
            FatalJitErrorCode.ReservedARM64CodeGenerationRegisterRequiresARM64 => "The reserved ARM64 code-generation register requires ARM64.",
            FatalJitErrorCode.JumpCompareIsMissingItsSecondOperand => "Jump compare is missing its second operand.",
            FatalJitErrorCode.ARM64BlockRelativeAddressRecordingIsNotPorted => "ARM64 block-relative address recording is not ported.",
            FatalJitErrorCode.ARM64SIMDUpperLaneSavesAreUnavailableForThisTarget => "ARM64 SIMD upper-lane saves are unavailable for this target.",
            FatalJitErrorCode.ARM64SIMDUpperLaneRestoresAreUnavailableForThisTarget => "ARM64 SIMD upper-lane restores are unavailable for this target.",
            FatalJitErrorCode.MissingEpilogNode => "Missing epilog node.",
            FatalJitErrorCode.UnsupportedJMPIndirection => "Unsupported JMP indirection.",
            FatalJitErrorCode.ARMArchitectureIndirectLoadsAreNotSupportedForThisTarget => "ARM architecture indirect loads are not supported for this target.",
            FatalJitErrorCode.NativeLocalAddressGenerationIsUnavailableForThisTarget => "Native local-address generation is unavailable for this target.",
            FatalJitErrorCode.ARMCodeGenerationWasRequestedForANonARMTarget => "ARM code generation was requested for a non-ARM target.",
            FatalJitErrorCode.SIMDConstantsAreNotSupportedByThisARMConfiguration => "SIMD constants are not supported by this ARM configuration.",
            FatalJitErrorCode.MaskedSIMDConstantsAreNotSupportedByThisARMConfiguration => "Masked SIMD constants are not supported by this ARM configuration.",
            FatalJitErrorCode.SwiftErrorReturnsAreNotSupportedByThisARMConfiguration => "Swift error returns are not supported by this ARM configuration.",
            FatalJitErrorCode.HardwareIntrinsicsAreNotSupportedByThisARMConfiguration => "Hardware intrinsics are not supported by this ARM configuration.",
            FatalJitErrorCode.SwiftErrorValuesAreNotSupportedByThisARMConfiguration => "Swift error values are not supported by this ARM configuration.",
            FatalJitErrorCode.ARM64CodeGenerationForArgumentIsNotSupportedOnThisTarget => "ARM64 code generation for {0} is not supported on this target.",
            FatalJitErrorCode.ARM32CodeGenerationForArgumentIsNotSupportedOnThisTarget => "ARM32 code generation for {0} is not supported on this target.",
            FatalJitErrorCode.GTNULLCHECKIsNotSupportedForARM32UseGTIND => "GT_NULLCHECK is not supported for ARM32; use GT_IND.",
            FatalJitErrorCode.UnknownSizeFrameGenerationRequiresSIMDEnabledARM64 => "Unknown-size frame generation requires SIMD-enabled ARM64.",
            FatalJitErrorCode.UnknownSizeFrameInitializationRequiresSIMDEnabledARM64 => "Unknown-size frame initialization requires SIMD-enabled ARM64.",
            FatalJitErrorCode.WasmThrowHelperGenerationUsesASeparateTargetPath => "Wasm throw-helper generation uses a separate target path.",
            FatalJitErrorCode.OverflowCheckingUsesASeparateTargetPath => "Overflow checking uses a separate target path.",
            FatalJitErrorCode.OverflowCheckingIsNotImplementedForThisTarget => "Overflow checking is not implemented for this target.",
            FatalJitErrorCode.JumpInstructionGenerationIsNotImplementedForThisTarget => "Jump instruction generation is not implemented for this target.",
            FatalJitErrorCode.HelperCallGenerationIsNotImplementedForThisTarget => "Helper call generation is not implemented for this target.",
            FatalJitErrorCode.AsyncResumeAddressGenerationRequiresWindowsAMD64 => "Async resume address generation requires Windows AMD64.",
            FatalJitErrorCode.SIMDReturnValuesAreNotSupportedOnThisWasmTarget => "SIMD return values are not supported on this Wasm target.",
            FatalJitErrorCode.NonlocalJumpGenerationIsUnsupportedForThisTarget => "Nonlocal jump generation is unsupported for this target.",
            FatalJitErrorCode.FunctionEntryAddressGenerationIsUnsupportedForThisTarget => "Function-entry address generation is unsupported for this target.",
            FatalJitErrorCode.PatchpointGenerationIsUnsupportedOnWasm => "Patchpoint generation is unsupported on Wasm.",
            FatalJitErrorCode.PatchpointTransferIsUnsupportedForThisTarget => "Patchpoint transfer is unsupported for this target.",
            FatalJitErrorCode.LockedAdditionGenerationOutsideXarchIsNotImplemented => "Locked addition generation outside xarch is not implemented.",
            FatalJitErrorCode.UnimplementedOnLOONGARCH64Yet => "unimplemented on LOONGARCH64 yet",
            FatalJitErrorCode.UnexpectedOperationInLockedInstructionGeneration => "Unexpected operation in locked instruction generation.",
            FatalJitErrorCode.LockedInstructionGenerationOutsideXarchIsNotImplemented => "Locked instruction generation outside xarch is not implemented.",
            FatalJitErrorCode.CompareExchangeGenerationOutsideXarchIsNotImplemented => "Compare-exchange generation outside xarch is not implemented.",
            FatalJitErrorCode.MemoryBarrierGenerationOutsideSupportedTargetsIsNotImplemented => "Memory barrier generation outside supported targets is not implemented.",
            FatalJitErrorCode.WasmRegisterBitcastGenerationIsNotPorted => "Wasm register bitcast generation is not ported.",
            FatalJitErrorCode.BlockMemoryGenerationIsNotImplementedForThisTarget => "Block memory generation is not implemented for this target.",
            FatalJitErrorCode.UnrolledMemmoveIsNotImplementedForThisTarget => "Unrolled memmove is not implemented for this target.",
            FatalJitErrorCode.CallRegisterArgumentPlacementRequiresANativeTarget => "Call register argument placement requires a native target.",
            FatalJitErrorCode.UnixAMD64VarargsAreUnsupported => "Unix AMD64 varargs are unsupported.",
            FatalJitErrorCode.CallGenerationIsNotImplementedForThisTarget => "Call generation is not implemented for this target.",
            FatalJitErrorCode.WasmTailcallEpilogRegisterVerificationIsNotPorted => "Wasm tailcall epilog-register verification is not ported.",
            FatalJitErrorCode.CalleeSavePushesRequireXarch => "Callee-save pushes require xarch.",
            FatalJitErrorCode.APXCalleeSavePushesRequireAMD64 => "APX callee-save pushes require AMD64.",
            FatalJitErrorCode.FloatingCalleeSaveRecordingRequiresXarch => "Floating callee-save recording requires xarch.",
            FatalJitErrorCode.FloatingCalleeSaveRestorationRequiresXarch => "Floating callee-save restoration requires xarch.",
            FatalJitErrorCode.EVEXConditionalCompareMappingRequiresAMD64 => "EVEX conditional-compare mapping requires AMD64.",
            FatalJitErrorCode.EVEXConditionalCompareOptionsRequireAMD64 => "EVEX conditional-compare options require AMD64.",
            FatalJitErrorCode.ConditionalCompareIsOnlySupportedOnAMD64AndARM64 => "Conditional compare is only supported on AMD64 and ARM64.",
            FatalJitErrorCode.RegisterCopyGenerationIsNotSupportedOnWasm => "Register copy generation is not supported on Wasm.",
            FatalJitErrorCode.IndexedRegisterCopiesAreNotSupportedOnWasm => "Indexed register copies are not supported on Wasm.",
            FatalJitErrorCode.FinallyCallGenerationIsNotSupportedByThisDispatcher => "Finally-call generation is not supported by this dispatcher.",
            FatalJitErrorCode.CatchReturnGenerationIsNotSupportedByThisDispatcher => "Catch-return generation is not supported by this dispatcher.",
            FatalJitErrorCode.AMD64EmitterTestsRequireAnAMD64Target => "AMD64 emitter tests require an AMD64 target.",
            FatalJitErrorCode.CalleeSaveRestorationIsNotSupportedByThisDispatcher => "Callee-save restoration is not supported by this dispatcher.",
            FatalJitErrorCode.RootEpilogGenerationIsNotSupportedByThisDispatcher => "Root epilog generation is not supported by this dispatcher.",
            FatalJitErrorCode.InstGenReturnIsNotUsedOnARM64 => "instGen_Return is not used on ARM64.",
            FatalJitErrorCode.ReturnInstructionGenerationIsNotImplementedOnThisTarget => "Return instruction generation is not implemented on this target.",
            FatalJitErrorCode.X87FloatingPointReturnGenerationRequiresX86 => "x87 floating-point return generation requires X86.",
            FatalJitErrorCode.FloatingCastsOutsideXarchAreNotImplemented => "Floating casts outside xarch are not implemented.",
            FatalJitErrorCode.InvalidIntegralToFloatingCastTarget => "Invalid integral-to-floating cast target.",
            FatalJitErrorCode.IntegralToFloatingCastsOutsideXarchAreNotImplemented => "Integral-to-floating casts outside xarch are not implemented.",
            FatalJitErrorCode.ParameterHomingRegisterSelectionRequiresAMD64 => "Parameter homing register selection requires AMD64.",
            FatalJitErrorCode.PrespilledUnmappedRegistersAreOnlySupportedOnARM => "Prespilled unmapped registers are only supported on ARM.",
            FatalJitErrorCode.UnwindRegionImmediateMaterializationIsNotImplementedForARM => "Unwind-region immediate materialization is not implemented for ARM.",
            FatalJitErrorCode.ImmediateMaterializationIsNotImplementedForThisTarget => "Immediate materialization is not implemented for this target.",
            FatalJitErrorCode.InstructionFlagsForImmediateMaterializationAreOnlySupportedOnARM => "Instruction flags for immediate materialization are only supported on ARM.",
            FatalJitErrorCode.FramePoisoningIsNotImplementedForWasm => "Frame poisoning is not implemented for Wasm.",
            FatalJitErrorCode.UnknownSizeFramePoisoningRequiresARM64 => "Unknown-size frame poisoning requires ARM64.",
            FatalJitErrorCode.FuncletPrologsRequireXarch => "Funclet prologs require xarch.",
            FatalJitErrorCode.FuncletEpilogsRequireXarch => "Funclet epilogs require xarch.",
            FatalJitErrorCode.FuncletFrameCaptureRequiresXarch => "Funclet frame capture requires xarch.",
            FatalJitErrorCode.JIT32GCEncoderRequiresX86 => "The JIT32 GC encoder requires X86.",
            FatalJitErrorCode.GCInfoPublicationIsUnsupportedForThisTarget => "GC-info publication is unsupported for this target.",
            FatalJitErrorCode.GCInfoPublicationRequiresAMD64 => "GC-info publication requires AMD64.",
            FatalJitErrorCode.GSCookieChecksAreNotPortedForThisTarget => "GS-cookie checks are not ported for this target.",
            FatalJitErrorCode.GSCookieInitializationIsNotPortedForThisTarget => "GS-cookie initialization is not ported for this target.",
            FatalJitErrorCode.LateDisassemblyRequiresANativeCallbackBridgeToPreserveDecoderErrorOutput => "Late disassembly requires a native callback bridge to preserve decoder-error output.",
            FatalJitErrorCode.ARMArchitectureIntrinsicGenerationIsUnavailableForThisTarget => "ARM architecture intrinsic generation is unavailable for this target.",
            FatalJitErrorCode.CeilingIntrinsicGenerationRequiresARM64 => "Ceiling intrinsic generation requires ARM64.",
            FatalJitErrorCode.FloorIntrinsicGenerationRequiresARM64 => "Floor intrinsic generation requires ARM64.",
            FatalJitErrorCode.TruncateIntrinsicGenerationRequiresARM64 => "Truncate intrinsic generation requires ARM64.",
            FatalJitErrorCode.RoundIntrinsicGenerationRequiresARM64 => "Round intrinsic generation requires ARM64.",
            FatalJitErrorCode.PopCountIntrinsicGenerationRequiresARM64 => "PopCount intrinsic generation requires ARM64.",
            FatalJitErrorCode.TrailingZeroCountIntrinsicGenerationRequiresARM64 => "TrailingZeroCount intrinsic generation requires ARM64.",
            FatalJitErrorCode.SaturateToInt8IntrinsicGenerationRequiresARM => "SaturateToInt8 intrinsic generation requires ARM.",
            FatalJitErrorCode.SaturateToInt16IntrinsicGenerationRequiresARM => "SaturateToInt16 intrinsic generation requires ARM.",
            FatalJitErrorCode.SaturateToUInt8IntrinsicGenerationRequiresARM => "SaturateToUInt8 intrinsic generation requires ARM.",
            FatalJitErrorCode.SaturateToUInt16IntrinsicGenerationRequiresARM => "SaturateToUInt16 intrinsic generation requires ARM.",
            FatalJitErrorCode.SIMDUpperRegisterRestorationIsUnsupported => "SIMD upper-register restoration is unsupported.",
            FatalJitErrorCode.SIMDUpperRegisterSavingIsUnsupported => "SIMD upper-register saving is unsupported.",
            FatalJitErrorCode.JMPArgumentPlacementRequiresANativeTarget => "JMP argument placement requires a native target.",
            FatalJitErrorCode.SysVAMD64JMPVarargsAreNotSupported => "SysV AMD64 JMP varargs are not supported.",
            FatalJitErrorCode.JMPVarargsPlacementIsNotYetPortedForThisTarget => "JMP varargs placement is not yet ported for this target.",
            FatalJitErrorCode.JumpTableEmissionIsNotSupportedOnWasm => "Jump table emission is not supported on Wasm.",
            FatalJitErrorCode.StackPointerAdjustmentWithProbingRequiresXarch => "Stack-pointer adjustment with probing requires xarch.",
            FatalJitErrorCode.LocalHeapGenerationUsesASeparateWasmPath => "Local heap generation uses a separate Wasm path.",
            FatalJitErrorCode.LocalHeapGenerationIsNotPortedForThisTarget => "Local heap generation is not ported for this target.",
            FatalJitErrorCode.LongUnsignedRemainderGenerationRequiresX86 => "Long unsigned remainder generation requires X86.",
            FatalJitErrorCode.LongToIntCastGenerationRequiresX86 => "Long-to-int cast generation requires X86.",
            FatalJitErrorCode.RawHexOutputFilePathIsInvalid => "Raw-hex output file path is invalid.",
            FatalJitErrorCode.RegisterLocalStoresAreNotSupportedOnWasm => "Register-local stores are not supported on Wasm.",
            FatalJitErrorCode.MultiRegisterSIMDLocalStoresRequireXarchSIMDSupport => "Multi-register SIMD local stores require xarch SIMD support.",
            FatalJitErrorCode.MultiRegisterSIMDLocalStoresAreUnsupportedForThisXarchABI => "Multi-register SIMD local stores are unsupported for this xarch ABI.",
            FatalJitErrorCode.NodeInstructionGenerationRequiresXarch => "Node instruction generation requires xarch.",
            FatalJitErrorCode.RISCVOnlyNodeReachedNonRISCVCodeGeneration => "RISC-V-only node reached non-RISC-V code generation.",
            FatalJitErrorCode.RISCVRotateNodeReachedNonRISCVCodeGeneration => "RISC-V rotate node reached non-RISC-V code generation.",
            FatalJitErrorCode.HardwareIntrinsicsAreUnavailableForThisTarget => "Hardware intrinsics are unavailable for this target.",
            FatalJitErrorCode.LoongArchConditionalBranchReachedAnotherTarget => "LoongArch conditional branch reached another target.",
            FatalJitErrorCode.RISCVSelectNodeReachedAnotherTarget => "RISC-V select node reached another target.",
            FatalJitErrorCode.RISCVAtomicNodeReachedAnotherTarget => "RISC-V atomic node reached another target.",
            FatalJitErrorCode.RISCVArithmeticNodeReachedAnotherTarget => "RISC-V arithmetic node reached another target.",
            FatalJitErrorCode.UnimplementedNodeTypeInCodeGeneration => "Unimplemented node type in code generation.",
            FatalJitErrorCode.SIMDConstantsAreUnavailableForThisTarget => "SIMD constants are unavailable for this target.",
            FatalJitErrorCode.MaskedConstantsAreUnavailableForThisTarget => "Masked constants are unavailable for this target.",
            FatalJitErrorCode.AMD64ConditionalCompareReachedAnotherTarget => "AMD64 conditional compare reached another target.",
            FatalJitErrorCode.SwiftErrorRegistersAreUnavailableForThisTarget => "Swift error registers are unavailable for this target.",
            FatalJitErrorCode.OSRCalleeSaveRestorationIsUnsupportedForThisTarget => "OSR callee-save restoration is unsupported for this target.",
            FatalJitErrorCode.OSRCalleeSaveRecordingRequiresAMD64 => "OSR callee-save recording requires AMD64.",
            FatalJitErrorCode.OSRLocalHomingIsNotPortedForThisTarget => "OSR local homing is not ported for this target.",
            FatalJitErrorCode.ProfilerLeaveCallbacksAreUnsupportedForThisTarget => "Profiler leave callbacks are unsupported for this target.",
            FatalJitErrorCode.ProfilerEnterCallbacksAreUnsupportedForThisTarget => "Profiler enter callbacks are unsupported for this target.",
            FatalJitErrorCode.UnixNativeAOTPrologsRequireCFIUnwindSupport => "Unix NativeAOT prologs require CFI unwind support.",
            FatalJitErrorCode.AlignedSIMDStoresRequireAMD64 => "Aligned SIMD stores require AMD64.",
            FatalJitErrorCode.BlockBasedPrologInitializationIsNotSupportedOnWasm => "Block-based prolog initialization is not supported on Wasm.",
            FatalJitErrorCode.PrologBlockInitializationRequiresASupportedTarget => "Prolog block initialization requires a supported target.",
            FatalJitErrorCode.ARM32StackAllocationRegisterSelectionIsNotSupportedOnThisTarget => "ARM32 stack-allocation register selection is not supported on this target.",
            FatalJitErrorCode.FramePointerEstablishmentIsNotPortedForThisTarget => "Frame pointer establishment is not ported for this target.",
            FatalJitErrorCode.LocalFrameAllocationIsNotPortedForThisTarget => "Local frame allocation is not ported for this target.",
            FatalJitErrorCode.PrologAVXStateClearingRequiresXarch => "Prolog AVX-state clearing requires xarch.",
            FatalJitErrorCode.EpilogAVXStateClearingRequiresXarch => "Epilog AVX-state clearing requires xarch.",
            FatalJitErrorCode.WasmHasNoArchitecturalIntegerZeroRegister => "Wasm has no architectural integer zero register.",
            FatalJitErrorCode.WasmHasNoArchitecturalFloatingPointRegisters => "Wasm has no architectural floating-point registers.",
            FatalJitErrorCode.FloatingRegisterZeroingIsNotImplementedForThisTarget => "Floating-register zeroing is not implemented for this target.",
            FatalJitErrorCode.ARM64FloatingRegisterZeroingIsUnavailable => "ARM64 floating-register zeroing is unavailable.",
            FatalJitErrorCode.WasmFrameInitializationIsUnavailableForThisTarget => "Wasm frame initialization is unavailable for this target.",
            FatalJitErrorCode.WasmGenericContextReportingIsUnavailableForThisTarget => "Wasm generic-context reporting is unavailable for this target.",
            FatalJitErrorCode.ARM32GenericContextStorageIsUnavailable => "ARM32 generic-context storage is unavailable.",
            FatalJitErrorCode.ARM64GenericContextStorageIsUnavailable => "ARM64 generic-context storage is unavailable.",
            FatalJitErrorCode.LoongArch64GenericContextStorageIsUnavailable => "LoongArch64 generic-context storage is unavailable.",
            FatalJitErrorCode.RISCV64GenericContextStorageIsUnavailable => "RISC-V64 generic-context storage is unavailable.",
            FatalJitErrorCode.NativeGenericContextLoadingIsUnavailableOnWasm => "Native generic-context loading is unavailable on Wasm.",
            FatalJitErrorCode.XarchGenericContextStorageIsUnavailable => "xarch generic-context storage is unavailable.",
            FatalJitErrorCode.ARM32ProfilingPreSpillInformationIsUnavailable => "ARM32 profiling pre-spill information is unavailable.",
            FatalJitErrorCode.GenericContextReportingIsNotImplementedForThisTarget => "Generic-context reporting is not implemented for this target.",
            FatalJitErrorCode.RegisterArgumentGenerationIsNotPortedForThisTarget => "Register argument generation is not ported for this target.",
            FatalJitErrorCode.ReturnTrapGenerationIsNotPortedForThisTarget => "Return-trap generation is not ported for this target.",
            FatalJitErrorCode.LongReturnGenerationIsOnlySupportedOnX86AndARM => "Long return generation is only supported on X86 and ARM.",
            FatalJitErrorCode.ReturnGCInformationRequiresAFixedRegisterTarget => "Return GC information requires a fixed-register target.",
            FatalJitErrorCode.SimpleReturnGenerationIsNotPortedForThisTarget => "Simple return generation is not ported for this target.",
            FatalJitErrorCode.ClearingTheWasmAsyncContinuationIsOnlySupportedOnWasm => "Clearing the Wasm async continuation is only supported on Wasm.",
            FatalJitErrorCode.SIMDSplitReturnsRequireSIMDSupportOnXarchOrARM => "SIMD split returns require SIMD support on xarch or ARM.",
            FatalJitErrorCode.SIMDSplitReturnsAreNotPortedForThisTarget => "SIMD split returns are not ported for this target.",
            FatalJitErrorCode.SwiftErrorReturnsAreNotSupportedOnThisTarget => "Swift error returns are not supported on this target.",
            FatalJitErrorCode.StackPointerCheckingIsNotPortedForThisTarget => "Stack-pointer checking is not ported for this target.",
            FatalJitErrorCode.RichDebugInfoFilePathIsInvalid => "Rich debug-info file path is invalid.",
            FatalJitErrorCode.SaturatingIncrementGenerationIsNotImplementedForThisTarget => "Saturating increment generation is not implemented for this target.",
            FatalJitErrorCode.BitModificationGenerationRequiresXarch => "Bit modification generation requires xarch.",
            FatalJitErrorCode.PhysicalRegisterGenerationIsNotImplementedForThisTarget => "Physical register generation is not implemented for this target.",
            FatalJitErrorCode.RegisterSpillsAreNotSupportedOnWasm => "Register spills are not supported on Wasm.",
            FatalJitErrorCode.RegisterReloadsAreNotSupportedOnWasm => "Register reloads are not supported on Wasm.",
            FatalJitErrorCode.ARM32CannotPlaceThisContainedStackArgument => "ARM32 cannot place this contained stack argument.",
            FatalJitErrorCode.ARMStructStackArgumentLocalHasNoLayout => "ARM struct stack argument local has no layout.",
            FatalJitErrorCode.SIMDInGenPutArgStkUnimplementedUnusedOnRISCV64Yet => "SIMD in genPutArgStk-----unimplemented/unused on RISCV64 yet----",
            FatalJitErrorCode.RISCVStructStackArgumentLocalHasNoLayout => "RISC-V struct stack argument local has no layout.",
            FatalJitErrorCode.StackArgumentGenerationRequiresXarch => "Stack argument generation requires xarch.",
            FatalJitErrorCode.StructStackArgumentGenerationRequiresXarch => "Struct stack argument generation requires xarch.",
            FatalJitErrorCode.PartialREPStructCopiesAreUnavailableOnX86 => "Partial REP struct copies are unavailable on x86.",
            FatalJitErrorCode.FirstIncomingStackArgumentIsNotImplementedForX86 => "The first incoming stack argument is not implemented for x86.",
            FatalJitErrorCode.IncomingStackArgumentSelectionIsNotYetPortedForThisTarget => "Incoming stack argument selection is not yet ported for this target.",
            FatalJitErrorCode.X86StackArgumentBasesRequireFixedOutgoingArguments => "x86 stack argument bases require fixed outgoing arguments.",
            FatalJitErrorCode.StackArgumentBaseSelectionRequiresXarch => "Stack argument base selection requires xarch.",
            FatalJitErrorCode.X86StackArgumentConstantMaterializationIsUnavailable => "x86 stack-argument constant materialization is unavailable.",
            FatalJitErrorCode.SIMD12StackArgumentsAreUnavailableForThisTarget => "SIMD12 stack arguments are unavailable for this target.",
            FatalJitErrorCode.StackLevelAdjustmentIsNotSupportedOnWasm => "Stack-level adjustment is not supported on Wasm.",
            FatalJitErrorCode.StackLevelMarkingIsNotAvailableForThisTarget => "Stack-level marking is not available for this target.",
            FatalJitErrorCode.StackSegmentHomingIsNotSupportedForThisTarget => "Stack-segment homing is not supported for this target.",
            FatalJitErrorCode.StackVectorArgumentUpperBitClearingRequiresSysVAMD64SIMD => "Stack vector argument upper-bit clearing requires SysV AMD64 SIMD.",
            FatalJitErrorCode.SwiftStackParameterHomingIsNotSupportedOnThisTarget => "Swift stack parameter homing is not supported on this target.",
            FatalJitErrorCode.ArgumentIsNotImplementedForTheActiveTarget => "{0} is not implemented for the active target.",
            FatalJitErrorCode.StructLocalOnTheFrameMustHaveALayout => "A struct local on the frame must have a layout.",
            FatalJitErrorCode.JIT32GCEncoderLookupTableHasNotBeenInitialized => "JIT32 GC encoder lookup table has not been initialized.",
            FatalJitErrorCode.PendingArgumentStackIsMissingItsOverflowStorage => "The pending-argument stack is missing its overflow storage.",
            FatalJitErrorCode.JIT32GCCallArgumentTableIsMissing => "JIT32 GC call argument table is missing.",
            FatalJitErrorCode.ConditionInstructionMappingIsNotPortedForThisTarget => "Condition instruction mapping is not ported for this target.",
            FatalJitErrorCode.RegisterMasksAreNotAvailableForTargetsWithoutAFixedRegisterSet => "Register masks are not available for targets without a fixed register set.",
            FatalJitErrorCode.TooManyInternalRegistersWereAddedToANode => "Too many internal registers were added to a node.",
            FatalJitErrorCode.InternalRegisterIndexIsOutOfRange => "The internal register index is out of range.",
            FatalJitErrorCode.NoInternalRegisterIsAvailableToExtract => "No internal register is available to extract.",
            FatalJitErrorCode.ExactlyOneInternalRegisterWasExpected => "Exactly one internal register was expected.",
            FatalJitErrorCode.InternalRegistersAreMissingForTheSpecifiedNode => "Internal registers are missing for the specified node.",
            FatalJitErrorCode.RegisterMaskTrackingIsNotAvailableForThisTarget => "Register-mask tracking is not available for this target.",
            FatalJitErrorCode.WebAssemblyRegisterTrackingIsNotAvailableForThisTarget => "WebAssembly register tracking is not available for this target.",
            FatalJitErrorCode.WebAssemblyRegisterTableIsNotInitialized => "The WebAssembly register table is not initialized.",
            FatalJitErrorCode.ARMConditionResultGenerationIsNotSupportedOnThisTarget => "ARM condition-result generation is not supported on this target.",
            FatalJitErrorCode.FloatingToIntegerCastsAreNotImplementedForThisTarget => "Floating-to-integer casts are not implemented for this target.",
            FatalJitErrorCode.IntegralToIntegralCastsAreNotImplementedForThisTarget => "Integral-to-integral casts are not implemented for this target.",
            FatalJitErrorCode.InvalidFloatingToIntegralCastSource => "Invalid floating-to-integral cast source.",
            FatalJitErrorCode.FloatingToIntegerCastGenerationOutsideARMIsNotPorted => "Floating-to-integer cast generation outside ARM is not ported.",
            FatalJitErrorCode.SplitLongCastGenerationIsOnlyImplementedFor32BitARM => "Split-long cast generation is only implemented for 32-bit ARM.",
            FatalJitErrorCode.FlagBranchGenerationHasNoNativeImplementationForThisTarget => "Flag branch generation has no native implementation for this target.",
            FatalJitErrorCode.ConditionalBranchSequencesHaveNoNativeImplementationForThisTarget => "Conditional branch sequences have no native implementation for this target.",
            FatalJitErrorCode.FlagResultGenerationHasNoNativeImplementationForThisTarget => "Flag-result generation has no native implementation for this target.",
            FatalJitErrorCode.OperandUseNumberingIsOnlyAvailableInDebugBuilds => "Operand-use numbering is only available in Debug builds.",
            FatalJitErrorCode.RegisterCopiesAreNotSupportedOnWasm => "Register copies are not supported on Wasm.",
            FatalJitErrorCode.IndexedRegisterConsumptionIsNotSupportedOnWasm => "Indexed register consumption is not supported on Wasm.",
            FatalJitErrorCode.SplitLongLocalStoresAreNotSupportedOnThisTarget => "Split-long local stores are not supported on this target.",
            FatalJitErrorCode.RegisterLocalSpillsAreNotSupportedOnWasm => "Register-local spills are not supported on Wasm.",
            FatalJitErrorCode.LocalVariableSpillsRequireAFixedRegisterSet => "Local-variable spills require a fixed register set.",
            FatalJitErrorCode.StructStackArgumentsAreNotSupportedOnWasm => "Struct stack arguments are not supported on Wasm.",
            FatalJitErrorCode.ThisStructFieldListStackArgumentPathIsNotSupportedOnThisTarget => "This struct field-list stack-argument path is not supported on this target.",
            FatalJitErrorCode.TargetSIMD12LocalStackStoreRecordingIsNotYetPorted => "Target SIMD12 local-stack store recording is not yet ported.",
            FatalJitErrorCode.TargetSIMD12LocalStackStoreRecordingIsOnlyImplementedForARM64 => "Target SIMD12 local-stack store recording is only implemented for ARM64.",
            FatalJitErrorCode.RegisterLocalReloadsAreNotSupportedOnWasm => "Register-local reloads are not supported on Wasm.",
            FatalJitErrorCode.IndexedRegisterReloadsAreNotSupportedOnWasm => "Indexed register reloads are not supported on Wasm.",
            FatalJitErrorCode.WebAssemblyRegisterCopySelectionIsNotImplemented => "WebAssembly register-copy selection is not implemented.",
            FatalJitErrorCode.UnsupportedTargetForTheExceptionObjectRegister => "Unsupported target for the exception-object register.",
            FatalJitErrorCode.UnsupportedTargetForTheExceptionObjectRegisterMask => "Unsupported target for the exception-object register mask.",
            FatalJitErrorCode.LoongArch64TernaryInstructionRecordingIsNotPorted => "LoongArch64 ternary instruction recording is not ported.",
            FatalJitErrorCode.ThisBlockStoreImplementationRequiresLoongArch64OrRISCV64 => "This block-store implementation requires LoongArch64 or RISC-V64.",
            FatalJitErrorCode.BlockCopyDestinationAddressModeRequiresABase => "A block-copy destination address mode requires a base.",
            FatalJitErrorCode.BlockCopySourceAddressModeRequiresABase => "A block-copy source address mode requires a base.",
            FatalJitErrorCode.LoongArch64MemoryBarrierGenerationRequiresLoongArch64 => "LoongArch64 memory barrier generation requires LoongArch64.",
            FatalJitErrorCode.LoongArch64VarargsAreNotSupported => "LoongArch64 varargs are not supported.",
            FatalJitErrorCode.VarargsAreNotSupportedOnLoongArch64 => "Varargs are not supported on LoongArch64.",
            FatalJitErrorCode.ImmediateMaterializationUsesASeparateTargetImplementation => "Immediate materialization uses a separate target implementation.",
            FatalJitErrorCode.LoongArch64SIMD12IndirectLoadsAreNotPorted => "LoongArch64 SIMD12 indirect loads are not ported.",
            FatalJitErrorCode.LoongArch64SIMD12IndirectStoresAreNotPorted => "LoongArch64 SIMD12 indirect stores are not ported.",
            FatalJitErrorCode.ThisLocalLoadImplementationRequiresLoongArch64OrRISCV64 => "This local-load implementation requires LoongArch64 or RISC-V64.",
            FatalJitErrorCode.LoongArch64SIMD12LocalStoreRecordingIsNotPorted => "LoongArch64 SIMD12 local-store recording is not ported.",
            FatalJitErrorCode.LoongArch64StructLocalFieldLoadsAreNotSupported => "LoongArch64 struct local-field loads are not supported.",
            FatalJitErrorCode.UnknownIntrinsicReachedUnreachableCode => "Unknown intrinsic reached unreachable code.",
            FatalJitErrorCode.HardwareIntrinsicGenerationIsNotPortedForThisTarget => "Hardware-intrinsic generation is not ported for this target.",
            FatalJitErrorCode.LoongArch64HardwareIntrinsicGenerationIsNotPorted => "LoongArch64 hardware-intrinsic generation is not ported.",
            FatalJitErrorCode.ZeroRegisterNullCheckGenerationRequiresLoongArch64OrRISCV64 => "Zero-register null-check generation requires LoongArch64 or RISC-V64.",
            FatalJitErrorCode.LoongArch64ProfilingCallbacksAreUnavailable => "LoongArch64 profiling callbacks are unavailable.",
            FatalJitErrorCode.LoongArch64GTSWAPGenerationIsNotPorted => "LoongArch64 GT_SWAP generation is not ported.",
            FatalJitErrorCode.LoongArch64SIMDIsUnavailableForThisConfiguration => "LoongArch64 SIMD is unavailable for this configuration.",
            FatalJitErrorCode.LoongArch64SIMDInstructionOptionsAreNotPorted => "LoongArch64 SIMD instruction options are not ported.",
            FatalJitErrorCode.LoongArch64IndirectLoadStoreRecordingRequiresLoongArch64 => "LoongArch64 indirect load/store recording requires LoongArch64.",
            FatalJitErrorCode.LoongArch64IndirectLoadStoreRecordingRequiresAnActiveCompiler => "LoongArch64 indirect load/store recording requires an active compiler.",
            FatalJitErrorCode.LoongArch64ContainedAddressIsMissingItsIndexTree => "LoongArch64 contained address is missing its index tree.",
            FatalJitErrorCode.LoongArch64IndexedAddressIsMissingItsBaseTree => "LoongArch64 indexed address is missing its base tree.",
            FatalJitErrorCode.UnsupportedLoongArch64IndirectLoadStoreSize => "Unsupported LoongArch64 indirect load/store size.",
            FatalJitErrorCode.LoongArch64ContainedAddressIsMissingItsBaseTree => "LoongArch64 contained address is missing its base tree.",
            FatalJitErrorCode.GTRORUnimplementedUnusedOnRISCV64Yet => "GT_ROR-----unimplemented/unused on RISCV64 yet----",
            FatalJitErrorCode.RISCVTernaryInstructionRecordingIsNotPorted => "RISC-V ternary instruction recording is not ported.",
            FatalJitErrorCode.RISCV64SIMD12IndirectLoadsAreNotPorted => "RISC-V64 SIMD12 indirect loads are not ported.",
            FatalJitErrorCode.RISCV64SIMD12IndirectStoresAreNotPorted => "RISC-V64 SIMD12 indirect stores are not ported.",
            FatalJitErrorCode.RISCV64StructLocalFieldLoadsAreNotSupported => "RISC-V64 struct local-field loads are not supported.",
            FatalJitErrorCode.RISCV64MultiRegisterLocalStoresAreNotPorted => "RISC-V64 multi-register local stores are not ported.",
            FatalJitErrorCode.RISCV64RelocatableImmediateInstructionRecordingIsNotPorted => "RISC-V64 relocatable immediate instruction recording is not ported.",
            FatalJitErrorCode.RISCVSIMD12LocalStoreRecordingIsNotPorted => "RISC-V SIMD12 local-store recording is not ported.",
            FatalJitErrorCode.RISCV64GTSWAPGenerationIsNotPorted => "RISC-V64 GT_SWAP generation is not ported.",
            FatalJitErrorCode.RISCV64SIMDIntrinsicGenerationIsNotPorted => "RISC-V64 SIMD intrinsic generation is not ported.",
            FatalJitErrorCode.RISCV64SIMDUpperLaneSavingIsNotPorted => "RISC-V64 SIMD upper-lane saving is not ported.",
            FatalJitErrorCode.RISCV64SIMDUpperLaneRestorationIsNotPorted => "RISC-V64 SIMD upper-lane restoration is not ported.",
            FatalJitErrorCode.RISCV64SIMD12LocalStoresAreNotPorted => "RISC-V64 SIMD12 local stores are not ported.",
            FatalJitErrorCode.RISCV64SIMDInstructionOptionsAreNotPorted => "RISC-V64 SIMD instruction options are not ported.",
            FatalJitErrorCode.RISCV64SIMDInstructionSelectionIsNotPorted => "RISC-V64 SIMD instruction selection is not ported.",
            FatalJitErrorCode.RISCV64SIMDInitializationIsNotPorted => "RISC-V64 SIMD initialization is not ported.",
            FatalJitErrorCode.RISCV64SIMDMultiOperandInitializationIsNotPorted => "RISC-V64 SIMD multi-operand initialization is not ported.",
            FatalJitErrorCode.RISCV64UnarySIMDIntrinsicGenerationIsNotPorted => "RISC-V64 unary SIMD intrinsic generation is not ported.",
            FatalJitErrorCode.RISCV64SIMDWideningIsNotPorted => "RISC-V64 SIMD widening is not ported.",
            FatalJitErrorCode.RISCV64SIMDNarrowingIsNotPorted => "RISC-V64 SIMD narrowing is not ported.",
            FatalJitErrorCode.RISCV64BinarySIMDIntrinsicGenerationIsNotPorted => "RISC-V64 binary SIMD intrinsic generation is not ported.",
            FatalJitErrorCode.RISCV64SIMDGetItemIntrinsicGenerationIsNotPorted => "RISC-V64 SIMD get-item intrinsic generation is not ported.",
            FatalJitErrorCode.RISCV64SIMDSetItemIntrinsicGenerationIsNotPorted => "RISC-V64 SIMD set-item intrinsic generation is not ported.",
            FatalJitErrorCode.RISCV64SIMDComparisonIsNotPorted => "RISC-V64 SIMD comparison is not ported.",
            FatalJitErrorCode.RISCV64DotProductSIMDIntrinsicGenerationIsNotPorted => "RISC-V64 dot-product SIMD intrinsic generation is not ported.",
            FatalJitErrorCode.WasmInternalRegisterTrackingIsNotPorted => "Wasm internal-register tracking is not ported.",
            FatalJitErrorCode.WasmInternalRegisterExtractionIsNotPorted => "Wasm internal-register extraction is not ported.",
            FatalJitErrorCode.WasmFinallyCallIsMissingItsReturnBlock => "Wasm finally call is missing its return block.",
            FatalJitErrorCode.WasmHardwareIntrinsicMemoryNodeHasNoAddress => "Wasm hardware intrinsic memory node has no address.",
            FatalJitErrorCode.UnsupportedWasmIntegerComparison => "Unsupported Wasm integer comparison.",
            FatalJitErrorCode.UnsupportedWasmFloatingPointComparison => "Unsupported Wasm floating-point comparison.",
            FatalJitErrorCode.GTINDBaseIsUnavailableForNullCheckCodegen => "GT_IND base is unavailable for null-check codegen.",
            FatalJitErrorCode.GTSTOREINDBaseIsUnavailableForNullCheckCodegen => "GT_STOREIND base is unavailable for null-check codegen.",
            FatalJitErrorCode.CallRequiringANullCheckHasNoThisArgument => "Call requiring a null check has no this argument.",
            FatalJitErrorCode.EarlyCallArgumentHasNoNode => "Early call argument has no node.",
            FatalJitErrorCode.LateCallArgumentHasNoNode => "Late call argument has no node.",
            FatalJitErrorCode.WasmHelperArgumentHasNoHardCodedSignature => "Wasm helper {0} has no hard-coded signature.",
            FatalJitErrorCode.InvalidWebAssemblyValueType => "Invalid WebAssembly value type.",
            FatalJitErrorCode.WasmArgumentIsNotPortedForArgument => "Wasm {0} is not ported for {1}.",
            FatalJitErrorCode.WasmArgumentIsNotPorted => "Wasm {0} is not ported.",
            FatalJitErrorCode.WasmLocalDeclarationsAreMissing => "Wasm local declarations are missing.",
            FatalJitErrorCode.UnsupportedWasmBitcast => "Unsupported Wasm bitcast.",
            FatalJitErrorCode.WasmFuncletLocalDeclarationsAreMissing => "Wasm funclet local declarations are missing.",
            FatalJitErrorCode.UnexpectedWasmValueTypeArgument => "Unexpected Wasm value type: {0}.",
            FatalJitErrorCode.SIMD12RegisterClearingRequiresXarchSIMDSupport => "SIMD12 register clearing requires xarch SIMD support.",
            FatalJitErrorCode.ComparisonFlagReuseRequiresXarch => "Comparison flag reuse requires xarch.",
            FatalJitErrorCode.BooleanConditionGenerationRequiresAMD64 => "Boolean condition generation requires AMD64.",
            FatalJitErrorCode.SIMDConstantsRequireSIMDSupport => "SIMD constants require SIMD support.",
            FatalJitErrorCode.MaskConstantsRequireMaskedHardwareIntrinsicSupport => "Mask constants require masked hardware-intrinsic support.",
            FatalJitErrorCode.CalleeSaveRestorationRequiresXarch => "Callee-save restoration requires xarch.",
            FatalJitErrorCode.RootEpilogGenerationRequiresXarch => "Root epilog generation requires xarch.",
            FatalJitErrorCode.IndirectHelperCallRequiresAnExplicitTargetRegisterOnThisArchitecture => "An indirect helper call requires an explicit target register on this architecture.",
            FatalJitErrorCode.AddressRelocationHintsRequireAMD64 => "Address relocation hints require AMD64.",
            FatalJitErrorCode.SIMD12IndirectLoadsRequireSIMDSupport => "SIMD12 indirect loads require SIMD support.",
            FatalJitErrorCode.FiniteCheckCodeGenerationRequiresXarch => "Finite-check code generation requires xarch.",
            FatalJitErrorCode.SIMD12LocalLoadsRequireSIMDSupport => "SIMD12 local loads require SIMD support.",
            FatalJitErrorCode.LocalStoreLifetimeUpdatesUseASeparateTargetImplementation => "Local-store lifetime updates use a separate target implementation.",
            FatalJitErrorCode.SIMD12LocalStoresRequireSIMDSupport => "SIMD12 local stores require SIMD support.",
            FatalJitErrorCode.ScalarOperationInstructionSelectionRequiresXarchOrARM => "Scalar operation instruction selection requires xarch or ARM.",
            FatalJitErrorCode.LongShiftGenerationRequiresX86 => "Long shift generation requires x86.",
            FatalJitErrorCode.AddCodeDscDumpIsOnlyAvailableInDebugBuilds => "AddCodeDsc.Dump is only available in Debug builds.",
            FatalJitErrorCode.ItemWithTheSameKeyHasAlreadyBeenAdded => "An item with the same key has already been added.",
            FatalJitErrorCode.VarargsRegisterMappingRequiresWindowsAMD64 => "Varargs register mapping requires Windows AMD64.",
            FatalJitErrorCode.ARM64HardwareIntrinsicSupportIsNotEnabled => "ARM64 hardware intrinsic support is not enabled.",
            FatalJitErrorCode.MDArrayMorphingRequiresTheWindowsAMD64Path => "MD-array morphing requires the Windows AMD64 path.",
            FatalJitErrorCode.SIMDAssertionsAreUnsupportedOnThisTarget => "SIMD assertions are unsupported on this target.",
            FatalJitErrorCode.AsyncAnalysisRequiresAnEntryBlock => "Async analysis requires an entry block",
            FatalJitErrorCode.AsyncAnalysisRequiresTrackedLocals => "Async analysis requires tracked locals",
            FatalJitErrorCode.AsyncCallResultHasNoUse => "Async call result has no use",
            FatalJitErrorCode.AsyncCallResultHasNoLIRUse => "Async call result has no LIR use",
            FatalJitErrorCode.AsyncCallResultHasNoLocalStore => "Async call result has no local store",
            FatalJitErrorCode.AsyncCallHasNoInsertionPoint => "Async call has no insertion point",
            FatalJitErrorCode.MorphedAsyncHelperLostItsArgumentPlaceholder => "Morphed async helper lost its argument placeholder",
            FatalJitErrorCode.UnregisteredInlineFrameContinuationMember => "Unregistered inline-frame continuation member",
            FatalJitErrorCode.MissingAsyncExecutionContext => "Missing async execution context",
            FatalJitErrorCode.MissingAsyncSynchronizationContext => "Missing async synchronization context",
            FatalJitErrorCode.MissingAsyncResumedIndicator => "Missing async resumed indicator",
            FatalJitErrorCode.AsyncAwaiterHasNoSignatureLayout => "Async awaiter has no signature layout",
            FatalJitErrorCode.AsyncAwaiterHasNoAllocatedContinuationMember => "Async awaiter has no allocated continuation member",
            FatalJitErrorCode.ImplicitByrefLocalHasNoLayout => "Implicit-byref local has no layout",
            FatalJitErrorCode.AsyncMethodHasNoEntryBlock => "Async method has no entry block",
            FatalJitErrorCode.AsyncLivenessHasNoTrackedLocalMapping => "Async liveness has no tracked-local mapping",
            FatalJitErrorCode.UnallocatedContinuationMemberOffset => "Unallocated continuation member offset",
            FatalJitErrorCode.ContinuationMemberOffsetMissingFromFinalLIR => "Continuation member offset missing from final LIR",
            FatalJitErrorCode.InlinedAsyncFrameContextChainIsInconsistent => "Inlined async frame context chain is inconsistent",
            FatalJitErrorCode.InlinedAsyncFrameIsMissingAContextArgument => "Inlined async frame is missing a context argument",
            FatalJitErrorCode.InlinedFrameTailRequiresAReturnBlock => "Inlined frame tail requires a return block",
            FatalJitErrorCode.InlineFrameTailMemberWasNeverRegistered => "Inline frame tail member was never registered",
            FatalJitErrorCode.UnrecognizedInlineFrameContextHandling => "Unrecognized inline-frame context handling",
            FatalJitErrorCode.OutstandingLIREdgeHasNoUse => "An outstanding LIR edge has no use",
            FatalJitErrorCode.AsyncResumedDefArgumentMustBeALocalAddress => "Async resumed-def argument must be a local address",
            FatalJitErrorCode.TerminatorBlockIsEmpty => "Terminator block is empty",
            FatalJitErrorCode.AsyncResultRequiresALocalDefinition => "Async result requires a local definition",
            FatalJitErrorCode.ReusableStateLacksItsReturnDefinition => "Reusable state lacks its return definition",
            FatalJitErrorCode.CommonResumedDefHasNoCallArgument => "Common resumed def has no call argument",
            FatalJitErrorCode.SharedContextHandlingRequiresAReturnBlock => "Shared context handling requires a return block",
            FatalJitErrorCode.AsyncStateHasNoResumedIndicator => "Async state has no resumed indicator",
            FatalJitErrorCode.AsyncStateHasNoExecutionContext => "Async state has no execution context",
            FatalJitErrorCode.AsyncStateHasNoSynchronizationContext => "Async state has no synchronization context",
            FatalJitErrorCode.NoAsyncStatesWereCreated => "No async states were created",
            FatalJitErrorCode.BlockWeightsRequireADFSTree => "Block weights require a DFS tree.",
            FatalJitErrorCode.BlockWeightsRequireNaturalLoops => "Block weights require natural loops.",
            FatalJitErrorCode.BlockWeightsRequireAnEntryBlock => "Block weights require an entry block.",
            FatalJitErrorCode.LoopWeightsRequireDominators => "Loop weights require dominators.",
            FatalJitErrorCode.CSEDiagnosticConfigurationIsOnlyAvailableInDebugBuilds => "CSE diagnostic configuration is only available in Debug builds.",
            FatalJitErrorCode.LoopHoistStatisticsAreOnlyAvailableInDebugBuilds => "Loop-hoist statistics are only available in Debug builds.",
            FatalJitErrorCode.LoopHoistStatisticsLockIsUnavailable => "The loop-hoist statistics lock is unavailable.",
            FatalJitErrorCode.CopyPropagationDefinitionNodesAreOnlyAvailableInDebugBuilds => "Copy-propagation definition nodes are only available in Debug builds.",
            FatalJitErrorCode.CopyPropagationLostALiveDefinition => "Copy propagation lost a live definition.",
            FatalJitErrorCode.CopyPropagationRequiresDefinitionsPushedThroughItsStackHelper => "Copy propagation requires definitions pushed through its stack helper.",
            FatalJitErrorCode.CopyPropagationRequiresDominators => "Copy propagation requires dominators.",
            FatalJitErrorCode.CompilerDebugBreakStateIsOnlyAvailableInDebugBuilds => "Compiler debug-break state is only available in Debug builds.",
            FatalJitErrorCode.DeferredProfileInconsistencyStateIsOnlyAvailableInDebugBuilds => "Deferred profile inconsistency state is only available in Debug builds.",
            FatalJitErrorCode.LocalDebugFlagsAreOnlyAvailableInDebugBuilds => "Local debug flags are only available in Debug builds.",
            FatalJitErrorCode.DoNotEnregisterReasonIsOnlyAvailableInDebugBuilds => "The do-not-enregister reason is only available in Debug builds.",
            FatalJitErrorCode.AddressExposedReasonIsOnlyAvailableInDebugBuilds => "Address-exposed reason is only available in Debug builds.",
            FatalJitErrorCode.SingleDefinitionDisqualificationReasonIsOnlyAvailableInDebugBuilds => "The single-definition disqualification reason is only available in Debug builds.",
            FatalJitErrorCode.CompilerDebugDataIsOnlyAvailableInDebugBuilds => "Compiler debug data is only available in Debug builds.",
            FatalJitErrorCode.TreeIDsAreOnlyAvailableInDebugBuilds => "Tree IDs are only available in Debug builds.",
            FatalJitErrorCode.InlineSizeDiagnosticsAreOnlyAvailableInDebugBuilds => "Inline size diagnostics are only available in Debug builds.",
            FatalJitErrorCode.BasicBlockDumpOrderIsOnlyAvailableInDebugBuilds => "Basic block dump order is only available in Debug builds.",
            FatalJitErrorCode.InlineMethodDiagnosticsAreOnlyAvailableInDebugBuilds => "Inline method diagnostics are only available in Debug builds.",
            FatalJitErrorCode.EHNormalizationDiagnosticsAreOnlyAvailableInDebugBuilds => "EH normalization diagnostics are only available in Debug builds.",
            FatalJitErrorCode.BasicBlockCreationStateIsOnlyAvailableInDebugBuilds => "Basic-block creation state is only available in Debug builds.",
            FatalJitErrorCode.FlowEdgeCreationStateIsOnlyAvailableInDebugBuilds => "Flow-edge creation state is only available in Debug builds.",
            FatalJitErrorCode.CallFinallyRangeEndedPrematurely => "Call-finally range ended prematurely.",
            FatalJitErrorCode.CallFinallyPairRequiresItsLeaveBlock => "A call-finally pair requires its leave block.",
            FatalJitErrorCode.TryRegionEndedPrematurely => "Try region ended prematurely.",
            FatalJitErrorCode.HandlerRegionEndedPrematurely => "Handler region ended prematurely.",
            FatalJitErrorCode.FinallyReturnRequiresAStatement => "Finally return requires a statement.",
            FatalJitErrorCode.FinallyReturnTargetsMustBeInitialized => "Finally return targets must be initialized.",
            FatalJitErrorCode.UnexpectedBlockKindArgument => "Unexpected block kind {0}.",
            FatalJitErrorCode.FilterRegionEndedPrematurely => "Filter region ended prematurely.",
            FatalJitErrorCode.LvaAlignFrameIsNotSupportedForThisTarget => "lvaAlignFrame is not supported for this target.",
            FatalJitErrorCode.ReservedRegisterFrameChecksAreUnavailableForThisTarget => "Reserved-register frame checks are unavailable for this target.",
            FatalJitErrorCode.PreSpilledRegisterArgumentsAreUnavailableForThisTarget => "Pre-spilled register arguments are unavailable for this target.",
            FatalJitErrorCode.FinallyHandlerRangeEndedPrematurely => "Finally handler range ended prematurely.",
            FatalJitErrorCode.SubsequentCloneNeedsItsPredecessor => "A subsequent clone needs its predecessor.",
            FatalJitErrorCode.FinallyReturnRequiresAStatement2 => "A finally return requires a statement.",
            FatalJitErrorCode.RecordedOperandIsNoLongerPresent => "The recorded operand is no longer present.",
            FatalJitErrorCode.VisitedOperandIsNotOwnedByItsParent => "The visited operand is not owned by its parent.",
            FatalJitErrorCode.BlockWithALastStatementMustHaveAFirstStatement => "A block with a last statement must have a first statement.",
            FatalJitErrorCode.FilterRegionHasNoFirstBlock => "Filter region has no first block.",
            FatalJitErrorCode.FilterRegionHasNoLastBlock => "Filter region has no last block.",
            FatalJitErrorCode.FlowGraphLoopDumpingRequiresNaturalLoops => "Flow graph loop dumping requires natural loops.",
            FatalJitErrorCode.NativeXMLFlowGraphJumpKindTableHasNoDefinedEntryForThisBlockKindB350 => "Native XML flow-graph jump-kind table has no defined entry for this block kind (B350).",
            FatalJitErrorCode.LvaToInitialSPRelativeOffsetIsNotImplementedForThisTarget => "lvaToInitialSPRelativeOffset is not implemented for this target.",
            FatalJitErrorCode.GSShadowAnalysisRequiresALocalTable => "GS shadow analysis requires a local table.",
            FatalJitErrorCode.GSShadowCopiesRequireALocalTable => "GS shadow copies require a local table.",
            FatalJitErrorCode.GSShadowRewritingRequiresALocalTable => "GS shadow rewriting requires a local table.",
            FatalJitErrorCode.IJWStructArgumentShadowCopiesRequireTheX86SpecialCopyHelper => "IJW struct-argument shadow copies require the x86 special-copy helper.",
            FatalJitErrorCode.GSShadowCopiesRequireAnEntryBlock => "GS shadow copies require an entry block.",
            FatalJitErrorCode.HardwareIntrinsicImportWithoutHardwareIntrinsicAndSIMDSupport => "Hardware-intrinsic import without hardware-intrinsic and SIMD support.",
            FatalJitErrorCode.ARM64ImmediateDiscoveryRequiresARM64HardwareIntrinsics => "ARM64 immediate discovery requires ARM64 hardware intrinsics.",
            FatalJitErrorCode.ARM64HardwareIntrinsicFallbackRequiresARM64HardwareIntrinsics => "ARM64 hardware-intrinsic fallback requires ARM64 hardware intrinsics.",
            FatalJitErrorCode.UnreachableARM64FallbackIntrinsic => "Unreachable ARM64 fallback intrinsic.",
            FatalJitErrorCode.ARM64HardwareIntrinsicSpecialImportRequiresARM64HardwareIntrinsics => "ARM64 hardware-intrinsic special import requires ARM64 hardware intrinsics.",
            FatalJitErrorCode.UnreachableARM64SIMDElementSize => "Unreachable ARM64 SIMD element size.",
            FatalJitErrorCode.SIMDCreationRequiresHardwareIntrinsics => "SIMD creation requires hardware intrinsics.",
            FatalJitErrorCode.HardwareIntrinsicSpecialImportRequiresHardwareIntrinsics => "Hardware-intrinsic special import requires hardware intrinsics.",
            FatalJitErrorCode.HardwareIntrinsicSpecialImportOutsideXarchIsNotPorted => "Hardware-intrinsic special import outside xarch is not ported.",
            FatalJitErrorCode.HardwareIntrinsicNonconstantFallbackRequiresHardwareIntrinsics => "Hardware-intrinsic nonconstant fallback requires hardware intrinsics.",
            FatalJitErrorCode.UnreachableWasmHardwareIntrinsicNonconstantFallback => "Unreachable Wasm hardware-intrinsic nonconstant fallback.",
            FatalJitErrorCode.HardwareIntrinsicNonconstantFallbackOutsideXarchIsNotPorted => "Hardware-intrinsic nonconstant fallback outside xarch is not ported.",
            FatalJitErrorCode.XarchHardwareIntrinsicFallbackRequiresXarchHardwareIntrinsics => "Xarch hardware-intrinsic fallback requires xarch hardware intrinsics.",
            FatalJitErrorCode.HardwareIntrinsicImmediateDiscoveryRequiresHardwareIntrinsics => "Hardware-intrinsic immediate discovery requires hardware intrinsics.",
            FatalJitErrorCode.HardwareIntrinsicImmediateDiscoveryOutsideXarch => "Hardware-intrinsic immediate discovery outside xarch.",
            FatalJitErrorCode.XarchHardwareIntrinsicSpecialImportRequiresXarchHardwareIntrinsics => "Xarch hardware-intrinsic special import requires xarch hardware intrinsics.",
            FatalJitErrorCode.WasmHardwareIntrinsicSpecialImportRequiresWasmHardwareIntrinsics => "Wasm hardware-intrinsic special import requires Wasm hardware intrinsics.",
            FatalJitErrorCode.CrossPlatformIntrinsicImportRequiresASupportedHardwareIntrinsicTarget => "Cross-platform intrinsic import requires a supported hardware-intrinsic target.",
            FatalJitErrorCode.UnsupportedSIMDNarrowingTarget => "Unsupported SIMD narrowing target.",
            FatalJitErrorCode.UnsupportedSIMDShiftTarget => "Unsupported SIMD shift target.",
            FatalJitErrorCode.SaturatingSIMDOperationsRequireASupportedHardwareIntrinsicTarget => "Saturating SIMD operations require a supported hardware-intrinsic target.",
            FatalJitErrorCode.UnsupportedSaturatingSIMDTarget => "Unsupported saturating SIMD target.",
            FatalJitErrorCode.HardwareIntrinsicRangeCheckingIsNotEnabled => "Hardware intrinsic range checking is not enabled.",
            FatalJitErrorCode.UnsupportedOrUnsetTargetArchitecture => "Unsupported or unset target architecture.",
            FatalJitErrorCode.InlineCandidateCallHasNoImportStatement => "Inline candidate call has no import statement.",
            FatalJitErrorCode.UnableToCloneTheFirstMathMinMaxOperand => "Unable to clone the first Math.Min/Max operand.",
            FatalJitErrorCode.UnableToCloneTheMathMinMaxResult => "Unable to clone the Math.Min/Max result.",
            FatalJitErrorCode.UnableToCloneTheSecondMathMinMaxOperand => "Unable to clone the second Math.Min/Max operand.",
            FatalJitErrorCode.InitializedIVCursorNeedsARecurrence => "An initialized IV cursor needs a recurrence.",
            FatalJitErrorCode.PostUsePlacementNeedsAUseStatement => "Post-use placement needs a use statement.",
            FatalJitErrorCode.CompilerMethodHashIsOnlyAvailableInDebugBuilds => "The compiler method hash is only available in Debug builds.",
            FatalJitErrorCode.VarargsFloatRegisterMappingRequiresWindowsAMD64 => "Varargs float register mapping requires Windows AMD64.",
            FatalJitErrorCode.LoopDiscoveryRequiresADFSTree => "Loop discovery requires a DFS tree.",
            FatalJitErrorCode.ForbiddenTestExpressionWasHoisted => "A forbidden test expression was hoisted.",
            FatalJitErrorCode.LoopHoistViolatedANodeTestAnnotation => "Loop hoist violated a node test annotation.",
            FatalJitErrorCode.SSADefinitionBlockMustBeAssignedBeforeLoopInvarianceAnalysis => "The SSA definition block must be assigned before loop-invariance analysis.",
            FatalJitErrorCode.LoopSideEffectAnalysisRequiresABlockToLoopMap => "Loop side-effect analysis requires a block-to-loop map.",
            FatalJitErrorCode.FieldAddressMustCarryAFieldSequence => "A field address must carry a field sequence.",
            FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds => "Compiler display options are only available in Debug builds.",
            FatalJitErrorCode.LoopAlignmentDiagnosticOptionsAreOnlyAvailableInDebugBuilds => "Loop-alignment diagnostic options are only available in Debug builds.",
            FatalJitErrorCode.PatchpointMetadataGenerationIsNotImplementedForThisTarget => "Patchpoint metadata generation is not implemented for this target.",
            FatalJitErrorCode.JITMethodHaltingIsOnlyAvailableInDebugBuilds => "JIT method halting is only available in Debug builds.",
            FatalJitErrorCode.TargetPrologUnwindPaddingIsNotPorted => "Target prolog unwind padding is not ported.",
            FatalJitErrorCode.MappedPhiOperandHasNoDefinition => "The mapped phi operand has no definition.",
            FatalJitErrorCode.NonEntryBlockInTheUniqueSuccessorScanHasNoPredecessor => "A non-entry block in the unique-successor scan has no predecessor.",
            FatalJitErrorCode.FlowGraphDFSTreeIsRequired => "The flow graph DFS tree is required.",
            FatalJitErrorCode.CodeGenerationStateIsRequired => "Code generation state is required.",
            FatalJitErrorCode.UnknownTargetArchitectureForFEATUREPARTIALSIMDCALLEESAVE => "Unknown target architecture for FEATURE_PARTIAL_SIMD_CALLEE_SAVE.",
            FatalJitErrorCode.CompilerRaMarkStkVarsOutsideAMD64ARM64 => "Compiler.raMarkStkVars outside AMD64/ARM64.",
            FatalJitErrorCode.CompilerDumpRegMaskOutsideAMD64 => "Compiler.dumpRegMask outside AMD64.",
            FatalJitErrorCode.GetCompileTimeVectorTByteLengthIsUnsupportedOnARM32 => "getCompileTimeVectorTByteLength is unsupported on ARM32.",
            FatalJitErrorCode.CompileTimeVectorTLengthIsNotSupportedOnThisTarget => "Compile-time Vector<T> length is not supported on this target.",
            FatalJitErrorCode.HardwareIntrinsicArgumentsAreNotSupportedOnThisTarget => "Hardware intrinsic arguments are not supported on this target.",
            FatalJitErrorCode.ScopeDiagnosticsAreOnlyAvailableInDebugBuilds => "Scope diagnostics are only available in Debug builds.",
            FatalJitErrorCode.TwoSourceSIMDShufflesRequireWebAssemblyHardwareIntrinsicSupport => "Two-source SIMD shuffles require WebAssembly hardware-intrinsic support.",
            FatalJitErrorCode.SIMDConversionRequiresHardwareIntrinsics => "SIMD conversion requires hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdCvtNodeRequiresItsTargetSpecificImplementation => "gtNewSimdCvtNode requires its target-specific implementation.",
            FatalJitErrorCode.NativeSIMDConversionRequiresHardwareIntrinsics => "Native SIMD conversion requires hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdCvtNativeNodeRequiresItsTargetSpecificImplementation => "gtNewSimdCvtNativeNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDCeilingRequiresHardwareIntrinsics => "SIMD ceiling requires hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdCeilNodeRequiresItsTargetSpecificImplementation => "gtNewSimdCeilNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDFloorRequiresHardwareIntrinsics => "SIMD floor requires hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdFloorNodeRequiresItsTargetSpecificImplementation => "gtNewSimdFloorNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDRoundingRequiresHardwareIntrinsics => "SIMD rounding requires hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdRoundNodeRequiresItsTargetSpecificImplementation => "gtNewSimdRoundNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDLoadsRequireHardwareIntrinsics => "SIMD loads require hardware intrinsics.",
            FatalJitErrorCode.AlignedSIMDLoadsRequireHardwareIntrinsics => "Aligned SIMD loads require hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdLoadAlignedNodeRequiresItsTargetSpecificImplementation => "gtNewSimdLoadAlignedNode requires its target-specific implementation.",
            FatalJitErrorCode.NonTemporalSIMDLoadsRequireHardwareIntrinsics => "Non-temporal SIMD loads require hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdLoadNonTemporalNodeRequiresItsTargetSpecificImplementation => "gtNewSimdLoadNonTemporalNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDStoresRequireHardwareIntrinsics => "SIMD stores require hardware intrinsics.",
            FatalJitErrorCode.AlignedSIMDStoresRequireHardwareIntrinsics => "Aligned SIMD stores require hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdStoreAlignedNodeRequiresItsTargetSpecificImplementation => "gtNewSimdStoreAlignedNode requires its target-specific implementation.",
            FatalJitErrorCode.NonTemporalSIMDStoresRequireHardwareIntrinsics => "Non-temporal SIMD stores require hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdStoreNonTemporalNodeRequiresItsTargetSpecificImplementation => "gtNewSimdStoreNonTemporalNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDDotProductsRequireHardwareIntrinsics => "SIMD dot products require hardware intrinsics.",
            FatalJitErrorCode.SIMDFusedMultiplyAddRequiresHardwareIntrinsics => "SIMD fused multiply-add requires hardware intrinsics.",
            FatalJitErrorCode.UnreachableWasmSIMDFusedMultiplyAdd => "Unreachable Wasm SIMD fused multiply-add.",
            FatalJitErrorCode.GtNewSimdFmaNodeRequiresItsTargetSpecificImplementation => "gtNewSimdFmaNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDComparisonsRequireHardwareIntrinsics => "SIMD comparisons require hardware intrinsics.",
            FatalJitErrorCode.SIMDTernaryLogicRequiresXarchHardwareIntrinsics => "SIMD ternary logic requires xarch hardware intrinsics.",
            FatalJitErrorCode.SIMDConcatenationRequiresHardwareIntrinsics => "SIMD concatenation requires hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdConcatNodeRequiresItsTargetSpecificImplementation => "gtNewSimdConcatNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDZipRequiresHardwareIntrinsics => "SIMD zip requires hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdZipNodeRequiresItsTargetSpecificImplementation => "gtNewSimdZipNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDUnzipRequiresHardwareIntrinsics => "SIMD unzip requires hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdUnzipNodeRequiresItsTargetSpecificImplementation => "gtNewSimdUnzipNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDReverseRequiresHardwareIntrinsics => "SIMD reverse requires hardware intrinsics.",
            FatalJitErrorCode.SaturatingSIMDNarrowingRequiresHardwareIntrinsics => "Saturating SIMD narrowing requires hardware intrinsics.",
            FatalJitErrorCode.SIMDIndicesRequireHardwareIntrinsics => "SIMD indices require hardware intrinsics.",
            FatalJitErrorCode.SIMDSequencesRequireHardwareIntrinsics => "SIMD sequences require hardware intrinsics.",
            FatalJitErrorCode.AlternatingSIMDSequencesRequireHardwareIntrinsics => "Alternating SIMD sequences require hardware intrinsics.",
            FatalJitErrorCode.SIMDSumsRequireHardwareIntrinsics => "SIMD sums require hardware intrinsics.",
            FatalJitErrorCode.GtNewSimdSumNodeRequiresItsTargetSpecificImplementation => "gtNewSimdSumNode requires its target-specific implementation.",
            FatalJitErrorCode.SIMDHardwareIntrinsicsAreUnsupportedOnThisTarget => "SIMD hardware intrinsics are unsupported on this target.",
            FatalJitErrorCode.SIMDEqualityIsUnsupportedOnThisTarget => "SIMD equality is unsupported on this target.",
            FatalJitErrorCode.SIMDBitwiseOperationsAreUnsupportedOnThisTarget => "SIMD bitwise operations are unsupported on this target.",
            FatalJitErrorCode.HardwareIntrinsicsAreUnsupportedOnThisTarget => "Hardware intrinsics are unsupported on this target.",
            FatalJitErrorCode.SIMDScalarCreationIsUnsupportedOnThisTarget => "SIMD scalar creation is unsupported on this target.",
            FatalJitErrorCode.SIMDScalarExtractionIsUnsupportedOnThisTarget => "SIMD scalar extraction is unsupported on this target.",
            FatalJitErrorCode.NodeTestDataIsOnlyAvailableInDebugBuilds => "Node test data is only available in Debug builds.",
            FatalJitErrorCode.CoreCLRTLSExpansionIsUnsupportedForThisTarget => "CoreCLR TLS expansion is unsupported for this target.",
            FatalJitErrorCode.ThreeOptLayoutRequiresTheDFSTree => "Three-opt layout requires the DFS tree.",
            FatalJitErrorCode.LayoutRequiresAnEntryBlock => "The layout requires an entry block.",
            FatalJitErrorCode.CallFinallyHeadMustHaveAPairedTail => "A call-finally head must have a paired tail.",
            FatalJitErrorCode.UnknownSizeLocalAllocationRequiresSIMDEnabledARM64 => "Unknown-size local allocation requires SIMD-enabled ARM64.",
            FatalJitErrorCode.UnknownSizeTemporaryAllocationRequiresSIMDEnabledARM64 => "Unknown-size temporary allocation requires SIMD-enabled ARM64.",
            FatalJitErrorCode.ColdUnwindInformationWasNotInitialized => "Cold unwind information was not initialized.",
            FatalJitErrorCode.TargetUnwindReservationIsNotPorted => "Target unwind reservation is not ported.",
            FatalJitErrorCode.TargetUnwindEmissionIsNotPorted => "Target unwind emission is not ported.",
            FatalJitErrorCode.TargetUnwindRecordingIsNotPorted => "Target unwind recording is not ported.",
            FatalJitErrorCode.TargetEpilogUnwindRecordingIsNotPorted => "Target epilog unwind recording is not ported.",
            FatalJitErrorCode.UnmappedVNHelperArgument => "Unmapped VN helper: {0}",
            FatalJitErrorCode.StructLocalHasNoLayout2 => "Struct local has no layout.",
            FatalJitErrorCode.StructStoreHasNoLayout => "Struct store has no layout.",
            FatalJitErrorCode.SSAStoreHasNoDefiningBlock => "An SSA store has no defining block.",
            FatalJitErrorCode.HandleArgumentInFgValueNumberAddExceptionSet => "Handle {0} in fgValueNumberAddExceptionSet",
            FatalJitErrorCode.UnexpectedSaturatingIntrinsicArgument => "Unexpected saturating intrinsic: {0}",
            FatalJitErrorCode.MemoryIntrinsicHasNoAddressOperand => "Memory intrinsic has no address operand.",
            FatalJitErrorCode.UnexpectedStoreOperatorArgument => "Unexpected store operator {0}",
            FatalJitErrorCode.CastNodesUseFgValueNumberCastTreeInsteadOfGetVNFuncForNode => "Cast nodes use fgValueNumberCastTree instead of GetVNFuncForNode.",
            FatalJitErrorCode.UnhandledLeafArgumentInFgValueNumberTree => "Unhandled leaf {0} in fgValueNumberTree.",
            FatalJitErrorCode.ARRELEMMustBeMorphedBeforeValueNumbering => "ARR_ELEM must be morphed before value numbering.",
            FatalJitErrorCode.WasmBlockReorderingRequiresAPrecedingBlock => "Wasm block reordering requires a preceding block.",
            FatalJitErrorCode.ConditionalWasmBlockRequiresAFinalLIRNode => "A conditional Wasm block requires a final LIR node.",
            FatalJitErrorCode.PairedWasmCallFinallyBlockHasNoFollowingBlock => "A paired Wasm call-finally block has no following block.",
            FatalJitErrorCode.WasmResumePadHasNoSwitchEdge => "A Wasm resume pad has no switch edge.",
            FatalJitErrorCode.WasmContinuationIsMissingItsResumePadOrSwitchEdge => "A Wasm continuation is missing its resume pad or switch edge.",
            FatalJitErrorCode.WasmGCRefCandidatesWereNotComputed => "Wasm GC-ref candidates were not computed.",
            FatalJitErrorCode.WasmTryEntryRepairRequiresAnEntryBlock => "Wasm try-entry repair requires an entry block.",
            FatalJitErrorCode.WasmTryEntryDispatcherWasNotInitialized => "A Wasm try-entry dispatcher was not initialized.",
            FatalJitErrorCode.WasmSideEntryTargetHasNoAssignedIndex => "A Wasm side-entry target has no assigned index.",
            FatalJitErrorCode.LoweringIsUnavailableDuringWasmVirtualIPSetup => "Lowering is unavailable during Wasm virtual-IP setup.",
            FatalJitErrorCode.HardwareIntrinsicAssertionPropagationIsNotEnabled => "Hardware intrinsic assertion propagation is not enabled.",
            FatalJitErrorCode.SSAStoreHasNoValue => "SSA store has no value.",
            FatalJitErrorCode.SSAStoreHasNoDefiningBlock2 => "SSA store has no defining block.",
            FatalJitErrorCode.ImmutableSourceReturnedNoData => "Immutable source returned no data.",
            FatalJitErrorCode.GSSecurityBlockerDiagnosticsAreOnlyAvailableInDebugBuilds => "GS security blocker diagnostics are only available in Debug builds.",
            FatalJitErrorCode.XarchRegisterMetadataIsNotDefinedForTheConfiguredTarget => "xarch register metadata is not defined for the configured target.",
            FatalJitErrorCode.JITStressIsOnlyAvailableInDebugBuilds => "JIT stress is only available in Debug builds.",
            FatalJitErrorCode.AsyncInliningStressIsOnlyAvailableInDebugBuilds => "Async inlining stress is only available in Debug builds.",
            FatalJitErrorCode.InlineStressIsOnlyAvailableInDebugBuilds => "Inline stress is only available in Debug builds.",
            FatalJitErrorCode.LocalVariableNamesAreOnlyAvailableInDebugBuilds => "Local variable names are only available in Debug builds.",
            FatalJitErrorCode.RegisterVariableNamesAreOnlyAvailableInDebugBuilds => "Register variable names are only available in Debug builds.",
            FatalJitErrorCode.VerboseOutputIsOnlyAvailableInDebugBuilds => "Verbose output is only available in Debug builds.",
            FatalJitErrorCode.VerboseTreeOutputIsOnlyAvailableInDebugBuilds => "Verbose tree output is only available in Debug builds.",
            FatalJitErrorCode.VerboseSSAOutputIsOnlyAvailableInDebugBuilds => "Verbose SSA output is only available in Debug builds.",
            FatalJitErrorCode.MaskRegistersAreNotDefinedForTheConfiguredTarget => "Mask registers are not defined for the configured target.",
            FatalJitErrorCode.SIMDIsNotEnabledForThisTarget => "SIMD is not enabled for this target.",
            FatalJitErrorCode.EVEXDebugSupportChecksAreUnavailableInRelease => "EVEX debug support checks are unavailable in Release.",
            FatalJitErrorCode.SkipMethodIsOnlyAvailableInDebugBuilds => "SkipMethod is only available in Debug builds.",
            FatalJitErrorCode.VNDiagnosticsRequireAValueNumberStore => "VN diagnostics require a value number store.",
            FatalJitErrorCode.WasmGlobalsAreNotAvailableOnThisTarget => "Wasm globals are not available on this target.",
            FatalJitErrorCode.ClassSizeFallbackIsOnlyAvailableInDebugBuilds => "Class size fallback is only available in Debug builds.",
            FatalJitErrorCode.NormalStaticsAreExpectedToBeHandledInTheImporter => "Normal statics are expected to be handled in the importer",
            FatalJitErrorCode.NewTargetRequiresFloatingToSmallIntegerSaturationSupport => "New target requires floating-to-small-integer saturation support.",
            FatalJitErrorCode.UnexpectedAccessTypeForR2RFieldAccess => "unexpected accessType for R2R field access",
            FatalJitErrorCode.HardwareIntrinsicMorphingIsUnsupportedForThisTarget => "Hardware intrinsic morphing is unsupported for this target.",
            FatalJitErrorCode.UnsupportedMaskedIntrinsicConversionTarget => "Unsupported masked intrinsic conversion target.",
            FatalJitErrorCode.GeometricSequenceIntrinsicIsUnsupportedForThisTarget => "Geometric-sequence intrinsic is unsupported for this target.",
            FatalJitErrorCode.FastTailCallArgumentChecksAreNotSupportedOnThisPlatform => "Fast tail-call argument checks are not supported on this platform.",
            FatalJitErrorCode.StatementLinkChecksAreOnlyAvailableInDebugBuilds => "Statement-link checks are only available in Debug builds.",
            FatalJitErrorCode.ProfileChecksAreOnlyAvailableInDebugBuilds => "Profile checks are only available in Debug builds.",
            FatalJitErrorCode.ProfileWeightChecksAreOnlyAvailableInDebugBuilds => "Profile weight checks are only available in Debug builds.",
            FatalJitErrorCode.IncomingProfileChecksAreOnlyAvailableInDebugBuilds => "Incoming profile checks are only available in Debug builds.",
            FatalJitErrorCode.OutgoingProfileChecksAreOnlyAvailableInDebugBuilds => "Outgoing profile checks are only available in Debug builds.",
            FatalJitErrorCode.ProfileEdgeFormattingIsOnlyAvailableInDebugBuilds => "Profile edge formatting is only available in Debug builds.",
            FatalJitErrorCode.TreeDumpsAreOnlyAvailableInDebugBuilds => "Tree dumps are only available in Debug builds.",
            FatalJitErrorCode.FilterEndBlockIsMissing => "Filter end block is missing.",
            FatalJitErrorCode.BasicBlockLookupInvalidationIsOnlyAvailableInDebugBuilds => "Basic block lookup invalidation is only available in Debug builds.",
            FatalJitErrorCode.BasicBlockTableDisplayIsOnlyAvailableInDebugBuilds => "Basic block table display is only available in Debug builds.",
            FatalJitErrorCode.IndirectCallVerificationIsOnlyAvailableInDebugBuilds => "Indirect-call verification is only available in Debug builds.",
            FatalJitErrorCode.MaskConversionFoldingIsNotSupportedOnThisTarget => "Mask conversion folding is not supported on this target.",
            FatalJitErrorCode.ScalableVectorMaskFoldingRequiresAnARM64Target => "Scalable-vector mask folding requires an ARM64 target.",
            FatalJitErrorCode.SIMDShufflesAreUnsupportedOnThisTarget => "SIMD shuffles are unsupported on this target.",
            FatalJitErrorCode.SIMDConstantShufflesAreUnsupportedOnThisTarget => "SIMD constant shuffles are unsupported on this target.",
            FatalJitErrorCode.SIMDVariableShufflesAreUnsupportedOnThisTarget => "SIMD variable shuffles are unsupported on this target.",
            FatalJitErrorCode.HardwareIntrinsicTreeCloningIsUnsupportedOnThisTarget => "Hardware intrinsic tree cloning is unsupported on this target.",
            FatalJitErrorCode.InvalidIntegralConstantCastDestination => "Invalid integral constant cast destination.",
            FatalJitErrorCode.InvalidLongConstantCastDestination => "Invalid long constant cast destination.",
            FatalJitErrorCode.InvalidFloatingConstantCastDestination => "Invalid floating constant cast destination.",
            FatalJitErrorCode.ScalableMaskConstantsAreOnlySupportedOnARM64 => "Scalable mask constants are only supported on ARM64.",
            FatalJitErrorCode.SIMDAbsoluteValueIsUnsupportedOnThisTarget => "SIMD absolute value is unsupported on this target.",
            FatalJitErrorCode.SIMDSquareRootIsUnsupportedOnThisTarget => "SIMD square root is unsupported on this target.",
            FatalJitErrorCode.SIMDComparisonReductionsAreUnsupportedOnThisTarget => "SIMD comparison reductions are unsupported on this target.",
            FatalJitErrorCode.SIMDConditionalSelectIsUnsupportedOnThisTarget => "SIMD conditional select is unsupported on this target.",
            FatalJitErrorCode.MaskedHardwareIntrinsicConversionIsUnsupportedInThisConfiguration => "Masked hardware intrinsic conversion is unsupported in this configuration.",
            FatalJitErrorCode.MaskToVectorConversionIsUnsupportedOnThisTarget => "Mask-to-vector conversion is unsupported on this target.",
            FatalJitErrorCode.VectorToMaskConversionIsUnsupportedOnThisTarget => "Vector-to-mask conversion is unsupported on this target.",
            FatalJitErrorCode.SIMDGetLowerIsUnsupportedOnThisTarget => "SIMD get-lower is unsupported on this target.",
            FatalJitErrorCode.WasmVectorGetLowerSupportIsNotPorted => "Wasm vector get-lower support is not ported.",
            FatalJitErrorCode.SIMDGetUpperIsUnsupportedOnThisTarget => "SIMD get-upper is unsupported on this target.",
            FatalJitErrorCode.WasmVectorGetUpperSupportIsNotPorted => "Wasm vector get-upper support is not ported.",
            FatalJitErrorCode.SIMDNativeMinMaxIsUnsupportedOnThisTarget => "SIMD native min/max is unsupported on this target.",
            FatalJitErrorCode.SIMDNarrowingIsUnsupportedOnThisTarget => "SIMD narrowing is unsupported on this target.",
            FatalJitErrorCode.SIMDOperationIsNotSupportedOnThisTarget => "SIMD operation is not supported on this target.",
            FatalJitErrorCode.WasmVectorWithLowerSupportIsNotPorted => "Wasm vector with-lower support is not ported.",
            FatalJitErrorCode.WasmVectorWithUpperSupportIsNotPorted => "Wasm vector with-upper support is not ported.",
            FatalJitErrorCode.InvalidScalableConstantElementSize => "Invalid scalable constant element size.",
            FatalJitErrorCode.StringConstantCostsAreUnsupportedOnThisTarget => "String constant costs are unsupported on this target.",
            FatalJitErrorCode.Failure32BitLongConstantCostsAreUnsupportedOnThisTarget => "32-bit long constant costs are unsupported on this target.",
            FatalJitErrorCode.IntegerConstantCostsAreUnsupportedOnThisTarget => "Integer constant costs are unsupported on this target.",
            FatalJitErrorCode.FloatingPointConstantCostsAreUnsupportedOnThisTarget => "Floating-point constant costs are unsupported on this target.",
            FatalJitErrorCode.CastCostEstimationIsUnsupportedOnThisTarget => "Cast cost estimation is unsupported on this target.",
            FatalJitErrorCode.JitTestLabelMarkImporterIsOnlyAvailableInDebugBuilds => "JitTestLabel.Mark importer is only available in Debug builds.",
            FatalJitErrorCode.ImporterILOffsetTrackingIsOnlyAvailableInDebugBuilds => "Importer IL-offset tracking is only available in Debug builds.",
            FatalJitErrorCode.ImplicitByrefPoisoningIsOnlyAvailableInDebugBuilds => "Implicit-byref poisoning is only available in Debug builds.",
            FatalJitErrorCode.UnsupportedPlatform => "Unsupported platform",
            FatalJitErrorCode.SIMDHalfOperationsAreUnsupportedOnThisTarget => "SIMD Half operations are unsupported on this target.",
            FatalJitErrorCode.UnsupportedHalfHardwareIntrinsicPlatform => "Unsupported Half hardware intrinsic platform",
            FatalJitErrorCode.ARMFrameAddressingIsUnavailableOnThisTarget => "ARM frame addressing is unavailable on this target.",
            FatalJitErrorCode.SIMD12LocalWideningIsUnsupportedOnThisTarget => "SIMD12 local widening is unsupported on this target.",
            FatalJitErrorCode.SIMDInitializationTempsAreUnsupportedOnThisTarget => "SIMD initialization temps are unsupported on this target.",
            FatalJitErrorCode.SIMDTypeClassificationIsUnsupportedOnThisTarget => "SIMD type classification is unsupported on this target.",
            FatalJitErrorCode.UnexpectedLoopComparisonArgument => "Unexpected loop comparison: {0}",
            FatalJitErrorCode.UnwindInformationHasNotBeenInitialized => "Unwind information has not been initialized.",
            FatalJitErrorCode.LIRRangeDoesNotReachItsLastNode => "The LIR range does not reach its last node.",
            FatalJitErrorCode.NoCompilerIsActiveOnThisThread => "No compiler is active on this thread.",
            FatalJitErrorCode.DebuggerLookupsAreOnlyAvailableInDebugBuilds => "Debugger lookups are only available in Debug builds.",
            FatalJitErrorCode.GenTreeSGtNodeSizesNativeAllocationSizesAreNotPorted => "GenTree::s_gtNodeSizes native allocation sizes are not ported.",
            FatalJitErrorCode.NativeSizeofGenTreeLclFldIsNotPorted => "Native sizeof(GenTreeLclFld) is not ported.",
            FatalJitErrorCode.NativeSizeofGenTreeCallIsNotPorted => "Native sizeof(GenTreeCall) is not ported.",
            FatalJitErrorCode.GenNodeSizeStatsCollectionIsNotPorted => "genNodeSizeStats collection is not ported.",
            FatalJitErrorCode.GenNodeSizeStatsPerFuncCollectionIsNotPorted => "genNodeSizeStatsPerFunc collection is not ported.",
            FatalJitErrorCode.GenTreeNcntHistCollectionIsNotPorted => "genTreeNcntHist collection is not ported.",
            FatalJitErrorCode.GenTreeNsizHistCollectionIsNotPorted => "genTreeNsizHist collection is not ported.",
            FatalJitErrorCode.NativeSizeofFlowEdgeIsNotPorted => "Native sizeof(FlowEdge) is not ported.",
            FatalJitErrorCode.IntegerArgumentRegisterIndexingOutsideAMD64ARM64 => "Integer argument-register indexing outside AMD64/ARM64.",
            FatalJitErrorCode.NodeDestructionDiagnosticsAreOnlyAvailableInDebugBuilds => "Node destruction diagnostics are only available in Debug builds.",
            FatalJitErrorCode.UnsupportedPlatformForHardwareIntrinsicRangeAnalysis => "Unsupported platform for hardware intrinsic range analysis.",
            FatalJitErrorCode.LocalDebugReasonIsOnlyAvailableInDebugBuilds => "The local debug reason is only available in Debug builds.",
            FatalJitErrorCode.LocalRegisterMasksRequireAFixedRegisterSet => "Local register masks require a fixed register set.",
            FatalJitErrorCode.UnableToAllocateTheICorJitInfoTimingProxy => "Unable to allocate the ICorJitInfo timing proxy.",
            FatalJitErrorCode.CompilerEEHandleChangedBeforeTimingProxyInstallation => "The compiler EE handle changed before timing proxy installation.",
            FatalJitErrorCode.AllVarsetOperationCounterIsNotInitialized => "The all-varset operation counter is not initialized.",
            FatalJitErrorCode.VarsetOperationCounterIsNotInitialized => "The varset operation counter is not initialized.",
            FatalJitErrorCode.DisassemblerHasNotBeenInitialized => "Disassembler has not been initialized.",
            FatalJitErrorCode.CoreDisToolsDoesNotSupportThisJITTarget => "CoreDisTools does not support this JIT target.",
            FatalJitErrorCode.CoreDisToolsDecoderCleanupRequiresTheFinishDisasmExport => "CoreDisTools decoder cleanup requires the FinishDisasm export.",
            FatalJitErrorCode.CoreDisToolsDecodingRequiresTheDumpInstructionExport => "CoreDisTools decoding requires the DumpInstruction export.",
            FatalJitErrorCode.CoreDisToolsReturnedANullBufferedOutputPointer => "CoreDisTools returned a null buffered output pointer.",
            FatalJitErrorCode.CoreDisToolsReturnedInvalidBufferedOutput => "CoreDisTools returned invalid buffered output.",
            FatalJitErrorCode.LateDisassemblyOutputIsOnlyAvailableInDebugBuilds => "Late disassembly output is only available in Debug builds.",
            FatalJitErrorCode.LateDisassemblyRequiresTheNativeDecoderAndSymbolCallbacks => "Late disassembly requires the native decoder and symbol callbacks.",
            FatalJitErrorCode.MSVCDISDoesNotSupportThisJITTarget => "MSVCDIS does not support this JIT target.",
            FatalJitErrorCode.LateDisassemblyRequiresDISPdisNewAndTheMSVCDISCCallbackABI => "Late disassembly requires DIS::PdisNew and the MSVCDIS C++ callback ABI.",
            FatalJitErrorCode.UnknownTargetForMultiRegisterArguments => "Unknown target for multi-register arguments.",
            FatalJitErrorCode.OutgoingEHDiagnosticsAreOnlyAvailableInDebugBuilds => "Outgoing EH diagnostics are only available in Debug builds.",
            FatalJitErrorCode.EHDiagnosticsRequireAnEmitter => "EH diagnostics require an emitter.",
            FatalJitErrorCode.UnknownOutgoingEHClauseType => "Unknown outgoing EH clause type.",
            FatalJitErrorCode.XarchAddressEncodingIsNotAvailableForThisTarget => "xarch address encoding is not available for this target.",
            FatalJitErrorCode.XarchAddressScaleDecodingIsNotAvailableForThisTarget => "xarch address scale decoding is not available for this target.",
            FatalJitErrorCode.XarchAddressInstructionSizingIsNotAvailableForThisTarget => "xarch address instruction sizing is not available for this target.",
            FatalJitErrorCode.DATA16RecordingRequiresXarch => "DATA16 recording requires xarch.",
            FatalJitErrorCode.XarchSIBRegisterEncodingIsNotAvailableForThisTarget => "xarch SIB register encoding is not available for this target.",
            FatalJitErrorCode.ARM64IndexedAddressRegisterInstructionRecordingIsNotPorted => "ARM64 indexed address/register instruction recording is not ported.",
            FatalJitErrorCode.IndexedAddressRegisterInstructionRecordingRequiresXarch => "Indexed address/register instruction recording requires xarch.",
            FatalJitErrorCode.ARM32AddressRegisterInstructionOptionsAreNotPorted => "ARM32 address/register instruction options are not ported.",
            FatalJitErrorCode.ARM64AddressRegisterInstructionRecordingIsNotPorted => "ARM64 address/register instruction recording is not ported.",
            FatalJitErrorCode.AddressRegisterInstructionRecordingRequiresXarch => "Address/register instruction recording requires xarch.",
            FatalJitErrorCode.ARM32RegisterRegisterInstructionFlagsAreNotSupportedForThisTarget => "ARM32 register-register instruction flags are not supported for this target.",
            FatalJitErrorCode.RISCV64RegisterAddressInstructionRecordingIsNotPorted => "RISC-V64 register-address instruction recording is not ported.",
            FatalJitErrorCode.RegisterAddressInstructionRecordingRequiresXarch => "Register/address instruction recording requires xarch.",
            FatalJitErrorCode.NonDestructiveBinaryInstructionRecordingRequiresXarch => "Non-destructive binary instruction recording requires xarch.",
            FatalJitErrorCode.ImplicitRegisterPairDestinationsAreOnlyDefinedForXarch => "Implicit register-pair destinations are only defined for xarch.",
            FatalJitErrorCode.BinaryOperandRecordingRequiresXarch => "Binary operand recording requires xarch.",
            FatalJitErrorCode.LabelOutputRequiresXarch => "Label output requires xarch.",
            FatalJitErrorCode.CallScratchRegistersAreOnlyDefinedForXarch => "Call scratch registers are only defined for xarch.",
            FatalJitErrorCode.NoGCCallRegisterMaskArgumentIsNotPortedForThisTarget => "No-GC call register mask {0} is not ported for this target.",
            FatalJitErrorCode.GCCallOutputRequiresAnActiveCompiler => "GC call output requires an active compiler.",
            FatalJitErrorCode.RISCVCallRecordingIsOnlySupportedOnRISCV64 => "RISC-V call recording is only supported on RISC-V64.",
            FatalJitErrorCode.RISCVCallRecordingRequiresAnActiveCompiler => "RISC-V call recording requires an active compiler.",
            FatalJitErrorCode.RISCVCallRecordingDoesNotSupportCallKindArgument => "RISC-V call recording does not support call kind {0}.",
            FatalJitErrorCode.UnexpectedWasmCallTypeArgument => "Unexpected Wasm call type {0}.",
            FatalJitErrorCode.CallInstructionRecordingIsNotImplementedForThisTarget => "Call instruction recording is not implemented for this target.",
            FatalJitErrorCode.REXWPrefixEmissionIsNotImplementedForThisTarget => "REX.W prefix emission is not implemented for this target.",
            FatalJitErrorCode.CallGCRegisterEncodingIsNotSupportedForThisTarget => "Call GC register encoding is not supported for this target.",
            FatalJitErrorCode.CallGCRegisterEncodingIsNotPortedForThisTarget => "Call GC register encoding is not ported for this target.",
            FatalJitErrorCode.ConditionalCompareImmediatesAreOnlySupportedForX64AndARM64 => "Conditional compare immediates are only supported for x64 and ARM64.",
            FatalJitErrorCode.ARM32LocalVariableImmediateResolutionRequiresAnActiveCompiler => "ARM32 local-variable immediate resolution requires an active compiler.",
            FatalJitErrorCode.AddressModeDescriptorAllocationRequiresXarch => "Address-mode descriptor allocation requires xarch.",
            FatalJitErrorCode.ConstantDataChunkLookupRequiresAMD64OrWebAssembly => "Constant-data chunk lookup requires AMD64 or WebAssembly.",
            FatalJitErrorCode.ARM64LocalPairDescriptorsAreUnavailableOnThisTarget => "ARM64 local-pair descriptors are unavailable on this target.",
            FatalJitErrorCode.RISCVLoadImmediateDescriptorsAreUnavailableOnThisTarget => "RISC-V load-immediate descriptors are unavailable on this target.",
            FatalJitErrorCode.ThisInstructionRequiresAnInitializedDescriptorWithItsNativeLayout => "This instruction requires an initialized descriptor with its native layout.",
            FatalJitErrorCode.BaseInstructionDescriptorSizeIsNotYetPortedForThisTarget => "Base instruction descriptor size is not yet ported for this target.",
            FatalJitErrorCode.ExtendedInstructionDescriptorLayoutIsNotYetPorted => "Extended instruction descriptor layout is not yet ported.",
            FatalJitErrorCode.InstructionDescriptorSizesAreNotYetPortedForThisTarget => "Instruction descriptor sizes are not yet ported for this target.",
            FatalJitErrorCode.ARM64CallDescriptorLayoutIsOnlySupportedOnARM64 => "The ARM64 call descriptor layout is only supported on ARM64.",
            FatalJitErrorCode.ARM64CallDescriptorLayoutIsNotEstablishedForThisABI => "The ARM64 call descriptor layout is not established for this ABI.",
            FatalJitErrorCode.InstructionTraversalRequiresDescriptorOwnership => "Instruction traversal requires descriptor ownership.",
            FatalJitErrorCode.InstructionTraversalRequiresSavedDescriptorStorage => "Instruction traversal requires saved descriptor storage.",
            FatalJitErrorCode.SavedInstructionGroupRequiresDescriptorStorage => "A saved instruction group requires descriptor storage.",
            FatalJitErrorCode.UnwindEmitterInstructionSizeQueriesRequireARM32 => "Unwind emitter instruction-size queries require ARM32.",
            FatalJitErrorCode.SingleIndirectInstructionRecordingRequiresXarch => "Single indirect instruction recording requires xarch.",
            FatalJitErrorCode.RegisterMemoryInstructionRecordingRequiresXarch => "Register-memory instruction recording requires xarch.",
            FatalJitErrorCode.RegisterMemoryImmediateInstructionRecordingRequiresXarch => "Register-memory-immediate instruction recording requires xarch.",
            FatalJitErrorCode.RegisterFieldImmediateInstructionRecordingRequiresXarch => "Register-field-immediate instruction recording requires xarch.",
            FatalJitErrorCode.AVX2GatherInstructionRecordingRequiresXarch => "AVX2 gather instruction recording requires xarch.",
            FatalJitErrorCode.ARM32ImmediateMemoryInstructionRecordingIsNotPorted => "ARM32 immediate memory instruction recording is not ported.",
            FatalJitErrorCode.ImmediateMemoryInstructionRecordingRequiresXarch => "Immediate memory instruction recording requires xarch.",
            FatalJitErrorCode.ARM32AbsoluteAddressInstructionRecordingRequiresAGeneralRegister => "ARM32 absolute-address instruction recording requires a general register.",
            FatalJitErrorCode.ARM32RelocatedAddressInstructionRecordingIsNotPorted => "ARM32 relocated-address instruction recording is not ported.",
            FatalJitErrorCode.AbsoluteAddressInstructionRecordingRequiresXarch => "Absolute-address instruction recording requires xarch.",
            FatalJitErrorCode.MemoryRegisterImmediateRecordingRequiresXarch => "Memory-register-immediate recording requires xarch.",
            FatalJitErrorCode.InstructionInputSizesAreNotAvailableForThisTarget => "Instruction input sizes are not available for this target.",
            FatalJitErrorCode.EVEXDisplacementCompressionIsNotAvailableForThisTarget => "EVEX displacement compression is not available for this target.",
            FatalJitErrorCode.ShortJumpPatchingIsNotSupportedForThisTarget => "Short jump patching is not supported for this target.",
            FatalJitErrorCode.LongJumpPatchingIsNotSupportedForThisTarget => "Long jump patching is not supported for this target.",
            FatalJitErrorCode.EpilogRecorderIsOnlySupportedByTheX86GCEncoder => "The epilog recorder is only supported by the x86 GC encoder.",
            FatalJitErrorCode.EmitterIsNotInitialized => "Emitter is not initialized.",
            FatalJitErrorCode.ExecutionCharacteristicsOutsideSupportedTargetsAreNotImplemented => "Execution characteristics outside supported targets are not implemented.",
            FatalJitErrorCode.XarchExecutionCharacteristicsAreNotAvailableForThisTarget => "xarch execution characteristics are not available for this target.",
            FatalJitErrorCode.NonDebugAlignmentPerformanceScoringRequiresLateDisassemblyPlacementMetadata => "Non-debug alignment performance scoring requires late-disassembly placement metadata.",
            FatalJitErrorCode.ExecutionCostEstimatesAreNotAvailableForThisTarget => "Execution-cost estimates are not available for this target.",
            FatalJitErrorCode.XarchInstructionLatencyDataIsNotAvailableForThisTarget => "xarch instruction latency data is not available for this target.",
            FatalJitErrorCode.XarchInstructionThroughputDataIsNotAvailableForThisTarget => "xarch instruction throughput data is not available for this target.",
            FatalJitErrorCode.XarchExecutionMemoryClassificationIsUnavailableOnThisTarget => "Xarch execution memory classification is unavailable on this target.",
            FatalJitErrorCode.RedundantComparisonQueriesOutsideXarchAreNotImplemented => "Redundant comparison queries outside xarch are not implemented.",
            FatalJitErrorCode.ZeroComparisonFlagQueriesOutsideXarchAreNotImplemented => "Zero-comparison flag queries outside xarch are not implemented.",
            FatalJitErrorCode.SignJumpFlagQueriesOutsideXarchAreNotImplemented => "Sign-jump flag queries outside xarch are not implemented.",
            FatalJitErrorCode.XarchInstructionFlagMetadataIsNotAvailableForThisTarget => "Xarch instruction-flag metadata is not available for this target.",
            FatalJitErrorCode.UnwindEmitterFragmentationRequiresARMLoongArch64OrRISCV64 => "Unwind emitter fragmentation requires ARM, LoongArch64, or RISC-V64.",
            FatalJitErrorCode.GCDeltaIndentationIsNotSupportedForThisTarget => "GC-delta indentation is not supported for this target.",
            FatalJitErrorCode.GCDeltaDiagnosticsRequireAnActiveCompiler => "GC delta diagnostics require an active compiler.",
            FatalJitErrorCode.GCDeltaDiagnosticsAreOnlyAvailableInDebugBuilds => "GC delta diagnostics are only available in Debug builds.",
            FatalJitErrorCode.GCTrackingDiagnosticsAreOnlyAvailableInDebugBuilds => "GC tracking diagnostics are only available in Debug builds.",
            FatalJitErrorCode.GCTrackingDiagnosticsRequireAnActiveCompiler => "GC tracking diagnostics require an active compiler.",
            FatalJitErrorCode.InstructionGroupDiagnosticsRequireAnActiveCompiler => "Instruction-group diagnostics require an active compiler.",
            FatalJitErrorCode.PlaceholderInstructionGroupRequiresPlaceholderData => "A placeholder instruction group requires placeholder data.",
            FatalJitErrorCode.JumpListDiagnosticsRequireDescriptorDebugInformation => "Jump-list diagnostics require descriptor debug information.",
            FatalJitErrorCode.InstructionGroupAddressRecordingRequiresXarchARM64RISCV64OrLoongArch64 => "Instruction-group address recording requires xarch, ARM64, RISC-V64, or LoongArch64.",
            FatalJitErrorCode.BackwardInstructionValidationRequiresDescriptorStorage => "Backward instruction validation requires descriptor storage.",
            FatalJitErrorCode.ImmediateOnlyInstructionRecordingRequiresXarch => "Immediate-only instruction recording requires xarch.",
            FatalJitErrorCode.X86StackDepthAdjustmentIsNotSupportedForThisTarget => "X86 stack-depth adjustment is not supported for this target.",
            FatalJitErrorCode.RegisterImmediateOutputRequiresXarch => "Register-immediate output requires xarch.",
            FatalJitErrorCode.EightByteImmediateEncodingRequiresAMD64 => "Eight-byte immediate encoding requires AMD64.",
            FatalJitErrorCode.EightByteImmediateOutputRequiresAMD64 => "Eight-byte immediate output requires AMD64.",
            FatalJitErrorCode.ImmediateOutputRequiresXarch => "Immediate output requires xarch.",
            FatalJitErrorCode.IndirectLoadRecordingRequiresXarch => "Indirect-load recording requires xarch.",
            FatalJitErrorCode.IndirectStoreRecordingRequiresXarch => "Indirect-store recording requires xarch.",
            FatalJitErrorCode.ContainedHardwareIntrinsicStoresRequireHardwareIntrinsicSupport => "Contained hardware-intrinsic stores require hardware-intrinsic support.",
            FatalJitErrorCode.EmitterGCDiagnosticStateIsOnlyAvailableInDebugBuilds => "Emitter GC diagnostic state is only available in Debug builds.",
            FatalJitErrorCode.LabelInstructionDescriptorsAreNotSupportedForThisTarget => "Label instruction descriptors are not supported for this target.",
            FatalJitErrorCode.LoongArch64BinaryInstructionRecordingIsUnusedAndNotPorted => "LoongArch64 binary instruction recording is unused and not ported.",
            FatalJitErrorCode.BinaryOperandRecordingIsNotSupportedForThisTarget => "Binary operand recording is not supported for this target.",
            FatalJitErrorCode.TernaryInstructionRecordingIsNotSupportedForThisTarget => "Ternary instruction recording is not supported for this target.",
            FatalJitErrorCode.EmitDispFrameRefUnusedOnLoongArch64 => "emitDispFrameRef-----unused on LoongArch64.",
            FatalJitErrorCode.EmitDispFrameRefUnimplementedUnusedOnRISCV64Yet => "emitDispFrameRef-----unimplemented/unused on RISCV64 yet----",
            FatalJitErrorCode.FrameReferenceDisplayIsNotImplementedForThisTarget => "Frame-reference display is not implemented for this target.",
            FatalJitErrorCode.StaticXarchReferenceDisplayRequiresXarch => "Static xarch reference display requires xarch.",
            FatalJitErrorCode.XarchInstructionFlagsAreUnavailableOnThisTarget => "xarch instruction flags are unavailable on this target.",
            FatalJitErrorCode.InstructionUpdateModesOutsideXarchAreNotImplemented => "Instruction update modes outside xarch are not implemented.",
            FatalJitErrorCode.InstructionFormatSelectionOutsideXarchIsNotImplemented => "Instruction format selection outside xarch is not implemented.",
            FatalJitErrorCode.InstructionSchedulingMetadataOutsideXarchIsNotImplemented => "Instruction scheduling metadata outside xarch is not implemented.",
            FatalJitErrorCode.APXInstructionClassificationOutsideXarchIsNotImplemented => "APX instruction classification outside xarch is not implemented.",
            FatalJitErrorCode.UnsupportedInstructionFormatTarget => "Unsupported instruction format target.",
            FatalJitErrorCode.InstructionGroupMustPrecedeTheNewGroup => "An instruction group must precede the new group.",
            FatalJitErrorCode.InstructionGroupsRequireAnActiveCompiler => "Instruction groups require an active compiler.",
            FatalJitErrorCode.XarchInstructionTupleInformationIsNotAvailableForThisTarget => "xarch instruction tuple information is not available for this target.",
            FatalJitErrorCode.XarchInstructionTupleDataIsNotAvailableForThisTarget => "xarch instruction tuple data is not available for this target.",
            FatalJitErrorCode.Signed20BitImmediatesAreNotSupportedForThisTarget => "Signed 20-bit immediates are not supported for this target.",
            FatalJitErrorCode.GeneralRegisterOrZeroValidationIsNotSupportedForThisTarget => "General-register-or-zero validation is not supported for this target.",
            FatalJitErrorCode.VectorIndexValidationIsNotSupportedForThisTarget => "Vector-index validation is not supported for this target.",
            FatalJitErrorCode.EmbeddedRoundingModesAreOnlySupportedOnXarch => "Embedded rounding modes are only supported on xarch.",
            FatalJitErrorCode.EVEXEmbeddedMaskingIsOnlySupportedOnXarch => "EVEX embedded masking is only supported on xarch.",
            FatalJitErrorCode.InstructionOutputIsNotImplementedForThisTarget => "Instruction output is not implemented for this target.",
            FatalJitErrorCode.Failure8ByteAPXImmediateOperandsRequireAMD64 => "8-byte APX immediate operands require AMD64.",
            FatalJitErrorCode.InstructionDisplayIsNotUsedOnLoongArch64 => "Instruction display is not used on LoongArch64.",
            FatalJitErrorCode.LoadStoreClassificationIsOnlySupportedOnARM32AndARM64 => "Load/store classification is only supported on ARM32 and ARM64.",
            FatalJitErrorCode.InstructionSanityCheckingIsNotImplementedForThisTarget => "Instruction sanity checking is not implemented for this target.",
            FatalJitErrorCode.InstructionDisplayIsNotImplementedForThisTarget => "Instruction display is not implemented for this target.",
            FatalJitErrorCode.ARM32InstructionClassificationIsOnlySupportedOnARM32 => "ARM32 instruction classification is only supported on ARM32.",
            FatalJitErrorCode.CurrentInstructionGroupIsRequired => "The current instruction group is required.",
            FatalJitErrorCode.XarchInstructionSizingRequiresAnXarchTarget => "Xarch instruction sizing requires an xarch target.",
            FatalJitErrorCode.WebAssemblyLocalStackStoreRecordingIsNotImplemented => "WebAssembly local-stack store recording is not implemented.",
            FatalJitErrorCode.TargetLocalStackStoreRecordingIsNotImplemented => "Target local-stack store recording is not implemented.",
            FatalJitErrorCode.UnboundLoongArch64JumpRequiresABasicBlockTarget => "An unbound LoongArch64 jump requires a basic-block target.",
            FatalJitErrorCode.LoongArch64JumpTargetHasNoInstructionGroupCookie => "A LoongArch64 jump target has no instruction-group cookie.",
            FatalJitErrorCode.JumpTargetBindingRequiresAnActiveCompiler => "Jump target binding requires an active compiler.",
            FatalJitErrorCode.BoundBranchRequiresAnInstructionGroupTarget => "A bound branch requires an instruction-group target.",
            FatalJitErrorCode.FuncletBranchCheckingRequiresAnActiveCompiler => "Funclet branch checking requires an active compiler.",
            FatalJitErrorCode.FuncletBranchValidationRequiresDebugInformation => "Funclet branch validation requires debug information.",
            FatalJitErrorCode.ARMJumpFormatClassificationIsNotSupportedForThisTarget => "ARM jump-format classification is not supported for this target.",
            FatalJitErrorCode.ARMLabelLoadClassificationIsNotSupportedForThisTarget => "ARM label-load classification is not supported for this target.",
            FatalJitErrorCode.UnconditionalJumpClassificationIsNotSupportedForThisTarget => "Unconditional jump classification is not supported for this target.",
            FatalJitErrorCode.RISCVJumpClassificationIsNotSupportedForThisTarget => "RISC-V jump classification is not supported for this target.",
            FatalJitErrorCode.CompareJumpClassificationIsNotSupportedForThisTarget => "Compare-jump classification is not supported for this target.",
            FatalJitErrorCode.RISCVCompareJumpClassificationIsNotSupportedForThisTarget => "RISC-V compare-jump classification is not supported for this target.",
            FatalJitErrorCode.MediumJumpSelectionIsNotSupportedForThisTarget => "Medium-jump selection is not supported for this target.",
            FatalJitErrorCode.ARM64ConstantLoadClassificationIsNotSupportedForThisTarget => "ARM64 constant-load classification is not supported for this target.",
            FatalJitErrorCode.WasmHasNoShortJumpInstructionForm => "Wasm has no short-jump instruction form.",
            FatalJitErrorCode.NonXarchShortJumpSelectionIsNotPorted => "Non-xarch short-jump selection is not ported.",
            FatalJitErrorCode.ShortJumpSelectionIsImplementedByTheLoongArch64Binder => "Short-jump selection is implemented by the LoongArch64 binder.",
            FatalJitErrorCode.JumpDistanceBindingRequiresAnActiveCompiler => "Jump distance binding requires an active compiler.",
            FatalJitErrorCode.UnknownARMJumpInstruction => "Unknown ARM jump instruction.",
            FatalJitErrorCode.UnknownARM64JumpInstruction => "Unknown ARM64 jump instruction.",
            FatalJitErrorCode.UnknownRISCVJumpInstruction => "Unknown RISC-V jump instruction.",
            FatalJitErrorCode.JumpDistanceBindingIsNotSupportedForThisTarget => "Jump distance binding is not supported for this target.",
            FatalJitErrorCode.ShortJumpSizeUpdateIsNotSupportedForThisTarget => "Short-jump size update is not supported for this target.",
            FatalJitErrorCode.ReverseJumpKindMappingIsNotSupportedForThisTarget => "Reverse jump-kind mapping is not supported for this target.",
            FatalJitErrorCode.ARMJumpKindMappingIsNotSupportedForThisTarget => "ARM jump-kind mapping is not supported for this target.",
            FatalJitErrorCode.BasicBlockLabelAddressRecordingIsNotSupportedForThisTarget => "Basic-block label address recording is not supported for this target.",
            FatalJitErrorCode.DataLabelAddressRecordingIsNotSupportedForThisTarget => "Data-label address recording is not supported for this target.",
            FatalJitErrorCode.ConditionalRegisterBranchesAreNotSupportedForThisTarget => "Conditional register branches are not supported for this target.",
            FatalJitErrorCode.LabelJumpInstructionRecordingIsNotSupportedForThisTarget => "Label jump instruction recording is not supported for this target.",
            FatalJitErrorCode.RISCVJumpRecordingIsNotSupportedForThisTarget => "RISC-V jump recording is not supported for this target.",
            FatalJitErrorCode.RISCVJumpRecordingRequiresAnActiveCompiler => "RISC-V jump recording requires an active compiler.",
            FatalJitErrorCode.RISCVJumpRecordingRequiresACurrentBasicBlock => "RISC-V jump recording requires a current basic block.",
            FatalJitErrorCode.XarchShortJumpSelectionIsNotSupportedForThisTarget => "Xarch short-jump selection is not supported for this target.",
            FatalJitErrorCode.JumpKindInstructionMappingIsNotSupportedForThisTarget => "Jump-kind instruction mapping is not supported for this target.",
            FatalJitErrorCode.JumpToNextInstructionRemovalIsOnlyAvailableForXarch => "Jump-to-next-instruction removal is only available for xarch.",
            FatalJitErrorCode.ZeroOperandInstructionRecordingIsUnsupportedForThisTargetArgument => "Zero-operand instruction recording is unsupported for this target ({0}).",
            FatalJitErrorCode.LoongArch64EmitterOperationsRequireLoongArch64 => "LoongArch64 emitter operations require LoongArch64.",
            FatalJitErrorCode.LocalStoreRecordingRequiresXarch => "Local store recording requires xarch.",
            FatalJitErrorCode.AlignedGroupRequiresAFinalAlignmentDescriptor => "An aligned group requires a final alignment descriptor.",
            FatalJitErrorCode.LoopAlignmentIsUnsupportedOnThisTarget => "Loop alignment is unsupported on this target.",
            FatalJitErrorCode.MemoryBarrierCoalescingIsNotSupportedForThisTarget => "Memory-barrier coalescing is not supported for this target.",
            FatalJitErrorCode.ARM64StaticFieldImmediateInstructionRecordingIsNotPorted => "ARM64 static-field immediate instruction recording is not ported.",
            FatalJitErrorCode.ARM32StaticFieldImmediateInstructionRecordingIsNotPorted => "ARM32 static-field immediate instruction recording is not ported.",
            FatalJitErrorCode.StaticFieldImmediateInstructionRecordingRequiresXarch => "Static-field immediate instruction recording requires xarch.",
            FatalJitErrorCode.StaticFieldRegisterImmediateInstructionRecordingRequiresXarch => "Static-field register-immediate instruction recording requires xarch.",
            FatalJitErrorCode.RegisterStackImmediateInstructionRecordingRequiresXarch => "Register-stack-immediate instruction recording requires xarch.",
            FatalJitErrorCode.MemoryOperandFormatMappingRequiresXarch => "Memory operand format mapping requires xarch.",
            FatalJitErrorCode.MemoryOperandDescriptorInitializationRequiresXarch => "Memory operand descriptor initialization requires xarch.",
            FatalJitErrorCode.InstructionMemoryFormatMappingRequiresXarch => "Instruction memory format mapping requires xarch.",
            FatalJitErrorCode.AddressScalingIsOnlySupportedOnXarch => "Address scaling is only supported on xarch.",
            FatalJitErrorCode.ARM64StaticFieldInstructionRecordingIsNotPorted => "ARM64 static-field instruction recording is not ported.",
            FatalJitErrorCode.ARM32StaticFieldInstructionRecordingIsNotPorted => "ARM32 static-field instruction recording is not ported.",
            FatalJitErrorCode.SingleStaticFieldInstructionRecordingRequiresXarch => "Single static-field instruction recording requires xarch.",
            FatalJitErrorCode.UnaryMemoryInstructionRecordingRequiresXarch => "Unary memory instruction recording requires xarch.",
            FatalJitErrorCode.TwoRegisterMemoryInstructionRecordingRequiresXarch => "Two-register memory instruction recording requires xarch.",
            FatalJitErrorCode.TwoRegisterMemoryImmediateRecordingRequiresXarch => "Two-register memory-immediate recording requires xarch.",
            FatalJitErrorCode.RegisterAddressRegisterInstructionRecordingRequiresXarch => "Register-address-register instruction recording requires xarch.",
            FatalJitErrorCode.ARM32FourRegisterInstructionOptionsAreNotPorted => "ARM32 four-register instruction options are not ported.",
            FatalJitErrorCode.FourRegisterInstructionRecordingRequiresXarch => "Four-register instruction recording requires xarch.",
            FatalJitErrorCode.AddressTwoRegisterInstructionRecordingRequiresXarch => "Address/two-register instruction recording requires xarch.",
            FatalJitErrorCode.RegisterRegisterInstructionSizingRequiresXarch => "Register-register instruction sizing requires xarch.",
            FatalJitErrorCode.RegisterImmediateInstructionSizingRequiresXarch => "Register-immediate instruction sizing requires xarch.",
            FatalJitErrorCode.TwoRegisterStaticFieldInstructionRecordingRequiresXarch => "Two-register static-field instruction recording requires xarch.",
            FatalJitErrorCode.TwoRegisterStackInstructionRecordingRequiresXarch => "Two-register stack instruction recording requires xarch.",
            FatalJitErrorCode.TwoRegisterFieldImmediateRecordingRequiresXarch => "Two-register field-immediate recording requires xarch.",
            FatalJitErrorCode.TwoRegisterStackImmediateRecordingRequiresXarch => "Two-register stack-immediate recording requires xarch.",
            FatalJitErrorCode.RegisterFieldRegisterInstructionRecordingRequiresXarch => "Register-field-register instruction recording requires xarch.",
            FatalJitErrorCode.RegisterStackRegisterInstructionRecordingRequiresXarch => "Register-stack-register instruction recording requires xarch.",
            FatalJitErrorCode.TwoRegisterImmediateInstructionRecordingRequiresXarch => "Two-register-immediate instruction recording requires xarch.",
            FatalJitErrorCode.IllegalInstructionWithinRISCVTwoRegisterImmediateInstructionRecording => "Illegal instruction within RISC-V two-register-immediate instruction recording.",
            FatalJitErrorCode.IllegalInstructionWithinRISCVRegisterTwoImmediateInstructionRecording => "Illegal instruction within RISC-V register-two-immediate instruction recording.",
            FatalJitErrorCode.ARM32ThreeRegisterInstructionOptionsAreNotPorted => "ARM32 three-register instruction options are not ported.",
            FatalJitErrorCode.IllegalInstructionWithinRISCVThreeRegisterInstructionRecording => "Illegal instruction within RISC-V three-register instruction recording.",
            FatalJitErrorCode.ThreeRegisterInstructionRecordingRequiresXarch => "Three-register instruction recording requires xarch.",
            FatalJitErrorCode.ThreeRegisterImmediateInstructionRecordingRequiresXarch => "Three-register-immediate instruction recording requires xarch.",
            FatalJitErrorCode.ThreeOperandIMULSelectionRequiresX86ArchitectureSupport => "Three-operand IMUL selection requires x86 architecture support.",
            FatalJitErrorCode.ThreeOperandIMULRegisterLayoutIsNotAvailableForThisTarget => "Three-operand IMUL register layout is not available for this target.",
            FatalJitErrorCode.DataSizePrefixOutputRequiresXarch => "Data-size prefix output requires xarch.",
            FatalJitErrorCode.NOPOutputRequiresXarch => "NOP output requires xarch.",
            FatalJitErrorCode.LoopAlignmentOutputRequiresXarch => "Loop-alignment output requires xarch.",
            FatalJitErrorCode.AlignmentDescriptorRequiresAFollowingLoopGroup => "An alignment descriptor requires a following loop group.",
            FatalJitErrorCode.TargetRegisterInstructionRecordingIsNotImplemented => "Target register instruction recording is not implemented.",
            FatalJitErrorCode.TargetTwoRegisterImmediateInstructionRecordingIsNotImplemented => "Target two-register-immediate instruction recording is not implemented.",
            FatalJitErrorCode.TargetRegisterImmediateInstructionRecordingIsNotImplemented => "Target register-immediate instruction recording is not implemented.",
            FatalJitErrorCode.AddressConstantMaterializationIsNotImplementedForThisTarget => "Address constant materialization is not implemented for this target.",
            FatalJitErrorCode.RISCV64AddressConstantMaterializationIsNotPorted => "RISC-V64 address constant materialization is not ported.",
            FatalJitErrorCode.LastInstructionCallTrackingIsNotSupportedForThisTarget => "Last-instruction call tracking is not supported for this target.",
            FatalJitErrorCode.PreEpilogNopInsertionIsNotSupportedForThisTarget => "Pre-epilog nop insertion is not supported for this target.",
            FatalJitErrorCode.InstructionRecordingOutsideAMD64IsNotPorted => "Instruction recording outside AMD64 is not ported.",
            FatalJitErrorCode.ExitSequenceRecordingIsNotImplementedForThisTarget => "Exit sequence recording is not implemented for this target.",
            FatalJitErrorCode.GeneralRegisterClassificationIsNotSupportedForThisTarget => "General-register classification is not supported for this target.",
            FatalJitErrorCode.ARM32DoubleRegisterValidationIsNotAvailableForThisTarget => "ARM32 double-register validation is not available for this target.",
            FatalJitErrorCode.EVEXBroadcastOptionsAreNotAvailableForThisTarget => "EVEX broadcast options are not available for this target.",
            FatalJitErrorCode.EVEXEmbeddedRoundingIsNotAvailableForThisTarget => "EVEX embedded rounding is not available for this target.",
            FatalJitErrorCode.EVEXNDDOptionsAreNotAvailableForThisTarget => "EVEX NDD options are not available for this target.",
            FatalJitErrorCode.EVEXNFOptionsAreNotAvailableForThisTarget => "EVEX NF options are not available for this target.",
            FatalJitErrorCode.APXPPXOptionsAreNotAvailableForThisTarget => "APX PPX options are not available for this target.",
            FatalJitErrorCode.EVEXDFVOptionsRequireAMD64 => "EVEX DFV options require AMD64.",
            FatalJitErrorCode.InstructionEncodabilityChecksRequireXarch => "Instruction encodability checks require xarch.",
            FatalJitErrorCode.VEXOutputRequiresXarch => "VEX output requires xarch.",
            FatalJitErrorCode.XarchRegisterRegisterSizingIsNotAvailableForThisTarget => "xarch register/register sizing is not available for this target.",
            FatalJitErrorCode.ARM32RegisterImmediateInstructionOptionsAreNotPorted => "ARM32 register-immediate instruction options are not ported.",
            FatalJitErrorCode.ARM32RegisterImmediateRecordingDoesNotAcceptATargetHandle => "ARM32 register-immediate recording does not accept a target handle.",
            FatalJitErrorCode.RegisterImmediateInstructionRecordingRequiresXarch => "Register-immediate instruction recording requires xarch.",
            FatalJitErrorCode.ARM32RegisterImmediateInstructionFlagsAreNotSupportedForThisTarget => "ARM32 register-immediate instruction flags are not supported for this target.",
            FatalJitErrorCode.ARM32RegisterRegisterInstructionOptionsAreNotPorted => "ARM32 register-register instruction options are not ported.",
            FatalJitErrorCode.RISCVRegisterRegisterInstructionOptionsAreNotImplemented => "RISC-V register-register instruction options are not implemented.",
            FatalJitErrorCode.RegisterRegisterInstructionRecordingRequiresXarch => "Register-register instruction recording requires xarch.",
            FatalJitErrorCode.APXNDDMoveRecordingRequiresXarch => "APX NDD move recording requires xarch.",
            FatalJitErrorCode.RegisterMoveRecordingRequiresXarch => "Register move recording requires xarch.",
            FatalJitErrorCode.ARM32RegisterMoveFlagsAreNotSupportedForThisTarget => "ARM32 register move flags are not supported for this target.",
            FatalJitErrorCode.MoveSignExtensionOptimizationRequiresXarch => "Move sign-extension optimization requires xarch.",
            FatalJitErrorCode.MoveElisionIsNotImplementedForThisTarget => "Move elision is not implemented for this target.",
            FatalJitErrorCode.RegisterDisplayRequiresAnActiveCompiler => "Register display requires an active compiler.",
            FatalJitErrorCode.InvalidARM64StackRegisterSizeArgument => "Invalid ARM64 stack register size {0}.",
            FatalJitErrorCode.InvalidARM64ZeroRegisterSizeArgument => "Invalid ARM64 zero register size {0}.",
            FatalJitErrorCode.InvalidARM64GeneralRegisterSizeArgument => "Invalid ARM64 general register size {0}.",
            FatalJitErrorCode.InvalidARM64RegisterArgument => "Invalid ARM64 register {0}.",
            FatalJitErrorCode.InvalidARM64VectorRegisterSizeArgument => "Invalid ARM64 vector register size {0}.",
            FatalJitErrorCode.EmitterRegisterNamesOutsideXarchAreNotImplemented => "Emitter register names outside xarch are not implemented.",
            FatalJitErrorCode.VectorRegisterNamesAreOnlySupportedOnARM64 => "Vector register names are only supported on ARM64.",
            FatalJitErrorCode.FloatingRegisterNamesAreOnlySupportedOnARM32 => "Floating register names are only supported on ARM32.",
            FatalJitErrorCode.FloatingRegisterDisplayRequiresAnActiveCompiler => "Floating register display requires an active compiler.",
            FatalJitErrorCode.XMMRegisterNamesAreOnlySupportedOnXarch => "XMM register names are only supported on xarch.",
            FatalJitErrorCode.YMMRegisterNamesAreOnlySupportedOnXarch => "YMM register names are only supported on xarch.",
            FatalJitErrorCode.ZMMRegisterNamesAreOnlySupportedOnXarch => "ZMM register names are only supported on xarch.",
            FatalJitErrorCode.XarchRegisterEncodingIsNotAvailableForThisTarget => "xarch register encoding is not available for this target.",
            FatalJitErrorCode.REXXOutputRequiresAMD64 => "REX.X output requires AMD64.",
            FatalJitErrorCode.REXBOutputRequiresAMD64 => "REX.B output requires AMD64.",
            FatalJitErrorCode.REXOutputRequiresAMD64 => "REX output requires AMD64.",
            FatalJitErrorCode.RegisterOutputRequiresXarch => "Register output requires xarch.",
            FatalJitErrorCode.APXRegisterOutputRequiresAMD64 => "APX register output requires AMD64.",
            FatalJitErrorCode.RegisterPairOutputRequiresXarch => "Register-pair output requires xarch.",
            FatalJitErrorCode.EightByteRegisterPairOutputRequiresAMD64 => "Eight-byte register-pair output requires AMD64.",
            FatalJitErrorCode.ThreeRegisterOutputRequiresXarch => "Three-register output requires xarch.",
            FatalJitErrorCode.EightByteThreeRegisterOutputRequiresAMD64 => "Eight-byte three-register output requires AMD64.",
            FatalJitErrorCode.XarchPrefixOutputIsNotAvailableForThisTarget => "xarch prefix output is not available for this target.",
            FatalJitErrorCode.REX2OutputRequiresAMD64 => "REX2 output requires AMD64.",
            FatalJitErrorCode.CCMPAndCTESTOutputRequiresAMD64 => "CCMP and CTEST output requires AMD64.",
            FatalJitErrorCode.InvalidCCMPOrCTESTCondition => "Invalid CCMP or CTEST condition.",
            FatalJitErrorCode.EVEXOutputRequiresXarch => "EVEX output requires xarch.",
            FatalJitErrorCode.InvalidEVEXEmbeddedRoundingMode => "Invalid EVEX embedded rounding mode.",
            FatalJitErrorCode.REXROutputRequiresAMD64 => "REX.R output requires AMD64.",
            FatalJitErrorCode.EVEXRPrimeOutputRequiresAMD64 => "EVEX R-prime output requires AMD64.",
            FatalJitErrorCode.XarchGCRegisterClassificationIsNotAvailableForThisTarget => "xarch GC register classification is not available for this target.",
            FatalJitErrorCode.XarchGCRegisterOutputIsNotAvailableForThisTarget => "xarch GC register output is not available for this target.",
            FatalJitErrorCode.PeepholeIterationRequiresALastInstruction => "Peephole iteration requires a last instruction.",
            FatalJitErrorCode.InstructionWriteClassificationRequiresXarch => "Instruction write classification requires xarch.",
            FatalJitErrorCode.ReadModifyWriteInstructionRecordingRequiresXarch => "Read-modify-write instruction recording requires xarch.",
            FatalJitErrorCode.UnaryReadModifyWriteInstructionRecordingRequiresXarch => "Unary read-modify-write instruction recording requires xarch.",
            FatalJitErrorCode.IndexedAddressInstructionRecordingRequiresXarch => "Indexed-address instruction recording requires xarch.",
            FatalJitErrorCode.BaseRegisterImmediateInstructionRecordingRequiresXarch => "Base register-immediate instruction recording requires xarch.",
            FatalJitErrorCode.XarchShiftInstructionClassificationIsNotSupportedForThisTarget => "Xarch shift instruction classification is not supported for this target.",
            FatalJitErrorCode.FixedSizePrologJumpsAreNotAvailableForWebAssembly => "Fixed-size prolog jumps are not available for WebAssembly.",
            FatalJitErrorCode.SIMDConstantEmissionRequiresSIMDSupport => "SIMD constant emission requires SIMD support.",
            FatalJitErrorCode.CompressedSIMDConstantLoadsRequireSIMDSupportOnXarch => "Compressed SIMD constant loads require SIMD support on xarch.",
            FatalJitErrorCode.SIMDMaskConstantEmissionRequiresMaskedHardwareIntrinsicSupport => "SIMD mask constant emission requires masked hardware intrinsic support.",
            FatalJitErrorCode.TwoRegisterBaseAddressInstructionRecordingRequiresAMD64 => "Two-register base-address instruction recording requires AMD64.",
            FatalJitErrorCode.SIMDRegisterImmediateRecordingRequiresXarch => "SIMD register-immediate recording requires xarch.",
            FatalJitErrorCode.SIMDMemoryInstructionRecordingRequiresXarch => "SIMD memory instruction recording requires xarch.",
            FatalJitErrorCode.SIMDStackInstructionRecordingRequiresXarch => "SIMD stack instruction recording requires xarch.",
            FatalJitErrorCode.SIMDAddressImmediateRecordingRequiresXarch => "SIMD address-immediate recording requires xarch.",
            FatalJitErrorCode.SIMDFieldImmediateRecordingRequiresXarch => "SIMD field-immediate recording requires xarch.",
            FatalJitErrorCode.SIMDStackImmediateRecordingRequiresXarch => "SIMD stack-immediate recording requires xarch.",
            FatalJitErrorCode.SIMDMultioperandRecordingRequiresXarch => "SIMD multioperand recording requires xarch.",
            FatalJitErrorCode.SIMDMultioperandRecordingRequiresHardwareIntrinsics => "SIMD multioperand recording requires hardware intrinsics.",
            FatalJitErrorCode.AVX512BlendClassificationIsNotImplementedForThisTarget => "AVX-512 blend classification is not implemented for this target.",
            FatalJitErrorCode.X86AVX512BlendClassificationWithoutHardwareIntrinsicsIsNotPorted => "x86 AVX-512 blend classification without hardware intrinsics is not ported.",
            FatalJitErrorCode.AVXBlendClassificationIsNotImplementedForThisTarget => "AVX blend classification is not implemented for this target.",
            FatalJitErrorCode.X86AVXBlendClassificationWithoutHardwareIntrinsicsIsNotPorted => "x86 AVX blend classification without hardware intrinsics is not ported.",
            FatalJitErrorCode.SSE41BlendClassificationIsNotImplementedForThisTarget => "SSE4.1 blend classification is not implemented for this target.",
            FatalJitErrorCode.SSE41BlendClassificationWithoutHardwareIntrinsicsIsNotPorted => "SSE4.1 blend classification without hardware intrinsics is not ported.",
            FatalJitErrorCode.SIMDRegisterInstructionRecordingRequiresXarch => "SIMD register instruction recording requires xarch.",
            FatalJitErrorCode.SIMDRegisterImmediateInstructionRecordingRequiresXarch => "SIMD register-immediate instruction recording requires xarch.",
            FatalJitErrorCode.LoongArch64JumpRecordingRequiresAnActiveCompiler => "LoongArch64 jump recording requires an active compiler.",
            FatalJitErrorCode.LoongArch64JumpRecordingRequiresACurrentBasicBlock => "LoongArch64 jump recording requires a current basic block.",
            FatalJitErrorCode.TargetConditionalBranchRecordingIsNotImplemented => "Target conditional-branch recording is not implemented.",
            FatalJitErrorCode.LoongArch64ConditionalBranchRecordingRequiresLoongArch64 => "LoongArch64 conditional-branch recording requires LoongArch64.",
            FatalJitErrorCode.RelocatedAddressInstructionRecordingRequiresAnActiveCompiler => "Relocated-address instruction recording requires an active compiler.",
            FatalJitErrorCode.AddressRangeChecksRequireAnActiveCompiler => "Address range checks require an active compiler.",
            FatalJitErrorCode.StackDepthTrackingIsDisabledForThisTarget => "Stack-depth tracking is disabled for this target.",
            FatalJitErrorCode.CurrentInstructionGroupIsRequired2 => "A current instruction group is required.",
            FatalJitErrorCode.MoveInstructionClassificationIsNotSupportedForThisTarget => "Move-instruction classification is not supported for this target.",
            FatalJitErrorCode.MOVSXDSideEffectClassificationIsOnlySupportedOn64BitXarch => "MOVSXD side-effect classification is only supported on 64-bit xarch.",
            FatalJitErrorCode.XarchStackMoveOptimizationIsNotSupportedForThisTarget => "Xarch stack-move optimization is not supported for this target.",
            FatalJitErrorCode.ARM64StackInstructionRecordingIsNotPorted => "ARM64 stack instruction recording is not ported.",
            FatalJitErrorCode.ARM32StackInstructionRecordingIsNotPorted => "ARM32 stack instruction recording is not ported.",
            FatalJitErrorCode.StackInstructionRecordingRequiresXarch => "Stack instruction recording requires xarch.",
            FatalJitErrorCode.ARM64StackImmediateInstructionRecordingIsNotPorted => "ARM64 stack-immediate instruction recording is not ported.",
            FatalJitErrorCode.ARM32StackImmediateInstructionRecordingIsNotPorted => "ARM32 stack-immediate instruction recording is not ported.",
            FatalJitErrorCode.StackImmediateInstructionRecordingRequiresXarch => "Stack-immediate instruction recording requires xarch.",
            FatalJitErrorCode.StaticFieldInstructionSizingRequiresAMD64 => "Static-field instruction sizing requires AMD64.",
            FatalJitErrorCode.ARM32StaticFieldRegisterInstructionRecordingIsNotPorted => "ARM32 static-field register instruction recording is not ported.",
            FatalJitErrorCode.StaticFieldRegisterStoresRequireXarch => "Static-field register stores require xarch.",
            FatalJitErrorCode.ARM32StaticFieldInstructionOptionsAreNotPorted => "ARM32 static-field instruction options are not ported.",
            FatalJitErrorCode.JitDataOffsetStaticFieldsAreNotPorted => "JitDataOffset static fields are not ported.",
            FatalJitErrorCode.ThreadLocalStorageStaticFieldsAreNotPorted => "Thread-Local-Storage static fields are not ported.",
            FatalJitErrorCode.NormalStaticFieldInstructionRecordingIsNotPorted => "Normal static-field instruction recording is not ported.",
            FatalJitErrorCode.ARM32StaticFieldInstructionRecordingRequiresAGeneralRegister => "ARM32 static-field instruction recording requires a general register.",
            FatalJitErrorCode.StaticFieldRegisterInstructionRecordingRequiresXarch => "Static-field register instruction recording requires xarch.",
            FatalJitErrorCode.SIMDStaticFieldInstructionRecordingRequiresXarch => "SIMD static-field instruction recording requires xarch.",
            FatalJitErrorCode.NativeEmitterLayoutReportingRequiresTheTargetSpecificPointerRegisterMaskAndDescriptorLayouts => "Native emitter layout reporting requires the target-specific pointer, register-mask and descriptor layouts.",
            FatalJitErrorCode.EmitterStatsOutsideXarchArmAndArm64IsNotPorted => "emitterStats outside xarch, arm, and arm64 is not ported.",
            FatalJitErrorCode.TargetLocalStackInstructionRecordingIsNotImplemented => "Target local-stack instruction recording is not implemented.",
            FatalJitErrorCode.LoongArch64EmbeddedDataRecordingRequiresALoongArch64Target => "LoongArch64 embedded-data recording requires a LoongArch64 target.",
            FatalJitErrorCode.LoongArch64EmbeddedDataRecordingRequiresAnActiveCompiler => "LoongArch64 embedded-data recording requires an active compiler.",
            FatalJitErrorCode.RISCVEmbeddedDataRecordingRequiresARISCV64Target => "RISC-V embedded-data recording requires a RISC-V64 target.",
            FatalJitErrorCode.InstructionByteDisplayIsNotImplementedForTheActiveTarget => "Instruction byte display is not implemented for the active target.",
            FatalJitErrorCode.TargetInstructionStoreClassificationIsNotImplemented => "Target instruction store classification is not implemented.",
            FatalJitErrorCode.WasmStoreClassificationIsUnreachableForInstructionArgument => "Wasm store classification is unreachable for instruction {0}.",
            FatalJitErrorCode.SingleRegisterInstructionRecordingRequiresXarch => "Single-register instruction recording requires xarch.",
            FatalJitErrorCode.BaseUnaryInstructionRecordingRequiresXarch => "Base unary instruction recording requires xarch.",
            FatalJitErrorCode.APXEVEXZuInstructionRecordingRequiresAMD64 => "APX EVEX.zu instruction recording requires AMD64.",
            FatalJitErrorCode.UnknownSizeFrameRecordingRequiresSIMDEnabledARM64 => "Unknown-size frame recording requires SIMD-enabled ARM64.",
            FatalJitErrorCode.ARM64FrameCountCannotBeEncodedByASingleMove => "The ARM64 frame count cannot be encoded by a single move.",
            FatalJitErrorCode.UnsupportedARM64UnknownSizeFrameImmediateInstruction => "Unsupported ARM64 unknown-size-frame immediate instruction.",
            FatalJitErrorCode.UnsupportedARM64UnknownSizeFrameRegisterPairInstruction => "Unsupported ARM64 unknown-size-frame register-pair instruction.",
            FatalJitErrorCode.VarargsShadowSlotHomingRequiresXarch => "Varargs shadow-slot homing requires xarch.",
            FatalJitErrorCode.ARM64EmitterOperationsAreNotSupportedForThisTarget => "ARM64 emitter operations are not supported for this target.",
            FatalJitErrorCode.ARM32EmitterOperationsAreNotSupportedForThisTarget => "ARM32 emitter operations are not supported for this target.",
            FatalJitErrorCode.RISCV64EmitterOperationsAreNotSupportedForThisTarget => "RISC-V64 emitter operations are not supported for this target.",
            FatalJitErrorCode.ARM32LocalFramePointerDescriptorFlagsAreOnlySupportedOnARM32 => "ARM32 local-frame-pointer descriptor flags are only supported on ARM32.",
            FatalJitErrorCode.LocalVariableInstructionDescriptorsAreNotSupportedForThisTarget => "Local-variable instruction descriptors are not supported for this target.",
            FatalJitErrorCode.CallDescriptorLayoutIsNotPortedForThisTarget => "The call descriptor layout is not ported for this target.",
            FatalJitErrorCode.InstructionDescriptorConstantsAreNotSupportedForThisTarget => "Instruction descriptor constants are not supported for this target.",
            FatalJitErrorCode.ConstantDescriptorSizeIsNotPortedForThisTarget => "Constant descriptor size is not ported for this target.",
            FatalJitErrorCode.DisplacementDescriptorSizeIsNotPortedForThisTarget => "Displacement descriptor size is not ported for this target.",
            FatalJitErrorCode.ConstantDisplacementDescriptorSizeIsNotPortedForThisTarget => "Constant/displacement descriptor size is not ported for this target.",
            FatalJitErrorCode.AddressModeDescriptorSizeIsNotPortedForThisTarget => "Address-mode descriptor size is not ported for this target.",
            FatalJitErrorCode.ConstantAddressModeDescriptorSizeIsNotPortedForThisTarget => "Constant/address-mode descriptor size is not ported for this target.",
            FatalJitErrorCode.InstructionFormatsAreNotSupportedForThisTarget => "Instruction formats are not supported for this target.",
            FatalJitErrorCode.ExplicitInstructionSizesAreNotSupportedForThisTarget => "Explicit instruction sizes are not supported for this target.",
            FatalJitErrorCode.InstructionEncodingStorageIsNotSupportedForThisTarget => "Instruction encoding storage is not supported for this target.",
            FatalJitErrorCode.LoongArch64JumpOffsetStorageIsNotSupportedForThisTarget => "LoongArch64 jump-offset storage is not supported for this target.",
            FatalJitErrorCode.TargetSpecificInstructionSizeIsNotSupportedForThisTarget => "Target-specific instruction size is not supported for this target.",
            FatalJitErrorCode.RegisterPointerCallDescriptorsAreNotSupportedForThisTarget => "Register-pointer call descriptors are not supported for this target.",
            FatalJitErrorCode.ARMRelocationDescriptorSizeIsNotSupportedForThisTarget => "ARM relocation descriptor size is not supported for this target.",
            FatalJitErrorCode.ARM64EmptyAlignmentDescriptorsAreNotSupportedForThisTarget => "ARM64 empty-alignment descriptors are not supported for this target.",
            FatalJitErrorCode.PreviousInstructionSizesAreOnlySupportedOnXarch => "Previous-instruction sizes are only supported on xarch.",
            FatalJitErrorCode.WebAssemblyInstructionDescriptorsAreNotSupportedForThisTarget => "WebAssembly instruction descriptors are not supported for this target.",
            FatalJitErrorCode.AlignDescriptorSizeIsNotYetPortedForThisTarget => "Align descriptor size is not yet ported for this target.",
            FatalJitErrorCode.XarchAlignmentPaddingIsUnsupportedOnThisTarget => "Xarch alignment padding is unsupported on this target.",
            FatalJitErrorCode.JumpDescriptorSizeIsNotYetPortedForThisTarget => "Jump descriptor size is not yet ported for this target.",
            FatalJitErrorCode.XarchOpcodeTableIsNotAvailableForThisTarget => "xarch opcode table is not available for this target.",
            FatalJitErrorCode.AbsoluteXarchRegisterNumberingIsNotAvailableForThisTarget => "Absolute xarch register numbering is not available for this target.",
            FatalJitErrorCode.InstructionGroupDebugDataIsOnlyAvailableInDebugBuilds => "Instruction-group debug data is only available in Debug builds.",
            FatalJitErrorCode.ContainedARM32IndirectHasNoBaseRegister => "Contained ARM32 indirect has no base register.",
            FatalJitErrorCode.ThumbBranchOutputIsOnlySupportedOnARM32 => "Thumb branch output is only supported on ARM32.",
            FatalJitErrorCode.ThumbCBZCBNZOutputRequiresAJumpDescriptor => "Thumb CBZ/CBNZ output requires a jump descriptor.",
            FatalJitErrorCode.UnknownThumbShortBranchFormat => "Unknown Thumb short-branch format.",
            FatalJitErrorCode.ARM32CallRecordingIsOnlySupportedOnARM32 => "ARM32 call recording is only supported on ARM32.",
            FatalJitErrorCode.ARM32ConditionalRegisterBranchesAreNotAvailableForThisTarget => "ARM32 conditional-register branches are not available for this target.",
            FatalJitErrorCode.ARM32DataLabelAddressRecordingIsNotAvailableForThisTarget => "ARM32 data-label address recording is not available for this target.",
            FatalJitErrorCode.InstructionDisplayRequiresDescriptorDebugInformation => "Instruction display requires descriptor debug information.",
            FatalJitErrorCode.ARM32LabelTableContainsANullBlock => "An ARM32 label table contains a null block.",
            FatalJitErrorCode.BoundARM32LabelTableContainsAnUnboundBlock => "A bound ARM32 label table contains an unbound block.",
            FatalJitErrorCode.BoundARM32LabelInstructionHasNoJumpDescriptor => "A bound ARM32 label instruction has no jump descriptor.",
            FatalJitErrorCode.BoundARM32LabelInstructionHasNoInstructionGroup => "A bound ARM32 label instruction has no instruction group.",
            FatalJitErrorCode.UnboundARM32LabelInstructionHasNoJumpDescriptor => "An unbound ARM32 label instruction has no jump descriptor.",
            FatalJitErrorCode.UnboundARM32LabelInstructionHasNoBasicBlock => "An unbound ARM32 label instruction has no basic block.",
            FatalJitErrorCode.BoundARM32BranchHasNoJumpDescriptor => "A bound ARM32 branch has no jump descriptor.",
            FatalJitErrorCode.BoundARM32BranchHasNoInstructionGroup => "A bound ARM32 branch has no instruction group.",
            FatalJitErrorCode.UnboundARM32BranchHasNoJumpDescriptor => "An unbound ARM32 branch has no jump descriptor.",
            FatalJitErrorCode.UnboundARM32BranchHasNoBasicBlock => "An unbound ARM32 branch has no basic block.",
            FatalJitErrorCode.SpillTemporaryMustExist => "The spill temporary must exist.",
            FatalJitErrorCode.InstructionDebugInformationMustExist => "Instruction debug information must exist.",
            FatalJitErrorCode.ARM32JumpRequiresABoundTargetGroup => "An ARM32 jump requires a bound target group.",
            FatalJitErrorCode.UnknownARM32BranchFormat => "Unknown ARM32 branch format.",
            FatalJitErrorCode.UnknownARM32LabelFormat => "Unknown ARM32 label format.",
            FatalJitErrorCode.ARM32MemoryBarrierCoalescingIsNotAvailableForThisTarget => "ARM32 memory-barrier coalescing is not available for this target.",
            FatalJitErrorCode.ARM32MultioperandRecordingIsNotAvailableForThisTarget => "ARM32 multioperand recording is not available for this target.",
            FatalJitErrorCode.ARM64CallRecordingIsOnlySupportedOnARM64 => "ARM64 call recording is only supported on ARM64.",
            FatalJitErrorCode.ARM64RegisterImmediateInstructionRecordingRequiresARM64 => "ARM64 register-immediate instruction recording requires ARM64.",
            FatalJitErrorCode.UnexpectedARM64LabelAddressInstructionArgument => "Unexpected ARM64 label-address instruction {0}.",
            FatalJitErrorCode.ARM64RelocatableAddressRecordingIsOnlySupportedOnARM64 => "ARM64 relocatable-address recording is only supported on ARM64.",
            FatalJitErrorCode.LoongArch64CallRecordingIsOnlySupportedOnLoongArch64 => "LoongArch64 call recording is only supported on LoongArch64.",
            FatalJitErrorCode.LoongArch64CallRecordingRequiresAnActiveCompiler => "LoongArch64 call recording requires an active compiler.",
            FatalJitErrorCode.LoongArch64CallOutputRequiresAnActiveCompiler => "LoongArch64 call output requires an active compiler.",
            FatalJitErrorCode.LoongArch64DisassemblyRequiresAnActiveCompiler => "LoongArch64 disassembly requires an active compiler.",
            FatalJitErrorCode.LoongArch64InstructionClassificationRequiresLoongArch64 => "LoongArch64 instruction classification requires LoongArch64.",
            FatalJitErrorCode.LoongArch64GCRegisterClassificationRequiresLoongArch64 => "LoongArch64 GC-register classification requires LoongArch64.",
            FatalJitErrorCode.LoongArch64StackStoreClassificationRequiresLoongArch64 => "LoongArch64 stack-store classification requires LoongArch64.",
            FatalJitErrorCode.LoongArch64SIMDInstructionEncodingRequiresFEATURESIMD => "LoongArch64 SIMD instruction encoding requires FEATURE_SIMD.",
            FatalJitErrorCode.LoongArch64InstructionOutputRequiresAnActiveCompiler => "LoongArch64 instruction output requires an active compiler.",
            FatalJitErrorCode.LoongArch64SpillTemporaryMustExist => "The LoongArch64 spill temporary must exist.",
            FatalJitErrorCode.LoongArch64InstructionOutputRequiresDebugDescriptorInformation => "LoongArch64 instruction output requires debug descriptor information.",
            FatalJitErrorCode.UnboundLoongArch64LabelRequiresABasicBlockTarget => "An unbound LoongArch64 label requires a basic-block target.",
            FatalJitErrorCode.LoongArch64LabelHasNoInstructionGroupCookie => "A LoongArch64 label has no instruction-group cookie.",
            FatalJitErrorCode.BoundLoongArch64LabelRequiresAnInstructionGroup => "A bound LoongArch64 label requires an instruction group.",
            FatalJitErrorCode.LoongArch64LabelRecordingRequiresAnActiveCompiler => "LoongArch64 label recording requires an active compiler.",
            FatalJitErrorCode.LoongArch64InstructionGroupLabelRecordingRequiresAnActiveCompiler => "LoongArch64 instruction-group label recording requires an active compiler.",
            FatalJitErrorCode.LoongArch64StackStoresRequireAnActiveCompiler => "LoongArch64 stack stores require an active compiler.",
            FatalJitErrorCode.LoongArch64StackLoadsRequireAnActiveCompiler => "LoongArch64 stack loads require an active compiler.",
            FatalJitErrorCode.LoongArch64InstructionValidationIsNotAvailableForThisTarget => "LoongArch64 instruction validation is not available for this target.",
            FatalJitErrorCode.LoongArch64VectorValidationIsNotAvailableWithoutSIMDSupport => "LoongArch64 vector validation is not available without SIMD support.",
            FatalJitErrorCode.LoongArch64JumpBindingRequiresAnActiveCompiler => "LoongArch64 jump binding requires an active compiler.",
            FatalJitErrorCode.LoongArch64JumpMustBeAssociatedWithAnInstructionGroup => "A LoongArch64 jump must be associated with an instruction group.",
            FatalJitErrorCode.LoongArch64JumpGroupIsMissingFromTheGroupList => "A LoongArch64 jump group is missing from the group list.",
            FatalJitErrorCode.BoundLoongArch64JumpRequiresATargetGroup => "A bound LoongArch64 jump requires a target group.",
            FatalJitErrorCode.LoongArch64JumpKindMappingIsNotSupportedForThisTarget => "LoongArch64 jump-kind mapping is not supported for this target.",
            FatalJitErrorCode.EmitInsToJumpKindUnimplementedOnLOONGARCH64Yet => "emitInsToJumpKind-----unimplemented on LOONGARCH64 yet----",
            FatalJitErrorCode.LoongArch64RegisterJumpRecordingIsNotSupportedForThisTarget => "LoongArch64 register jump recording is not supported for this target.",
            FatalJitErrorCode.EmitInsJRUnimplementedUnusedOnLOONGARCH64Yet => "emitIns_J_R-----unimplemented/unused on LOONGARCH64 yet----",
            FatalJitErrorCode.LoongArch64ImmediateMaterializationRequiresLoongArch64 => "LoongArch64 immediate materialization requires LoongArch64.",
            FatalJitErrorCode.BoundRISCVBranchRequiresAnInstructionGroup => "A bound RISC-V branch requires an instruction group.",
            FatalJitErrorCode.UnboundRISCVBranchRequiresABasicBlock => "An unbound RISC-V branch requires a basic block.",
            FatalJitErrorCode.RISCVDisassemblyRequiresAnActiveCompiler => "RISC-V disassembly requires an active compiler.",
            FatalJitErrorCode.RISCVBranchDisassemblyRequiresAnInstructionDescriptor => "RISC-V branch disassembly requires an instruction descriptor.",
            FatalJitErrorCode.UnsupportedRISCVSystemInstructionDisassembly => "Unsupported RISC-V system instruction disassembly.",
            FatalJitErrorCode.BoundRISCVJumpRequiresAnInstructionGroup => "A bound RISC-V jump requires an instruction group.",
            FatalJitErrorCode.UnsupportedRISCVFloatingPointMinMaxInstruction => "Unsupported RISC-V floating-point min/max instruction.",
            FatalJitErrorCode.UnsupportedRISCVFloatingPointConversionInstruction => "Unsupported RISC-V floating-point conversion instruction.",
            FatalJitErrorCode.UnsupportedRISCVFloatingPointRegisterOperation => "Unsupported RISC-V floating-point register operation.",
            FatalJitErrorCode.UnsupportedRISCVFloatingPointCompareInstruction => "Unsupported RISC-V floating-point compare instruction.",
            FatalJitErrorCode.UnsupportedRISCVFloatingPointDisassemblyOpcode => "Unsupported RISC-V floating-point disassembly opcode.",
            FatalJitErrorCode.RISCVInstructionDisassemblyRequiresDebugDescriptorInfo => "RISC-V instruction disassembly requires debug descriptor info.",
            FatalJitErrorCode.RISCV64EmissionRequiresAnActiveCompiler => "RISC-V64 emission requires an active compiler.",
            FatalJitErrorCode.IndirectLoadStoreRecordingRequiresAnActiveCompiler => "Indirect load/store recording requires an active compiler.",
            FatalJitErrorCode.ContainedRISCVAddressModeIsMissingItsBaseRegister => "Contained RISC-V address mode is missing its base register.",
            FatalJitErrorCode.ContainedRISCVAddressModeIsMissingItsBaseTree => "Contained RISC-V address mode is missing its base tree.",
            FatalJitErrorCode.InvalidCompressedRegisterNumber => "Invalid compressed register number.",
            FatalJitErrorCode.InvalidCompressedArithmeticInstruction => "Invalid compressed arithmetic instruction.",
            FatalJitErrorCode.UnboundRISCVLabelRequiresABasicBlock => "An unbound RISC-V label requires a basic block.",
            FatalJitErrorCode.RISCVBasicBlockHasNoInstructionGroupCookie => "The RISC-V basic block has no instruction-group cookie.",
            FatalJitErrorCode.BoundRISCVLabelRequiresAnInstructionGroup => "A bound RISC-V label requires an instruction group.",
            FatalJitErrorCode.RISCVJumpRequiresATargetInstructionGroup => "A RISC-V jump requires a target instruction group.",
            FatalJitErrorCode.MethodIsTooLarge => "Method is too large.",
            FatalJitErrorCode.RISCVCallOutputRequiresAnActiveCompiler => "RISC-V call output requires an active compiler.",
            FatalJitErrorCode.RISCVInstructionOutputRequiresAnActiveCompiler => "RISC-V instruction output requires an active compiler.",
            FatalJitErrorCode.RISCVSpillTemporaryMustExist => "The RISC-V spill temporary must exist.",
            FatalJitErrorCode.RISCVInstructionOutputRequiresDebugDescriptorInfo => "RISC-V instruction output requires debug descriptor info.",
            FatalJitErrorCode.RISCVLabelInstructionsRequireDebugDescriptorInfo => "RISC-V label instructions require debug descriptor info.",
            FatalJitErrorCode.IllegalInstructionWithinRISCVTwoRegisterInstructionRecording => "Illegal instruction within RISC-V two-register instruction recording.",
            FatalJitErrorCode.WasmV128ImmediateMustContainAtLeast16Bytes => "A Wasm v128 immediate must contain at least 16 bytes.",
            FatalJitErrorCode.UnexpectedWasmSIMDElementSize => "Unexpected Wasm SIMD element size.",
            FatalJitErrorCode.UnexpectedWasmLaneImmediateDescriptor => "Unexpected Wasm lane immediate descriptor.",
            FatalJitErrorCode.UnexpectedWasmInstructionDisplayFormatArgument => "Unexpected Wasm instruction display format {0}.",
            FatalJitErrorCode.WasmJumpTargetHasNoEmitterLabel => "Wasm jump target has no emitter label.",
            FatalJitErrorCode.WasmFunctionStartLocationIsMissing => "Wasm function start location is missing.",
            FatalJitErrorCode.WasmFunctionEndLocationIsMissing => "Wasm function end location is missing.",
            FatalJitErrorCode.UnexpectedWasmInstructionOutputFormatArgument => "Unexpected Wasm instruction output format {0}.",
            FatalJitErrorCode.WebAssemblyEmitterOperationsRequireWebAssembly => "WebAssembly emitter operations require WebAssembly.",
            FatalJitErrorCode.UnexpectedWasmInstructionDescriptorFormatArgument => "Unexpected Wasm instruction descriptor format {0}.",
            FatalJitErrorCode.AddressOutputIsNotImplementedForThisTarget => "Address output is not implemented for this target.",
            FatalJitErrorCode.AbsoluteAccumulatorAddressOutputIsUnavailableOnAMD64 => "Absolute accumulator address output is unavailable on AMD64.",
            FatalJitErrorCode.StackOutputIsNotImplementedForThisTarget => "Stack output is not implemented for this target.",
            FatalJitErrorCode.UnexpectedGCReferenceStackInstructionFormatArgumentForArgument => "Unexpected GC reference stack instruction format {0} for {1}.",
            FatalJitErrorCode.SIMDStackStoresAreNotEnabledForThisTarget => "SIMD stack stores are not enabled for this target.",
            FatalJitErrorCode.StaticOutputIsNotImplementedForThisTarget => "Static output is not implemented for this target.",
            FatalJitErrorCode.InitializedCompilerIsRequiredForStaticOutput => "An initialized compiler is required for static output.",
            FatalJitErrorCode.UnexpectedGCReferenceStaticInstructionFormatArgumentForArgument => "Unexpected GC reference static instruction format {0} for {1}.",
            FatalJitErrorCode.XarchInstructionRecordingRequiresXarch => "Xarch instruction recording requires xarch.",
            FatalJitErrorCode.SafeCvtDoesNotSupportDestinationTypeArgument => "SafeCvt does not support destination type {0}.",
            FatalJitErrorCode.CriticalEdgeProbeWasNotSplit => "Critical edge probe was not split",
            FatalJitErrorCode.JitSynthesisExceptionWeightRequiresAtLeastOneValue => "JitSynthesisExceptionWeight requires at least one value.",
            FatalJitErrorCode.WasmSCCAnalysisRequiresADFSTree => "Wasm SCC analysis requires a DFS tree.",
            FatalJitErrorCode.WasmSCCAnalysisRequiresPostorderTraits => "Wasm SCC analysis requires postorder traits.",
            FatalJitErrorCode.CallFinallyPairTailRequiresItsCallFinallyBlock => "A call-finally pair tail requires its call-finally block.",
            FatalJitErrorCode.UsedAddCodeDescriptorHasNoDestinationBlock => "A used add-code descriptor has no destination block.",
            FatalJitErrorCode.CallFinallyPairRequiresAFollowingBlock => "A call-finally pair requires a following block.",
            FatalJitErrorCode.UnconditionalWasmBranchRequiresATarget => "An unconditional Wasm branch requires a target.",
            FatalJitErrorCode.ConditionalWasmBranchRequiresATrueTarget => "A conditional Wasm branch requires a true target.",
            FatalJitErrorCode.ConditionalWasmBranchRequiresAFalseTarget => "A conditional Wasm branch requires a false target.",
            FatalJitErrorCode.EHFilterEntryRequiresAFilterBlock => "An EH filter entry requires a filter block.",
            FatalJitErrorCode.WasmDFSRequiresAMethodEntryBlock => "Wasm DFS requires a method entry block.",
            FatalJitErrorCode.IrreducibleSCCRequiresADispatchBlock => "An irreducible SCC requires a dispatch block.",
            FatalJitErrorCode.GCFilterLifetimesRequireWindowsAMD64 => "GC filter lifetimes require Windows AMD64.",
            FatalJitErrorCode.ModernGCEncodingRequiresWindowsAMD64 => "Modern GC encoding requires Windows AMD64.",
            FatalJitErrorCode.JIT32GCTableDumpingRequiresTheJIT32GCEncoder => "JIT32 GC table dumping requires the JIT32 GC encoder.",
            FatalJitErrorCode.InvalidGCDumpRegister => "Invalid GC dump register.",
            FatalJitErrorCode.InvalidGCDumpCalleeSavedRegister => "Invalid GC dump callee-saved register.",
            FatalJitErrorCode.ModernGCTableEncodingRequiresWindowsAMD64 => "Modern GC table encoding requires Windows AMD64.",
            FatalJitErrorCode.MorphingVarargCallNotYetImplementedOnNonWindowsTargets => "Morphing Vararg call not yet implemented on non Windows targets.",
            FatalJitErrorCode.DontExtendStateIsOnlySupportedOnXarchTargets => "Dont-extend state is only supported on xarch targets.",
            FatalJitErrorCode.GenTreeDebugDataIsOnlyAvailableInDebugBuilds => "GenTree debug data is only available in Debug builds.",
            FatalJitErrorCode.XarchEmbeddedMaskingCompatibilityIsUnsupportedOnThisTarget => "Xarch embedded-masking compatibility is unsupported on this target.",
            FatalJitErrorCode.EmbeddedHardwareIntrinsicMaskingRequiresHardwareIntrinsicSupport => "Embedded hardware-intrinsic masking requires hardware-intrinsic support.",
            FatalJitErrorCode.NodeBashingStatisticsAreNotEnabled => "Node-bashing statistics are not enabled.",
            FatalJitErrorCode.GenTreeOperatorCountsAreNotEnabled => "GenTree operator counts are not enabled.",
            FatalJitErrorCode.GenTreeSGtTrueSizesNativeLogicalSizesAreNotPorted => "GenTree::s_gtTrueSizes native logical sizes are not ported.",
            FatalJitErrorCode.UnexpectedScalableVectorConstantKind => "Unexpected scalable vector constant kind.",
            FatalJitErrorCode.GenTreeConstructionRequiresAnActiveCompilerInDebugBuilds => "GenTree construction requires an active compiler in Debug builds.",
            FatalJitErrorCode.ContainmentDiagnosticsAreOnlyAvailableInDebugBuilds => "Containment diagnostics are only available in Debug builds.",
            FatalJitErrorCode.LIRDiagnosticsAreOnlyAvailableInDebugBuilds => "LIR diagnostics are only available in Debug builds.",
            FatalJitErrorCode.TreeFlagDiagnosticsAreOnlyAvailableInDebugBuilds => "Tree flag diagnostics are only available in Debug builds.",
            FatalJitErrorCode.MorphStressCloningIsOnlyAvailableInDebugBuilds => "Morph stress cloning is only available in Debug builds.",
            FatalJitErrorCode.ReplaceALongConstantAtItsOwningUseRatherThanChangingItsManagedRuntimeType => "Replace a long constant at its owning use rather than changing its managed runtime type",
            FatalJitErrorCode.ReadyToRunAllocationEntryPointsAreUnsupported => "ReadyToRun allocation entry points are unsupported.",
            FatalJitErrorCode.ReadyToRunFieldLookupsAreUnsupported => "ReadyToRun field lookups are unsupported.",
            FatalJitErrorCode.ReadyToRunFunctionPointerEntryPointsAreUnsupported => "ReadyToRun function-pointer entry points are unsupported.",
            FatalJitErrorCode.RMWHardwareIntrinsicOperandSelectionIsUnsupportedOnThisTarget => "RMW hardware-intrinsic operand selection is unsupported on this target.",
            FatalJitErrorCode.EmbeddedBroadcastCompatibilityIsOnlySupportedOnXarchTargets => "Embedded-broadcast compatibility is only supported on xarch targets.",
            FatalJitErrorCode.CodeGenerationIsNotInitialized => "Code generation is not initialized.",
            FatalJitErrorCode.UnexpectedEmbeddedRoundingIntrinsic => "Unexpected embedded-rounding intrinsic.",
            FatalJitErrorCode.HardwareIntrinsicImmediateOperandsAreOnlyAvailableOnWebAssembly => "Hardware intrinsic immediate operands are only available on WebAssembly.",
            FatalJitErrorCode.AddressBaseFitChecksAreUnsupportedOnThisTarget => "Address-base fit checks are unsupported on this target.",
            FatalJitErrorCode.ReadyToRunIntrinsicEntryPointsAreUnsupported => "ReadyToRun intrinsic entry points are unsupported.",
            FatalJitErrorCode.HardwareIntrinsicUserCallsAreUnsupportedOnThisTarget => "Hardware-intrinsic user calls are unsupported on this target.",
            FatalJitErrorCode.AdditionalIntrinsicRegistersAreUnsupportedOnThisTarget => "Additional intrinsic registers are unsupported on this target.",
            FatalJitErrorCode.MaskedHardwareIntrinsicsAreNotEnabledForThisTarget => "Masked hardware intrinsics are not enabled for this target.",
            FatalJitErrorCode.HardwareIntrinsicNodesAreUnsupportedOnThisTarget => "Hardware intrinsic nodes are unsupported on this target.",
            FatalJitErrorCode.ComparisonOperandSizesAreOnlySupportedOnXarchTargets => "Comparison operand sizes are only supported on xarch targets.",
            FatalJitErrorCode.LongMultiplyShapeChecksAreOnlySupportedOnNonWasm32BitTargets => "Long-multiply shape checks are only supported on non-Wasm 32-bit targets.",
            FatalJitErrorCode.GTCCMPIsOnlySupportedOnX64AndARM64 => "GT_CCMP is only supported on x64 and ARM64.",
            FatalJitErrorCode.ArgumentLoadSizesAreOnlySupportedOnXarchTargets => "Argument load sizes are only supported on xarch targets.",
            FatalJitErrorCode.ArgumentPaddingIsOnlySupportedOnTheUnixX86ABI => "Argument padding is only supported on the Unix x86 ABI.",
            FatalJitErrorCode.RMWStatusIsNotSupportedOnThisTarget => "RMW status is not supported on this target.",
            FatalJitErrorCode.VisitedOperandDoesNotBelongToItsUser => "The visited operand does not belong to its user.",
            FatalJitErrorCode.HardwareIntrinsicConstantEvaluationIsUnsupportedOnThisTarget => "Hardware intrinsic constant evaluation is unsupported on this target.",
            FatalJitErrorCode.ABIReturnRegisterMasksAreUnsupportedOnThisTarget => "ABI return-register masks are unsupported on this target.",
            FatalJitErrorCode.StatementDebugDataIsOnlyAvailableInDebugBuilds => "Statement debug data is only available in Debug builds.",
            FatalJitErrorCode.ScalableVectorsAreOnlySupportedOnARM64 => "Scalable vectors are only supported on ARM64.",
            FatalJitErrorCode.TreeOperationDiagnosticsAreOnlyAvailableInDebugBuilds => "Tree operation diagnostics are only available in Debug builds.",
            FatalJitErrorCode.OperatorStructTypeMetadataIsOnlyAvailableInDebugOrNodeSizeMeasurementBuilds => "Operator struct type metadata is only available in Debug or node-size measurement builds.",
            FatalJitErrorCode.IteratorIsNotInitialized => "The iterator is not initialized.",
            FatalJitErrorCode.JITLoggingIsOnlyAvailableInDebugBuilds => "JIT logging is only available in Debug builds.",
            FatalJitErrorCode.UnexpectedARM64ImmediateIntrinsicArgument => "Unexpected ARM64 immediate intrinsic {0}.",
            FatalJitErrorCode.ARM64IntrinsicImmediatesAreUnavailableForThisTarget => "ARM64 intrinsic immediates are unavailable for this target.",
            FatalJitErrorCode.HardwareIntrinsicContainmentIsNotPortedForThisTarget => "Hardware-intrinsic containment is not ported for this target.",
            FatalJitErrorCode.FloatingPointComparisonIntrinsicsRequireAnXarchTarget => "Floating-point comparison intrinsics require an xarch target.",
            FatalJitErrorCode.ImmediateBoundsAreOnlyDefinedForXarchIntrinsics => "Immediate bounds are only defined for xarch intrinsics.",
            FatalJitErrorCode.XarchIntrinsicImmediatesAreUnavailableForThisTarget => "Xarch intrinsic immediates are unavailable for this target.",
            FatalJitErrorCode.ScalarInputIntrinsicVariantsAreOnlyAvailableOnARM64 => "Scalar-input intrinsic variants are only available on ARM64.",
            FatalJitErrorCode.TupleDerivedIntrinsicBaseTypesAreOnlyAvailableOnARM64 => "Tuple-derived intrinsic base types are only available on ARM64.",
            FatalJitErrorCode.UnexpectedARM64ScalarInputVariantIntrinsic => "Unexpected ARM64 scalar-input variant intrinsic.",
            FatalJitErrorCode.PackedSIMDImmediateBoundsAreOnlyAvailableOnWebAssembly => "Packed SIMD immediate bounds are only available on WebAssembly.",
            FatalJitErrorCode.PackedSIMDImmediateOperandsAreOnlyAvailableOnWebAssembly => "Packed SIMD immediate operands are only available on WebAssembly.",
            FatalJitErrorCode.TableDrivenHardwareIntrinsicsAreOnlySupportedOnXarch => "Table-driven hardware intrinsics are only supported on xarch.",
            FatalJitErrorCode.HardwareIntrinsicMetadataIsUnsupportedOnThisTarget => "Hardware-intrinsic metadata is unsupported on this target.",
            FatalJitErrorCode.ValueIsNotAHardwareIntrinsicIdentity => "The value is not a hardware intrinsic identity.",
            FatalJitErrorCode.HardwareIntrinsicArgumentIsNotPresentInTheConfiguredTargetSNativeMetadataTable => "Hardware intrinsic '{0}' is not present in the configured target's native metadata table.",
            FatalJitErrorCode.HardwareIntrinsicSIMDSizeLookupIsUnsupportedOnThisTarget => "Hardware-intrinsic SIMD size lookup is unsupported on this target.",
            FatalJitErrorCode.FloatingPointIntrinsicCostsAreOnlyDefinedForXarch => "Floating-point intrinsic costs are only defined for xarch.",
            FatalJitErrorCode.IntegerIntrinsicCostsAreOnlyDefinedForXarch => "Integer intrinsic costs are only defined for xarch.",
            FatalJitErrorCode.UnexpectedTernaryLogicOperationArgument => "Unexpected ternary-logic operation: {0}.",
            FatalJitErrorCode.InvalidConstantTernaryLogicOperation => "Invalid constant ternary-logic operation.",
            FatalJitErrorCode.InvalidThreeInputTernaryOperation => "Invalid three-input ternary operation.",
            FatalJitErrorCode.InvalidFirstTernaryLogicUsage => "Invalid first ternary-logic usage.",
            FatalJitErrorCode.InvalidSecondTernaryLogicUsage => "Invalid second ternary-logic usage.",
            FatalJitErrorCode.InvalidConditionalTernaryLogicUsage => "Invalid conditional ternary-logic usage.",
            FatalJitErrorCode.ARM64HardwareIntrinsicsAreUnavailableForThisTarget => "ARM64 hardware intrinsics are unavailable for this target.",
            FatalJitErrorCode.ARM64EmitInsRLRecordingIsNotPorted => "ARM64 emitIns_R_L recording is not ported.",
            FatalJitErrorCode.HardwareIntrinsicsAreDisabled => "Hardware intrinsics are disabled.",
            FatalJitErrorCode.AVXFamilyGenerationRequiresXarch => "AVX family generation requires xarch.",
            FatalJitErrorCode.FMAGenerationRequiresXarch => "FMA generation requires xarch.",
            FatalJitErrorCode.PermuteVar2xGenerationRequiresXarch => "PermuteVar2x generation requires xarch.",
            FatalJitErrorCode.BaseHardwareIntrinsicsRequireXarchSupport => "Base hardware intrinsics require xarch support.",
            FatalJitErrorCode.HardwareIntrinsicCodeGenerationRequiresXarchSupport => "Hardware-intrinsic code generation requires xarch support.",
            FatalJitErrorCode.X86BaseIntrinsicsRequireXarchSupport => "X86 base intrinsics require xarch support.",
            FatalJitErrorCode.FullInlinePolicyIsOnlyAvailableInDebugBuilds => "Full inline policy is only available in Debug builds.",
            FatalJitErrorCode.RandomInlinePolicyIsOnlyAvailableInDebugBuilds => "Random inline policy is only available in Debug builds.",
            FatalJitErrorCode.ReplayInlinePolicyIsOnlyAvailableInDebugBuilds => "Replay inline policy is only available in Debug builds.",
            FatalJitErrorCode.ReplayFileIsNotOpen => "Replay file is not open.",
            FatalJitErrorCode.SizeInlinePolicyIsOnlyAvailableInDebugBuilds => "Size inline policy is only available in Debug builds.",
            FatalJitErrorCode.ARM64ConditionMappingIsNotAvailableForThisTarget => "ARM64 condition mapping is not available for this target.",
            FatalJitErrorCode.ARM32ImmediateValidationIsNotSupportedForThisTarget => "ARM32 immediate validation is not supported for this target.",
            FatalJitErrorCode.ARM32StackPointerImmediateValidationIsNotSupportedForThisTarget => "ARM32 stack-pointer immediate validation is not supported for this target.",
            FatalJitErrorCode.ARM32ZeroMaterializationIsNotSupportedForThisTarget => "ARM32 zero materialization is not supported for this target.",
            FatalJitErrorCode.ARM32RelocationMaterializationIsNotSupportedForThisTarget => "ARM32 relocation materialization is not supported for this target.",
            FatalJitErrorCode.ARM32DataLabelMaterializationIsNotSupportedForThisTarget => "ARM32 data-label materialization is not supported for this target.",
            FatalJitErrorCode.ARMImmediateValidationIsNotAvailableForTheConfiguredTarget => "ARM immediate validation is not available for the configured target.",
            FatalJitErrorCode.BranchTargetRangeValidationIsNotSupportedForThisTarget => "Branch target range validation is not supported for this target.",
            FatalJitErrorCode.IntegerExtensionInstructionSelectionIsNotImplementedOnThisTarget => "Integer extension instruction selection is not implemented on this target.",
            FatalJitErrorCode.MaskedRegisterExtensionRequiresMaskedHardwareIntrinsics => "Masked-register extension requires masked hardware intrinsics.",
            FatalJitErrorCode.MaskedRegisterExtensionIsNotImplementedForThisTarget => "Masked-register extension is not implemented for this target.",
            FatalJitErrorCode.FloatingPointExtensionInstructionSelectionIsNotImplementedOnThisTarget => "Floating-point extension instruction selection is not implemented on this target.",
            FatalJitErrorCode.InvalidIntegerToFloatingConversion => "Invalid integer-to-floating conversion.",
            FatalJitErrorCode.InvalidUnsignedToFloatingConversion => "Invalid unsigned-to-floating conversion.",
            FatalJitErrorCode.LongToFloat => "long to float",
            FatalJitErrorCode.LongToDouble => "long to double",
            FatalJitErrorCode.InvalidLongToFloatingConversion => "Invalid long-to-floating conversion.",
            FatalJitErrorCode.FloatToLong => "float to long",
            FatalJitErrorCode.InvalidFloatConversion => "Invalid float conversion.",
            FatalJitErrorCode.DoubleToLong => "double to long",
            FatalJitErrorCode.InvalidDoubleConversion => "Invalid double conversion.",
            FatalJitErrorCode.FloatingConversionInstructionSelectionRequiresXarchOrARM => "Floating conversion instruction selection requires xarch or ARM.",
            FatalJitErrorCode.InstRVSHUnknownTarget => "inst_RV_SH - unknown target",
            FatalJitErrorCode.NoInstructionNameTableIsDefinedForTheConfiguredTarget => "No instruction-name table is defined for the configured target.",
            FatalJitErrorCode.WebAssemblySIMDLoadsRequireSIMDSupport => "WebAssembly SIMD loads require SIMD support.",
            FatalJitErrorCode.IntegerLoadSelectionRequiresASupportedTarget => "Integer load selection requires a supported target.",
            FatalJitErrorCode.MaskedRegisterLoadsRequireMaskedHardwareIntrinsics => "Masked-register loads require masked hardware intrinsics.",
            FatalJitErrorCode.MaskedRegisterLoadsAreNotImplementedForThisTarget => "Masked-register loads are not implemented for this target.",
            FatalJitErrorCode.FloatingPointLoadSelectionRequiresASupportedTarget => "Floating-point load selection requires a supported target.",
            FatalJitErrorCode.InvalidFloatingMathOperator => "Invalid floating math operator.",
            FatalJitErrorCode.FloatingPointMathInstructionSelectionIsNotImplementedForThisTarget => "Floating-point math instruction selection is not implemented for this target.",
            FatalJitErrorCode.InstructionOperandClassificationRequiresXarch => "Instruction operand classification requires xarch.",
            FatalJitErrorCode.HardwareInstructionOperandsRequireHardwareIntrinsics => "Hardware instruction operands require hardware intrinsics.",
            FatalJitErrorCode.VectorConstantOperandsRequireSIMDSupport => "Vector constant operands require SIMD support.",
            FatalJitErrorCode.EmitterVectorConstantOperandsAreNotImplementedOutsideXarch => "Emitter vector-constant operands are not implemented outside xarch.",
            FatalJitErrorCode.MaskConstantOperandsRequireMaskedHardwareIntrinsics => "Mask constant operands require masked hardware intrinsics.",
            FatalJitErrorCode.SingleOperandInstructionGenerationRequiresXarch => "Single-operand instruction generation requires xarch.",
            FatalJitErrorCode.RegisterOperandInstructionGenerationRequiresXarch => "Register/operand instruction generation requires xarch.",
            FatalJitErrorCode.RegisterRegisterImmediateInstructionGenerationRequiresXarch => "Register/register/immediate instruction generation requires xarch.",
            FatalJitErrorCode.RegisterMemoryImmediateInstructionGenerationRequiresXarch => "Register/memory/immediate instruction generation requires xarch.",
            FatalJitErrorCode.SIMDRegisterOperandImmediateGenerationRequiresXarch => "SIMD register/operand/immediate generation requires xarch.",
            FatalJitErrorCode.EmbeddedBroadcastInstructionOptionsRequireXarchHardwareIntrinsics => "Embedded broadcast instruction options require xarch hardware intrinsics.",
            FatalJitErrorCode.ThreeOperandRegisterMemoryInstructionGenerationRequiresXarch => "Three-operand register/memory instruction generation requires xarch.",
            FatalJitErrorCode.InstRVRVRVIsNotImplementedForThisTarget => "inst_RV_RV_RV is not implemented for this target.",
            FatalJitErrorCode.StackVariableReferenceRequiresALocalVariableOrStore => "Stack variable reference requires a local variable or store.",
            FatalJitErrorCode.IntegerCopyInstructionSelectionIsNotImplementedOnThisTarget => "Integer copy instruction selection is not implemented on this target.",
            FatalJitErrorCode.MaskedRegisterCopiesRequireMaskedHardwareIntrinsics => "Masked-register copies require masked hardware intrinsics.",
            FatalJitErrorCode.MaskedRegisterCopiesAreNotImplementedForThisTarget => "Masked-register copies are not implemented for this target.",
            FatalJitErrorCode.FloatingPointCopyInstructionSelectionIsNotImplementedOnThisTarget => "Floating-point copy instruction selection is not implemented on this target.",
            FatalJitErrorCode.MaskedRegisterToIntegerCopiesAreNotImplementedForThisTarget => "Masked-register to integer copies are not implemented for this target.",
            FatalJitErrorCode.FloatingPointToIntegerCopiesAreNotImplementedOnThisTarget => "Floating-point to integer copies are not implemented on this target.",
            FatalJitErrorCode.IntegerToFloatingPointCopiesAreNotImplementedOnThisTarget => "Integer to floating-point copies are not implemented on this target.",
            FatalJitErrorCode.RegisterMoveDestinationTypeIsNotImplemented => "Register move destination type is not implemented.",
            FatalJitErrorCode.LoongArch64InstRVIsUnusedAndNotImplemented => "LoongArch64 inst_RV is unused and not implemented.",
            FatalJitErrorCode.RISCVInstRVIsUnusedAndNotImplemented => "RISC-V inst_RV is unused and not implemented.",
            FatalJitErrorCode.SetConditionGenerationIsNotImplementedOnThisTarget => "Set-condition generation is not implemented on this target.",
            FatalJitErrorCode.EightByteFloatingRegisterStoreRequiresA64BitTarget => "An eight-byte floating register store requires a 64-bit target.",
            FatalJitErrorCode.MaskedRegisterStoresRequireMaskedHardwareIntrinsics => "Masked-register stores require masked hardware intrinsics.",
            FatalJitErrorCode.EightByteIntegerRegisterStoreRequiresA64BitTarget => "An eight-byte integer register store requires a 64-bit target.",
            FatalJitErrorCode.WebAssemblySIMDStoresRequireSIMDSupport => "WebAssembly SIMD stores require SIMD support.",
            FatalJitErrorCode.IntegerStoreSelectionRequiresASupportedTarget => "Integer store selection requires a supported target.",
            FatalJitErrorCode.MaskedRegisterStoresAreNotImplementedForThisTarget => "Masked-register stores are not implemented for this target.",
            FatalJitErrorCode.FloatingPointStoreSelectionRequiresASupportedTarget => "Floating-point store selection requires a supported target.",
            FatalJitErrorCode.XarchFloatingPointStackStoresAreNotSupportedForThisTarget => "Xarch floating-point stack stores are not supported for this target.",
            FatalJitErrorCode.ArgumentIsNotSupportedForThisTarget => "{0} is not supported for this target.",
            FatalJitErrorCode.SIMDUnimplementedYetLOONGARCH => "SIMD Unimplemented yet LOONGARCH",
            FatalJitErrorCode.ExactlyOneTargetArchitectureMustBeSelected => "Exactly one target architecture must be selected.",
            FatalJitErrorCode.TargetBitnessDoesNotMatchTheSelectedArchitecture => "Target bitness does not match the selected architecture.",
            FatalJitErrorCode.WhenUNIXAMD64ABIIsEnabledTARGETAMD64MustBeEnabled => "When UNIX_AMD64_ABI is enabled, TARGET_AMD64 must be enabled.",
            FatalJitErrorCode.WhenUNIXX86ABIIsEnabledTARGETX86MustBeEnabled => "When UNIX_X86_ABI is enabled, TARGET_X86 must be enabled.",
            FatalJitErrorCode.ILInstructionDumpingIsOnlyAvailableInDebugBuilds => "IL instruction dumping is only available in Debug builds.",
            FatalJitErrorCode.JitTlsScopesAreOnlyAvailableInDebugBuilds => "JitTls scopes are only available in Debug builds.",
            FatalJitErrorCode.ImporterDebugStateIsOnlyAvailableInDebugBuilds => "Importer debug state is only available in Debug builds.",
            FatalJitErrorCode.ImporterDebugStateRequiresAnActiveJitTlsScope => "Importer debug state requires an active JitTls scope.",
            FatalJitErrorCode.JitTlsLogEnvIsOnlyAvailableInDebugBuilds => "JitTls.LogEnv is only available in Debug builds.",
            FatalJitErrorCode.NonPointerGCRegisterMarkingWithoutAFixedRegisterSetIsNotImplemented => "Non-pointer GC register marking without a fixed register set is not implemented.",
            FatalJitErrorCode.SmallPrimitiveKeysOfTypeArgumentAreNotSupported => "Small primitive keys of type {0} are not supported.",
            FatalJitErrorCode.LargePrimitiveKeysOfSizeArgumentAreNotSupported => "Large primitive keys of size {0} are not supported.",
            FatalJitErrorCode.PointerKeysOfTypeArgumentAreNotSupported => "Pointer keys of type {0} are not supported.",
            FatalJitErrorCode.ValueReferenceIsUninitialized => "The value reference is uninitialized.",
            FatalJitErrorCode.HashTableMustBeAllocatedBeforeRemovingKeys => "The hash table must be allocated before removing keys.",
            FatalJitErrorCode.HashTableBehaviorReturnedFromItsNoMemoryHandler => "The hash-table behavior returned from its no-memory handler.",
            FatalJitErrorCode.IteratorTraitsAreNotDefinedForArgument => "Iterator traits are not defined for {0}.",
            FatalJitErrorCode.ListIteratorsAreNotPartOfTheSameSequence => "The list iterators are not part of the same sequence.",
            FatalJitErrorCode.EndIteratorCannotBeDereferenced => "The end iterator cannot be dereferenced.",
            FatalJitErrorCode.IteratorRefersToAnErasedNode => "The iterator refers to an erased node.",
            FatalJitErrorCode.ListIsEmpty => "The list is empty.",
            FatalJitErrorCode.EndIteratorIsNotReachableFromTheStartIterator => "The end iterator is not reachable from the start iterator.",
            FatalJitErrorCode.CannotRemoveAnElementFromAnEmptyList => "Cannot remove an element from an empty list.",
            FatalJitErrorCode.EndIteratorDoesNotBelongToThisList => "The end iterator does not belong to this list.",
            FatalJitErrorCode.IteratorDoesNotReferToALiveNodeInThisList => "The iterator does not refer to a live node in this list.",
            FatalJitErrorCode.ListNodeTransferRequiresASharedAllocationScope => "List node transfer requires a shared allocation scope.",
            FatalJitErrorCode.VectorIteratorsMustReferToTheSameAllocation => "Vector iterators must refer to the same allocation.",
            FatalJitErrorCode.VectorIsEmpty => "The vector is empty.",
            FatalJitErrorCode.CannotRemoveAnElementFromAnEmptyVector => "Cannot remove an element from an empty vector.",
            FatalJitErrorCode.FirstIteratorIsNotInitialized => "The first iterator is not initialized.",
            FatalJitErrorCode.RangeIteratorsMustReferToTheSameVector => "The range iterators must refer to the same vector.",
            FatalJitErrorCode.EndIteratorPrecedesTheStartIterator => "The end iterator precedes the start iterator.",
            FatalJitErrorCode.VectorChangedDuringEnumeration => "The vector changed during enumeration.",
            FatalJitErrorCode.IteratorDoesNotBelongToThisVector => "The iterator does not belong to this vector.",
            FatalJitErrorCode.VectorIteratorWasInvalidatedByReallocation => "The vector iterator was invalidated by reallocation.",
            FatalJitErrorCode.ClassLayoutNamesAreOnlyAvailableInDebugBuilds => "Class layout names are only available in Debug builds.",
            FatalJitErrorCode.ValueConsumptionStateIsOnlyAvailableInDebugBuilds => "Value consumption state is only available in Debug builds.",
            FatalJitErrorCode.ConsumptionStateIsOnlyAvailableInDebugBuilds => "Consumption state is only available in Debug builds.",
            FatalJitErrorCode.LIRValidationIsOnlyAvailableInDebugBuilds => "LIR validation is only available in Debug builds.",
            FatalJitErrorCode.TreeRangeFlagTraversalIsOnlyAvailableInDebugBuilds => "Tree-range flag traversal is only available in Debug builds.",
            FatalJitErrorCode.EarlyTreeLivenessRequiresTheNonSSAEarlyPolicy => "Early tree liveness requires the non-SSA early policy.",
            FatalJitErrorCode.LIRLivenessOrchestrationRequiresANonEarlyLIRPolicy => "LIR liveness orchestration requires a non-early LIR policy.",
            FatalJitErrorCode.SSATreeLivenessRequiresTheNonLIRSSAPolicy => "SSA tree liveness requires the non-LIR SSA policy.",
            FatalJitErrorCode.StackArgumentLoweringIsNotSupportedForWasm => "Stack argument lowering is not supported for Wasm.",
            FatalJitErrorCode.StackArgumentLoweringIsNotPortedForThisTarget => "Stack argument lowering is not ported for this target.",
            FatalJitErrorCode.MultiplyContainmentIsNotPortedForThisTarget => "Multiply containment is not ported for this target.",
            FatalJitErrorCode.MultiplyLoweringIsNotPortedForThisTarget => "Multiply lowering is not ported for this target.",
            FatalJitErrorCode.UnexpectedARM64ImmediateIntrinsicOperandCount => "Unexpected ARM64 immediate intrinsic operand count.",
            FatalJitErrorCode.UnexpectedARM64ContainmentIntrinsicArgument => "Unexpected ARM64 containment intrinsic: {0}.",
            FatalJitErrorCode.ARM64ComparisonLocalCouldNotBeCloned => "ARM64 comparison local could not be cloned.",
            FatalJitErrorCode.UnexpectedARM64VectorComparisonBaseTypeArgument => "Unexpected ARM64 vector comparison base type: {0}.",
            FatalJitErrorCode.ARM64DotProductLocalCouldNotBeCloned => "ARM64 dot-product local could not be cloned.",
            FatalJitErrorCode.ExpectedAnArrayMetadataNode => "Expected an array metadata node.",
            FatalJitErrorCode.BinaryContainmentIsNotPortedForThisTarget => "Binary containment is not ported for this target.",
            FatalJitErrorCode.BinaryArithmeticLoweringIsNotPortedForThisTarget => "Binary arithmetic lowering is not ported for this target.",
            FatalJitErrorCode.BlockStoreAddressContainmentIsNotPortedForThisTarget => "Block-store address containment is not ported for this target.",
            FatalJitErrorCode.BoundsCheckContainmentOutsideXarchARM64LoongArch64AndRISCV64IsNotPorted => "Bounds-check containment outside xarch, ARM64, LoongArch64 and RISC-V64 is not ported.",
            FatalJitErrorCode.UnbalancedIndirectCallDataFlow => "Unbalanced indirect call data flow.",
            FatalJitErrorCode.CallOperandContainmentIsNotPortedForThisTarget => "Call operand containment is not ported for this target.",
            FatalJitErrorCode.TailCallHelperTargetArgumentIsMissing => "Tail-call helper target argument is missing.",
            FatalJitErrorCode.TailCallHelperTargetPlacementIsMissing => "Tail-call helper target placement is missing.",
            FatalJitErrorCode.TailCallHelperFlagsArgumentIsMissing => "Tail-call helper flags argument is missing.",
            FatalJitErrorCode.TailCallHelperFlagsPlacementIsMissing => "Tail-call helper flags placement is missing.",
            FatalJitErrorCode.TailCallHelperSizeArgumentIsMissing => "Tail-call helper size argument is missing.",
            FatalJitErrorCode.TailCallHelperSizePlacementIsMissing => "Tail-call helper size placement is missing.",
            FatalJitErrorCode.TailCallHelperIncomingSizeArgumentIsMissing => "Tail-call helper incoming size argument is missing.",
            FatalJitErrorCode.UnexpectedFloatingConversionDestination => "Unexpected floating conversion destination.",
            FatalJitErrorCode.CastLoweringIsNotPortedForThisTarget => "Cast lowering is not ported for this target.",
            FatalJitErrorCode.CastContainmentIsNotPortedForThisTarget => "Cast containment is not ported for this target.",
            FatalJitErrorCode.ComparisonContainmentIsNotPortedForThisTarget => "Comparison containment is not ported for this target.",
            FatalJitErrorCode.TargetSpecificLowerJTrueOverrideIsNotPorted => "The target-specific LowerJTrue override is not ported.",
            FatalJitErrorCode.UnexpectedConditionType => "Unexpected condition type.",
            FatalJitErrorCode.ConditionalCompareLoweringIsOnlySupportedForX64AndARM64 => "Conditional compare lowering is only supported for x64 and ARM64.",
            FatalJitErrorCode.ConditionalCompareContainmentIsNotPortedForThisTarget => "Conditional compare containment is not ported for this target.",
            FatalJitErrorCode.ImmediateContainmentIsNotPortedForThisTarget => "Immediate containment is not ported for this target.",
            FatalJitErrorCode.LoweringLowerCFGCallDispatcherRegisterIsNotPortedForThisTarget => "Lowering.LowerCFGCall dispatcher register is not ported for this target.",
            FatalJitErrorCode.LongToLongNoOpCastDecompositionIsNotPorted => "Long-to-long no-op cast decomposition is not ported.",
            FatalJitErrorCode.CastDecompositionIsNotPorted => "Cast decomposition is not ported.",
            FatalJitErrorCode.HardwareIntrinsicLongDecompositionIsNotAvailable => "Hardware-intrinsic long decomposition is not available.",
            FatalJitErrorCode.UnexpectedGTHWINTRINSICNodeInLongDecomposition => "Unexpected GT_HWINTRINSIC node in long decomposition.",
            FatalJitErrorCode.LongNegationDecompositionIsNotPortedForThisTarget => "Long negation decomposition is not ported for this target.",
            FatalJitErrorCode.UnexpectedLongShiftOperator => "Unexpected long shift operator.",
            FatalJitErrorCode.InterlockedOperationsOnTYPLONGAreNotPorted => "Interlocked operations on TYP_LONG are not ported.",
            FatalJitErrorCode.IllegalTYPLONGNodeInDecomposition => "Illegal TYP_LONG node in Decomposition.",
            FatalJitErrorCode.GetHiOperCalledForInvalidOper => "GetHiOper called for invalid oper",
            FatalJitErrorCode.GetLoOperCalledForInvalidOper => "GetLoOper called for invalid oper",
            FatalJitErrorCode.DivisionContainmentIsNotPortedForThisTarget => "Division containment is not ported for this target.",
            FatalJitErrorCode.FastTailcallLoweringIsNotAvailableForThisTarget => "Fast tailcall lowering is not available for this target.",
            FatalJitErrorCode.StackArgumentMustPrecedeTheTailCall => "Stack argument must precede the tail call.",
            FatalJitErrorCode.FastTailcallLoweringRequiresACurrentBlock => "Fast tailcall lowering requires a current block.",
            FatalJitErrorCode.StackArgumentHasNoOperand => "Stack argument has no operand.",
            FatalJitErrorCode.TailCallArgumentUseExtendsBeyondTheCall => "Tail-call argument use extends beyond the call.",
            FatalJitErrorCode.StructParameterHasNoLayout => "Struct parameter has no layout.",
            FatalJitErrorCode.UnexpectedBroadcastSizeArgument => "Unexpected broadcast size: {0}.",
            FatalJitErrorCode.UnexpectedBroadcastTypeArgument => "Unexpected broadcast type: {0}.",
            FatalJitErrorCode.PTESTLocalCouldNotBeCloned => "PTEST local could not be cloned.",
            FatalJitErrorCode.UnexpectedVectorComparisonBaseTypeArgument => "Unexpected vector comparison base type: {0}.",
            FatalJitErrorCode.EVEXMaskLocalCouldNotBeCloned => "EVEX mask local could not be cloned.",
            FatalJitErrorCode.ConditionalSelectMaskLocalCouldNotBeCloned => "Conditional-select mask local could not be cloned.",
            FatalJitErrorCode.HardwareConditionCodeLoweringIsNotAvailableForThisTarget => "Hardware condition-code lowering is not available for this target.",
            FatalJitErrorCode.SIMDLoweringIsUnsupportedOnThisTarget => "SIMD lowering is unsupported on this target.",
            FatalJitErrorCode.UnhandledBinaryImmediateHardwareIntrinsicArgument => "Unhandled binary immediate hardware intrinsic: {0}.",
            FatalJitErrorCode.UnhandledBinaryHelperHardwareIntrinsicArgument => "Unhandled binary helper hardware intrinsic: {0}.",
            FatalJitErrorCode.UnexpectedBinaryHardwareIntrinsicCategoryArgument => "Unexpected binary hardware intrinsic category: {0}.",
            FatalJitErrorCode.UnexpectedHardwareIntrinsicOperandCountArgument => "Unexpected hardware intrinsic operand count: {0}.",
            FatalJitErrorCode.LoongArch64HardwareIntrinsicContainmentIsNotPorted => "LoongArch64 hardware-intrinsic containment is not ported.",
            FatalJitErrorCode.UnexpectedQuaternaryHardwareIntrinsicCategoryArgument => "Unexpected quaternary hardware intrinsic category: {0}.",
            FatalJitErrorCode.TernaryLogicControlArgumentWasNotNormalizedBeforeContainment => "Ternary logic control {0:X2} was not normalized before containment.",
            FatalJitErrorCode.UnhandledQuaternaryImmediateHardwareIntrinsicArgument => "Unhandled quaternary immediate hardware intrinsic: {0}.",
            FatalJitErrorCode.InvalidPermutationElementSize => "Invalid permutation element size.",
            FatalJitErrorCode.UnhandledTernaryHardwareIntrinsicArgument => "Unhandled ternary hardware intrinsic: {0}.",
            FatalJitErrorCode.UnhandledTernaryImmediateHardwareIntrinsicArgument => "Unhandled ternary immediate hardware intrinsic: {0}.",
            FatalJitErrorCode.UnexpectedTernaryHardwareIntrinsicCategoryArgument => "Unexpected ternary hardware intrinsic category: {0}.",
            FatalJitErrorCode.UnexpectedUnaryHardwareIntrinsicCategoryArgument => "Unexpected unary hardware intrinsic category: {0}.",
            FatalJitErrorCode.VectorCreateLoweringIsNotPortedForThisTarget => "Vector Create lowering is not ported for this target.",
            FatalJitErrorCode.VectorDotMultiplySumLoweringIsNotPortedForThisTarget => "Vector Dot multiply/sum lowering is not ported for this target.",
            FatalJitErrorCode.RISCV64HardwareIntrinsicDotLoweringIsNotPorted => "RISC-V64 hardware-intrinsic Dot lowering is not ported.",
            FatalJitErrorCode.LoongArch64HardwareIntrinsicDotLoweringIsNotPorted => "LoongArch64 hardware-intrinsic Dot lowering is not ported.",
            FatalJitErrorCode.VectorDotLoweringIsNotPortedForThisTarget => "Vector Dot lowering is not ported for this target.",
            FatalJitErrorCode.VectorGetElementLoweringIsNotPortedForThisTarget => "Vector GetElement lowering is not ported for this target.",
            FatalJitErrorCode.FusedMultiplyLoweringIsNotPortedForThisTarget => "Fused multiply lowering is not ported for this target.",
            FatalJitErrorCode.HardwareIntrinsicLoweringIsNotAvailable => "Hardware-intrinsic lowering is not available.",
            FatalJitErrorCode.LoongArch64HardwareIntrinsicLoweringIsNotPorted => "LoongArch64 hardware-intrinsic lowering is not ported.",
            FatalJitErrorCode.RISCV64HardwareIntrinsicLoweringIsNotPorted => "RISC-V64 hardware-intrinsic lowering is not ported.",
            FatalJitErrorCode.HardwareIntrinsicLoweringIsNotPortedForThisTarget => "Hardware-intrinsic lowering is not ported for this target.",
            FatalJitErrorCode.UnexpectedTupleTypeArgument => "Unexpected tuple type: {0}.",
            FatalJitErrorCode.UnexpectedIntrinsicCategoryArgument => "Unexpected intrinsic category: {0}.",
            FatalJitErrorCode.VectorToScalarLoweringIsNotPortedForThisTarget => "Vector ToScalar lowering is not ported for this target.",
            FatalJitErrorCode.VectorWithElementLoweringIsNotPortedForThisTarget => "Vector WithElement lowering is not ported for this target.",
            FatalJitErrorCode.ClosedStoreTreeRangeMustIncludeItsRoot => "A closed store tree range must include its root.",
            FatalJitErrorCode.StoreCoalescingRequiresCodeGenerationState => "Store coalescing requires code generation state.",
            FatalJitErrorCode.RISCVSIMDIndirectionContainmentIsNotPorted => "RISC-V SIMD indirection containment is not ported.",
            FatalJitErrorCode.LoongArchSIMDIndirectionContainmentIsNotPorted => "LoongArch SIMD indirection containment is not ported.",
            FatalJitErrorCode.FoldableWasmAddressIsMissingItsBase => "Foldable Wasm address is missing its base.",
            FatalJitErrorCode.IndirectionContainmentIsNotPortedForThisTarget => "Indirection containment is not ported for this target.",
            FatalJitErrorCode.UnusedIndirectionLoweringIsNotPortedForThisTarget => "Unused indirection lowering is not ported for this target.",
            FatalJitErrorCode.ScalarIntrinsicContainmentIsNotPortedForThisTarget => "Scalar intrinsic containment is not ported for this target.",
            FatalJitErrorCode.OnlyUnaryCastsAndBinaryOperationsCanBeFoldedInLIR => "Only unary casts and binary operations can be folded in LIR.",
            FatalJitErrorCode.NonlocalJumpContainmentIsNotPortedForThisTarget => "Nonlocal jump containment is not ported for this target.",
            FatalJitErrorCode.LocalStoreLoweringIsNotPortedForThisTarget => "Local-store lowering is not ported for this target.",
            FatalJitErrorCode.LocalStoreContainmentIsNotPortedForThisTarget => "Local-store containment is not ported for this target.",
            FatalJitErrorCode.UnsupportedMemoryComparisonLoadWidth => "Unsupported memory comparison load width.",
            FatalJitErrorCode.IndirectPInvokeTargetIsNotInLIR => "Indirect P/Invoke target is not in LIR.",
            FatalJitErrorCode.RelativePInvokeTargetLookupIsUnsupportedByTheNativeContract => "Relative P/Invoke target lookup is unsupported by the native contract.",
            FatalJitErrorCode.InvalidPInvokeTargetAccessType => "Invalid P/Invoke target access type.",
            FatalJitErrorCode.SwiftErrorHandlingIsNotSupportedForThisTarget => "Swift error handling is not supported for this target.",
            FatalJitErrorCode.SwiftCallHasNoErrorRegisterConsumer => "Swift call has no error-register consumer.",
            FatalJitErrorCode.ReachedBlockStartWhileSearchingForTailCallArguments => "Reached block start while searching for tail-call arguments.",
            FatalJitErrorCode.RISCV64SIMDLoweringWasDispatchedForAnotherTarget => "RISC-V64 SIMD lowering was dispatched for another target.",
            FatalJitErrorCode.RISCV64SIMDLoweringIsNotPorted => "RISC-V64 SIMD lowering is not ported.",
            FatalJitErrorCode.RISCV64SIMDContainmentWasDispatchedForAnotherTarget => "RISC-V64 SIMD containment was dispatched for another target.",
            FatalJitErrorCode.RISCV64SIMDContainmentIsNotPorted => "RISC-V64 SIMD containment is not ported.",
            FatalJitErrorCode.RISCV64HardwareIntrinsicContainmentWasDispatchedIncorrectly => "RISC-V64 hardware-intrinsic containment was dispatched incorrectly.",
            FatalJitErrorCode.RISCV64HardwareIntrinsicContainmentIsNotPorted => "RISC-V64 hardware-intrinsic containment is not ported.",
            FatalJitErrorCode.RISCVConditionalSelectRequiresZicond => "RISC-V conditional select requires Zicond.",
            FatalJitErrorCode.ShiftContainmentIsNotPortedForThisTarget => "Shift containment is not ported for this target.",
            FatalJitErrorCode.IntegralDivisionShouldBeLoweredToHelperCallsForThisTarget => "Integral division should be lowered to helper calls for this target.",
            FatalJitErrorCode.Failure64BitSignedMagicDivisionRequiresA64BitTarget => "64-bit signed magic division requires a 64-bit target.",
            FatalJitErrorCode.IndirectStoreLoweringIsNotPortedForThisTarget => "Indirect-store lowering is not ported for this target.",
            FatalJitErrorCode.UnexpectedTupleTypeForANarrowingConversion => "Unexpected tuple type for a narrowing conversion.",
            FatalJitErrorCode.IndirectStoreContainmentIsNotPortedForThisTarget => "Indirect-store containment is not ported for this target.",
            FatalJitErrorCode.HFAStructCallLoweringIsNotSupportedForThisTarget => "HFA struct-call lowering is not supported for this target.",
            FatalJitErrorCode.HardwareIntrinsicStructCallLoweringIsNotAvailable => "Hardware-intrinsic struct-call lowering is not available.",
            FatalJitErrorCode.RangeToLowerMustNotBeEmpty => "The range to lower must not be empty.",
            FatalJitErrorCode.HardwareIntrinsicLoweringRequiresHardwareIntrinsicsToBeEnabled => "Hardware-intrinsic lowering requires hardware intrinsics to be enabled.",
            FatalJitErrorCode.UnsignedIntegerDivisionShouldBeLoweredToHelperCallsForThisTarget => "Unsigned integer division should be lowered to helper calls for this target.",
            FatalJitErrorCode.NativeWidthDivisionMustUseThe32BitPathOnThisTarget => "Native-width division must use the 32-bit path on this target.",
            FatalJitErrorCode.WasmStackificationWasDispatchedForAnotherTarget => "Wasm stackification was dispatched for another target.",
            FatalJitErrorCode.WasmStackificationRootHasNoPrecedingNode => "A Wasm stackification root has no preceding node.",
            FatalJitErrorCode.WasmStackificationUseHasNoPrecedingNode => "A Wasm stackification use has no preceding node.",
            FatalJitErrorCode.WasmStackificationDidNotProcessANode => "Wasm stackification did not process a node.",
            FatalJitErrorCode.WasmStackifierMoveRequiresAnExclusiveRangeEndpoint => "A Wasm stackifier move requires an exclusive range endpoint.",
            FatalJitErrorCode.StackifierTemporaryDisappearedWhileInUse => "A stackifier temporary disappeared while in use.",
            FatalJitErrorCode.RootUseHasNoOwningNode => "A root use has no owning node.",
            FatalJitErrorCode.RootUseHasNoOperandEdge => "A root use has no operand edge.",
            FatalJitErrorCode.WasmHardwareIntrinsicLoweringIsNotAvailable => "Wasm hardware-intrinsic lowering is not available.",
            FatalJitErrorCode.IntervalIndexIsOnlyAvailableInDebugBuilds => "The interval index is only available in Debug builds.",
            FatalJitErrorCode.LSRAAllocationTableInitialRegisterSetOnThisTarget => "LSRA allocation table initial register set on this target.",
            FatalJitErrorCode.UpperVectorIntervalsMustRetainTheirRelatedLocal => "Upper-vector intervals must retain their related local.",
            FatalJitErrorCode.InvalidLSRAStatistic => "Invalid LSRA statistic.",
            FatalJitErrorCode.LSRAStatisticsRequireInitializedBlockInformation => "LSRA statistics require initialized block information.",
            FatalJitErrorCode.LSRAStatisticsAreNotEnabled => "LSRA statistics are not enabled.",
            FatalJitErrorCode.LSRAStatisticBlockNumberIsOutsideTheBlockTable => "LSRA statistic block number is outside the block table.",
            FatalJitErrorCode.LSRAStatisticsDoNotCoverAPreResolutionBlock => "LSRA statistics do not cover a pre-resolution block.",
            FatalJitErrorCode.PreResolutionBlockHasNoLSRAStatistics => "A pre-resolution block has no LSRA statistics.",
            FatalJitErrorCode.StressSpillingRequiresAnAssignedRegister => "Stress spilling requires an assigned register.",
            FatalJitErrorCode.UpperVectorSaveRequiresAMatchingRestore => "An upper-vector save requires a matching restore.",
            FatalJitErrorCode.LiveThisIntervalRequiresAnIncomingMap => "A live this interval requires an incoming map.",
            FatalJitErrorCode.UnsupportedFullLSRAAllocationEventArgument => "Unsupported full LSRA allocation event: {0}.",
            FatalJitErrorCode.ConsecutiveUpperVectorRestoreRequiresARelatedInterval => "A consecutive upper-vector restore requires a related interval.",
            FatalJitErrorCode.ConsecutiveUpperVectorRestoreMustPrecedeAUse => "A consecutive upper-vector restore must precede a use.",
            FatalJitErrorCode.ConsecutiveRegisterSequenceEndedPrematurely => "A consecutive register sequence ended prematurely.",
            FatalJitErrorCode.ConsecutiveSequenceRequiresAdditionalReferences => "A consecutive sequence requires additional references.",
            FatalJitErrorCode.ConsecutiveSequenceEndedPrematurely => "A consecutive sequence ended prematurely.",
            FatalJitErrorCode.ActiveConsecutiveIntervalHasNoRegister => "An active consecutive interval has no register.",
            FatalJitErrorCode.ConsecutiveRegisterReferenceIsMissingItsSuccessor => "Consecutive register reference is missing its successor.",
            FatalJitErrorCode.ARM64RMWOperandPositionExceedsThree => "ARM64 RMW operand position exceeds three.",
            FatalJitErrorCode.EmbeddedFMAAllocationRequiresBlockSequence => "Embedded FMA allocation requires block sequence.",
            FatalJitErrorCode.ARM64IntrinsicArgumentHasUnhandledImmediateRequirements => "ARM64 intrinsic {0} has unhandled immediate requirements.",
            FatalJitErrorCode.UnhandledARM64LowVectorIntrinsicArgument => "Unhandled ARM64 low-vector intrinsic {0}.",
            FatalJitErrorCode.ARM64IntrinsicArgumentHasUnhandledConsecutiveRegisterRequirements => "ARM64 intrinsic {0} has unhandled consecutive register requirements.",
            FatalJitErrorCode.UnexpectedNonContainedArgumentInARM64LSRA => "Unexpected non-contained {0} in ARM64 LSRA.",
            FatalJitErrorCode.UnexpectedARM64ScalableVectorConstantKind => "Unexpected ARM64 scalable vector constant kind.",
            FatalJitErrorCode.ARM64IntegerRemainderMustBeLoweredAndFloatingRemainderIsNotImplemented => "ARM64 integer remainder must be lowered and floating remainder is not implemented.",
            FatalJitErrorCode.ARM32StoreReferenceBuildingRequiresInitializedCodeGen => "ARM32 store reference building requires initialized CodeGen.",
            FatalJitErrorCode.ContainedARM32BlockDestinationRequiresABaseAddress => "Contained ARM32 block destination requires a base address.",
            FatalJitErrorCode.ContainedARM32BlockSourceRequiresABaseAddress => "Contained ARM32 block source requires a base address.",
            FatalJitErrorCode.ARM32REGSWIFTERRORIsNotDefined => "ARM32 REG_SWIFT_ERROR is not defined.",
            FatalJitErrorCode.X86ByteBlockStoreRequiresAnInternalIntegerDefinition => "An x86 byte block store requires an internal integer definition.",
            FatalJitErrorCode.UnsupportedARM64InitializationBlockKindArgument => "Unsupported ARM64 initialization block kind {0}.",
            FatalJitErrorCode.UnsupportedARM64CopyBlockKindArgument => "Unsupported ARM64 copy block kind {0}.",
            FatalJitErrorCode.ContainedARM64BlockDestinationRequiresABaseAddress => "Contained ARM64 block destination requires a base address.",
            FatalJitErrorCode.ContainedARM64BlockSourceRequiresABaseAddress => "Contained ARM64 block source requires a base address.",
            FatalJitErrorCode.UnsupportedLoongArch64InitializationBlockKindArgument => "Unsupported LoongArch64 initialization block kind {0}.",
            FatalJitErrorCode.UnsupportedRISCVInitializationBlockKindArgument => "Unsupported RISC-V initialization block kind {0}.",
            FatalJitErrorCode.UnsupportedLoongArch64CopyBlockKindArgument => "Unsupported LoongArch64 copy block kind {0}.",
            FatalJitErrorCode.UnsupportedRISCVCopyBlockKindArgument => "Unsupported RISC-V copy block kind {0}.",
            FatalJitErrorCode.LinearScanBuildBlockStoreOnThisTargetIsNotImplemented => "LinearScan.buildBlockStore on this target is not implemented.",
            FatalJitErrorCode.PartialX86FieldStoreRequiresAnInternalRegister => "A partial x86 field store requires an internal register.",
            FatalJitErrorCode.LinearScanBuildPutArgStkOnThisTargetIsNotImplemented => "LinearScan.buildPutArgStk on this target is not implemented.",
            FatalJitErrorCode.LSRACallReferenceBuildingIsNotImplementedOnThisTarget => "LSRA call reference building is not implemented on this target.",
            FatalJitErrorCode.WriteBarrierRegisterContractIsNotAvailableOnThisTarget => "The write-barrier register contract is not available on this target.",
            FatalJitErrorCode.OptimizedWriteBarriersRequireInitializedCodeGen => "Optimized write barriers require initialized CodeGen.",
            FatalJitErrorCode.SwiftErrorRegisterNeedsAnArgumentUseAtTheCall => "Swift error register needs an argument use at the call.",
            FatalJitErrorCode.LSRASwiftErrorLifetimeIsNotImplementedOnThisTarget => "LSRA Swift error lifetime is not implemented on this target.",
            FatalJitErrorCode.LSRAAsyncContinuationRegisterIsNotDefinedOnThisTarget => "LSRA async continuation register is not defined on this target.",
            FatalJitErrorCode.LinearScanAllocationDiagnosticStateIsOnlyAvailableInDebugBuilds => "Linear-scan allocation diagnostic state is only available in Debug builds.",
            FatalJitErrorCode.ResolutionValidationRequiresAnIncomingMap => "Resolution validation requires an incoming map.",
            FatalJitErrorCode.ResolutionValidationRequiresAnOutgoingMap => "Resolution validation requires an outgoing map.",
            FatalJitErrorCode.ReadyResolutionTargetRequiresASourceInterval => "Ready resolution target requires a source interval.",
            FatalJitErrorCode.DoubleResolutionRequiresAPendingSource => "Double resolution requires a pending source.",
            FatalJitErrorCode.FloatingResolutionRequiresASourceInterval => "Floating resolution requires a source interval.",
            FatalJitErrorCode.DoubleResolutionRequiresASourceInterval => "Double resolution requires a source interval.",
            FatalJitErrorCode.LSRAAddedLocalMustHaveAnOwningUse => "An LSRA-added local must have an owning use.",
            FatalJitErrorCode.FinalAllocationVerificationWithEnregisteredLocalsIsNotAvailable => "Final allocation verification with enregistered locals is not available.",
            FatalJitErrorCode.ResolutionBlocksRequireEnregisteredLocals => "Resolution blocks require enregistered locals.",
            FatalJitErrorCode.ResolutionMovesRequireEnregisteredLocals => "Resolution moves require enregistered locals.",
            FatalJitErrorCode.AllocationReferenceMustReferToAnInterval => "An allocation reference must refer to an interval.",
            FatalJitErrorCode.ReloadRequiresAnAssignedRegister => "A reload requires an assigned register.",
            FatalJitErrorCode.AssignedIntervalRequiresARegisterRecord => "An assigned interval requires a register record.",
            FatalJitErrorCode.RegisterMoveRequiresAPreviousAssignment => "A register move requires a previous assignment.",
            FatalJitErrorCode.RegisterMoveRequiresADestinationRegister => "A register move requires a destination register.",
            FatalJitErrorCode.CopyRequiresADestinationRegister => "A copy requires a destination register.",
            FatalJitErrorCode.RegisterAssignmentRequiresARegisterRecord => "A register assignment requires a register record.",
            FatalJitErrorCode.UnsupportedFinalAllocationReferenceTypeArgument => "Unsupported final-allocation reference type: {0}.",
            FatalJitErrorCode.CopyRequiresAHomeRegister => "A copy requires a home register.",
            FatalJitErrorCode.UpperVectorReferencesRequireARelatedInterval => "Upper-vector references require a related interval.",
            FatalJitErrorCode.FramePointerBaseRegisterMaskIsNotPorted => "Frame-pointer base register mask is not ported.",
            FatalJitErrorCode.ReservedFrameOffsetRegisterIsNotPorted => "Reserved frame-offset register is not ported.",
            FatalJitErrorCode.SavedLocallocStackPointerRegisterIsNotPorted => "Saved localloc stack-pointer register is not ported.",
            FatalJitErrorCode.TrackedLocalsRequireAnIndexToLocalMapping => "Tracked locals require an index-to-local mapping.",
            FatalJitErrorCode.TrackedLocalsRequireAVariableToRegisterMap => "Tracked locals require a variable-to-register map.",
            FatalJitErrorCode.NoIncomingRegisterMappingExistsForVArgument => "No incoming register mapping exists for V{0:D2}.",
            FatalJitErrorCode.FixedUseRequiresItsPrecedingRegisterReference => "A fixed use requires its preceding register reference.",
            FatalJitErrorCode.VariableLocationPredecessorsRequireLSRABlockInformation => "Variable-location predecessors require LSRA block information.",
            FatalJitErrorCode.ResolutionBlocksRequireASplitEdgeMapping => "Resolution blocks require a split-edge mapping.",
            FatalJitErrorCode.NoSplitEdgeMappingExistsForBBArgument => "No split-edge mapping exists for BB{0:D2}.",
            FatalJitErrorCode.OptimizedWriteBarriersAreUnsupportedOnThisTarget => "Optimized write barriers are unsupported on this target.",
            FatalJitErrorCode.UnrecognizedDoubleAlignmentPolicy => "Unrecognized double-alignment policy.",
            FatalJitErrorCode.UpperVectorLocalIntervalsAreUnsupportedOnThisTarget => "Upper-vector local intervals are unsupported on this target.",
            FatalJitErrorCode.LastUseVerificationRequiresTrackedLocalMapping => "Last-use verification requires tracked local mapping.",
            FatalJitErrorCode.ParameterPreferenceStressRequiresInitializedCodegenState => "Parameter preference stress requires initialized codegen state.",
            FatalJitErrorCode.UnexpectedLocalResolutionReferenceArgument => "Unexpected local resolution reference: {0}.",
            FatalJitErrorCode.ARM32MultiRegParameterAssignmentIsNotPorted => "ARM32 multi-reg parameter assignment is not ported.",
            FatalJitErrorCode.FinalAllocationVerificationRequiresAnOutgoingMap => "Final allocation verification requires an outgoing map.",
            FatalJitErrorCode.FinalAllocationVerificationRequiresAnIncomingMap => "Final allocation verification requires an incoming map.",
            FatalJitErrorCode.VerifiedAllocationReferenceRequiresAnInterval => "A verified allocation reference requires an interval.",
            FatalJitErrorCode.DefinitionRequiresAnAssignedRegister => "A definition requires an assigned register.",
            FatalJitErrorCode.MovedIntervalRequiresAnAssignedRegister => "A moved interval requires an assigned register.",
            FatalJitErrorCode.MoveRequiresADestinationRegister => "A move requires a destination register.",
            FatalJitErrorCode.CopyRequiresAnAssignedRegister => "A copy requires an assigned register.",
            FatalJitErrorCode.IntervalRequiresAnAssignedRegister => "An interval requires an assigned register.",
            FatalJitErrorCode.UnsupportedFinalAllocationReferenceArgument => "Unsupported final-allocation reference: {0}.",
            FatalJitErrorCode.CopiedIntervalRequiresARegister => "A copied interval requires a register.",
            FatalJitErrorCode.UpperVectorReferencesRequireALocal => "Upper-vector references require a local.",
            FatalJitErrorCode.ResolutionBlockRequiresAnIncomingMap => "A resolution block requires an incoming map.",
            FatalJitErrorCode.ResolutionBlockRequiresAnOutgoingMap => "A resolution block requires an outgoing map.",
            FatalJitErrorCode.Failure32BitLongReturnRegisterContractIsNotAvailableOnThisTarget => "The 32-bit long return-register contract is not available on this target.",
            FatalJitErrorCode.ReturnRegisterContractIsNotAvailableOnThisTarget => "The return-register contract is not available on this target.",
            FatalJitErrorCode.LoongArch64HardwareIntrinsicLSRAIsNotPorted => "LoongArch64 hardware-intrinsic LSRA is not ported.",
            FatalJitErrorCode.LinearScanBuildIndirOnThisTargetIsNotImplemented => "LinearScan.buildIndir on this target is not implemented.",
            FatalJitErrorCode.LinearScanBuildLclHeapOnThisTargetIsNotImplemented => "LinearScan.buildLclHeap on this target is not implemented.",
            FatalJitErrorCode.RegisterCandidateLocalsRequireLSRAIntervalMappings => "Register-candidate locals require LSRA interval mappings.",
            FatalJitErrorCode.RegisterCandidateLocalHasNoLSRAInterval => "A register-candidate local has no LSRA interval.",
            FatalJitErrorCode.UnsupportedMinimalLSRADumpEventArgument => "Unsupported minimal LSRA dump event: {0}.",
            FatalJitErrorCode.UnexpectedNonContainedArgumentInLSRANodeBuilding => "Unexpected non-contained {0} in LSRA node building.",
            FatalJitErrorCode.LinearScanTargetIsNotSupported => "LinearScan target is not supported.",
            FatalJitErrorCode.LiveIntervalMustHaveAFirstReference => "A live interval must have a first reference.",
            FatalJitErrorCode.TrackedLocalArgumentHasNoLSRAInterval => "Tracked local {0} has no LSRA interval.",
            FatalJitErrorCode.LSRACannotDefineARegisterForArgument => "LSRA cannot define a register for {0}.",
            FatalJitErrorCode.ARM64MultiRegisterHWIntrinsicHasUnsupportedSIMDSizeArgument => "ARM64 multi-register HW intrinsic has unsupported SIMD size {0}.",
            FatalJitErrorCode.MultiRegisterHWIntrinsicRegisterTypingIsNotImplementedForThisTarget => "Multi-register HW intrinsic register typing is not implemented for this target.",
            FatalJitErrorCode.NoUpperVectorIntervalExistsForTrackedLocalArgument => "No upper-vector interval exists for tracked local {0}.",
            FatalJitErrorCode.DefinitionListEntryMustRetainItsDefiningNode => "A definition-list entry must retain its defining node.",
            FatalJitErrorCode.AssignedARM32DoubleMustRetainItsFirstRegister => "An assigned ARM32 double must retain its first register.",
            FatalJitErrorCode.DoubleRegisterSpillRequiresAnOccupiedHalf => "A double-register spill requires an occupied half.",
            FatalJitErrorCode.SpilledIntervalRequiresAFollowingReference => "A spilled interval requires a following reference.",
            FatalJitErrorCode.SpilledIntervalMustRetainItsAssignedRegister => "A spilled interval must retain its assigned register.",
            FatalJitErrorCode.InsufficientConsecutiveRegisterCandidates => "Insufficient consecutive register candidates.",
            FatalJitErrorCode.UnexpectedLSRARegisterSelectionHeuristic => "Unexpected LSRA register-selection heuristic.",
            FatalJitErrorCode.RegisterSelectionDebugDataIsOnlyAvailableInDebugBuilds => "Register-selection debug data is only available in Debug builds.",
            FatalJitErrorCode.LSRASelectorHeuristicMappingIsIncomplete => "LSRA selector heuristic mapping is incomplete.",
            FatalJitErrorCode.InvalidLsraOrderingValue => "Invalid lsraOrdering value.",
            FatalJitErrorCode.UnknownRISCVIntrinsicArgument => "Unknown RISC-V intrinsic {0}.",
            FatalJitErrorCode.RISCV64SIMDLSRAIsNotPorted => "RISC-V64 SIMD LSRA is not ported.",
            FatalJitErrorCode.RISCV64HardwareIntrinsicLSRAIsNotPorted => "RISC-V64 hardware-intrinsic LSRA is not ported.",
            FatalJitErrorCode.LSRAShiftRotateReferenceBuildingIsNotImplementedOutsideXarch => "LSRA shift/rotate reference building is not implemented outside xarch.",
            FatalJitErrorCode.LSRADivisionReferenceBuildingIsNotImplementedOutsideXarch => "LSRA division reference building is not implemented outside xarch.",
            FatalJitErrorCode.LSRAMultiplicationReferenceBuildingIsNotImplementedOutsideXarch => "LSRA multiplication reference building is not implemented outside xarch.",
            FatalJitErrorCode.LinearScanBuildCastOnThisTargetIsNotImplemented => "LinearScan.buildCast on this target is not implemented.",
            FatalJitErrorCode.UnsupportedARM64ScalarIntrinsicArgument => "Unsupported ARM64 scalar intrinsic {0}.",
            FatalJitErrorCode.LinearScanBuildIntrinsicOnThisTargetIsNotImplemented => "LinearScan.buildIntrinsic on this target is not implemented.",
            FatalJitErrorCode.LinearScanBuildSelectOnThisTargetIsNotImplemented => "LinearScan.buildSelect on this target is not implemented.",
            FatalJitErrorCode.UnsupportedLSRARegisterStressLimit0xArgument => "Unsupported LSRA register stress limit: 0x{0:X}.",
            FatalJitErrorCode.UnsupportedARM64LSRAStressRegisterType => "Unsupported ARM64 LSRA stress register type.",
            FatalJitErrorCode.LSRAStressRegisterLimitingOnThisTargetIsNotPorted => "LSRA stress register limiting on this target is not ported.",
            FatalJitErrorCode.UnsupportedLSRARegisterTypeArgument => "Unsupported LSRA register type: {0}.",
            FatalJitErrorCode.EntryBlockHasNoLSRAStatistics => "The entry block has no LSRA statistics.",
            FatalJitErrorCode.ConsecutiveRegisterAllocationIsNotImplemented => "Consecutive-register allocation is not implemented.",
            FatalJitErrorCode.PartialSIMDCalleeSavesAreNotSupportedOnThisTarget => "Partial SIMD callee saves are not supported on this target.",
            FatalJitErrorCode.RefPositionDebugDataIsOnlyAvailableInDebugBuilds => "RefPosition debug data is only available in Debug builds.",
            FatalJitErrorCode.ConsecutiveRegisterDiagnosticsAreOnlyAvailableInDebugBuilds => "Consecutive-register diagnostics are only available in Debug builds.",
            FatalJitErrorCode.MissingEnumeratorAllocationBlock => "Missing enumerator allocation block.",
            FatalJitErrorCode.MissingEnumeratorDefinition => "Missing enumerator definition.",
            FatalJitErrorCode.ConditionalEscapeRequiresDominators => "Conditional escape requires dominators.",
            FatalJitErrorCode.ConditionalAllocationSDefinitionHasNoDominator => "Conditional allocation's definition has no dominator.",
            FatalJitErrorCode.MissingEnumeratorAllocationTree => "Missing enumerator allocation tree.",
            FatalJitErrorCode.ConditionalEscapeRequiresDFS => "Conditional escape requires DFS.",
            FatalJitErrorCode.MissingAllocationBlock => "Missing allocation block.",
            FatalJitErrorCode.MissingClonedBlock => "Missing cloned block.",
            FatalJitErrorCode.MissingTrackedLocalMap => "Missing tracked-local map.",
            FatalJitErrorCode.MissingConnectionGraph => "Missing connection graph.",
            FatalJitErrorCode.MissingCloneForEnumeratorAppearance => "Missing clone for enumerator appearance.",
            FatalJitErrorCode.MissingClonedStatement => "Missing cloned statement.",
            FatalJitErrorCode.MissingFirstClonedBlock => "Missing first cloned block.",
            FatalJitErrorCode.MissingAllocationTree => "Missing allocation tree.",
            FatalJitErrorCode.MissingGuardInClonedEnumeratorRegion => "Missing guard in cloned enumerator region.",
            FatalJitErrorCode.MissingGuardStatement => "Missing guard statement.",
            FatalJitErrorCode.MissingAncestorInObjectAllocationAnalysis => "Missing ancestor in object-allocation analysis.",
            FatalJitErrorCode.UnexpectedTypeForStackPointerRetyping => "Unexpected type for stack-pointer retyping.",
            FatalJitErrorCode.CSESSAUseNeedsALocalTree => "A CSE SSA use needs a local tree.",
            FatalJitErrorCode.CSEHeuristicDiagnosticsAreOnlyAvailableInDebugBuilds => "CSE heuristic diagnostics are only available in Debug builds.",
            FatalJitErrorCode.CSEHeuristicCommonCanConsiderTreeTargetIntrinsicCategories => "CSE_HeuristicCommon.CanConsiderTree target intrinsic categories",
            FatalJitErrorCode.TrackedLocalMapIsNotAvailable => "Tracked local map is not available.",
            FatalJitErrorCode.RLCSEUpdateCandidateWasPreviouslyConsumed => "RL CSE update candidate was previously consumed.",
            FatalJitErrorCode.RLCSEUpdateSequenceExceedsTheNativeRewardCapacity => "RL CSE update sequence exceeds the native reward capacity.",
            FatalJitErrorCode.RLHookCSECandidateIsMissingItsDescriptor => "RLHook CSE candidate is missing its descriptor.",
            FatalJitErrorCode.RLHookCSEFeatureNamesHaveNotBeenInitialized => "RLHook CSE feature names have not been initialized.",
            FatalJitErrorCode.RandomCSEPermutationIsMissingADescriptor => "Random CSE permutation is missing a descriptor.",
            FatalJitErrorCode.ReplayCSECandidateIsMissingItsDescriptor => "Replay CSE candidate is missing its descriptor.",
            FatalJitErrorCode.LocationWasNotInitialized => "Location was not initialized.",
            FatalJitErrorCode.DecomposedAddressWasUsedMoreTimesThanPlanned => "Decomposed address was used more times than planned.",
            FatalJitErrorCode.BlockStoreRequiresAnAddress => "A block store requires an address.",
            FatalJitErrorCode.BlockReadRequiresAnAddress => "A block read requires an address.",
            FatalJitErrorCode.StructLocalRequiresALayout => "A struct local requires a layout.",
            FatalJitErrorCode.ReplacementMustBelongToAStructLocal => "A replacement must belong to a struct local.",
            FatalJitErrorCode.ReplacementMustBelongToAPromotedAggregate => "A replacement must belong to a promoted aggregate.",
            FatalJitErrorCode.PromotedRetbufferDefinitionMustHaveAnExactSize => "A promoted retbuffer definition must have an exact size.",
            FatalJitErrorCode.PhysicalPromotionLivenessHasNotInitializedThisBlock => "Physical promotion liveness has not initialized this block.",
            FatalJitErrorCode.CallMustDefineItsRetbufferLocal => "The call must define its retbuffer local.",
            FatalJitErrorCode.PhysicalPromotionRequiresTheCurrentDFSTree => "Physical promotion requires the current DFS tree.",
            FatalJitErrorCode.PhysicalPromotionHasNotStartedABlock => "Physical promotion has not started a block.",
            FatalJitErrorCode.PhysicalPromotionHasNotStartedAStatement => "Physical promotion has not started a statement.",
            FatalJitErrorCode.PhysicalPromotionHasNotPreparedReadbacks => "Physical promotion has not prepared readbacks.",
            FatalJitErrorCode.NonFirstStatementMustHaveAPredecessor => "A non-first statement must have a predecessor.",
            FatalJitErrorCode.StructArgumentNeedsALayout => "A struct argument needs a layout.",
            FatalJitErrorCode.OptimizedRetbufferCallNeedsARetbufferArgument => "An optimized retbuffer call needs a retbuffer argument.",
            FatalJitErrorCode.StructLocalNeedsALayout => "A struct local needs a layout.",
            FatalJitErrorCode.StructUseNeedsALayout => "A struct use needs a layout.",
            FatalJitErrorCode.DyingPromotedLocalNeedsAggregateInformation => "A dying promoted local needs aggregate information.",
            FatalJitErrorCode.StructAccessRequiresLayout => "Struct access requires layout.",
            FatalJitErrorCode.CandidateRequiresStructLayout => "Candidate requires struct layout.",
            FatalJitErrorCode.SpilledReductionOperandHasNoDefinition => "Spilled reduction operand has no definition.",
            FatalJitErrorCode.FailedToCloneReductionOperand => "Failed to clone reduction operand.",
            FatalJitErrorCode.SpilledVectorOperandHasNoDefinition => "Spilled vector operand has no definition.",
            FatalJitErrorCode.FailedToCloneVectorOperand => "Failed to clone vector operand.",
            FatalJitErrorCode.TargetSpecificHardwareImmediateRationalizationIsNotPorted => "Target-specific hardware immediate rationalization is not ported.",
            FatalJitErrorCode.UnexpectedMaskIntrinsic => "Unexpected mask intrinsic.",
            FatalJitErrorCode.TargetSpecificMostSignificantBitRationalizationIsNotPorted => "Target-specific most-significant-bit rationalization is not ported.",
            FatalJitErrorCode.WasmRegisterAllocationHasNoCurrentBlock => "Wasm register allocation has no current block.",
            FatalJitErrorCode.WasmIndirectionsRequireABaseAddress => "Wasm indirections require a base address.",
            FatalJitErrorCode.WasmStructStoresRequireALocalLayout => "Wasm struct stores require a local layout.",
            FatalJitErrorCode.WasmLocalStackStoresRequireAnActiveLoweringPhase => "Wasm local stack stores require an active lowering phase.",
            FatalJitErrorCode.LocalVArgumentHasNoTrackedRegisterIndex => "Local V{0} has no tracked-register index.",
            FatalJitErrorCode.WasmParameterRegisterTargetHasNoRegisterMapping => "A Wasm parameter register target has no register mapping.",
            FatalJitErrorCode.WasmTemporaryRegisterBankIsNotInitialized => "A Wasm temporary register bank is not initialized.",
            FatalJitErrorCode.CollectedWasmRegisterReferenceIsMissing => "A collected Wasm register reference is missing.",
            FatalJitErrorCode.WasmRegisterAllocationRequiresCodeGeneration => "Wasm register allocation requires code generation.",
            FatalJitErrorCode.WasmFuncletAllocationStateIsNotInitialized => "Wasm funclet allocation state is not initialized.",
            FatalJitErrorCode.CalleeSavedRegisterMasksAreUnavailableForThisTarget => "Callee-saved register masks are unavailable for this target.",
            FatalJitErrorCode.FloatingPointStackSpillsRequireX86 => "Floating-point stack spills require x86.",
            FatalJitErrorCode.LiveRegisterMasksRequireAFixedRegisterSet => "Live register masks require a fixed register set.",
            FatalJitErrorCode.OSRIntegerCalleeSavedMasksRequireAMD64 => "OSR integer callee-saved masks require AMD64.",
            FatalJitErrorCode.SIMDSpillTempsAreUnsupportedForThisTarget => "SIMD spill temps are unsupported for this target.",
            FatalJitErrorCode.TreeSpillsRequireAFixedRegisterSet => "Tree spills require a fixed register set.",
            FatalJitErrorCode.ScalarEvolutionRequiresValueNumbering => "Scalar evolution requires value numbering.",
            FatalJitErrorCode.ScalarEvolutionRequiresALoop => "Scalar evolution requires a loop.",
            FatalJitErrorCode.UnexpectedOperatorArgument => "Unexpected operator {0}.",
            FatalJitErrorCode.UnexpectedExitOperationArgument => "Unexpected exit operation {0}.",
            FatalJitErrorCode.UnexpectedRelationalOperationArgument => "Unexpected relational operation {0}.",
            FatalJitErrorCode.ExitingBlockMustHaveACondition => "An exiting block must have a condition.",
            FatalJitErrorCode.UnexpectedRelationalResultArgument => "Unexpected relational result {0}.",
            FatalJitErrorCode.UnexpectedBinaryScalarEvolutionOperationArgument => "Unexpected binary scalar evolution operation {0}.",
            FatalJitErrorCode.UnexpectedScalarEvolutionOperationArgument => "Unexpected scalar evolution operation {0}.",
            FatalJitErrorCode.UnexpectedOperationArgument => "Unexpected operation {0}.",
            FatalJitErrorCode.UnexpectedBinaryOperationArgument => "Unexpected binary operation {0}.",
            FatalJitErrorCode.ScalarEvolutionLoopDataIsUnavailable => "Scalar-evolution loop data is unavailable.",
            FatalJitErrorCode.ScalarEvolutionDebugDataIsOnlyAvailableInDebugBuilds => "Scalar-evolution debug data is only available in Debug builds.",
            FatalJitErrorCode.LiveRangeDiagnosticsAreOnlyAvailableInDebugBuilds => "Live range diagnostics are only available in Debug builds.",
            FatalJitErrorCode.ExpectedFirstNodeToPrecedeEndNode => "Expected first node to precede end node.",
            FatalJitErrorCode.ARM64ScalableSIMDConstantsAreNotSupportedOnThisTarget => "ARM64 scalable SIMD constants are not supported on this target.",
            FatalJitErrorCode.UnexpectedARM64ScalableConstantBaseType => "Unexpected ARM64 scalable constant base type.",
            FatalJitErrorCode.SIMDLocalAlignmentForThisTargetIsNotImplemented => "SIMD local alignment for this target is not implemented.",
            FatalJitErrorCode.UnsupportedARM64SIMDElementType => "Unsupported ARM64 SIMD element type.",
            FatalJitErrorCode.UnsupportedARM64MaskElementType => "Unsupported ARM64 mask element type.",
            FatalJitErrorCode.MaskConversionsAreOnlySupportedOnXarchAndARM64 => "Mask conversions are only supported on xarch and ARM64.",
            FatalJitErrorCode.MaskEvaluationIsOnlySupportedOnXarchAndARM64 => "Mask evaluation is only supported on xarch and ARM64.",
            FatalJitErrorCode.SIMDUpperLaneSavesRequireXarchSIMDSupport => "SIMD upper-lane saves require xarch SIMD support.",
            FatalJitErrorCode.SIMDUpperLaneRestoresRequireXarchSIMDSupport => "SIMD upper-lane restores require xarch SIMD support.",
            FatalJitErrorCode.X86SIMD12StackArgumentsAreUnsupportedForThisTarget => "x86 SIMD12 stack arguments are unsupported for this target.",
            FatalJitErrorCode.SIMD12IndirectStoresRequireXarchSIMDSupport => "SIMD12 indirect stores require xarch SIMD support.",
            FatalJitErrorCode.NoDefaultNativeHashIsDefinedForArgument => "No default native hash is defined for {0}.",
            FatalJitErrorCode.HashTableCouldNotReinsertAnOccupiedBucket => "The hash table could not reinsert an occupied bucket.",
            FatalJitErrorCode.HashTableHasNoFreeBucket => "The hash table has no free bucket.",
            FatalJitErrorCode.SSADefinitionBlocksAreNotFinalized => "SSA definition blocks are not finalized.",
            FatalJitErrorCode.SSAFrontiersAreNotFinalized => "SSA frontiers are not finalized.",
            FatalJitErrorCode.SSADFSTreeIsNotAvailable => "SSA DFS tree is not available.",
            FatalJitErrorCode.PhiArgumentHasNoReachingDefinition => "Phi argument has no reaching definition.",
            FatalJitErrorCode.SSAUseHasNoReachingDefinition => "SSA use has no reaching definition.",
            FatalJitErrorCode.SSADefinitionHasNoStatement => "SSA definition has no statement.",
            FatalJitErrorCode.SSAStatementHasNoMatchingDefinition => "SSA statement has no matching definition.",
            FatalJitErrorCode.SSAStatementUseHasNoTree => "SSA statement use has no tree.",
            FatalJitErrorCode.SSADefinitionHasNoTree => "SSA definition has no tree.",
            FatalJitErrorCode.SSAInsertionRequiresValueNumbering => "SSA insertion requires value numbering.",
            FatalJitErrorCode.SSAUseHasNoTree => "SSA use has no tree.",
            FatalJitErrorCode.CannotPopAnEmptySSARenameStack => "Cannot pop an empty SSA rename stack.",
            FatalJitErrorCode.ActiveSSARenameStackIsEmpty => "The active SSA rename stack is empty.",
            FatalJitErrorCode.MemorySSARenameStackIsEmpty => "The memory SSA rename stack is empty.",
            FatalJitErrorCode.InvalidTargetArchitecture => "Invalid target architecture.",
            FatalJitErrorCode.RegisterSuccessorIsNotSupportedForThisTarget => "Register successor is not supported for this target.",
            FatalJitErrorCode.RegisterPredecessorIsNotSupportedForThisTarget => "Register predecessor is not supported for this target.",
            FatalJitErrorCode.RegisterOrderIsUnavailableForThisTarget => "Register order is unavailable for this target.",
            FatalJitErrorCode.FloatingPointRegisterOrderIsUnavailableForThisTarget => "Floating-point register order is unavailable for this target.",
            FatalJitErrorCode.MaskRegisterAllocationOrderIsUnavailableForThisTarget => "Mask-register allocation order is unavailable for this target.",
            FatalJitErrorCode.FixedRegisterMasksAreNotSupportedOnThisTarget => "Fixed register masks are not supported on this target.",
            FatalJitErrorCode.SwiftCallingConventionsAreNotSupportedForThisTarget => "Swift calling conventions are not supported for this target.",
            FatalJitErrorCode.Argument => "{0}",
            FatalJitErrorCode.RegisterMaskEAXIsNotAvailableForTheConfiguredTarget => "Register mask 'EAX' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskECXIsNotAvailableForTheConfiguredTarget => "Register mask 'ECX' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskEDXIsNotAvailableForTheConfiguredTarget => "Register mask 'EDX' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskEBXIsNotAvailableForTheConfiguredTarget => "Register mask 'EBX' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskESPIsNotAvailableForTheConfiguredTarget => "Register mask 'ESP' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskEBPIsNotAvailableForTheConfiguredTarget => "Register mask 'EBP' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskESIIsNotAvailableForTheConfiguredTarget => "Register mask 'ESI' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskEDIIsNotAvailableForTheConfiguredTarget => "Register mask 'EDI' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskRAXIsNotAvailableForTheConfiguredTarget => "Register mask 'RAX' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskRCXIsNotAvailableForTheConfiguredTarget => "Register mask 'RCX' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskRDXIsNotAvailableForTheConfiguredTarget => "Register mask 'RDX' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskRBXIsNotAvailableForTheConfiguredTarget => "Register mask 'RBX' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskRSPIsNotAvailableForTheConfiguredTarget => "Register mask 'RSP' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskRBPIsNotAvailableForTheConfiguredTarget => "Register mask 'RBP' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskRSIIsNotAvailableForTheConfiguredTarget => "Register mask 'RSI' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskRDIIsNotAvailableForTheConfiguredTarget => "Register mask 'RDI' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM0IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM1IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM2IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM3IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM4IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM5IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM6IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM7IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskK0IsNotAvailableForTheConfiguredTarget => "Register mask 'K0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskK1IsNotAvailableForTheConfiguredTarget => "Register mask 'K1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskK2IsNotAvailableForTheConfiguredTarget => "Register mask 'K2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskK3IsNotAvailableForTheConfiguredTarget => "Register mask 'K3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskK4IsNotAvailableForTheConfiguredTarget => "Register mask 'K4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskK5IsNotAvailableForTheConfiguredTarget => "Register mask 'K5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskK6IsNotAvailableForTheConfiguredTarget => "Register mask 'K6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskK7IsNotAvailableForTheConfiguredTarget => "Register mask 'K7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskSTKIsNotAvailableForTheConfiguredTarget => "Register mask 'STK' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR8IsNotAvailableForTheConfiguredTarget => "Register mask 'R8' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR9IsNotAvailableForTheConfiguredTarget => "Register mask 'R9' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR10IsNotAvailableForTheConfiguredTarget => "Register mask 'R10' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR11IsNotAvailableForTheConfiguredTarget => "Register mask 'R11' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR12IsNotAvailableForTheConfiguredTarget => "Register mask 'R12' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR13IsNotAvailableForTheConfiguredTarget => "Register mask 'R13' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR14IsNotAvailableForTheConfiguredTarget => "Register mask 'R14' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR15IsNotAvailableForTheConfiguredTarget => "Register mask 'R15' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR16IsNotAvailableForTheConfiguredTarget => "Register mask 'R16' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR17IsNotAvailableForTheConfiguredTarget => "Register mask 'R17' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR18IsNotAvailableForTheConfiguredTarget => "Register mask 'R18' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR19IsNotAvailableForTheConfiguredTarget => "Register mask 'R19' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR20IsNotAvailableForTheConfiguredTarget => "Register mask 'R20' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR21IsNotAvailableForTheConfiguredTarget => "Register mask 'R21' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR22IsNotAvailableForTheConfiguredTarget => "Register mask 'R22' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR23IsNotAvailableForTheConfiguredTarget => "Register mask 'R23' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR24IsNotAvailableForTheConfiguredTarget => "Register mask 'R24' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR25IsNotAvailableForTheConfiguredTarget => "Register mask 'R25' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR26IsNotAvailableForTheConfiguredTarget => "Register mask 'R26' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR27IsNotAvailableForTheConfiguredTarget => "Register mask 'R27' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR28IsNotAvailableForTheConfiguredTarget => "Register mask 'R28' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR29IsNotAvailableForTheConfiguredTarget => "Register mask 'R29' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR30IsNotAvailableForTheConfiguredTarget => "Register mask 'R30' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR31IsNotAvailableForTheConfiguredTarget => "Register mask 'R31' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM8IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM8' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM9IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM9' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM10IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM10' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM11IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM11' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM12IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM12' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM13IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM13' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM14IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM14' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM15IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM15' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM16IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM16' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM17IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM17' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM18IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM18' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM19IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM19' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM20IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM20' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM21IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM21' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM22IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM22' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM23IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM23' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM24IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM24' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM25IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM25' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM26IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM26' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM27IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM27' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM28IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM28' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM29IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM29' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM30IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM30' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskXMM31IsNotAvailableForTheConfiguredTarget => "Register mask 'XMM31' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR0IsNotAvailableForTheConfiguredTarget => "Register mask 'R0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR1IsNotAvailableForTheConfiguredTarget => "Register mask 'R1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR2IsNotAvailableForTheConfiguredTarget => "Register mask 'R2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR3IsNotAvailableForTheConfiguredTarget => "Register mask 'R3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR4IsNotAvailableForTheConfiguredTarget => "Register mask 'R4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR5IsNotAvailableForTheConfiguredTarget => "Register mask 'R5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR6IsNotAvailableForTheConfiguredTarget => "Register mask 'R6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskR7IsNotAvailableForTheConfiguredTarget => "Register mask 'R7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskSPIsNotAvailableForTheConfiguredTarget => "Register mask 'SP' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskLRIsNotAvailableForTheConfiguredTarget => "Register mask 'LR' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskPCIsNotAvailableForTheConfiguredTarget => "Register mask 'PC' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFPIsNotAvailableForTheConfiguredTarget => "Register mask 'FP' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF0IsNotAvailableForTheConfiguredTarget => "Register mask 'F0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF1IsNotAvailableForTheConfiguredTarget => "Register mask 'F1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF2IsNotAvailableForTheConfiguredTarget => "Register mask 'F2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF3IsNotAvailableForTheConfiguredTarget => "Register mask 'F3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF4IsNotAvailableForTheConfiguredTarget => "Register mask 'F4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF5IsNotAvailableForTheConfiguredTarget => "Register mask 'F5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF6IsNotAvailableForTheConfiguredTarget => "Register mask 'F6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF7IsNotAvailableForTheConfiguredTarget => "Register mask 'F7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF8IsNotAvailableForTheConfiguredTarget => "Register mask 'F8' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF9IsNotAvailableForTheConfiguredTarget => "Register mask 'F9' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF10IsNotAvailableForTheConfiguredTarget => "Register mask 'F10' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF11IsNotAvailableForTheConfiguredTarget => "Register mask 'F11' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF12IsNotAvailableForTheConfiguredTarget => "Register mask 'F12' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF13IsNotAvailableForTheConfiguredTarget => "Register mask 'F13' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF14IsNotAvailableForTheConfiguredTarget => "Register mask 'F14' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF15IsNotAvailableForTheConfiguredTarget => "Register mask 'F15' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF16IsNotAvailableForTheConfiguredTarget => "Register mask 'F16' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF17IsNotAvailableForTheConfiguredTarget => "Register mask 'F17' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF18IsNotAvailableForTheConfiguredTarget => "Register mask 'F18' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF19IsNotAvailableForTheConfiguredTarget => "Register mask 'F19' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF20IsNotAvailableForTheConfiguredTarget => "Register mask 'F20' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF21IsNotAvailableForTheConfiguredTarget => "Register mask 'F21' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF22IsNotAvailableForTheConfiguredTarget => "Register mask 'F22' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF23IsNotAvailableForTheConfiguredTarget => "Register mask 'F23' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF24IsNotAvailableForTheConfiguredTarget => "Register mask 'F24' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF25IsNotAvailableForTheConfiguredTarget => "Register mask 'F25' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF26IsNotAvailableForTheConfiguredTarget => "Register mask 'F26' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF27IsNotAvailableForTheConfiguredTarget => "Register mask 'F27' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF28IsNotAvailableForTheConfiguredTarget => "Register mask 'F28' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF29IsNotAvailableForTheConfiguredTarget => "Register mask 'F29' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF30IsNotAvailableForTheConfiguredTarget => "Register mask 'F30' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskF31IsNotAvailableForTheConfiguredTarget => "Register mask 'F31' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskIP0IsNotAvailableForTheConfiguredTarget => "Register mask 'IP0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskIP1IsNotAvailableForTheConfiguredTarget => "Register mask 'IP1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskPRIsNotAvailableForTheConfiguredTarget => "Register mask 'PR' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskZRIsNotAvailableForTheConfiguredTarget => "Register mask 'ZR' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV0IsNotAvailableForTheConfiguredTarget => "Register mask 'V0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV1IsNotAvailableForTheConfiguredTarget => "Register mask 'V1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV2IsNotAvailableForTheConfiguredTarget => "Register mask 'V2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV3IsNotAvailableForTheConfiguredTarget => "Register mask 'V3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV4IsNotAvailableForTheConfiguredTarget => "Register mask 'V4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV5IsNotAvailableForTheConfiguredTarget => "Register mask 'V5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV6IsNotAvailableForTheConfiguredTarget => "Register mask 'V6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV7IsNotAvailableForTheConfiguredTarget => "Register mask 'V7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV8IsNotAvailableForTheConfiguredTarget => "Register mask 'V8' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV9IsNotAvailableForTheConfiguredTarget => "Register mask 'V9' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV10IsNotAvailableForTheConfiguredTarget => "Register mask 'V10' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV11IsNotAvailableForTheConfiguredTarget => "Register mask 'V11' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV12IsNotAvailableForTheConfiguredTarget => "Register mask 'V12' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV13IsNotAvailableForTheConfiguredTarget => "Register mask 'V13' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV14IsNotAvailableForTheConfiguredTarget => "Register mask 'V14' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV15IsNotAvailableForTheConfiguredTarget => "Register mask 'V15' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV16IsNotAvailableForTheConfiguredTarget => "Register mask 'V16' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV17IsNotAvailableForTheConfiguredTarget => "Register mask 'V17' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV18IsNotAvailableForTheConfiguredTarget => "Register mask 'V18' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV19IsNotAvailableForTheConfiguredTarget => "Register mask 'V19' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV20IsNotAvailableForTheConfiguredTarget => "Register mask 'V20' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV21IsNotAvailableForTheConfiguredTarget => "Register mask 'V21' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV22IsNotAvailableForTheConfiguredTarget => "Register mask 'V22' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV23IsNotAvailableForTheConfiguredTarget => "Register mask 'V23' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV24IsNotAvailableForTheConfiguredTarget => "Register mask 'V24' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV25IsNotAvailableForTheConfiguredTarget => "Register mask 'V25' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV26IsNotAvailableForTheConfiguredTarget => "Register mask 'V26' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV27IsNotAvailableForTheConfiguredTarget => "Register mask 'V27' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV28IsNotAvailableForTheConfiguredTarget => "Register mask 'V28' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV29IsNotAvailableForTheConfiguredTarget => "Register mask 'V29' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV30IsNotAvailableForTheConfiguredTarget => "Register mask 'V30' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskV31IsNotAvailableForTheConfiguredTarget => "Register mask 'V31' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP0IsNotAvailableForTheConfiguredTarget => "Register mask 'P0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP1IsNotAvailableForTheConfiguredTarget => "Register mask 'P1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP2IsNotAvailableForTheConfiguredTarget => "Register mask 'P2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP3IsNotAvailableForTheConfiguredTarget => "Register mask 'P3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP4IsNotAvailableForTheConfiguredTarget => "Register mask 'P4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP5IsNotAvailableForTheConfiguredTarget => "Register mask 'P5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP6IsNotAvailableForTheConfiguredTarget => "Register mask 'P6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP7IsNotAvailableForTheConfiguredTarget => "Register mask 'P7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP8IsNotAvailableForTheConfiguredTarget => "Register mask 'P8' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP9IsNotAvailableForTheConfiguredTarget => "Register mask 'P9' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP10IsNotAvailableForTheConfiguredTarget => "Register mask 'P10' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP11IsNotAvailableForTheConfiguredTarget => "Register mask 'P11' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP12IsNotAvailableForTheConfiguredTarget => "Register mask 'P12' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP13IsNotAvailableForTheConfiguredTarget => "Register mask 'P13' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP14IsNotAvailableForTheConfiguredTarget => "Register mask 'P14' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskP15IsNotAvailableForTheConfiguredTarget => "Register mask 'P15' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFFRIsNotAvailableForTheConfiguredTarget => "Register mask 'FFR' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskRAIsNotAvailableForTheConfiguredTarget => "Register mask 'RA' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskTPIsNotAvailableForTheConfiguredTarget => "Register mask 'TP' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskA0IsNotAvailableForTheConfiguredTarget => "Register mask 'A0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskA1IsNotAvailableForTheConfiguredTarget => "Register mask 'A1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskA2IsNotAvailableForTheConfiguredTarget => "Register mask 'A2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskA3IsNotAvailableForTheConfiguredTarget => "Register mask 'A3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskA4IsNotAvailableForTheConfiguredTarget => "Register mask 'A4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskA5IsNotAvailableForTheConfiguredTarget => "Register mask 'A5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskA6IsNotAvailableForTheConfiguredTarget => "Register mask 'A6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskA7IsNotAvailableForTheConfiguredTarget => "Register mask 'A7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskT0IsNotAvailableForTheConfiguredTarget => "Register mask 'T0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskT1IsNotAvailableForTheConfiguredTarget => "Register mask 'T1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskT2IsNotAvailableForTheConfiguredTarget => "Register mask 'T2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskT3IsNotAvailableForTheConfiguredTarget => "Register mask 'T3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskT4IsNotAvailableForTheConfiguredTarget => "Register mask 'T4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskT5IsNotAvailableForTheConfiguredTarget => "Register mask 'T5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskT6IsNotAvailableForTheConfiguredTarget => "Register mask 'T6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskT7IsNotAvailableForTheConfiguredTarget => "Register mask 'T7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskT8IsNotAvailableForTheConfiguredTarget => "Register mask 'T8' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskX0IsNotAvailableForTheConfiguredTarget => "Register mask 'X0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS0IsNotAvailableForTheConfiguredTarget => "Register mask 'S0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS1IsNotAvailableForTheConfiguredTarget => "Register mask 'S1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS2IsNotAvailableForTheConfiguredTarget => "Register mask 'S2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS3IsNotAvailableForTheConfiguredTarget => "Register mask 'S3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS4IsNotAvailableForTheConfiguredTarget => "Register mask 'S4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS5IsNotAvailableForTheConfiguredTarget => "Register mask 'S5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS6IsNotAvailableForTheConfiguredTarget => "Register mask 'S6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS7IsNotAvailableForTheConfiguredTarget => "Register mask 'S7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS8IsNotAvailableForTheConfiguredTarget => "Register mask 'S8' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskGPIsNotAvailableForTheConfiguredTarget => "Register mask 'GP' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS9IsNotAvailableForTheConfiguredTarget => "Register mask 'S9' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS10IsNotAvailableForTheConfiguredTarget => "Register mask 'S10' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskS11IsNotAvailableForTheConfiguredTarget => "Register mask 'S11' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskZEROIsNotAvailableForTheConfiguredTarget => "Register mask 'ZERO' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT0IsNotAvailableForTheConfiguredTarget => "Register mask 'FT0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT1IsNotAvailableForTheConfiguredTarget => "Register mask 'FT1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT2IsNotAvailableForTheConfiguredTarget => "Register mask 'FT2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT3IsNotAvailableForTheConfiguredTarget => "Register mask 'FT3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT4IsNotAvailableForTheConfiguredTarget => "Register mask 'FT4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT5IsNotAvailableForTheConfiguredTarget => "Register mask 'FT5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT6IsNotAvailableForTheConfiguredTarget => "Register mask 'FT6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT7IsNotAvailableForTheConfiguredTarget => "Register mask 'FT7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS0IsNotAvailableForTheConfiguredTarget => "Register mask 'FS0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS1IsNotAvailableForTheConfiguredTarget => "Register mask 'FS1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFA0IsNotAvailableForTheConfiguredTarget => "Register mask 'FA0' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFA1IsNotAvailableForTheConfiguredTarget => "Register mask 'FA1' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFA2IsNotAvailableForTheConfiguredTarget => "Register mask 'FA2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFA3IsNotAvailableForTheConfiguredTarget => "Register mask 'FA3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFA4IsNotAvailableForTheConfiguredTarget => "Register mask 'FA4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFA5IsNotAvailableForTheConfiguredTarget => "Register mask 'FA5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFA6IsNotAvailableForTheConfiguredTarget => "Register mask 'FA6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFA7IsNotAvailableForTheConfiguredTarget => "Register mask 'FA7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS2IsNotAvailableForTheConfiguredTarget => "Register mask 'FS2' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS3IsNotAvailableForTheConfiguredTarget => "Register mask 'FS3' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS4IsNotAvailableForTheConfiguredTarget => "Register mask 'FS4' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS5IsNotAvailableForTheConfiguredTarget => "Register mask 'FS5' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS6IsNotAvailableForTheConfiguredTarget => "Register mask 'FS6' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS7IsNotAvailableForTheConfiguredTarget => "Register mask 'FS7' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS8IsNotAvailableForTheConfiguredTarget => "Register mask 'FS8' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS9IsNotAvailableForTheConfiguredTarget => "Register mask 'FS9' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS10IsNotAvailableForTheConfiguredTarget => "Register mask 'FS10' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFS11IsNotAvailableForTheConfiguredTarget => "Register mask 'FS11' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT8IsNotAvailableForTheConfiguredTarget => "Register mask 'FT8' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT9IsNotAvailableForTheConfiguredTarget => "Register mask 'FT9' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT10IsNotAvailableForTheConfiguredTarget => "Register mask 'FT10' is not available for the configured target.",
            FatalJitErrorCode.RegisterMaskFT11IsNotAvailableForTheConfiguredTarget => "Register mask 'FT11' is not available for the configured target.",
            FatalJitErrorCode.NYIWASM64 => "NYI: WASM64",
            FatalJitErrorCode.FloatingRegisterNamesAreOnlyAvailableInDebugBuilds => "Floating register names are only available in Debug builds.",
            FatalJitErrorCode.UnsupportedTargetArchitecture => "Unsupported target architecture.",
            FatalJitErrorCode.WebAssemblyRegisterNamesRequireTheWebAssemblyTarget => "WebAssembly register names require the WebAssembly target.",
            FatalJitErrorCode.ProfilerEntryRegisterIsUnavailableForThisABI => "Profiler entry register is unavailable for this ABI.",
            FatalJitErrorCode.ProfilerCallTargetIsUnavailableForThisABI => "Profiler call target is unavailable for this ABI.",
            FatalJitErrorCode.AMD64ABIHasNoFifthIntegerArgumentRegister => "The AMD64 ABI has no fifth integer argument register.",
            FatalJitErrorCode.AMD64ABIHasNoSixthIntegerArgumentRegister => "The AMD64 ABI has no sixth integer argument register.",
            FatalJitErrorCode.AMD64ABIHasNoFifthFloatingPointArgumentRegister => "The AMD64 ABI has no fifth floating-point argument register.",
            FatalJitErrorCode.AMD64ABIHasNoSixthFloatingPointArgumentRegister => "The AMD64 ABI has no sixth floating-point argument register.",
            FatalJitErrorCode.AMD64ABIHasNoSeventhFloatingPointArgumentRegister => "The AMD64 ABI has no seventh floating-point argument register.",
            FatalJitErrorCode.AMD64ABIHasNoEighthFloatingPointArgumentRegister => "The AMD64 ABI has no eighth floating-point argument register.",
            FatalJitErrorCode.SwiftABIErrorRegisterIsUnavailableForThisABI => "Swift ABI error register is unavailable for this ABI.",
            FatalJitErrorCode.SwiftABISelfRegisterIsUnavailableForThisABI => "Swift ABI self register is unavailable for this ABI.",
            FatalJitErrorCode.SwiftABIReturnRegistersAreUnavailableForThisABI => "Swift ABI return registers are unavailable for this ABI.",
            FatalJitErrorCode.SwiftABIReturnBufferIsUnavailableForThisABI => "Swift ABI return buffer is unavailable for this ABI.",
            FatalJitErrorCode.WebAssemblyRegisterOperationsRequireTheWebAssemblyTarget => "WebAssembly register operations require the WebAssembly target.",
            FatalJitErrorCode.TargetDWARFRegisterMappingIsNotPorted => "The target DWARF register mapping is not ported.",
            FatalJitErrorCode.UnexpectedARMDWARFRegister => "Unexpected ARM DWARF register.",
            FatalJitErrorCode.TargetCFIRegisterPolicyIsNotPorted => "The target CFI register policy is not ported.",
            FatalJitErrorCode.PushUnwindRecordingIsNotImplementedForThisTarget => "Push unwind recording is not implemented for this target.",
            FatalJitErrorCode.PairedPushUnwindRecordingIsNotImplementedForThisTarget => "Paired-push unwind recording is not implemented for this target.",
            FatalJitErrorCode.PointerAuthenticationUnwindRecordingIsNotImplementedForThisTarget => "Pointer-authentication unwind recording is not implemented for this target.",
            FatalJitErrorCode.ReturnUnwindRecordingIsNotImplementedForThisTarget => "Return unwind recording is not implemented for this target.",
            FatalJitErrorCode.StackUnwindRecordingIsNotImplementedForThisTarget => "Stack unwind recording is not implemented for this target.",
            FatalJitErrorCode.FrameRegisterUnwindRecordingIsNotImplementedForThisTarget => "Frame-register unwind recording is not implemented for this target.",
            FatalJitErrorCode.RegisterSaveUnwindRecordingIsNotImplementedForThisTarget => "Register-save unwind recording is not implemented for this target.",
            FatalJitErrorCode.UnwindPaddingIsNotImplementedForThisTarget => "Unwind padding is not implemented for this target.",
            FatalJitErrorCode.RegisterPairUnwindRecordingIsNotImplementedForThisTarget => "Register-pair unwind recording is not implemented for this target.",
            FatalJitErrorCode.PreindexedRegisterPairUnwindRecordingIsNotImplementedForThisTarget => "Preindexed register-pair unwind recording is not implemented for this target.",
            FatalJitErrorCode.PreindexedRegisterUnwindRecordingIsNotImplementedForThisTarget => "Preindexed register unwind recording is not implemented for this target.",
            FatalJitErrorCode.SizedUnwindPaddingRequiresARM32 => "Sized unwind padding requires ARM32.",
            FatalJitErrorCode.XarchWasmUnwindEEPublicationRequiresXarchOrWasm => "Xarch/Wasm unwind EE publication requires xarch or Wasm.",
            FatalJitErrorCode.UnwindEEPublicationForThisTargetIsNotPorted => "Unwind EE publication for this target is not ported.",
            FatalJitErrorCode.UnwindOpcodeSizeDecodingForThisTargetIsNotPorted => "Unwind opcode-size decoding for this target is not ported.",
            FatalJitErrorCode.UnwindDiagnosticDecodingForThisTargetIsNotPorted => "Unwind diagnostic decoding for this target is not ported.",
            FatalJitErrorCode.UnwindNOPDiagnosticsAreOnlyAvailableInDebugBuilds => "Unwind NOP diagnostics are only available in Debug builds.",
            FatalJitErrorCode.UnwindInitializationStateIsOnlyAvailableInDebugBuilds => "Unwind initialization state is only available in Debug builds.",
            FatalJitErrorCode.UnwindStorageDiagnosticsForThisTargetAreNotPorted => "Unwind storage diagnostics for this target are not ported.",
            FatalJitErrorCode.UnwindInstructionSizeQueriesRequireARM32 => "Unwind instruction-size queries require ARM32.",
            FatalJitErrorCode.FragmentedUnwindStorageForThisTargetIsNotPorted => "Fragmented unwind storage for this target is not ported.",
            FatalJitErrorCode.UnwindPrologDiagnosticsForThisTargetAreNotPorted => "Unwind prolog diagnostics for this target are not ported.",
            FatalJitErrorCode.UnwindEpilogDiagnosticsForThisTargetAreNotPorted => "Unwind epilog diagnostics for this target are not ported.",
            FatalJitErrorCode.CompilerFreeUnwindEpilogDiagnosticsRequireLoongArch64 => "Compiler-free unwind epilog diagnostics require LoongArch64.",
            FatalJitErrorCode.UnwindEpilogScopeDiagnosticsForThisTargetAreNotPorted => "Unwind epilog-scope diagnostics for this target are not ported.",
            FatalJitErrorCode.NumberedUnwindFragmentDiagnosticsRequireRISCV64 => "Numbered unwind fragment diagnostics require RISC-V64.",
            FatalJitErrorCode.EmitterIsRequiredToReserveUnwindInformation => "An emitter is required to reserve unwind information.",
            FatalJitErrorCode.PrologUnwindCodesHaveNotBeenRecorded => "The prolog unwind codes have not been recorded.",
            FatalJitErrorCode.EmitterIsRequiredToPublishUnwindInformation => "An emitter is required to publish unwind information.",
            FatalJitErrorCode.EmitterIsRequiredToResolveUnwindLocations => "An emitter is required to resolve unwind locations.",
            FatalJitErrorCode.PrologUnwindCodesHaveNotBeenReserved => "The prolog unwind codes have not been reserved.",
            FatalJitErrorCode.ARM32UnwindDiagnosticsAreOnlyAvailableInDebugBuilds => "ARM32 unwind diagnostics are only available in Debug builds.",
            FatalJitErrorCode.ARM32UnwindDiagnosticsRequireARM32 => "ARM32 unwind diagnostics require ARM32.",
            FatalJitErrorCode.UnwindCodesBaseGetCodesStorageIsNotPorted => "UnwindCodesBase::GetCodes storage is not ported.",
            FatalJitErrorCode.UnwindSaveRegPairIsNotImplementedForLoongArch64 => "unwindSaveRegPair is not implemented for LoongArch64.",
            FatalJitErrorCode.UnwindPushIsUnreachableUseOneOfTheUnwindSaveRegFunctionsInstead => "unwindPush is unreachable; use one of the unwindSaveReg* functions instead.",
            FatalJitErrorCode.UnwindSaveRegPairUnusedOnRISCV64Yet => "unwindSaveRegPair-----unused on RISCV64 yet----",
            FatalJitErrorCode.RISCVUnwindDiagnosticsAreOnlyAvailableInDebugBuilds => "RISC-V unwind diagnostics are only available in Debug builds.",
            FatalJitErrorCode.RISCVUnwindDiagnosticsRequireRISCV64 => "RISC-V unwind diagnostics require RISC-V64.",
            FatalJitErrorCode.WasmUnwindStartLocationIsMissing => "Wasm unwind start location is missing.",
            FatalJitErrorCode.WasmUnwindEndLocationIsMissing => "Wasm unwind end location is missing.",
            FatalJitErrorCode.InvalidFloatingPointJITConfigurationValue => "Invalid floating-point JIT configuration value.",
            FatalJitErrorCode.ConfigurationArraysAreOnlyAvailableInDebugBuilds => "Configuration arrays are only available in Debug builds.",
            FatalJitErrorCode.ConfigIntArrayRequiresAConfigurationString => "ConfigIntArray requires a configuration string.",
            FatalJitErrorCode.RegisterRangeFormattingIsNotSupportedForThisTarget => "Register range formatting is not supported for this target.",
            FatalJitErrorCode.FixedWidth32ByteAnd64ByteVectorsAreOnlySupportedOnXarch => "Fixed-width 32-byte and 64-byte vectors are only supported on xarch.",
            FatalJitErrorCode.FixedWidth32ByteVectorsAreOnlySupportedOnXarch => "Fixed-width 32-byte vectors are only supported on xarch.",
            FatalJitErrorCode.FixedWidth64ByteVectorsAreOnlySupportedOnXarch => "Fixed-width 64-byte vectors are only supported on xarch.",
            FatalJitErrorCode.ValueNumberDiagnosticsAreOnlyAvailableInDebugBuilds => "Value number diagnostics are only available in Debug builds.",
            FatalJitErrorCode.UnexpectedScalableSIMDConstantKind => "Unexpected scalable SIMD constant kind.",
            FatalJitErrorCode.UnexpectedVNConstantType => "Unexpected VN constant type.",
            FatalJitErrorCode.HardwareIntrinsicsAreNotEnabledForThisTarget => "Hardware intrinsics are not enabled for this target.",
            FatalJitErrorCode.UnsupportedDoubleMathIntrinsicArgument => "Unsupported double math intrinsic: {0}",
            FatalJitErrorCode.UnsupportedFloatMathIntrinsicArgument => "Unsupported float math intrinsic: {0}",
            FatalJitErrorCode.UnsupportedIntegralMathSourceArgument => "Unsupported integral math source: {0}",
            FatalJitErrorCode.UnsupportedBitCountingIntrinsicArgument => "Unsupported bit-counting intrinsic: {0}",
            FatalJitErrorCode.UnsupportedUnaryMathIntrinsicArgument => "Unsupported unary math intrinsic: {0}",
            FatalJitErrorCode.UnsupportedBinaryDoubleIntrinsicArgument => "Unsupported binary double intrinsic: {0}",
            FatalJitErrorCode.UnsupportedBinaryFloatIntrinsicArgument => "Unsupported binary float intrinsic: {0}",
            FatalJitErrorCode.UnsupportedBinaryMathIntrinsicArgument => "Unsupported binary math intrinsic: {0}",
            FatalJitErrorCode.NativeMinMaxEvaluationIsOnlySupportedOnRISCVAndWasm => "Native min/max evaluation is only supported on RISC-V and Wasm.",
            FatalJitErrorCode.MostSignificantBitExtractionIsNotSupportedForThisTarget => "Most-significant-bit extraction is not supported for this target.",
            FatalJitErrorCode.UnrecognizedMapValueType => "Unrecognized map value type.",
            FatalJitErrorCode.ScalableMaskValuesAreOnlySupportedOnARM64WithMaskedIntrinsicsEnabled => "Scalable mask values are only supported on ARM64 with masked intrinsics enabled.",
            FatalJitErrorCode.SIMDTypesAreNotEnabledForThisTarget => "SIMD types are not enabled for this target.",
            FatalJitErrorCode.ARM64ScalableVectorTypeIsNotSupportedOnXarch => "The ARM64 scalable-vector type is not supported on xarch.",
            FatalJitErrorCode.FixedWidth32ByteAnd64ByteVectorsAreNotSupportedOnARM64 => "Fixed-width 32-byte and 64-byte vectors are not supported on ARM64.",
            FatalJitErrorCode.ArchitectureSpecificSIMDTypesAreNotSupportedForThisTarget => "Architecture-specific SIMD types are not supported for this target.",
            FatalJitErrorCode.NativeVariableTypeOrdinalArgumentIsInvalid => "Native variable type ordinal {0} is invalid.",
            FatalJitErrorCode.ConfigMethodRangeIsOnlyAvailableInDebugBuilds => "ConfigMethodRange is only available in Debug builds.",
            FatalJitErrorCode.AsyncContinuationVariableListHasNotBeenCreated => "Async continuation variable list has not been created",
            FatalJitErrorCode.AsyncSuspensionPointListHasNotBeenCreated => "Async suspension point list has not been created",
            FatalJitErrorCode.InlinedAsyncFrameHasNoContextHandling => "Inlined async frame has no context handling",
            FatalJitErrorCode.WasmControlFlowIntervalsAreNotAvailableForDiagnostics => "Wasm control-flow intervals are not available for diagnostics.",
            FatalJitErrorCode.UnsupportedHardwareIntrinsicPlatform => "Unsupported hardware intrinsic platform",
            FatalJitErrorCode.HardwareIntrinsicLookupRangesAreUnavailableForThisTarget => "Hardware intrinsic lookup ranges are unavailable for this target.",
            FatalJitErrorCode.HardwareIntrinsicTablesAreUnavailableForThisTarget => "Hardware-intrinsic tables are unavailable for this target.",
            FatalJitErrorCode.MissingEnumeratorAppearances => "Missing enumerator appearances.",
            FatalJitErrorCode.MissingEnumeratorAppearanceList => "Missing enumerator appearance list.",
            FatalJitErrorCode.MissingAllocationTempAppearances => "Missing allocation-temp appearances.",
            FatalJitErrorCode.MissingUseTempAppearances => "Missing use-temp appearances.",
            FatalJitErrorCode.MissingEnumeratorAppearanceMap => "Missing enumerator appearance map.",
            FatalJitErrorCode.MissingBlocksToClone => "Missing blocks to clone.",
            FatalJitErrorCode.CloneExtentWasNotComputed => "Clone extent was not computed.",
            FatalJitErrorCode.CSEHeuristicMetricsHaveNotBeenInitialized => "CSE heuristic metrics have not been initialized.",
            FatalJitErrorCode.CSEHeuristicMetricsAreOnlyAvailableInDebugBuilds => "CSE heuristic metrics are only available in Debug builds.",
            FatalJitErrorCode.CSELocalWeightsHaveNotBeenCaptured => "CSE local weights have not been captured.",
            FatalJitErrorCode.CSEHeuristicLikelihoodsHaveNotBeenInitialized => "CSE heuristic likelihoods have not been initialized.",
            FatalJitErrorCode.RLCSEFeatureMetricsHaveNotBeenInitialized => "RL CSE feature metrics have not been initialized.",
            FatalJitErrorCode.RLCSEBaseLikelihoodsHaveNotBeenInitialized => "RL CSE base likelihoods have not been initialized.",
            FatalJitErrorCode.RLCSELikelihoodsHaveNotBeenInitialized => "RL CSE likelihoods have not been initialized.",
            _ => null,
        };

        return message;
    }
    /// <summary>Gets the stable identifier for this fatal JIT failure.</summary>
    public FatalJitErrorCode ErrorCode { get; }

    public CorJitResult Result { get; }
}
