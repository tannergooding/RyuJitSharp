// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using RyuJitSharp.UnitTests;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ManagedInstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.Target.UnitTests;

[NonParallelizable]
internal static unsafe class SystemHalfImporterTests
{
    [SetUp]
    public static void RequireXarchOrArm64HardwareIntrinsics()
    {
        Assume.That(
            Configuration.FEATURE_HW_INTRINSICS &&
            (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64));
    }

    [TestCase(NI_System_Half_get_MinValue, "get_MinValue", 0xFBFF)]
    [TestCase(NI_System_Half_get_MaxValue, "get_MaxValue", 0x7BFF)]
    [TestCase(NI_System_Half_get_Epsilon, "get_Epsilon", 0x0001)]
    [TestCase(NI_System_Half_get_NaN, "get_NaN", 0xFE00)]
    [TestCase(NI_System_Half_get_PositiveInfinity, "get_PositiveInfinity", 0x7C00)]
    [TestCase(NI_System_Half_get_NegativeInfinity, "get_NegativeInfinity", 0xFC00)]
    [TestCase(NI_System_Half_get_One, "get_One", 0x3C00)]
    [TestCase(NI_System_Half_get_Zero, "get_Zero", 0x0000)]
    public static void ConstantIntrinsicImportsTheExpectedHalfPayload(
        NamedIntrinsic intrinsic, string methodName, int expectedBits)
    {
        var methodNameBytes = Encoding.UTF8.GetBytes(methodName + "\0");
        var classNameBytes = "Half\0"u8.ToArray();
        var namespaceNameBytes = "System\0"u8.ToArray();

        fixed (byte* methodNamePointer = methodNameBytes)
        fixed (byte* classNamePointer = classNameBytes)
        fixed (byte* namespaceNamePointer = namespaceNameBytes)
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.resolveToken = &ResolveHalfToken;
            vtable.Base.Base.getEEInfo = &GetEEInfo;
            vtable.Base.Base.runWithSPMIErrorTrap = &RunWithSPMIErrorTrap;
            vtable.Base.getCallInfo = &GetHalfCallInfo;
            vtable.Base.Base.getArrayRank = &GetHalfArrayRank;
            vtable.Base.Base.getMethodNameFromMetadata = &GetHalfMethodName;
            vtable.Base.Base.getClassNameFromMetadata = &GetHalfClassName;
            vtable.Base.Base.printClassName = &PrintHalfClassName;
            vtable.Base.Base.getTypeInstantiationArgument = &GetHalfTypeInstantiationArgument;
            vtable.Base.Base.getClassSize = &GetHalfClassSize;
            vtable.Base.Base.getClassAttribs = &GetHalfClassAttributes;
            vtable.Base.Base.isValueClass = &IsHalfValueClass;
            vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
            vtable.Base.notifyInstructionSetUsage =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool, byte>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte, byte>)&NotifyInstructionSetUsage;

