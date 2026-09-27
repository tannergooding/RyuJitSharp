// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoCallConv;
using static RyuJitSharp.CorInfoType;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class DiscretionaryPolicyTests
{
    private static JitConfigValues s_previousConfig;
    private static Compiler? s_previousCompiler;
#if DEBUG
    private static JitTls? s_jitTls;
#endif

    [SetUp]
    public static unsafe void SetUp()
    {
        s_previousConfig = Globals.JitConfig;
        s_previousCompiler = JitTls.Compiler;
#if DEBUG
        s_jitTls = new JitTls(null);
#endif
        JitTls.Compiler = CreateCompiler();
        object config = new JitConfigValues();
        SetField(typeof(JitConfigValues), config, "_jitInlinePolicyProfileThreshold", 40);
        Globals.JitConfig = (JitConfigValues)config;
    }

    [TearDown]
    public static void TearDown()
    {
        Globals.JitConfig = s_previousConfig;
        JitTls.Compiler = s_previousCompiler;
#if DEBUG
        s_jitTls?.Dispose();
        s_jitTls = null;
#endif
    }

    [TestCase(InlineObservation.CALLEE_NOT_PROFITABLE_INLINE, false)]
    [TestCase(InlineObservation.CALLEE_DOES_NOT_RETURN, false)]
    [TestCase(InlineObservation.CALLEE_TOO_MUCH_IL, true)]
    public static void NeverPropagationMatchesNative(InlineObservation observation, bool propagate)
    {
        var policy = new ProbePolicy(CreateCompiler());
        policy.SetObservation(observation);
        Assert.That(policy.PropagateNeverToRuntime(), Is.EqualTo(propagate));
        Assert.That(new ModelPolicy(CreateCompiler(), true).PropagateNeverToRuntime(), Is.True);
    }

    [TestCase(typeof(ModelPolicy), 119, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)]
    [TestCase(typeof(ModelPolicy), 120, InlineObservation.CALLEE_TOO_MUCH_IL)]
    [TestCase(typeof(ProfilePolicy), 999, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)]
    [TestCase(typeof(ProfilePolicy), 1000, InlineObservation.CALLEE_TOO_MUCH_IL)]
    public static void EarlySizeCutoffs(Type policyType, int size, InlineObservation expected)
    {
        DiscretionaryPolicy policy = policyType == typeof(ModelPolicy)
            ? new ModelPolicy(CreateCompiler(), true)
            : new ProfilePolicy(CreateCompiler(), true);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, size);
        Assert.That(policy.Observation, Is.EqualTo(expected));
    }

    [TestCase(false, false, 6, InlineObservation.CALLEE_TOO_MANY_BASIC_BLOCKS)]
    [TestCase(true, false, 6, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)]
    [TestCase(true, true, 1, InlineObservation.CALLEE_DOES_NOT_RETURN)]
    [TestCase(true, true, 2, InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)]
    public static void ProfileBlockRestrictions(bool weights, bool noReturn, int blocks, InlineObservation expected)
    {
        var policy = new ProfilePolicy(CreateCompiler(), true);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteBool(InlineObservation.CALLEE_DOES_NOT_RETURN, noReturn);
        policy.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, weights);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 30);
        policy.NoteInt(InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS, blocks);
        Assert.That(policy.Observation, Is.EqualTo(expected));
    }

    [TestCase(InlineObservation.CALLEE_MAXSTACK, 200)]
    [TestCase(InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS, 80)]
    [TestCase(InlineObservation.CALLSITE_WEIGHT, 9)]
    public static void DiscretionaryObservationsBypassLegacyRejection(InlineObservation observation, int value)
    {
        var policy = new ProbePolicy(CreateCompiler());
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 1000);
        policy.NoteInt(observation, value);
        policy.NoteBool(InlineObservation.CALLSITE_RARE_GC_STRUCT, true);
        Assert.That(policy.Observation, Is.EqualTo(InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE));
    }

    [TestCase(0, InlineCallsiteFrequency.BORING, -128, -65)]
    [TestCase(1000, InlineCallsiteFrequency.BORING, 521, -65)]
    [TestCase(1000, InlineCallsiteFrequency.HOT, 532, -73)]
    public static void ModelEstimatesUseNativeCoefficients(int nativeSize, InlineCallsiteFrequency frequency, int size, int instructions)
    {
        var policy = new ProbePolicy(CreateCompiler());
        policy.SetNativeSize(nativeSize);
        policy.NoteInt(InlineObservation.CALLSITE_FREQUENCY, (int)frequency);
        policy.Estimate();
        Assert.Multiple(() => {
            Assert.That(policy.CodeSizeEstimate(), Is.EqualTo(size));
            Assert.That(policy.PerCallInstructionEstimate, Is.EqualTo(instructions));
        });
    }

    [Test]
    public static unsafe void MethodInfoIncludesImplicitArgumentsAndTruncatesExplicitArguments()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArgType = &GetArgumentType;
        vtable.Base.Base.getArgNext = &GetNextArgument;
        vtable.Base.Base.getClassSize = &GetClassSize;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = CreateCompiler();
        compiler.info.compCompHnd = &jitInfo;

        var info = VoidMethod();
        info.locals.numArgs = 3;
        info.args.callConv = CORINFO_CALLCONV_HASTHIS | CORINFO_CALLCONV_PARAMTYPE;
        info.args.numArgs = 7;
        info.args.args = (CORINFO_ARG_LIST_STRUCT_*)1;
        info.args.retType = CORINFO_TYPE_VALUECLASS;
        info.args.retTypeClass = (CORINFO_CLASS_STRUCT_*)1;

        var policy = new ProbePolicy(compiler);
        policy.Observe(info);
        CorInfoType[] expectedArgTypes = [
            CORINFO_TYPE_CLASS, CORINFO_TYPE_NATIVEINT, CORINFO_TYPE_VALUECLASS,
            CORINFO_TYPE_BOOL, CORINFO_TYPE_CLASS, CORINFO_TYPE_CLASS,
        ];
        nuint[] expectedArgSizes = [8, 8, 16, 8, 8, 8];

        Assert.Multiple(() => {
            Assert.That(policy.ArgCount, Is.EqualTo(9));
            Assert.That(policy.LocalCount, Is.EqualTo(3));
            Assert.That(policy.ArgTypes, Is.EqualTo(expectedArgTypes));
            Assert.That(policy.ArgSizes, Is.EqualTo(expectedArgSizes));
            Assert.That(policy.ReturnSize, Is.EqualTo((nuint)16));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe CorInfoTypeWithMod GetArgumentType(ICorJitInfo* self, CORINFO_SIG_INFO* sig,
        CORINFO_ARG_LIST_STRUCT_* argument, CORINFO_CLASS_STRUCT_** type)
    {
        *type = (CORINFO_CLASS_STRUCT_*)1;
        return (CorInfoTypeWithMod)((nuint)argument switch {
            1 => CORINFO_TYPE_VALUECLASS,
            2 => CORINFO_TYPE_BOOL,
            _ => CORINFO_TYPE_CLASS,
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe CORINFO_ARG_LIST_STRUCT_* GetNextArgument(ICorJitInfo* self,
        CORINFO_ARG_LIST_STRUCT_* argument)
    {
        return (CORINFO_ARG_LIST_STRUCT_*)((nuint)argument + 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
    {
        return 9;
    }

    [TestCase(0, InlineCallsiteFrequency.BORING, InlineObservation.CALLEE_IS_SIZE_DECREASING_INLINE)]
    [TestCase(1000, InlineCallsiteFrequency.BORING, InlineObservation.CALLEE_NOT_PROFITABLE_INLINE)]
    [TestCase(1000, InlineCallsiteFrequency.HOT, InlineObservation.CALLEE_IS_PROFITABLE_INLINE)]
    public static void ModelProfitability(int nativeSize, InlineCallsiteFrequency frequency, InlineObservation expected)
    {
        var policy = new ModelPolicy(CreateCompiler(), true);
        Prepare(policy, nativeSize, frequency);
        policy.DetermineProfitability(VoidMethod());
        Assert.That(policy.Observation, Is.EqualTo(expected));
    }

    [TestCase(false, 0, InlineObservation.CALLSITE_NOT_PROFITABLE_INLINE)]
    [TestCase(true, 0, InlineObservation.CALLEE_IS_SIZE_DECREASING_INLINE)]
    [TestCase(true, 1, InlineObservation.CALLEE_NOT_PROFITABLE_INLINE)]
    [TestCase(true, 2, InlineObservation.CALLEE_IS_PROFITABLE_INLINE)]
    public static void ProfileProfitability(bool weights, int frequency, InlineObservation expected)
    {
        var policy = new ProfilePolicy(CreateCompiler(), true);
        Prepare(policy, weights ? (frequency == 0 ? 0 : 1000) : 0, InlineCallsiteFrequency.BORING);
        policy.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, weights);
        policy.NoteDouble(InlineObservation.CALLSITE_PROFILE_FREQUENCY, frequency);
        policy.DetermineProfitability(VoidMethod());
        Assert.That(policy.Observation, Is.EqualTo(expected));
    }

#if DEBUG
    [TestCase(-500, 100, InlineObservation.CALLEE_IS_SIZE_DECREASING_INLINE)]
    [TestCase(100, 100, InlineObservation.CALLEE_NOT_PROFITABLE_INLINE)]
    public static void SizePolicyComparesScaledRootEstimates(int currentSize, int initialSize, InlineObservation expected)
    {
        var compiler = CreateCompiler();
        var strategy = CreateStrategy(compiler);
        SetField(typeof(InlineStrategy), strategy, "_currentSizeEstimate", currentSize);
        SetField(typeof(InlineStrategy), strategy, "_initialSizeEstimate", initialSize);
        var policy = new SizePolicy(compiler, true);
        Prepare(policy, 1000, InlineCallsiteFrequency.BORING);
        policy.DetermineProfitability(VoidMethod());
        Assert.That(policy.Observation, Is.EqualTo(expected));
    }

    [TestCase(typeof(DiscretionaryPolicy), "DiscretionaryPolicy")]
    [TestCase(typeof(ModelPolicy), "ModelPolicy")]
    [TestCase(typeof(ProfilePolicy), "ProfilePolicy")]
    [TestCase(typeof(RandomPolicy), "RandomPolicy")]
    [TestCase(typeof(FullPolicy), "FullPolicy")]
    [TestCase(typeof(SizePolicy), "SizePolicy")]
    public static void DebugPolicyNamesMatchNative(Type policyType, string name)
    {
        var compiler = CreateCompiler();
        var strategy = CreateStrategy(compiler);
        SetField(typeof(InlineStrategy), strategy, "_random", new CLRRandom(1));
        var policy = policyType.Name switch {
            nameof(DiscretionaryPolicy) => new DiscretionaryPolicy(compiler, true),
            nameof(ModelPolicy) => new ModelPolicy(compiler, true),
            nameof(ProfilePolicy) => new ProfilePolicy(compiler, true),
            nameof(RandomPolicy) => new RandomPolicy(compiler, true),
            nameof(FullPolicy) => new FullPolicy(compiler, true),
            nameof(SizePolicy) => new SizePolicy(compiler, true),
            _ => throw new ArgumentOutOfRangeException(nameof(policyType)),
        };
        Assert.That(policy.Name, Is.EqualTo(name));
    }

    [TestCase(1, 100, InlineObservation.CALLEE_IS_PROFITABLE_INLINE)]
    [TestCase(2, 100, InlineObservation.CALLSITE_IS_TOO_DEEP)]
    [TestCase(1, 101, InlineObservation.CALLEE_TOO_MUCH_IL)]
    public static void FullPolicyChecksLimits(int depth, int size, InlineObservation expected)
    {
        var compiler = CreateCompiler();
        var strategy = CreateStrategy(compiler);
        SetField(typeof(InlineStrategy), strategy, "_maxInlineDepth", 1);
        SetField(typeof(InlineStrategy), strategy, "_maxInlineSize", 100);
        var policy = new FullPolicy(compiler, true);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, size);
        // FullPolicy intentionally defers the depth check to profitability.
        SetField(typeof(DefaultPolicy), policy, "_callsiteDepth", depth);
        policy.DetermineProfitability(VoidMethod());
        Assert.That(policy.Observation, Is.EqualTo(expected));
        Assert.That(policy.BudgetCheck(), Is.False);
    }

    [TestCase(16, 75)]
    [TestCase(30, 50)]
    [TestCase(40, 40)]
    [TestCase(50, 30)]
    [TestCase(75, 20)]
    [TestCase(100, 10)]
    [TestCase(200, 5)]
    [TestCase(201, 1)]
    public static void RandomPolicyUsesNativeThresholds(int size, int threshold)
    {
        var compiler = CreateCompiler();
        var strategy = CreateStrategy(compiler);
        var random = new CLRRandom(1);
        SetField(typeof(InlineStrategy), strategy, "_random", random);
        var expectedRandom = new CLRRandom(1).Next(1, 100);
        var policy = new RandomPolicy(compiler, true);
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, size);
        policy.DetermineProfitability(VoidMethod());
        Assert.That(policy.Observation, Is.EqualTo(expectedRandom <= threshold
            ? InlineObservation.CALLEE_RANDOM_ACCEPT
            : InlineObservation.CALLEE_RANDOM_REJECT));
    }

    [TestCase(OPCODE.CEE_LDARG_0, "ArgAccessCount")]
    [TestCase(OPCODE.CEE_LDC_I4_0, "IntConstantCount")]
    [TestCase(OPCODE.CEE_DIV, "ComplexMathCount")]
    [TestCase(OPCODE.CEE_ADD_OVF, "OverflowMathCount")]
    [TestCase(OPCODE.CEE_LDELEM_REF, "RefArrayLoadCount")]
    [TestCase(OPCODE.CEE_STELEM_I1, "IntArrayStoreCount")]
    [TestCase(OPCODE.CEE_LDSFLD, "StaticFieldLoadCount")]
    [TestCase(OPCODE.CEE_RET, "ReturnCount")]
    [TestCase(OPCODE.CEE_THROW, "ThrowCount")]
    public static void OpcodeBinsAppearInNativeData(OPCODE opcode, string column)
    {
        var policy = new ProbePolicy(CreateCompiler());
        policy.NoteInt(InlineObservation.CALLEE_OPCODE, (int)opcode);
        var (schema, data) = GetCsv(policy);
        Assert.That(schema, Has.Length.GreaterThan(1), "The policy must emit opcode CSV schema.");
        Assert.That(data, Has.Length.GreaterThan(1), "The policy must emit opcode CSV data.");

        var index = Array.IndexOf(schema, column);
        // Native DumpData writes ReturnCount before ThrowCount, while DumpSchema names them in the opposite order.
        if (column is "ReturnCount" or "ThrowCount")
        {
            index = Array.IndexOf(schema, column is "ReturnCount" ? "ThrowCount" : "ReturnCount");
        }

        Assert.That(index, Is.GreaterThanOrEqualTo(0), $"Missing opcode CSV column {column}.");
        Assert.That(data[index], Is.EqualTo("1"));
    }

    [Test]
    public static void CsvPreservesNativeHeaderAndDataOrder()
    {
        var policy = new ProbePolicy(CreateCompiler());
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 32);
        policy.NoteInt(InlineObservation.CALLSITE_WEIGHT, 7);
        var (schema, data) = GetCsv(policy);
        var expectedSchema =
            "ILSize,CallsiteFrequency,InstructionCount,LoadStoreCount,BlockCount,Maxstack,ArgCount," +
            "Arg0Type,Arg1Type,Arg2Type,Arg3Type,Arg4Type,Arg5Type," +
            "Arg0Size,Arg1Size,Arg2Size,Arg3Size,Arg4Size,Arg5Size," +
            "LocalCount,ReturnType,ReturnSize,ArgAccessCount,LocalAccessCount,IntConstantCount,FloatConstantCount," +
            "IntLoadCount,FloatLoadCount,IntStoreCount,FloatStoreCount,SimpleMathCount,ComplexMathCount,OverflowMathCount," +
            "IntArrayLoadCount,FloatArrayLoadCount,RefArrayLoadCount,StructArrayLoadCount," +
            "IntArrayStoreCount,FloatArrayStoreCount,RefArrayStoreCount,StructArrayStoreCount," +
            "StructOperationCount,ObjectModelCount,FieldLoadCount,FieldStoreCount,StaticFieldLoadCount,StaticFieldStoreCount," +
            "LoadAddressCount,ThrowCount,ReturnCount,CallCount,CallSiteWeight," +
            "IsForceInline,IsInstanceCtor,IsFromPromotableValueClass,HasSimd,LooksLikeWrapperMethod," +
            "ArgFeedsConstantTest,IsMostlyLoadStore,ArgFeedsRangeCheck,ConstantArgFeedsConstantTest," +
            "CalleeNativeSizeEstimate,CallsiteNativeSizeEstimate,ModelCodeSizeEstimate,ModelPerCallInstructionEstimate," +
            "IsClassCtor,IsSameThis,CallerHasNewArray,CallerHasNewObj,CalleeDoesNotReturn,CalleeHasGCStruct,CallsiteDepth";

        Assert.Multiple(() => {
            Assert.That(string.Join(',', schema), Is.EqualTo(expectedSchema));
            Assert.That(schema[0], Is.EqualTo("ILSize"));
            Assert.That(schema[7], Is.EqualTo("Arg0Type"));
            Assert.That(schema[13], Is.EqualTo("Arg0Size"));
            Assert.That(schema[^1], Is.EqualTo("CallsiteDepth"));
            Assert.That(data[0], Is.EqualTo("32"));
            Assert.That(data[Array.IndexOf(schema, "CallSiteWeight")], Is.EqualTo("7"));
            Assert.That(data.Length, Is.EqualTo(schema.Length + 2));
            Assert.That(data.Skip(7).Take(12), Is.All.EqualTo("0"));
        });
    }

    private static (string[] Schema, string[] Data) GetCsv(DiscretionaryPolicy policy)
    {
        using var schemaStream = new MemoryStream();
        using var schemaWriter = new StreamWriter(schemaStream, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true);
        policy.DumpSchema(schemaWriter);
        schemaWriter.Flush();
        using var dataStream = new MemoryStream();
        using var dataWriter = new StreamWriter(dataStream, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true);
        policy.DumpData(dataWriter);
        dataWriter.Flush();
        return (System.Text.Encoding.UTF8.GetString(schemaStream.ToArray()).Split(','),
            System.Text.Encoding.UTF8.GetString(dataStream.ToArray()).Split(','));
    }
#endif

    private static CORINFO_METHOD_INFO VoidMethod()
    {
        var info = new CORINFO_METHOD_INFO();
        info.args.retType = CorInfoType.CORINFO_TYPE_VOID;
        return info;
    }

    private static void Prepare(DiscretionaryPolicy policy, int nativeSize, InlineCallsiteFrequency frequency)
    {
        policy.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
        policy.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 30);
        policy.NoteInt(InlineObservation.CALLSITE_FREQUENCY, (int)frequency);
        SetField(typeof(DefaultPolicy), policy, "_stateMachine", CreateStateMachine(nativeSize));
    }

    private static CodeSeqSM CreateStateMachine(int nativeSize)
    {
        var machine = new CodeSeqSM();
        SetField(typeof(CodeSeqSM), machine, "_nativeSize", nativeSize);
        return machine;
    }

    private static InlineStrategy CreateStrategy(Compiler compiler)
    {
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        compiler._inlineStrategy = strategy;
        return strategy;
    }

    private static Compiler CreateCompiler()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTable = [];
        return compiler;
    }

    private static void SetField([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
        object target, string name, object value)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field {name}.");
        field.SetValue(target, value);
    }

    private sealed class ProbePolicy(Compiler compiler) : DiscretionaryPolicy(compiler, true)
    {
        public uint ArgCount => _argCount;
        public nuint[] ArgSizes => _argSize;
        public CorInfoType[] ArgTypes => _argType;
        public uint LocalCount => _localCount;
        public int PerCallInstructionEstimate => _perCallInstructionEstimate;
        public nuint ReturnSize => _returnSize;

        public void Estimate()
        {
            EstimateCodeSize();
            EstimatePerformanceImpact();
        }

        public void SetNativeSize(int nativeSize)
        {
            _stateMachine = CreateStateMachine(nativeSize);
        }

        public void SetObservation(InlineObservation observation)
        {
            _observation = observation;
        }

        public void Observe(in CORINFO_METHOD_INFO info)
        {
            MethodInfoObservations(info);
        }
    }
}
