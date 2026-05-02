// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

// Actual ASCII string
global using unsafe VarName = byte*;

global using NodeToTestDataMap = System.Collections.Generic.Dictionary<RyuJitSharp.GenTree, RyuJitSharp.TestLabelAndNum>;

global using FieldHandleSet = System.Collections.Generic.Dictionary<RyuJitSharp.Pointer<RyuJitSharp.CORINFO_FIELD_STRUCT_>, RyuJitSharp.FieldKindForVN>;

global using ClassHandleSet = System.Collections.Generic.Dictionary<RyuJitSharp.Pointer<RyuJitSharp.CORINFO_CLASS_STRUCT_>, bool>;

global using BlockToFlowEdgeMap = System.Collections.Generic.Dictionary<RyuJitSharp.BasicBlock, RyuJitSharp.FlowEdge>;

global using BasicBlockLocalPairSet = System.Collections.Generic.Dictionary<RyuJitSharp.Compiler.BasicBlockLocalPair, bool>;

global using unsafe fgWalkPostFn = delegate*<ref RyuJitSharp.GenTree, RyuJitSharp.Compiler.fgWalkData, RyuJitSharp.Compiler.fgWalkResult>;

global using unsafe fgWalkPreFn = delegate*<ref RyuJitSharp.GenTree, RyuJitSharp.Compiler.fgWalkData, RyuJitSharp.Compiler.fgWalkResult>;

global using GenTreeStack = System.Collections.Generic.Stack<RyuJitSharp.GenTree>;

global using NodeToUnsignedMap = System.Collections.Generic.Dictionary<RyuJitSharp.GenTree, uint>;

global using VarToLikelyClassMap = System.Collections.Generic.Dictionary<uint, RyuJitSharp.Compiler.InferredGdvEntry>;

global using HelperToManagedMap = System.Collections.Generic.Dictionary<RyuJitSharp.Pointer<RyuJitSharp.CORINFO_METHOD_STRUCT_>, RyuJitSharp.Pointer<RyuJitSharp.CORINFO_METHOD_STRUCT_>>;

global using NodeToLoopMemoryBlockMap = System.Collections.Generic.Dictionary<RyuJitSharp.GenTree, RyuJitSharp.BasicBlock>;

global using SignatureToLookupInfoMap = System.Collections.Generic.Dictionary<RyuJitSharp.Pointer, RyuJitSharp.CORINFO_RUNTIME_LOOKUP>;

#if SWIFT_SUPPORT
global using SwiftLoweringMap = System.Collections.Generic.Dictionary<RyuJitSharp.Pointer<RyuJitSharp.CORINFO_CLASS_STRUCT_>, RyuJitSharp.Pointer<RyuJitSharp.CORINFO_SWIFT_LOWERING>>;
#endif

// method that returns if you should split here
global using unsafe fgSplitPredicate = delegate*<RyuJitSharp.GenTree, RyuJitSharp.GenTree, RyuJitSharp.Compiler.fgWalkData, bool>;

global using AddCodeDscMap = System.Collections.Generic.Dictionary<RyuJitSharp.Compiler.AddCodeDscKey, RyuJitSharp.Compiler.AddCodeDsc>;

// To represent sets of VN's that have already been hoisted in outer loops.
global using VNSet = System.Collections.Generic.Dictionary<uint, bool>;

global using CopyPropSsaDefStack = System.Collections.Generic.Stack<RyuJitSharp.Compiler.CopyPropSsaDef>;

global using LclNumToLiveDefsMap = System.Collections.Generic.Dictionary<uint, System.Collections.Generic.Stack<RyuJitSharp.Compiler.CopyPropSsaDef>>;

global using LocalNumberToNullCheckTreeMap = System.Collections.Generic.Dictionary<uint, RyuJitSharp.GenTree>;

global using CallSiteDebugInfoTable = System.Collections.Generic.Dictionary<RyuJitSharp.GenTree, RyuJitSharp.DebugInfo>;

global using VarNumToScopeDscMap = System.Collections.Generic.Dictionary<uint, RyuJitSharp.Compiler.VarScopeMapInfo>;

#if DEBUG
global using NodeToIntMap = System.Collections.Generic.Dictionary<RyuJitSharp.GenTree, int>;
#endif

#if TARGET_RISCV64 || TARGET_LOONGARCH64
global using FpStructLoweringMap = System.Collections.Generic.Dictionary<RyuJitSharp.Pointer<RyuJitSharp.CORINFO_CLASS_STRUCT_>, RyuJitSharp.Pointer<RyuJitSharp.CORINFO_FPSTRUCT_LOWERING>>;
#endif