            var state = new HalfImportState
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                HalfClass = new HalfClassInfo
                {
                    Name = classNamePointer,
                    Namespace = namespaceNamePointer,
                    Size = sizeof(ushort),
                },
                MethodName = methodNamePointer,
                Method = (CORINFO_METHOD_STRUCT_*)0x2000,
            };

            using var jitTls = TestConfiguration.CreateJitTls(&state.JitInfo);
            var previousCompiler = JitTls.Compiler;
            var previousConfig = JitConfig;
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            JitFlags flags = default;
            CORINFO_METHOD_INFO methodInfo = default;
            compiler.opts.jitFlags = &flags;
            compiler.SetMinOpts(false);
            compiler.info = new Compiler.Info
            {
                compCompHnd = &state.JitInfo,
                compScopeHnd = (CORINFO_MODULE_STRUCT_*)&state,
                compMethodInfo = &methodInfo,
                compMethodHnd = (CORINFO_METHOD_STRUCT_*)0x3000,
                compMaxStack = 1,
                compRetBuffArg = BAD_VAR_NUM,
                compMatchedVM = true,
                compFullName = nameof(SystemHalfImporterTests),
            };
            JitTls.Compiler = compiler;
            JitConfig = new JitConfigValues();
            InlineSize(ref JitConfig) = 100;
            InlineDepth(ref JitConfig) = 20;
            ForceDepth(ref JitConfig) = 20;

            try
            {
                compiler._inlineStrategy = new InlineStrategy(compiler);
                compiler.lvaTable = [];
                compiler.lvaCount = 0;
                compiler.stackState.esStack = new StackEntry[1];
                compiler.opts.compSupportsISA.AddInstructionSet(
                    Configuration.TARGET_XARCH ? InstructionSet_AVX10v1 : InstructionSet_Fp16);
                Assert.That(compiler.lookupNamedIntrinsic(state.Method), Is.EqualTo(intrinsic));

                var block = new BasicBlock(null, null)
                {
                    bbCodeOffs = 0,
                    bbCodeOffsEnd = 5,
                    bbWeight = 1,
                };
                compiler.compCurBB = block;
                compiler.fgFirstBB = block;
                compiler.fgLastBB = block;

                var il = new byte[] { (byte)OPCODE.CEE_CALL, 1, 0, 0, 0 };
                fixed (byte* code = il)
                {
                    compiler.info.compCode = code;
                    compiler.info.compILCodeSize = il.Length;
                    compiler.impImportBlockCode(block);
                }

                var result = compiler.impStackTop().val;
                Assert.That(result.Type, Is.EqualTo(TYP_STRUCT));
                Assert.That(result.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));

                var statements = ImporterStatements(compiler)
                    ?? throw new AssertionException("The Half intrinsic did not materialize its result.");
                var vector = FindVectorConstant(statements.RootNode)
                    ?? throw new AssertionException("The Half intrinsic did not materialize a vector constant.");

                Assert.That(vector.SimdVal.u64[0] & 0xFFFFUL, Is.EqualTo((ulong)expectedBits));
            }
            finally
            {
                JitTls.Compiler = previousCompiler;
                JitConfig = previousConfig;
            }
        }
    }

    private struct HalfClassInfo
    {
        public byte* Name;
        public byte* Namespace;
        public int Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HalfImportState
    {
        public ICorJitInfo JitInfo;
        public HalfClassInfo HalfClass;
        public byte* MethodName;
        public CORINFO_METHOD_STRUCT_* Method;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ResolveHalfToken(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* token)
    {
        var state = (HalfImportState*)self;
        token->hClass = (CORINFO_CLASS_STRUCT_*)&state->HalfClass;
        token->hMethod = state->Method;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = new CORINFO_EE_INFO
        {
            targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI,
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte RunWithSPMIErrorTrap(
        ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> function, void* parameter)
    {
        function(parameter);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetHalfArrayRank(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetHalfTypeInstantiationArgument(
        ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index) => null;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintHalfClassName(
        ICorJitInfo* self,
        CORINFO_CLASS_STRUCT_* type,
        byte* buffer,
        nint bufferSize,
        nint* requiredBufferSize)
    {
        var name = "System.Half"u8;
        var requiredSize = name.Length + 1;
        if (bufferSize < requiredSize)
        {
            *requiredBufferSize = requiredSize;
            return 0;
        }

        name.CopyTo(new Span<byte>(buffer, name.Length));
        buffer[name.Length] = 0;
        *requiredBufferSize = requiredSize;
        return name.Length;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetHalfCallInfo(
        ICorJitInfo* self,
        CORINFO_RESOLVED_TOKEN* token,
        CORINFO_RESOLVED_TOKEN* constrainedToken,
        CORINFO_METHOD_STRUCT_* caller,
        CORINFO_CALLINFO_FLAGS flags,
        CORINFO_CALL_INFO* callInfo)
    {
        var state = (HalfImportState*)self;
        *callInfo = default;
        callInfo->hMethod = state->Method;
        callInfo->methodFlags = CorInfoFlag.CORINFO_FLG_INTRINSIC | CorInfoFlag.CORINFO_FLG_STATIC;
        callInfo->sig.retType = CorInfoType.CORINFO_TYPE_VALUECLASS;
        callInfo->sig.retTypeClass = (CORINFO_CLASS_STRUCT_*)&state->HalfClass;
        callInfo->sig.retTypeSigClass = (CORINFO_CLASS_STRUCT_*)&state->HalfClass;
        callInfo->kind = CORINFO_CALL_KIND.CORINFO_CALL;
        callInfo->accessAllowed = CorInfoIsAccessAllowedResult.CORINFO_ACCESS_ALLOWED;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetHalfMethodName(
        ICorJitInfo* self,
        CORINFO_METHOD_STRUCT_* method,
        byte** className,
        byte** namespaceName,
        byte** enclosingClassNames,
        nint maxEnclosingClassNames)
    {
        var state = (HalfImportState*)self;
        *className = state->HalfClass.Name;
        *namespaceName = state->HalfClass.Namespace;
        for (var index = 0; index < maxEnclosingClassNames; index++)
        {
            enclosingClassNames[index] = null;
        }

        return state->MethodName;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetHalfClassName(
        ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, byte** namespaceName)
    {
        var classInfo = (HalfClassInfo*)type;
        *namespaceName = classInfo->Namespace;
        return classInfo->Name;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetHalfClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => ((HalfClassInfo*)type)->Size;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetHalfClassAttributes(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => CorInfoFlag.CORINFO_FLG_INTRINSIC_TYPE | CorInfoFlag.CORINFO_FLG_VALUECLASS;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsHalfValueClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte NotifyInstructionSetUsage(
        ICorJitInfo* self, CORINFO_InstructionSet instructionSet, byte supported, byte preserveNegativeDependency)
        => supported;

    private static GenTreeVecCon? FindVectorConstant(GenTree tree)
    {
        if (tree is GenTreeVecCon vector)
        {
            return vector;
        }

        GenTreeVecCon? result = null;
        _ = tree.VisitOperands(operand =>
        {
            result ??= FindVectorConstant(operand);
            return GenTree.VisitResult.Continue;
        });

        return result;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "impStmtList")]
    private static extern ref Statement? ImporterStatements(Compiler compiler);

    private static ref int InlineSize(ref JitConfigValues values)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref values)._jitInlineSize;

    private static ref int InlineDepth(ref JitConfigValues values)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref values)._jitInlineDepth;

    private static ref int ForceDepth(ref JitConfigValues values)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref values)._jitForceInlineDepth;
}
