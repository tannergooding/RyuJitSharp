// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

/// <summary>Identifies a fatal JIT failure independently of its optional detailed message.</summary>
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
    DirectFailureMessage077 = 77,
    /// <summary>Stable direct-failure message identifier 78.</summary>
    DirectFailureMessage078 = 78,
    /// <summary>Stable direct-failure message identifier 79.</summary>
    DirectFailureMessage079 = 79,
    /// <summary>Stable direct-failure message identifier 80.</summary>
    DirectFailureMessage080 = 80,
    /// <summary>Stable direct-failure message identifier 81.</summary>
    DirectFailureMessage081 = 81,
    /// <summary>Stable direct-failure message identifier 82.</summary>
    DirectFailureMessage082 = 82,
    /// <summary>Stable direct-failure message identifier 83.</summary>
    DirectFailureMessage083 = 83,
    /// <summary>Stable direct-failure message identifier 84.</summary>
    DirectFailureMessage084 = 84,
    /// <summary>Stable direct-failure message identifier 85.</summary>
    DirectFailureMessage085 = 85,
    /// <summary>Stable direct-failure message identifier 86.</summary>
    DirectFailureMessage086 = 86,
    /// <summary>Stable direct-failure message identifier 87.</summary>
    DirectFailureMessage087 = 87,
    /// <summary>Stable direct-failure message identifier 88.</summary>
    DirectFailureMessage088 = 88,
    /// <summary>Stable direct-failure message identifier 89.</summary>
    DirectFailureMessage089 = 89,
    /// <summary>Stable direct-failure message identifier 90.</summary>
    DirectFailureMessage090 = 90,
    /// <summary>Stable direct-failure message identifier 91.</summary>
    DirectFailureMessage091 = 91,
    /// <summary>Stable direct-failure message identifier 92.</summary>
    DirectFailureMessage092 = 92,
    /// <summary>Stable direct-failure message identifier 93.</summary>
    DirectFailureMessage093 = 93,
    /// <summary>Stable direct-failure message identifier 94.</summary>
    DirectFailureMessage094 = 94,
    /// <summary>Stable direct-failure message identifier 95.</summary>
    DirectFailureMessage095 = 95,
    /// <summary>Stable direct-failure message identifier 96.</summary>
    DirectFailureMessage096 = 96,
    /// <summary>Stable direct-failure message identifier 97.</summary>
    DirectFailureMessage097 = 97,
    /// <summary>Stable direct-failure message identifier 98.</summary>
    DirectFailureMessage098 = 98,
    /// <summary>Stable direct-failure message identifier 99.</summary>
    DirectFailureMessage099 = 99,
    /// <summary>Stable direct-failure message identifier 100.</summary>
    DirectFailureMessage100 = 100,
    /// <summary>Stable direct-failure message identifier 101.</summary>
    DirectFailureMessage101 = 101,
    /// <summary>Stable direct-failure message identifier 102.</summary>
    DirectFailureMessage102 = 102,
    /// <summary>Stable direct-failure message identifier 103.</summary>
    DirectFailureMessage103 = 103,
    /// <summary>Stable direct-failure message identifier 104.</summary>
    DirectFailureMessage104 = 104,
    /// <summary>Stable direct-failure message identifier 105.</summary>
    DirectFailureMessage105 = 105,
    /// <summary>Stable direct-failure message identifier 106.</summary>
    DirectFailureMessage106 = 106,
    /// <summary>Stable direct-failure message identifier 107.</summary>
    DirectFailureMessage107 = 107,
    /// <summary>Stable direct-failure message identifier 108.</summary>
    DirectFailureMessage108 = 108,
    /// <summary>Stable direct-failure message identifier 109.</summary>
    DirectFailureMessage109 = 109,
    /// <summary>Stable direct-failure message identifier 110.</summary>
    DirectFailureMessage110 = 110,
    /// <summary>Stable direct-failure message identifier 111.</summary>
    DirectFailureMessage111 = 111,
    /// <summary>Stable direct-failure message identifier 112.</summary>
    DirectFailureMessage112 = 112,
    /// <summary>Stable direct-failure message identifier 113.</summary>
    DirectFailureMessage113 = 113,
    /// <summary>Stable direct-failure message identifier 114.</summary>
    DirectFailureMessage114 = 114,
    /// <summary>Stable direct-failure message identifier 115.</summary>
    DirectFailureMessage115 = 115,
    /// <summary>Stable direct-failure message identifier 116.</summary>
    DirectFailureMessage116 = 116,
    /// <summary>Stable direct-failure message identifier 117.</summary>
    DirectFailureMessage117 = 117,
    /// <summary>Stable direct-failure message identifier 118.</summary>
    DirectFailureMessage118 = 118,
    /// <summary>Stable direct-failure message identifier 119.</summary>
    DirectFailureMessage119 = 119,
    /// <summary>Stable direct-failure message identifier 120.</summary>
    DirectFailureMessage120 = 120,
    /// <summary>Stable direct-failure message identifier 121.</summary>
    DirectFailureMessage121 = 121,
    /// <summary>Stable direct-failure message identifier 122.</summary>
    DirectFailureMessage122 = 122,
    /// <summary>Stable direct-failure message identifier 123.</summary>
    DirectFailureMessage123 = 123,
    /// <summary>Stable direct-failure message identifier 124.</summary>
    DirectFailureMessage124 = 124,
    /// <summary>Stable direct-failure message identifier 125.</summary>
    DirectFailureMessage125 = 125,
    /// <summary>Stable direct-failure message identifier 126.</summary>
    DirectFailureMessage126 = 126,
    /// <summary>Stable direct-failure message identifier 127.</summary>
    DirectFailureMessage127 = 127,
    /// <summary>Stable direct-failure message identifier 128.</summary>
    DirectFailureMessage128 = 128,
    /// <summary>Stable direct-failure message identifier 129.</summary>
    DirectFailureMessage129 = 129,
    /// <summary>Stable direct-failure message identifier 130.</summary>
    DirectFailureMessage130 = 130,
    /// <summary>Stable direct-failure message identifier 131.</summary>
    DirectFailureMessage131 = 131,
    /// <summary>Stable direct-failure message identifier 132.</summary>
    DirectFailureMessage132 = 132,
    /// <summary>Stable direct-failure message identifier 133.</summary>
    DirectFailureMessage133 = 133,
    /// <summary>Stable direct-failure message identifier 134.</summary>
    DirectFailureMessage134 = 134,
    /// <summary>Stable direct-failure message identifier 135.</summary>
    DirectFailureMessage135 = 135,
    /// <summary>Stable direct-failure message identifier 136.</summary>
    DirectFailureMessage136 = 136,
    /// <summary>Stable direct-failure message identifier 137.</summary>
    DirectFailureMessage137 = 137,
    /// <summary>Stable direct-failure message identifier 138.</summary>
    DirectFailureMessage138 = 138,
    /// <summary>Stable direct-failure message identifier 139.</summary>
    DirectFailureMessage139 = 139,
    /// <summary>Stable direct-failure message identifier 140.</summary>
    DirectFailureMessage140 = 140,
    /// <summary>Stable direct-failure message identifier 141.</summary>
    DirectFailureMessage141 = 141,
    /// <summary>Stable direct-failure message identifier 142.</summary>
    DirectFailureMessage142 = 142,
    /// <summary>Stable direct-failure message identifier 143.</summary>
    DirectFailureMessage143 = 143,
    /// <summary>Stable direct-failure message identifier 144.</summary>
    DirectFailureMessage144 = 144,
    /// <summary>Stable direct-failure message identifier 145.</summary>
    DirectFailureMessage145 = 145,
    /// <summary>Stable direct-failure message identifier 146.</summary>
    DirectFailureMessage146 = 146,
    /// <summary>Stable direct-failure message identifier 147.</summary>
    DirectFailureMessage147 = 147,
    /// <summary>Stable direct-failure message identifier 148.</summary>
    DirectFailureMessage148 = 148,
    /// <summary>Stable direct-failure message identifier 149.</summary>
    DirectFailureMessage149 = 149,
    /// <summary>Stable direct-failure message identifier 150.</summary>
    DirectFailureMessage150 = 150,
    /// <summary>Stable direct-failure message identifier 151.</summary>
    DirectFailureMessage151 = 151,
    /// <summary>Stable direct-failure message identifier 152.</summary>
    DirectFailureMessage152 = 152,
    /// <summary>Stable direct-failure message identifier 153.</summary>
    DirectFailureMessage153 = 153,
    /// <summary>Stable direct-failure message identifier 154.</summary>
    DirectFailureMessage154 = 154,
    /// <summary>Stable direct-failure message identifier 155.</summary>
    DirectFailureMessage155 = 155,
    /// <summary>Stable direct-failure message identifier 156.</summary>
    DirectFailureMessage156 = 156,
    /// <summary>Stable direct-failure message identifier 157.</summary>
    DirectFailureMessage157 = 157,
    /// <summary>Stable direct-failure message identifier 158.</summary>
    DirectFailureMessage158 = 158,
    /// <summary>Stable direct-failure message identifier 159.</summary>
    DirectFailureMessage159 = 159,
    /// <summary>Stable direct-failure message identifier 160.</summary>
    DirectFailureMessage160 = 160,
    /// <summary>Stable direct-failure message identifier 161.</summary>
    DirectFailureMessage161 = 161,
    /// <summary>Stable direct-failure message identifier 162.</summary>
    DirectFailureMessage162 = 162,
    /// <summary>Stable direct-failure message identifier 163.</summary>
    DirectFailureMessage163 = 163,
    /// <summary>Stable direct-failure message identifier 164.</summary>
    DirectFailureMessage164 = 164,
    /// <summary>Stable direct-failure message identifier 165.</summary>
    DirectFailureMessage165 = 165,
    /// <summary>Stable direct-failure message identifier 166.</summary>
    DirectFailureMessage166 = 166,
    /// <summary>Stable direct-failure message identifier 167.</summary>
    DirectFailureMessage167 = 167,
    /// <summary>Stable direct-failure message identifier 168.</summary>
    DirectFailureMessage168 = 168,
    /// <summary>Stable direct-failure message identifier 169.</summary>
    DirectFailureMessage169 = 169,
    /// <summary>Stable direct-failure message identifier 170.</summary>
    DirectFailureMessage170 = 170,
    /// <summary>Stable direct-failure message identifier 171.</summary>
    DirectFailureMessage171 = 171,
    /// <summary>Stable direct-failure message identifier 172.</summary>
    DirectFailureMessage172 = 172,
    /// <summary>Stable direct-failure message identifier 173.</summary>
    DirectFailureMessage173 = 173,
    /// <summary>Stable direct-failure message identifier 174.</summary>
    DirectFailureMessage174 = 174,
    /// <summary>Stable direct-failure message identifier 175.</summary>
    DirectFailureMessage175 = 175,
    /// <summary>Stable direct-failure message identifier 176.</summary>
    DirectFailureMessage176 = 176,
    /// <summary>Stable direct-failure message identifier 177.</summary>
    DirectFailureMessage177 = 177,
    /// <summary>Stable direct-failure message identifier 178.</summary>
    DirectFailureMessage178 = 178,
    /// <summary>Stable direct-failure message identifier 179.</summary>
    DirectFailureMessage179 = 179,
    /// <summary>Stable direct-failure message identifier 180.</summary>
    DirectFailureMessage180 = 180,
    /// <summary>Stable direct-failure message identifier 181.</summary>
    DirectFailureMessage181 = 181,
    /// <summary>Stable direct-failure message identifier 182.</summary>
    DirectFailureMessage182 = 182,
    /// <summary>Stable direct-failure message identifier 183.</summary>
    DirectFailureMessage183 = 183,
    /// <summary>Stable direct-failure message identifier 184.</summary>
    DirectFailureMessage184 = 184,
    /// <summary>Stable direct-failure message identifier 185.</summary>
    DirectFailureMessage185 = 185,
    /// <summary>Stable direct-failure message identifier 186.</summary>
    DirectFailureMessage186 = 186,
    /// <summary>Stable direct-failure message identifier 187.</summary>
    DirectFailureMessage187 = 187,
    /// <summary>Stable direct-failure message identifier 188.</summary>
    DirectFailureMessage188 = 188,
    /// <summary>Stable direct-failure message identifier 189.</summary>
    DirectFailureMessage189 = 189,
    /// <summary>Stable direct-failure message identifier 190.</summary>
    DirectFailureMessage190 = 190,
    /// <summary>Stable direct-failure message identifier 191.</summary>
    DirectFailureMessage191 = 191,
    /// <summary>Stable direct-failure message identifier 192.</summary>
    DirectFailureMessage192 = 192,
    /// <summary>Stable direct-failure message identifier 193.</summary>
    DirectFailureMessage193 = 193,
    /// <summary>Stable direct-failure message identifier 194.</summary>
    DirectFailureMessage194 = 194,
    /// <summary>Stable direct-failure message identifier 195.</summary>
    DirectFailureMessage195 = 195,
    /// <summary>Stable direct-failure message identifier 196.</summary>
    DirectFailureMessage196 = 196,
    /// <summary>Stable direct-failure message identifier 197.</summary>
    DirectFailureMessage197 = 197,
    /// <summary>Stable direct-failure message identifier 198.</summary>
    DirectFailureMessage198 = 198,
    /// <summary>Stable direct-failure message identifier 199.</summary>
    DirectFailureMessage199 = 199,
    /// <summary>Stable direct-failure message identifier 200.</summary>
    DirectFailureMessage200 = 200,
    /// <summary>Stable direct-failure message identifier 201.</summary>
    DirectFailureMessage201 = 201,
    /// <summary>Stable direct-failure message identifier 202.</summary>
    DirectFailureMessage202 = 202,
    /// <summary>Stable direct-failure message identifier 203.</summary>
    DirectFailureMessage203 = 203,
    /// <summary>Stable direct-failure message identifier 204.</summary>
    DirectFailureMessage204 = 204,
    /// <summary>Stable direct-failure message identifier 205.</summary>
    DirectFailureMessage205 = 205,
    /// <summary>Stable direct-failure message identifier 206.</summary>
    DirectFailureMessage206 = 206,
    /// <summary>Stable direct-failure message identifier 207.</summary>
    DirectFailureMessage207 = 207,
    /// <summary>Stable direct-failure message identifier 208.</summary>
    DirectFailureMessage208 = 208,
    /// <summary>Stable direct-failure message identifier 209.</summary>
    DirectFailureMessage209 = 209,
    /// <summary>Stable direct-failure message identifier 210.</summary>
    DirectFailureMessage210 = 210,
    /// <summary>Stable direct-failure message identifier 211.</summary>
    DirectFailureMessage211 = 211,
    /// <summary>Stable direct-failure message identifier 212.</summary>
    DirectFailureMessage212 = 212,
    /// <summary>Stable direct-failure message identifier 213.</summary>
    DirectFailureMessage213 = 213,
    /// <summary>Stable direct-failure message identifier 214.</summary>
    DirectFailureMessage214 = 214,
    /// <summary>Stable direct-failure message identifier 215.</summary>
    DirectFailureMessage215 = 215,
    /// <summary>Stable direct-failure message identifier 216.</summary>
    DirectFailureMessage216 = 216,
    /// <summary>Stable direct-failure message identifier 217.</summary>
    DirectFailureMessage217 = 217,
    /// <summary>Stable direct-failure message identifier 218.</summary>
    DirectFailureMessage218 = 218,
    /// <summary>Stable direct-failure message identifier 219.</summary>
    DirectFailureMessage219 = 219,
    /// <summary>Stable direct-failure message identifier 220.</summary>
    DirectFailureMessage220 = 220,
    /// <summary>Stable direct-failure message identifier 221.</summary>
    DirectFailureMessage221 = 221,
    /// <summary>Stable direct-failure message identifier 222.</summary>
    DirectFailureMessage222 = 222,
    /// <summary>Stable direct-failure message identifier 223.</summary>
    DirectFailureMessage223 = 223,
    /// <summary>Stable direct-failure message identifier 224.</summary>
    DirectFailureMessage224 = 224,
    /// <summary>Stable direct-failure message identifier 225.</summary>
    DirectFailureMessage225 = 225,
    /// <summary>Stable direct-failure message identifier 226.</summary>
    DirectFailureMessage226 = 226,
    /// <summary>Stable direct-failure message identifier 227.</summary>
    DirectFailureMessage227 = 227,
    /// <summary>Stable direct-failure message identifier 228.</summary>
    DirectFailureMessage228 = 228,
    /// <summary>Stable direct-failure message identifier 229.</summary>
    DirectFailureMessage229 = 229,
    /// <summary>Stable direct-failure message identifier 230.</summary>
    DirectFailureMessage230 = 230,
    /// <summary>Stable direct-failure message identifier 231.</summary>
    DirectFailureMessage231 = 231,
    /// <summary>Stable direct-failure message identifier 232.</summary>
    DirectFailureMessage232 = 232,
    /// <summary>Stable direct-failure message identifier 233.</summary>
    DirectFailureMessage233 = 233,
    /// <summary>Stable direct-failure message identifier 234.</summary>
    DirectFailureMessage234 = 234,
    /// <summary>Stable direct-failure message identifier 235.</summary>
    DirectFailureMessage235 = 235,
    /// <summary>Stable direct-failure message identifier 236.</summary>
    DirectFailureMessage236 = 236,
    /// <summary>Stable direct-failure message identifier 237.</summary>
    DirectFailureMessage237 = 237,
    /// <summary>Stable direct-failure message identifier 238.</summary>
    DirectFailureMessage238 = 238,
    /// <summary>Stable direct-failure message identifier 239.</summary>
    DirectFailureMessage239 = 239,
    /// <summary>Stable direct-failure message identifier 240.</summary>
    DirectFailureMessage240 = 240,
    /// <summary>Stable direct-failure message identifier 241.</summary>
    DirectFailureMessage241 = 241,
    /// <summary>Stable direct-failure message identifier 242.</summary>
    DirectFailureMessage242 = 242,
    /// <summary>Stable direct-failure message identifier 243.</summary>
    DirectFailureMessage243 = 243,
    /// <summary>Stable direct-failure message identifier 244.</summary>
    DirectFailureMessage244 = 244,
    /// <summary>Stable direct-failure message identifier 245.</summary>
    DirectFailureMessage245 = 245,
    /// <summary>Stable direct-failure message identifier 246.</summary>
    DirectFailureMessage246 = 246,
    /// <summary>Stable direct-failure message identifier 247.</summary>
    DirectFailureMessage247 = 247,
    /// <summary>Stable direct-failure message identifier 248.</summary>
    DirectFailureMessage248 = 248,
    /// <summary>Stable direct-failure message identifier 249.</summary>
    DirectFailureMessage249 = 249,
    /// <summary>Stable direct-failure message identifier 250.</summary>
    DirectFailureMessage250 = 250,
    /// <summary>Stable direct-failure message identifier 251.</summary>
    DirectFailureMessage251 = 251,
    /// <summary>Stable direct-failure message identifier 252.</summary>
    DirectFailureMessage252 = 252,
    /// <summary>Stable direct-failure message identifier 253.</summary>
    DirectFailureMessage253 = 253,
    /// <summary>Stable direct-failure message identifier 254.</summary>
    DirectFailureMessage254 = 254,
    /// <summary>Stable direct-failure message identifier 255.</summary>
    DirectFailureMessage255 = 255,
    /// <summary>Stable direct-failure message identifier 256.</summary>
    DirectFailureMessage256 = 256,
    /// <summary>Stable direct-failure message identifier 257.</summary>
    DirectFailureMessage257 = 257,
    /// <summary>Stable direct-failure message identifier 258.</summary>
    DirectFailureMessage258 = 258,
    /// <summary>Stable direct-failure message identifier 259.</summary>
    DirectFailureMessage259 = 259,
    /// <summary>Stable direct-failure message identifier 260.</summary>
    DirectFailureMessage260 = 260,
    /// <summary>Stable direct-failure message identifier 261.</summary>
    DirectFailureMessage261 = 261,
    /// <summary>Stable direct-failure message identifier 262.</summary>
    DirectFailureMessage262 = 262,
    /// <summary>Stable direct-failure message identifier 263.</summary>
    DirectFailureMessage263 = 263,
    /// <summary>Stable direct-failure message identifier 264.</summary>
    DirectFailureMessage264 = 264,
    /// <summary>Stable direct-failure message identifier 265.</summary>
    DirectFailureMessage265 = 265,
    /// <summary>Stable direct-failure message identifier 266.</summary>
    DirectFailureMessage266 = 266,
    /// <summary>Stable direct-failure message identifier 267.</summary>
    DirectFailureMessage267 = 267,
    /// <summary>Stable direct-failure message identifier 268.</summary>
    DirectFailureMessage268 = 268,
    /// <summary>Stable direct-failure message identifier 269.</summary>
    DirectFailureMessage269 = 269,
    /// <summary>Stable direct-failure message identifier 270.</summary>
    DirectFailureMessage270 = 270,
    /// <summary>Stable direct-failure message identifier 271.</summary>
    DirectFailureMessage271 = 271,
    /// <summary>Stable direct-failure message identifier 272.</summary>
    DirectFailureMessage272 = 272,
    /// <summary>Stable direct-failure message identifier 273.</summary>
    DirectFailureMessage273 = 273,
    /// <summary>Stable direct-failure message identifier 274.</summary>
    DirectFailureMessage274 = 274,
    /// <summary>Stable direct-failure message identifier 275.</summary>
    DirectFailureMessage275 = 275,
    /// <summary>Stable direct-failure message identifier 276.</summary>
    DirectFailureMessage276 = 276,
    /// <summary>Stable direct-failure message identifier 277.</summary>
    DirectFailureMessage277 = 277,
    /// <summary>Stable direct-failure message identifier 278.</summary>
    DirectFailureMessage278 = 278,
    /// <summary>Stable direct-failure message identifier 279.</summary>
    DirectFailureMessage279 = 279,
    /// <summary>Stable direct-failure message identifier 280.</summary>
    DirectFailureMessage280 = 280,
    /// <summary>Stable direct-failure message identifier 281.</summary>
    DirectFailureMessage281 = 281,
    /// <summary>Stable direct-failure message identifier 282.</summary>
    DirectFailureMessage282 = 282,
    /// <summary>Stable direct-failure message identifier 283.</summary>
    DirectFailureMessage283 = 283,
    /// <summary>Stable direct-failure message identifier 284.</summary>
    DirectFailureMessage284 = 284,
    /// <summary>Stable direct-failure message identifier 285.</summary>
    DirectFailureMessage285 = 285,
    /// <summary>Stable direct-failure message identifier 286.</summary>
    DirectFailureMessage286 = 286,
    /// <summary>Stable direct-failure message identifier 287.</summary>
    DirectFailureMessage287 = 287,
    /// <summary>Stable direct-failure message identifier 288.</summary>
    DirectFailureMessage288 = 288,
    /// <summary>Stable direct-failure message identifier 289.</summary>
    DirectFailureMessage289 = 289,
    /// <summary>Stable direct-failure message identifier 290.</summary>
    DirectFailureMessage290 = 290,
    /// <summary>Stable direct-failure message identifier 291.</summary>
    DirectFailureMessage291 = 291,
    /// <summary>Stable direct-failure message identifier 292.</summary>
    DirectFailureMessage292 = 292,
    /// <summary>Stable direct-failure message identifier 293.</summary>
    DirectFailureMessage293 = 293,
    /// <summary>Stable direct-failure message identifier 294.</summary>
    DirectFailureMessage294 = 294,
    /// <summary>Stable direct-failure message identifier 295.</summary>
    DirectFailureMessage295 = 295,
    /// <summary>Stable direct-failure message identifier 296.</summary>
    DirectFailureMessage296 = 296,
    /// <summary>Stable direct-failure message identifier 297.</summary>
    DirectFailureMessage297 = 297,
    /// <summary>Stable direct-failure message identifier 298.</summary>
    DirectFailureMessage298 = 298,
    /// <summary>Stable direct-failure message identifier 299.</summary>
    DirectFailureMessage299 = 299,
    /// <summary>Stable direct-failure message identifier 300.</summary>
    DirectFailureMessage300 = 300,
    /// <summary>Stable direct-failure message identifier 301.</summary>
    DirectFailureMessage301 = 301,
    /// <summary>Stable direct-failure message identifier 302.</summary>
    DirectFailureMessage302 = 302,
    /// <summary>Stable direct-failure message identifier 303.</summary>
    DirectFailureMessage303 = 303,
    /// <summary>Stable direct-failure message identifier 304.</summary>
    DirectFailureMessage304 = 304,
    /// <summary>Stable direct-failure message identifier 305.</summary>
    DirectFailureMessage305 = 305,
    /// <summary>Stable direct-failure message identifier 306.</summary>
    DirectFailureMessage306 = 306,
    /// <summary>Stable direct-failure message identifier 307.</summary>
    DirectFailureMessage307 = 307,
    /// <summary>Stable direct-failure message identifier 308.</summary>
    DirectFailureMessage308 = 308,
    /// <summary>Stable direct-failure message identifier 309.</summary>
    DirectFailureMessage309 = 309,
    /// <summary>Stable direct-failure message identifier 310.</summary>
    DirectFailureMessage310 = 310,
    /// <summary>Stable direct-failure message identifier 311.</summary>
    DirectFailureMessage311 = 311,
    /// <summary>Stable direct-failure message identifier 312.</summary>
    DirectFailureMessage312 = 312,
    /// <summary>Stable direct-failure message identifier 313.</summary>
    DirectFailureMessage313 = 313,
    /// <summary>Stable direct-failure message identifier 314.</summary>
    DirectFailureMessage314 = 314,
    /// <summary>Stable direct-failure message identifier 315.</summary>
    DirectFailureMessage315 = 315,
    /// <summary>Stable direct-failure message identifier 316.</summary>
    DirectFailureMessage316 = 316,
    /// <summary>Stable direct-failure message identifier 317.</summary>
    DirectFailureMessage317 = 317,
    /// <summary>Stable direct-failure message identifier 318.</summary>
    DirectFailureMessage318 = 318,
    /// <summary>Stable direct-failure message identifier 319.</summary>
    DirectFailureMessage319 = 319,
    /// <summary>Stable direct-failure message identifier 320.</summary>
    DirectFailureMessage320 = 320,
    /// <summary>Stable direct-failure message identifier 321.</summary>
    DirectFailureMessage321 = 321,
    /// <summary>Stable direct-failure message identifier 322.</summary>
    DirectFailureMessage322 = 322,
    /// <summary>Stable direct-failure message identifier 323.</summary>
    DirectFailureMessage323 = 323,
    /// <summary>Stable direct-failure message identifier 324.</summary>
    DirectFailureMessage324 = 324,
    /// <summary>Stable direct-failure message identifier 325.</summary>
    DirectFailureMessage325 = 325,
    /// <summary>Stable direct-failure message identifier 326.</summary>
    DirectFailureMessage326 = 326,
    /// <summary>Stable direct-failure message identifier 327.</summary>
    DirectFailureMessage327 = 327,
    /// <summary>Stable direct-failure message identifier 328.</summary>
    DirectFailureMessage328 = 328,
    /// <summary>Stable direct-failure message identifier 329.</summary>
    DirectFailureMessage329 = 329,
    /// <summary>Stable direct-failure message identifier 330.</summary>
    DirectFailureMessage330 = 330,
    /// <summary>Stable direct-failure message identifier 331.</summary>
    DirectFailureMessage331 = 331,
    /// <summary>Stable direct-failure message identifier 332.</summary>
    DirectFailureMessage332 = 332,
    /// <summary>Stable direct-failure message identifier 333.</summary>
    DirectFailureMessage333 = 333,
    /// <summary>Stable direct-failure message identifier 334.</summary>
    DirectFailureMessage334 = 334,
    /// <summary>Stable direct-failure message identifier 335.</summary>
    DirectFailureMessage335 = 335,
    /// <summary>Stable direct-failure message identifier 336.</summary>
    DirectFailureMessage336 = 336,
    /// <summary>Stable direct-failure message identifier 337.</summary>
    DirectFailureMessage337 = 337,
    /// <summary>Stable direct-failure message identifier 338.</summary>
    DirectFailureMessage338 = 338,
    /// <summary>Stable direct-failure message identifier 339.</summary>
    DirectFailureMessage339 = 339,
    /// <summary>Stable direct-failure message identifier 340.</summary>
    DirectFailureMessage340 = 340,
    /// <summary>Stable direct-failure message identifier 341.</summary>
    DirectFailureMessage341 = 341,
    /// <summary>Stable direct-failure message identifier 342.</summary>
    DirectFailureMessage342 = 342,
    /// <summary>Stable direct-failure message identifier 343.</summary>
    DirectFailureMessage343 = 343,
    /// <summary>Stable direct-failure message identifier 344.</summary>
    DirectFailureMessage344 = 344,
    /// <summary>Stable direct-failure message identifier 345.</summary>
    DirectFailureMessage345 = 345,
    /// <summary>Stable direct-failure message identifier 346.</summary>
    DirectFailureMessage346 = 346,
    /// <summary>Stable direct-failure message identifier 347.</summary>
    DirectFailureMessage347 = 347,
    /// <summary>Stable direct-failure message identifier 348.</summary>
    DirectFailureMessage348 = 348,
    /// <summary>Stable direct-failure message identifier 349.</summary>
    DirectFailureMessage349 = 349,
    /// <summary>Stable direct-failure message identifier 350.</summary>
    DirectFailureMessage350 = 350,
    /// <summary>Stable direct-failure message identifier 351.</summary>
    DirectFailureMessage351 = 351,
    /// <summary>Stable direct-failure message identifier 352.</summary>
    DirectFailureMessage352 = 352,
    /// <summary>Stable direct-failure message identifier 353.</summary>
    DirectFailureMessage353 = 353,
    /// <summary>Stable direct-failure message identifier 354.</summary>
    DirectFailureMessage354 = 354,
    /// <summary>Stable direct-failure message identifier 355.</summary>
    DirectFailureMessage355 = 355,
    /// <summary>Stable direct-failure message identifier 356.</summary>
    DirectFailureMessage356 = 356,
    /// <summary>Stable direct-failure message identifier 357.</summary>
    DirectFailureMessage357 = 357,
    /// <summary>Stable direct-failure message identifier 358.</summary>
    DirectFailureMessage358 = 358,
    /// <summary>Stable direct-failure message identifier 359.</summary>
    DirectFailureMessage359 = 359,
    /// <summary>Stable direct-failure message identifier 360.</summary>
    DirectFailureMessage360 = 360,
    /// <summary>Stable direct-failure message identifier 361.</summary>
    DirectFailureMessage361 = 361,
    /// <summary>Stable direct-failure message identifier 362.</summary>
    DirectFailureMessage362 = 362,
    /// <summary>Stable direct-failure message identifier 363.</summary>
    DirectFailureMessage363 = 363,
    /// <summary>Stable direct-failure message identifier 364.</summary>
    DirectFailureMessage364 = 364,
    /// <summary>Stable direct-failure message identifier 365.</summary>
    DirectFailureMessage365 = 365,
    /// <summary>Stable direct-failure message identifier 366.</summary>
    DirectFailureMessage366 = 366,
    /// <summary>Stable direct-failure message identifier 367.</summary>
    DirectFailureMessage367 = 367,
    /// <summary>Stable direct-failure message identifier 368.</summary>
    DirectFailureMessage368 = 368,
    /// <summary>Stable direct-failure message identifier 369.</summary>
    DirectFailureMessage369 = 369,
    /// <summary>Stable direct-failure message identifier 370.</summary>
    DirectFailureMessage370 = 370,
    /// <summary>Stable direct-failure message identifier 371.</summary>
    DirectFailureMessage371 = 371,
    /// <summary>Stable direct-failure message identifier 372.</summary>
    DirectFailureMessage372 = 372,
    /// <summary>Stable direct-failure message identifier 373.</summary>
    DirectFailureMessage373 = 373,
    /// <summary>Stable direct-failure message identifier 374.</summary>
    DirectFailureMessage374 = 374,
    /// <summary>Stable direct-failure message identifier 375.</summary>
    DirectFailureMessage375 = 375,
    /// <summary>Stable direct-failure message identifier 376.</summary>
    DirectFailureMessage376 = 376,
    /// <summary>Stable direct-failure message identifier 377.</summary>
    DirectFailureMessage377 = 377,
    /// <summary>Stable direct-failure message identifier 378.</summary>
    DirectFailureMessage378 = 378,
    /// <summary>Stable direct-failure message identifier 379.</summary>
    DirectFailureMessage379 = 379,
    /// <summary>Stable direct-failure message identifier 380.</summary>
    DirectFailureMessage380 = 380,
    /// <summary>Stable direct-failure message identifier 381.</summary>
    DirectFailureMessage381 = 381,
    /// <summary>Stable direct-failure message identifier 382.</summary>
    DirectFailureMessage382 = 382,
    /// <summary>Stable direct-failure message identifier 383.</summary>
    DirectFailureMessage383 = 383,
    /// <summary>Stable direct-failure message identifier 384.</summary>
    DirectFailureMessage384 = 384,
    /// <summary>Stable direct-failure message identifier 385.</summary>
    DirectFailureMessage385 = 385,
    /// <summary>Stable direct-failure message identifier 386.</summary>
    DirectFailureMessage386 = 386,
    /// <summary>Stable direct-failure message identifier 387.</summary>
    DirectFailureMessage387 = 387,
    /// <summary>Stable direct-failure message identifier 388.</summary>
    DirectFailureMessage388 = 388,
    /// <summary>Stable direct-failure message identifier 389.</summary>
    DirectFailureMessage389 = 389,
    /// <summary>Stable direct-failure message identifier 390.</summary>
    DirectFailureMessage390 = 390,
    /// <summary>Stable direct-failure message identifier 391.</summary>
    DirectFailureMessage391 = 391,
    /// <summary>Stable direct-failure message identifier 392.</summary>
    DirectFailureMessage392 = 392,
    /// <summary>Stable direct-failure message identifier 393.</summary>
    DirectFailureMessage393 = 393,
    /// <summary>Stable direct-failure message identifier 394.</summary>
    DirectFailureMessage394 = 394,
    /// <summary>Stable direct-failure message identifier 395.</summary>
    DirectFailureMessage395 = 395,
    /// <summary>Stable direct-failure message identifier 396.</summary>
    DirectFailureMessage396 = 396,
    /// <summary>Stable direct-failure message identifier 397.</summary>
    DirectFailureMessage397 = 397,
    /// <summary>Stable direct-failure message identifier 398.</summary>
    DirectFailureMessage398 = 398,
    /// <summary>Stable direct-failure message identifier 399.</summary>
    DirectFailureMessage399 = 399,
    /// <summary>Stable direct-failure message identifier 400.</summary>
    DirectFailureMessage400 = 400,
    /// <summary>Stable direct-failure message identifier 401.</summary>
    DirectFailureMessage401 = 401,
    /// <summary>Stable direct-failure message identifier 402.</summary>
    DirectFailureMessage402 = 402,
    /// <summary>Stable direct-failure message identifier 403.</summary>
    DirectFailureMessage403 = 403,
    /// <summary>Stable direct-failure message identifier 404.</summary>
    DirectFailureMessage404 = 404,
    /// <summary>Stable direct-failure message identifier 405.</summary>
    DirectFailureMessage405 = 405,
    /// <summary>Stable direct-failure message identifier 406.</summary>
    DirectFailureMessage406 = 406,
    /// <summary>Stable direct-failure message identifier 407.</summary>
    DirectFailureMessage407 = 407,
    /// <summary>Stable direct-failure message identifier 408.</summary>
    DirectFailureMessage408 = 408,
    /// <summary>Stable direct-failure message identifier 409.</summary>
    DirectFailureMessage409 = 409,
    /// <summary>Stable direct-failure message identifier 410.</summary>
    DirectFailureMessage410 = 410,
    /// <summary>Stable direct-failure message identifier 411.</summary>
    DirectFailureMessage411 = 411,
    /// <summary>Stable direct-failure message identifier 412.</summary>
    DirectFailureMessage412 = 412,
    /// <summary>Stable direct-failure message identifier 413.</summary>
    DirectFailureMessage413 = 413,
    /// <summary>Stable direct-failure message identifier 414.</summary>
    DirectFailureMessage414 = 414,
    /// <summary>Stable direct-failure message identifier 415.</summary>
    DirectFailureMessage415 = 415,
    /// <summary>Stable direct-failure message identifier 416.</summary>
    DirectFailureMessage416 = 416,
    /// <summary>Stable direct-failure message identifier 417.</summary>
    DirectFailureMessage417 = 417,
    /// <summary>Stable direct-failure message identifier 418.</summary>
    DirectFailureMessage418 = 418,
    /// <summary>Stable direct-failure message identifier 419.</summary>
    DirectFailureMessage419 = 419,
    /// <summary>Stable direct-failure message identifier 420.</summary>
    DirectFailureMessage420 = 420,
    /// <summary>Stable direct-failure message identifier 421.</summary>
    DirectFailureMessage421 = 421,
    /// <summary>Stable direct-failure message identifier 422.</summary>
    DirectFailureMessage422 = 422,
    /// <summary>Stable direct-failure message identifier 423.</summary>
    DirectFailureMessage423 = 423,
    /// <summary>Stable direct-failure message identifier 424.</summary>
    DirectFailureMessage424 = 424,
    /// <summary>Stable direct-failure message identifier 425.</summary>
    DirectFailureMessage425 = 425,
    /// <summary>Stable direct-failure message identifier 426.</summary>
    DirectFailureMessage426 = 426,
    /// <summary>Stable direct-failure message identifier 427.</summary>
    DirectFailureMessage427 = 427,
    /// <summary>Stable direct-failure message identifier 428.</summary>
    DirectFailureMessage428 = 428,
    /// <summary>Stable direct-failure message identifier 429.</summary>
    DirectFailureMessage429 = 429,
    /// <summary>Stable direct-failure message identifier 430.</summary>
    DirectFailureMessage430 = 430,
    /// <summary>Stable direct-failure message identifier 431.</summary>
    DirectFailureMessage431 = 431,
    /// <summary>Stable direct-failure message identifier 432.</summary>
    DirectFailureMessage432 = 432,
    /// <summary>Stable direct-failure message identifier 433.</summary>
    DirectFailureMessage433 = 433,
    /// <summary>Stable direct-failure message identifier 434.</summary>
    DirectFailureMessage434 = 434,
    /// <summary>Stable direct-failure message identifier 435.</summary>
    DirectFailureMessage435 = 435,
    /// <summary>Stable direct-failure message identifier 436.</summary>
    DirectFailureMessage436 = 436,
    /// <summary>Stable direct-failure message identifier 437.</summary>
    DirectFailureMessage437 = 437,
    /// <summary>Stable direct-failure message identifier 438.</summary>
    DirectFailureMessage438 = 438,
    /// <summary>Stable direct-failure message identifier 439.</summary>
    DirectFailureMessage439 = 439,
    /// <summary>Stable direct-failure message identifier 440.</summary>
    DirectFailureMessage440 = 440,
    /// <summary>Stable direct-failure message identifier 441.</summary>
    DirectFailureMessage441 = 441,
    /// <summary>Stable direct-failure message identifier 442.</summary>
    DirectFailureMessage442 = 442,
    /// <summary>Stable direct-failure message identifier 443.</summary>
    DirectFailureMessage443 = 443,
    /// <summary>Stable direct-failure message identifier 444.</summary>
    DirectFailureMessage444 = 444,
    /// <summary>Stable direct-failure message identifier 445.</summary>
    DirectFailureMessage445 = 445,
    /// <summary>Stable direct-failure message identifier 446.</summary>
    DirectFailureMessage446 = 446,
    /// <summary>Stable failure identifier for a missing LSRA register bank.</summary>
    MissingRegisterBank = 447,
    /// <summary>Stable failure identifier for an unexpected LSRA reference kind.</summary>
    UnexpectedReferenceKind = 448,
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
    internal static void ThrowFatalJitException(CorJitResult result, FatalJitErrorCode errorCode)
    {
        var detailedMessage = Configuration.DETAILED_FATAL_ERROR_MESSAGES
            ? GetStaticFailureMessage(result, errorCode)
            : null;
        ThrowFatalJitException(result, errorCode, detailedMessage, innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException()
    {
        ThrowFatalJitException(
            CORJIT_INTERNALERROR,
            FatalJitErrorCode.Unspecified,
            detailedMessage: null,
            innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(string? detailedMessage)
    {
        ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.Unspecified, detailedMessage);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(string? detailedMessage, Exception? innerException)
    {
        ThrowFatalJitException(
            CORJIT_INTERNALERROR,
            FatalJitErrorCode.Unspecified,
            detailedMessage,
            innerException);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(CorJitResult result)
    {
        ThrowFatalJitException(
            result,
            FatalJitErrorCode.Unspecified,
            detailedMessage: null,
            innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(FatalJitErrorCode errorCode, string? detailedMessage)
    {
        ThrowFatalJitException(CORJIT_INTERNALERROR, errorCode, detailedMessage);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(CorJitResult result, string? detailedMessage)
    {
        ThrowFatalJitException(result, FatalJitErrorCode.Unspecified, detailedMessage);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(
        CorJitResult result,
        string? detailedMessage,
        Exception? innerException)
    {
        ThrowFatalJitException(
            result,
            FatalJitErrorCode.Unspecified,
            detailedMessage,
            innerException);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(
        CorJitResult result,
        FatalJitErrorCode errorCode,
        string? detailedMessage)
    {
        ThrowFatalJitException(result, errorCode, detailedMessage, innerException: null);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFatalJitException(
        CorJitResult result,
        FatalJitErrorCode errorCode,
        string? detailedMessage,
        Exception? innerException)
    {
        throw new FatalJitException(result, errorCode, detailedMessage, innerException);
    }

    private static string GetStaticFailureMessage(CorJitResult result, FatalJitErrorCode errorCode)
    {
        var message = errorCode switch
        {
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
            FatalJitErrorCode.DirectFailureMessage077 => "Invalid ABI classifier kind.",
            FatalJitErrorCode.DirectFailureMessage078 => "A block statement must have a cloneable root.",
            FatalJitErrorCode.DirectFailureMessage079 => "EH-to-VM clause order has not been initialized.",
            FatalJitErrorCode.DirectFailureMessage080 => "EH try end has no successor.",
            FatalJitErrorCode.DirectFailureMessage081 => "EH handler end has no successor.",
            FatalJitErrorCode.DirectFailureMessage082 => "EH filter has no start block.",
            FatalJitErrorCode.DirectFailureMessage083 => "VM-to-EH clause order has not been initialized.",
            FatalJitErrorCode.DirectFailureMessage084 => "Unknown EH handler type.",
            FatalJitErrorCode.DirectFailureMessage085 => "IP mapping has no predecessor at a duplicate native offset.",
            FatalJitErrorCode.DirectFailureMessage086 => "Multi-register returns into SIMD registers are unsupported on Windows AMD64.",
            FatalJitErrorCode.DirectFailureMessage087 => "32-bit long shift reached a 64-bit target.",
            FatalJitErrorCode.DirectFailureMessage088 => "Swift error returns are unavailable for this target.",
            FatalJitErrorCode.DirectFailureMessage089 => "The x86 long multiply node reached another target.",
            FatalJitErrorCode.DirectFailureMessage090 => "Swift error values are unavailable for this target.",
            FatalJitErrorCode.DirectFailureMessage091 => "32-bit long node reached a 64-bit target.",
            FatalJitErrorCode.DirectFailureMessage092 => "Insertion into a homed parameter register is not supported on this target.",
            FatalJitErrorCode.DirectFailureMessage093 => "Push struct arguments are only supported on x86.",
            FatalJitErrorCode.DirectFailureMessage094 => "The fixed-register table is not initialized.",
            FatalJitErrorCode.DirectFailureMessage095 => "Unexpected block kind at the end of code generation.",
            FatalJitErrorCode.DirectFailureMessage096 => "Invalid scalar memmove width.",
            FatalJitErrorCode.DirectFailureMessage097 => "Missing JMP epilog node.",
            FatalJitErrorCode.DirectFailureMessage098 => "Invalid shift operator.",
            FatalJitErrorCode.DirectFailureMessage099 => "An MD-array temporary must be an int or object reference.",
            FatalJitErrorCode.DirectFailureMessage100 => "Bounds-check coalescing requires value numbering.",
            FatalJitErrorCode.DirectFailureMessage101 => "Unexpected block kind for code-size estimation.",
            FatalJitErrorCode.DirectFailureMessage102 => "Invalid GPR size.",
            FatalJitErrorCode.DirectFailureMessage103 => "Cloning a try region requires a block map.",
            FatalJitErrorCode.DirectFailureMessage104 => "Cloning a try region requires an insertion point.",
            FatalJitErrorCode.DirectFailureMessage105 => "A callfinally continuation requires a preceding block.",
            FatalJitErrorCode.DirectFailureMessage106 => "Cloning a try region requires its blocks.",
            FatalJitErrorCode.DirectFailureMessage107 => "A finally return requires its successor table.",
            FatalJitErrorCode.DirectFailureMessage108 => "A cloned block requires its block-map entry.",
            FatalJitErrorCode.DirectFailureMessage109 => "A forward-substitution block has a broken statement list.",
            FatalJitErrorCode.DirectFailureMessage110 => "A non-final statement must have a successor.",
            FatalJitErrorCode.DirectFailureMessage111 => "The inserted local segment is not connected.",
            FatalJitErrorCode.DirectFailureMessage112 => "A collected forward-substitution use lost its owner.",
            FatalJitErrorCode.DirectFailureMessage113 => "A cheap forward-substitution address could not be cloned.",
            FatalJitErrorCode.DirectFailureMessage114 => "The final forward-substitution use lost its owner.",
            FatalJitErrorCode.DirectFailureMessage115 => "A forward-substitution candidate needs a following statement.",
            FatalJitErrorCode.DirectFailureMessage116 => "The last forward-substitution use lost its owner.",
            FatalJitErrorCode.DirectFailureMessage117 => "A forwarded store must have a locals list.",
            FatalJitErrorCode.DirectFailureMessage118 => "SWITCH should be a call rather than an inlined poll.",
            FatalJitErrorCode.DirectFailureMessage119 => "Unknown block type for updating predecessor lists.",
            FatalJitErrorCode.DirectFailureMessage120 => "A non-first statement needs a predecessor.",
            FatalJitErrorCode.DirectFailureMessage121 => "Unexpected scalar comparison.",
            FatalJitErrorCode.DirectFailureMessage122 => "Unexpected special comparison.",
            FatalJitErrorCode.DirectFailureMessage123 => "Unexpected masked memory intrinsic.",
            FatalJitErrorCode.DirectFailureMessage124 => "Unsupported SIMD conversion target.",
            FatalJitErrorCode.DirectFailureMessage125 => "Missing inline candidate information.",
            FatalJitErrorCode.DirectFailureMessage126 => "IV optimization requires dominators.",
            FatalJitErrorCode.DirectFailureMessage127 => "A conditional loop exit must have a terminal statement.",
            FatalJitErrorCode.DirectFailureMessage128 => "The counted loop must have a preheader.",
            FatalJitErrorCode.DirectFailureMessage129 => "Loop occurrences must be initialized before visiting.",
            FatalJitErrorCode.DirectFailureMessage130 => "Strength reduction requires dominators.",
            FatalJitErrorCode.DirectFailureMessage131 => "An active IV cursor needs a recurrence.",
            FatalJitErrorCode.DirectFailureMessage132 => "Rephrasing an IV requires constant steps.",
            FatalJitErrorCode.DirectFailureMessage133 => "Only 32- and 64-bit IVs can be rephrased.",
            FatalJitErrorCode.DirectFailureMessage134 => "GC-pointer strength reduction requires value numbering.",
            FatalJitErrorCode.DirectFailureMessage135 => "A constant IV step must materialize.",
            FatalJitErrorCode.DirectFailureMessage136 => "A replacement cursor needs its recurrence.",
            FatalJitErrorCode.DirectFailureMessage137 => "The strength-reduced use was lost from its statement.",
            FatalJitErrorCode.DirectFailureMessage138 => "An induction variable use is not owned by its statement.",
            FatalJitErrorCode.DirectFailureMessage139 => "IV widening requires natural loops.",
            FatalJitErrorCode.DirectFailureMessage140 => "IV widening requires an entry block.",
            FatalJitErrorCode.DirectFailureMessage141 => "A non-preheader IV initializer needs its SSA definition.",
            FatalJitErrorCode.DirectFailureMessage142 => "Loop-cloning visitor requires a current block.",
            FatalJitErrorCode.DirectFailureMessage143 => "Loop-cloning visitor requires a current statement.",
            FatalJitErrorCode.DirectFailureMessage144 => "Array candidate has no use block.",
            FatalJitErrorCode.DirectFailureMessage145 => "Unknown loop-cloning candidate.",
            FatalJitErrorCode.DirectFailureMessage146 => "Unrecognized loop-cloning exit comparison.",
            FatalJitErrorCode.DirectFailureMessage147 => "Loop cloning requires discovered natural loops.",
            FatalJitErrorCode.DirectFailureMessage148 => "Span candidate has no use block.",
            FatalJitErrorCode.DirectFailureMessage149 => "Span candidate has no bounds check.",
            FatalJitErrorCode.DirectFailureMessage150 => "Unknown loop-cloning optimization.",
            FatalJitErrorCode.DirectFailureMessage151 => "Cloned hoist expression has fewer operands than its source.",
            FatalJitErrorCode.DirectFailureMessage152 => "Loop hoisting requires a clone of its invariant expression.",
            FatalJitErrorCode.DirectFailureMessage153 => "Loop hoisting requires loop discovery.",
            FatalJitErrorCode.DirectFailureMessage154 => "Loop hoisting requires loop side-effect analysis.",
            FatalJitErrorCode.DirectFailureMessage155 => "Loop hoisting requires a DFS tree.",
            FatalJitErrorCode.DirectFailureMessage156 => "Loop hoisting requires dominators.",
            FatalJitErrorCode.DirectFailureMessage157 => "Loop hoisting requires an SSA definition block.",
            FatalJitErrorCode.DirectFailureMessage158 => "Loop unrolling requires discovered natural loops.",
            FatalJitErrorCode.DirectFailureMessage159 => "An analyzed loop must have a test block.",
            FatalJitErrorCode.DirectFailureMessage160 => "An analyzed loop must have a test tree.",
            FatalJitErrorCode.DirectFailureMessage161 => "An analyzed loop must have an increment.",
            FatalJitErrorCode.DirectFailureMessage162 => "Bad precondition in Compiler::optUnrollLoops().",
            FatalJitErrorCode.DirectFailureMessage163 => "Unexpected loop increment operator.",
            FatalJitErrorCode.DirectFailureMessage164 => "A loop try region passed feasibility but could not be cloned.",
            FatalJitErrorCode.DirectFailureMessage165 => "Unexpected loop iterator type.",
            FatalJitErrorCode.DirectFailureMessage166 => "Unexpected loop iterator operator.",
            FatalJitErrorCode.DirectFailureMessage167 => "Unexpected loop test operator.",
            FatalJitErrorCode.DirectFailureMessage168 => "Range-check cloning requires value numbering.",
            FatalJitErrorCode.DirectFailureMessage169 => "The final range check is not reachable from the first.",
            FatalJitErrorCode.DirectFailureMessage170 => "Range-check cloning exhausted the native hash table sizes.",
            FatalJitErrorCode.DirectFailureMessage171 => "The cloned fast path lost its predecessor.",
            FatalJitErrorCode.DirectFailureMessage172 => "The bounds check to remove is not in its original statement.",
            FatalJitErrorCode.DirectFailureMessage173 => "The fast path must contain a statement.",
            FatalJitErrorCode.DirectFailureMessage174 => "The first bounds-check index cannot be cloned.",
            FatalJitErrorCode.DirectFailureMessage175 => "The first bounds-check length cannot be cloned.",
            FatalJitErrorCode.DirectFailureMessage176 => "The cloned bounds-check index cannot be cloned again.",
            FatalJitErrorCode.DirectFailureMessage177 => "The lower-bound index cannot be cloned.",
            FatalJitErrorCode.DirectFailureMessage178 => "The split fast path must have a successor.",
            FatalJitErrorCode.DirectFailureMessage179 => "The lower-bound block must have a test.",
            FatalJitErrorCode.DirectFailureMessage180 => "The upper-bound block must have a test.",
            FatalJitErrorCode.DirectFailureMessage181 => "A runtime lookup helper requires a signature argument.",
            FatalJitErrorCode.DirectFailureMessage182 => "Runtime lookup signature is not registered.",
            FatalJitErrorCode.DirectFailureMessage183 => "An expandable runtime lookup requires indirections and a null check.",
            FatalJitErrorCode.DirectFailureMessage184 => "Cannot clone runtime lookup result local.",
            FatalJitErrorCode.DirectFailureMessage185 => "A runtime lookup helper requires a context argument.",
            FatalJitErrorCode.DirectFailureMessage186 => "Cannot clone runtime lookup context.",
            FatalJitErrorCode.DirectFailureMessage187 => "Cannot clone indirect runtime lookup offset.",
            FatalJitErrorCode.DirectFailureMessage188 => "Missing indirect runtime lookup offset.",
            FatalJitErrorCode.DirectFailureMessage189 => "Cannot clone dictionary size-check base.",
            FatalJitErrorCode.DirectFailureMessage190 => "Cannot clone runtime lookup slot.",
            FatalJitErrorCode.DirectFailureMessage191 => "A dynamic runtime lookup requires a nonzero final offset.",
            FatalJitErrorCode.DirectFailureMessage192 => "Unsupported SIMD conversion width.",
            FatalJitErrorCode.DirectFailureMessage193 => "Unsupported SIMD conversion source.",
            FatalJitErrorCode.DirectFailureMessage194 => "Unsupported SIMD index type.",
            FatalJitErrorCode.DirectFailureMessage195 => "Unsupported SIMD zip element width.",
            FatalJitErrorCode.DirectFailureMessage196 => "Unsupported SIMD unzip element width.",
            FatalJitErrorCode.DirectFailureMessage197 => "Unsupported saturating narrowing type.",
            FatalJitErrorCode.DirectFailureMessage198 => "Only SIMD OR and XOR operations are supported.",
            FatalJitErrorCode.DirectFailureMessage199 => "Static initialization call has no owning use.",
            FatalJitErrorCode.DirectFailureMessage200 => "NativeAOT TLS expansion is not supported for this target.",
            FatalJitErrorCode.DirectFailureMessage201 => "CoreCLR TLS expansion is not supported for this target.",
            FatalJitErrorCode.DirectFailureMessage202 => "Thread static helper requires a type index.",
            FatalJitErrorCode.DirectFailureMessage203 => "Cannot clone a thread static expression.",
            FatalJitErrorCode.DirectFailureMessage204 => "Cannot clone ReadUtf8 destination length.",
            FatalJitErrorCode.DirectFailureMessage205 => "Cannot clone ReadUtf8 destination pointer.",
            FatalJitErrorCode.DirectFailureMessage206 => "Cannot clone ReadUtf8 byte length.",
            FatalJitErrorCode.DirectFailureMessage207 => "Loop memory dependence requires a value number store.",
            FatalJitErrorCode.DirectFailureMessage208 => "A loop entry guard requires a condition.",
            FatalJitErrorCode.DirectFailureMessage209 => "Invalid compiler phase.",
            FatalJitErrorCode.DirectFailureMessage210 => "Unknown IP mapping kind.",
            FatalJitErrorCode.DirectFailureMessage211 => "Invalid instruction input size.",
            FatalJitErrorCode.DirectFailureMessage212 => "Invalid instruction format for APX NDD.",
            FatalJitErrorCode.DirectFailureMessage213 => "Jump removal requires a final instruction group.",
            FatalJitErrorCode.DirectFailureMessage214 => "Alignment adjustment requires a final instruction group.",
            FatalJitErrorCode.DirectFailureMessage215 => "Invalid display operand size.",
            FatalJitErrorCode.DirectFailureMessage216 => "Non-interruptible group has no instructions.",
            FatalJitErrorCode.DirectFailureMessage217 => "Invalid stack slot base.",
            FatalJitErrorCode.DirectFailureMessage218 => "Invalid GC slot flags.",
            FatalJitErrorCode.DirectFailureMessage219 => "Filter has no starting block.",
            FatalJitErrorCode.DirectFailureMessage220 => "Unknown generic context parameter type.",
            FatalJitErrorCode.DirectFailureMessage221 => "GC struct local has no layout.",
            FatalJitErrorCode.DirectFailureMessage222 => "Stack slot map was not initialized.",
            FatalJitErrorCode.DirectFailureMessage223 => "Live stack slot was not assigned.",
            FatalJitErrorCode.DirectFailureMessage224 => "Register slot map was not initialized.",
            FatalJitErrorCode.DirectFailureMessage225 => "Live register slot was not assigned.",
            FatalJitErrorCode.DirectFailureMessage226 => "Outgoing argument terminator is missing.",
            FatalJitErrorCode.DirectFailureMessage227 => "GT_SLLI_UW scale-index handling is only supported on RISC-V64.",
            FatalJitErrorCode.DirectFailureMessage228 => "Invalid node type for GetRegTypeByIndex.",
            FatalJitErrorCode.DirectFailureMessage229 => "Invalid node type for GetRegSpillFlagByIdx.",
            FatalJitErrorCode.DirectFailureMessage230 => "Invalid node type for indexed register spill flags.",
            FatalJitErrorCode.DirectFailureMessage231 => "Unsupported call kind in vzeroupper classification.",
            FatalJitErrorCode.DirectFailureMessage232 => "ARM64 conditional-select nodes are only supported on ARM64.",
            FatalJitErrorCode.DirectFailureMessage233 => "ARM64 bitfield nodes are only supported on ARM64.",
            FatalJitErrorCode.DirectFailureMessage234 => "SIMD constants are unsupported on this target.",
            FatalJitErrorCode.DirectFailureMessage235 => "Scalable SIMD constants are only supported on ARM64.",
            FatalJitErrorCode.DirectFailureMessage236 => "This vector operation requires fixed-width constant storage.",
            FatalJitErrorCode.DirectFailureMessage237 => "Unexpected SIMD constant type.",
            FatalJitErrorCode.DirectFailureMessage238 => "Unexpected scalar broadcast instruction.",
            FatalJitErrorCode.DirectFailureMessage239 => "Unexpected InlineDecision.",
            FatalJitErrorCode.DirectFailureMessage240 => "Invalid inline observation.",
            FatalJitErrorCode.DirectFailureMessage241 => "Invalid integral-to-floating conversion.",
            FatalJitErrorCode.DirectFailureMessage242 => "Invalid floating conversion source type.",
            FatalJitErrorCode.DirectFailureMessage243 => "Invalid sign-extension size.",
            FatalJitErrorCode.DirectFailureMessage244 => "Unexpected instruction with pseudo name.",
            FatalJitErrorCode.DirectFailureMessage245 => "No point in measuring DEBUG code.",
            FatalJitErrorCode.DirectFailureMessage246 => "Cycle counters only hooked up on x86/x64.",
            FatalJitErrorCode.DirectFailureMessage247 => "EH code offset requires an emitter.",
            FatalJitErrorCode.DirectFailureMessage248 => "A GC call descriptor is required before allocating its argument table.",
            FatalJitErrorCode.DirectFailureMessage249 => "LC_Array.ToGenTree does not support MD arrays in the pinned native implementation.",
            FatalJitErrorCode.DirectFailureMessage250 => "Cannot materialize an invalid loop-cloning expression.",
            FatalJitErrorCode.DirectFailureMessage251 => "Unknown loop-cloning identifier type.",
            FatalJitErrorCode.DirectFailureMessage252 => "Cannot materialize an invalid loop-cloning identifier.",
            FatalJitErrorCode.DirectFailureMessage253 => "Condition-code lowering requires a flag-producing hardware intrinsic.",
            FatalJitErrorCode.DirectFailureMessage254 => "Vector element extraction requires an arithmetic base type.",
            FatalJitErrorCode.DirectFailureMessage255 => "Fused multiply lowering requires an FMA intrinsic.",
            FatalJitErrorCode.DirectFailureMessage256 => "Unexpected floating comparison intrinsic.",
            FatalJitErrorCode.DirectFailureMessage257 => "Unexpected integer comparison intrinsic.",
            FatalJitErrorCode.DirectFailureMessage258 => "Unexpected scalar comparison intrinsic.",
            FatalJitErrorCode.DirectFailureMessage259 => "Unsupported SIMD memory comparison operation.",
            FatalJitErrorCode.DirectFailureMessage260 => "Swift error returns are not supported for this target.",
            FatalJitErrorCode.DirectFailureMessage261 => "Entry allocation requires block information.",
            FatalJitErrorCode.DirectFailureMessage262 => "Entry allocation requires a first block.",
            FatalJitErrorCode.DirectFailureMessage263 => "A stack entry definition requires a next reference.",
            FatalJitErrorCode.DirectFailureMessage264 => "The next stack entry reference requires a tree.",
            FatalJitErrorCode.DirectFailureMessage265 => "Upper-vector references require a related local.",
            FatalJitErrorCode.DirectFailureMessage266 => "Upper-vector save requires an assigned interval.",
            FatalJitErrorCode.DirectFailureMessage267 => "Special putarg requires a source local.",
            FatalJitErrorCode.DirectFailureMessage268 => "Exposed use requires an incoming local map.",
            FatalJitErrorCode.DirectFailureMessage269 => "A preassigned local requires a last reference.",
            FatalJitErrorCode.DirectFailureMessage270 => "A conflicting preassigned local requires a next reference.",
            FatalJitErrorCode.DirectFailureMessage271 => "LSRA block sequencing can only be initialized once.",
            FatalJitErrorCode.DirectFailureMessage272 => "LSRA block sequence was already initialized.",
            FatalJitErrorCode.DirectFailureMessage273 => "LSRA block metadata requires computed predecessor lists.",
            FatalJitErrorCode.DirectFailureMessage274 => "The LSRA block sequence does not cover the compiler flow graph.",
            FatalJitErrorCode.DirectFailureMessage275 => "The LSRA block sequence does not cover every compiler block.",
            FatalJitErrorCode.DirectFailureMessage276 => "LSRA requires a non-empty compiler flow graph.",
            FatalJitErrorCode.DirectFailureMessage277 => "LSRA block traversal requires an initialized block sequence.",
            FatalJitErrorCode.DirectFailureMessage278 => "LSRA block visitation requires initialized block traits.",
            FatalJitErrorCode.DirectFailureMessage279 => "Call reference building requires initialized codegen state.",
            FatalJitErrorCode.DirectFailureMessage280 => "Windows x64 varargs requires an XMM0-XMM3 argument register.",
            FatalJitErrorCode.DirectFailureMessage281 => "LSRA requires initialized codegen state.",
            FatalJitErrorCode.DirectFailureMessage282 => "LSRA internal definition state contains an empty entry.",
            FatalJitErrorCode.DirectFailureMessage283 => "An LSRA definition-list entry must retain its reference.",
            FatalJitErrorCode.DirectFailureMessage284 => "Upper-vector saves require the current basic block.",
            FatalJitErrorCode.DirectFailureMessage285 => "An LSRA definition-list entry must retain its defining node.",
            FatalJitErrorCode.DirectFailureMessage286 => "LSRA struct temporaries must be local variables or calls.",
            FatalJitErrorCode.DirectFailureMessage287 => "Invalid LSRA edge resolution type.",
            FatalJitErrorCode.DirectFailureMessage288 => "LSRA edge resolution requires allocated local maps and block sequence.",
            FatalJitErrorCode.DirectFailureMessage289 => "Split edge requires a unique original predecessor.",
            FatalJitErrorCode.DirectFailureMessage290 => "A split edge requires a single successor.",
            FatalJitErrorCode.DirectFailureMessage291 => "A split edge requires a single predecessor.",
            FatalJitErrorCode.DirectFailureMessage292 => "Critical edge requires an outgoing register map.",
            FatalJitErrorCode.DirectFailureMessage293 => "A switch edge requires its switch table.",
            FatalJitErrorCode.DirectFailureMessage294 => "Switch edge resolution requires codegen state.",
            FatalJitErrorCode.DirectFailureMessage295 => "A conditional edge requires its branch.",
            FatalJitErrorCode.DirectFailureMessage296 => "Critical edge resolution requires the shared map.",
            FatalJitErrorCode.DirectFailureMessage297 => "Critical edge requires a successor register map.",
            FatalJitErrorCode.DirectFailureMessage298 => "Branch resolution requires a terminator.",
            FatalJitErrorCode.DirectFailureMessage299 => "Swap resolution requires a terminator.",
            FatalJitErrorCode.DirectFailureMessage300 => "Resolution requires a predecessor outgoing map.",
            FatalJitErrorCode.DirectFailureMessage301 => "Resolution requires a successor block.",
            FatalJitErrorCode.DirectFailureMessage302 => "Resolution requires a successor register map.",
            FatalJitErrorCode.DirectFailureMessage303 => "Split resolution requires a successor.",
            FatalJitErrorCode.DirectFailureMessage304 => "Critical resolution requires a successor.",
            FatalJitErrorCode.DirectFailureMessage305 => "Unknown LSRA edge-resolution type.",
            FatalJitErrorCode.DirectFailureMessage306 => "Ready resolution target requires an interval.",
            FatalJitErrorCode.DirectFailureMessage307 => "Swap resolution requires the displaced interval.",
            FatalJitErrorCode.DirectFailureMessage308 => "Swap resolution requires the source interval.",
            FatalJitErrorCode.DirectFailureMessage309 => "Cycle spill requires a displaced interval.",
            FatalJitErrorCode.DirectFailureMessage310 => "Cycle resolution requires a source interval.",
            FatalJitErrorCode.DirectFailureMessage311 => "Cycle scratch move requires a displaced interval.",
            FatalJitErrorCode.DirectFailureMessage312 => "Stack resolution requires a pending interval.",
            FatalJitErrorCode.DirectFailureMessage313 => "Edge resolution requires the predecessor's register map.",
            FatalJitErrorCode.DirectFailureMessage314 => "Edge resolution requires the successor's register map.",
            FatalJitErrorCode.DirectFailureMessage315 => "Shared critical resolution requires a register map.",
            FatalJitErrorCode.DirectFailureMessage316 => "LSRA frame type was not selected.",
            FatalJitErrorCode.DirectFailureMessage317 => "An intrinsic reference requires an initialized block sequence.",
            FatalJitErrorCode.DirectFailureMessage318 => "LSRA interval requires a concrete register type.",
            FatalJitErrorCode.DirectFailureMessage319 => "Write-barrier kill-set construction requires initialized CodeGen.",
            FatalJitErrorCode.DirectFailureMessage320 => "Call kill-set construction requires initialized CodeGen.",
            FatalJitErrorCode.DirectFailureMessage321 => "Virtual-stub calls require initialized register metadata.",
            FatalJitErrorCode.DirectFailureMessage322 => "Local block locations require enregistered locals.",
            FatalJitErrorCode.DirectFailureMessage323 => "Block locations require an outgoing variable map.",
            FatalJitErrorCode.DirectFailureMessage324 => "Block locations require initialized LSRA block information.",
            FatalJitErrorCode.DirectFailureMessage325 => "Block locations require an incoming variable map.",
            FatalJitErrorCode.DirectFailureMessage326 => "Block locations require a predecessor outgoing map.",
            FatalJitErrorCode.DirectFailureMessage327 => "Resolution of a predecessor-spilled register requires a next reference.",
            FatalJitErrorCode.DirectFailureMessage328 => "An out-of-order interval requires a next reference.",
            FatalJitErrorCode.DirectFailureMessage329 => "An assigned interval requires its register record.",
            FatalJitErrorCode.DirectFailureMessage330 => "Local interval construction requires enregistered locals.",
            FatalJitErrorCode.DirectFailureMessage331 => "Interval construction requires initialized codegen state.",
            FatalJitErrorCode.DirectFailureMessage332 => "A tailcall block must contain its jump or call.",
            FatalJitErrorCode.DirectFailureMessage333 => "Write-through heuristics require block sequencing.",
            FatalJitErrorCode.DirectFailureMessage334 => "Tracked local mapping is required for interval construction.",
            FatalJitErrorCode.DirectFailureMessage335 => "Local register resolution requires matching block/node ownership and enregistered locals.",
            FatalJitErrorCode.DirectFailureMessage336 => "Contained local resolution requires an incoming variable map.",
            FatalJitErrorCode.DirectFailureMessage337 => "A contained local use requires its tree node.",
            FatalJitErrorCode.DirectFailureMessage338 => "Reload resolution requires an incoming variable map.",
            FatalJitErrorCode.DirectFailureMessage339 => "A copied or moved local reference requires its tree node.",
            FatalJitErrorCode.DirectFailureMessage340 => "A copied local reference requires its owning block.",
            FatalJitErrorCode.DirectFailureMessage341 => "Local register resolution requires enregistered locals.",
            FatalJitErrorCode.DirectFailureMessage342 => "Local register resolution requires an entry block.",
            FatalJitErrorCode.DirectFailureMessage343 => "Local register resolution requires an entry variable map.",
            FatalJitErrorCode.DirectFailureMessage344 => "Local register resolution requires a block reference.",
            FatalJitErrorCode.DirectFailureMessage345 => "Local register resolution requires an ordered block reference.",
            FatalJitErrorCode.DirectFailureMessage346 => "Upper-vector saves require a kill node.",
            FatalJitErrorCode.DirectFailureMessage347 => "An upper-vector interval requires its local.",
            FatalJitErrorCode.DirectFailureMessage348 => "An upper-vector save requires its interval.",
            FatalJitErrorCode.DirectFailureMessage349 => "Upper-vector restores require a local.",
            FatalJitErrorCode.DirectFailureMessage350 => "A spilled or moved temporary requires its next reference.",
            FatalJitErrorCode.DirectFailureMessage351 => "A saved temporary requires a later use.",
            FatalJitErrorCode.DirectFailureMessage352 => "A register parameter requires an initial reference.",
            FatalJitErrorCode.DirectFailureMessage353 => "An upper-vector save requires its local interval.",
            FatalJitErrorCode.DirectFailureMessage354 => "An upper-vector restore requires its local interval.",
            FatalJitErrorCode.DirectFailureMessage355 => "An upper-vector restore requires an owning use.",
            FatalJitErrorCode.DirectFailureMessage356 => "A vector restore before a branch requires a terminator.",
            FatalJitErrorCode.DirectFailureMessage357 => "A local source use requires its tree.",
            FatalJitErrorCode.DirectFailureMessage358 => "Minimal register allocation requires locals to remain un-enregistered.",
            FatalJitErrorCode.DirectFailureMessage359 => "Minimal register allocation cannot run with enregistered locals.",
            FatalJitErrorCode.DirectFailureMessage360 => "Minimal LSRA references must not be local-variable intervals.",
            FatalJitErrorCode.DirectFailureMessage361 => "An upper-vector save requires an assigned register.",
            FatalJitErrorCode.DirectFailureMessage362 => "An upper-vector interval must have a first reference.",
            FatalJitErrorCode.DirectFailureMessage363 => "A required copy-register allocation found no register.",
            FatalJitErrorCode.DirectFailureMessage364 => "Minimal interval construction cannot run with enregistered locals.",
            FatalJitErrorCode.DirectFailureMessage365 => "Minimal register resolution cannot run with enregistered locals.",
            FatalJitErrorCode.DirectFailureMessage366 => "Minimal resolution cannot save an enregistered local vector.",
            FatalJitErrorCode.DirectFailureMessage367 => "Upper-vector restores require enregistered-local resolution.",
            FatalJitErrorCode.DirectFailureMessage368 => "A contained address use requires an address mode.",
            FatalJitErrorCode.DirectFailureMessage369 => "A late call argument must have a lowered node.",
            FatalJitErrorCode.DirectFailureMessage370 => "LSRA definition list entry has no reference position.",
            FatalJitErrorCode.DirectFailureMessage371 => "LSRA could not match a use with its definition.",
            FatalJitErrorCode.DirectFailureMessage372 => "Tracked LSRA locals require interval mappings.",
            FatalJitErrorCode.DirectFailureMessage373 => "GetRegTypeByIndex cannot determine a multi-register local field type.",
            FatalJitErrorCode.DirectFailureMessage374 => "LSRA cannot determine a multi-register definition type.",
            FatalJitErrorCode.DirectFailureMessage375 => "LSRA upper-vector lookup encountered a non-upper-vector interval.",
            FatalJitErrorCode.DirectFailureMessage376 => "Upper-vector intervals require a related local interval.",
            FatalJitErrorCode.DirectFailureMessage377 => "A partially spilled vector requires a preceding upper-vector save.",
            FatalJitErrorCode.DirectFailureMessage378 => "Register assignment requires initialized codegen state.",
            FatalJitErrorCode.DirectFailureMessage379 => "Cannot clear an unassigned physical register.",
            FatalJitErrorCode.DirectFailureMessage380 => "Cannot unassign a physical register without an interval.",
            FatalJitErrorCode.DirectFailureMessage381 => "An active interval spill requires its last reference.",
            FatalJitErrorCode.DirectFailureMessage382 => "A restorable register has no previous interval.",
            FatalJitErrorCode.DirectFailureMessage383 => "An upper-vector interval requires its related local interval.",
            FatalJitErrorCode.DirectFailureMessage384 => "An upper-vector interval must be related to a local interval.",
            FatalJitErrorCode.DirectFailureMessage385 => "A single-definition interval must have a first reference.",
            FatalJitErrorCode.DirectFailureMessage386 => "Only local-variable intervals can be live on block entry.",
            FatalJitErrorCode.DirectFailureMessage387 => "Register kills require initialized codegen state.",
            FatalJitErrorCode.DirectFailureMessage388 => "A non-empty kill list must have a tail reference.",
            FatalJitErrorCode.DirectFailureMessage389 => "A minimal copy-register assignment requires an active interval.",
            FatalJitErrorCode.DirectFailureMessage390 => "An active copy-register interval must retain its assigned register.",
            FatalJitErrorCode.DirectFailureMessage391 => "The interval's assigned register does not match its physical register.",
            FatalJitErrorCode.DirectFailureMessage392 => "A no-spill unassignment requires an active interval.",
            FatalJitErrorCode.DirectFailureMessage393 => "Minimal register-state reset cannot run with enregistered locals.",
            FatalJitErrorCode.DirectFailureMessage394 => "Spill-weight calculation requires an assigned interval.",
            FatalJitErrorCode.DirectFailureMessage395 => "Spill-weight calculation requires a recent reference.",
            FatalJitErrorCode.DirectFailureMessage396 => "Register selection has not been reset.",
            FatalJitErrorCode.DirectFailureMessage397 => "Minimal LSRA selection requires local variables to remain un-enregistered.",
            FatalJitErrorCode.DirectFailureMessage398 => "Minimal LSRA selection has no candidate registers.",
            FatalJitErrorCode.DirectFailureMessage399 => "A required reference lost all candidate registers.",
            FatalJitErrorCode.DirectFailureMessage400 => "Minimal LSRA selection did not choose exactly one register.",
            FatalJitErrorCode.DirectFailureMessage401 => "A single-register heuristic must select exactly one register.",
            FatalJitErrorCode.DirectFailureMessage402 => "Register selection is missing its range-end reference.",
            FatalJitErrorCode.DirectFailureMessage403 => "Spill-cost selection requires each busy candidate to have an assigned interval.",
            FatalJitErrorCode.DirectFailureMessage404 => "A current next-interval location must have a reference.",
            FatalJitErrorCode.DirectFailureMessage405 => "Copy or reload requires an owning LIR use.",
            FatalJitErrorCode.DirectFailureMessage406 => "A grouped LSRA copy or reload requires a multi-register source.",
            FatalJitErrorCode.DirectFailureMessage407 => "The LSRA copy or reload register is already assigned.",
            FatalJitErrorCode.DirectFailureMessage408 => "Unsupported scalar math intrinsic.",
            FatalJitErrorCode.DirectFailureMessage409 => "Def-use conflict resolution is only valid for temporary intervals.",
            FatalJitErrorCode.DirectFailureMessage410 => "A conflicting definition must be followed by its use.",
            FatalJitErrorCode.DirectFailureMessage411 => "Def-use conflict references must be ordered references of one interval.",
            FatalJitErrorCode.DirectFailureMessage412 => "A temporary definition must retain its source tree node.",
            FatalJitErrorCode.DirectFailureMessage413 => "A fixed use must be present in the next-fixed-reference map.",
            FatalJitErrorCode.DirectFailureMessage414 => "An assigned interval must retain its recent reference.",
            FatalJitErrorCode.DirectFailureMessage415 => "Cannot spill an unassigned physical register.",
            FatalJitErrorCode.DirectFailureMessage416 => "A physical register without a recent reference must belong to a local.",
            FatalJitErrorCode.DirectFailureMessage417 => "A local without a recent reference must be a parameter.",
            FatalJitErrorCode.DirectFailureMessage418 => "Kill references do not have a spill weight.",
            FatalJitErrorCode.DirectFailureMessage419 => "A spilled interval must have a first reference.",
            FatalJitErrorCode.DirectFailureMessage420 => "LSRA block weights are unavailable before interval construction.",
            FatalJitErrorCode.DirectFailureMessage421 => "A rejected stack allocation requires a reason.",
            FatalJitErrorCode.DirectFailureMessage422 => "Conditional escape cloning requires a DFS tree.",
            FatalJitErrorCode.DirectFailureMessage423 => "Conditional escape cloning requires dominators.",
            FatalJitErrorCode.DirectFailureMessage424 => "Conditional escape cloning requires a definition block.",
            FatalJitErrorCode.DirectFailureMessage425 => "Unexpected object allocation candidate kind.",
            FatalJitErrorCode.DirectFailureMessage426 => "A new-array helper requires a length argument.",
            FatalJitErrorCode.DirectFailureMessage427 => "The selected try region could not be cloned.",
            FatalJitErrorCode.DirectFailureMessage428 => "An empty-static allocation requires one predecessor.",
            FatalJitErrorCode.DirectFailureMessage429 => "An empty-static predecessor requires its branch statement.",
            FatalJitErrorCode.DirectFailureMessage430 => "Struct field store must have a layout.",
            FatalJitErrorCode.DirectFailureMessage431 => "Retyping struct fields requires their new layout.",
            FatalJitErrorCode.DirectFailureMessage432 => "Unexpected parent while retyping a stack-object use.",
            FatalJitErrorCode.DirectFailureMessage433 => "Retyped struct local requires its layout.",
            FatalJitErrorCode.DirectFailureMessage434 => "UNBOX requires its object argument.",
            FatalJitErrorCode.DirectFailureMessage435 => "UNBOX requires a cloneable local.",
            FatalJitErrorCode.DirectFailureMessage436 => "Delegate invoke requires a this argument.",
            FatalJitErrorCode.DirectFailureMessage437 => "Delegate invoke requires a cloneable receiver.",
            FatalJitErrorCode.DirectFailureMessage438 => "UNBOX type test must retain its side-effect call.",
            FatalJitErrorCode.DirectFailureMessage439 => "Tracked struct requires its layout.",
            FatalJitErrorCode.DirectFailureMessage440 => "Invalid variable location kind.",
            FatalJitErrorCode.DirectFailureMessage441 => "Invalid stack variable type.",
            FatalJitErrorCode.DirectFailureMessage442 => "Invalid register variable type.",
            FatalJitErrorCode.DirectFailureMessage443 => "Variable live ranges have not been initialized.",
            FatalJitErrorCode.DirectFailureMessage444 => "Tracked local mapping has not been initialized.",
            FatalJitErrorCode.DirectFailureMessage445 => "Invalid register number.",
            FatalJitErrorCode.DirectFailureMessage446 => "A precise map store requires the current block.",
            FatalJitErrorCode.MissingRegisterBank => "LSRA has no register bank for this register type.",
            FatalJitErrorCode.UnexpectedReferenceKind => "Unexpected reference kind during register resolution.",
            _ => null,
        };

        if (message is null)
        {
            ThrowFatalJitException(result, errorCode, detailedMessage: null);
            return default!;
        }

        return message;
    }
    /// <summary>Gets the stable identifier for this fatal JIT failure.</summary>
    public FatalJitErrorCode ErrorCode { get; }

    public CorJitResult Result { get; }
}
