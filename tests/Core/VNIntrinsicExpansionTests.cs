// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class VNIntrinsicExpansionTests
{
    private static string s_content = "";
    private static bool s_immutable = true;
    private static bool s_contentAvailable = true;
    private static int s_contentQueries;

#if DEBUG
    [TestCase("", "")]
    [TestCase("Hello", "Hello")]
    [TestCase("A\r\nB", "A  B")]
    [TestCase("A\0B", "A")]
    [TestCase("\0hidden", "")]
    [TestCase("A\0\r\nB", "A")]
    [TestCase("A\u00E9\u20AC", "A\u00E9\u20AC")]
    public static void ObjectDescriptionMatchesNativeCString(string source, string expected)
    {
        WithCompiler(source, (compiler, _, _, _) =>
        {
            var output = CodeGenLifeTransitionTests.Capture(() => compiler.eePrintObjectDescription("object", null));
            Assert.That(output, Is.EqualTo($"object '{expected}'"));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    public static void LiteralDumpMatchesNativeBoundaries(int kind)
    {
        var (source, expected) = kind switch
        {
            0 => ("", "\"\""),
            1 => ("Hi", "\"Hi\""),
            2 => ("X\uD800Y", "\"X\uFFFDY\""),
            3 => ("A\0B", "\"A\""),
            4 => (new string('A', 51), $"\"{new string('A', 50)}...\""),
            5 => (new string('A', 49) + "\uD83D\uDE00", $"\"{new string('A', 49)}\uFFFD...\""),
            _ => ("", "<unknown string literal>"),
        };
        WithCompiler(source, (compiler, _, _, _) =>
        {
            s_contentAvailable = kind != 6;
            var output = CodeGenLifeTransitionTests.Capture(() => compiler.eePrintStringLiteral(null, 1));
            Assert.That(output, Is.EqualTo(expected));
        });
    }
#endif

    [TestCase("A", "41")]
    [TestCase("A\u00E9\u20AC", "41C3A9E282AC")]
    [TestCase("\uD83D\uDE00", "F09F9880")]
    public static void BoundedConverterMatchesMinipalFallback(string text, string expectedHex)
    {
        Span<byte> output = stackalloc byte[256];
        var source = MemoryMarshal.Cast<char, ushort>(text.AsSpan());
        var count = Compiler.ConvertReadUtf8Constant(source, output);

        Assert.That(Convert.ToHexString(output[..count]), Is.EqualTo(expectedHex));
    }

    [Test]
    public static void MalformedSurrogatesUseOneReplacementPerCodeUnit()
    {
        Span<byte> output = stackalloc byte[256];
        var text = new[] { '\uD800', 'A', '\uDC00' };
        var source = MemoryMarshal.Cast<char, ushort>(text.AsSpan());
        var count = Compiler.ConvertReadUtf8Constant(source, output);
        Assert.That(Convert.ToHexString(output[..count]), Is.EqualTo("EFBFBD41EFBFBD"));
    }

    [Test]
    public static void BoundedConverterRejectsIncompleteOutput()
    {
        Span<byte> output = stackalloc byte[256];
        var source = MemoryMarshal.Cast<char, ushort>(new string('\u20AC', 86).AsSpan());
        Assert.That(Compiler.ConvertReadUtf8Constant(source, output), Is.Zero);

        var exact = MemoryMarshal.Cast<char, ushort>(new string('\u00E9', 128).AsSpan());
        Assert.That(Compiler.ConvertReadUtf8Constant(exact, output), Is.EqualTo(256));
        Assert.That(output.ToArray().All(value => value is 0xC3 or 0xA9), Is.True);
    }

    [TestCase(253, 256)]
    [TestCase(254, 254)]
    public static void TrailingUnpairedSurrogatePreservesNativePartialFallback(int prefixLength, int expectedLength)
    {
        var chars = new char[prefixLength + 1];
        chars.AsSpan(0, prefixLength).Fill('A');
        chars[^1] = '\uD800';
        Span<byte> output = stackalloc byte[256];

        var count = Compiler.ConvertReadUtf8Constant(MemoryMarshal.Cast<char, ushort>(chars), output);
        Assert.That(count, Is.EqualTo(expectedLength));
        Assert.That(output[..prefixLength].ToArray().All(value => value == (byte)'A'), Is.True);
        if (expectedLength == 256)
        {
            Assert.That(Convert.ToHexString(output[prefixLength..]), Is.EqualTo("EFBFBD"));
        }
    }

    [TestCase("Hi!", 3)]
    [TestCase("\u00E9\uD83D\uDE00!", 7)]
    [TestCase("abcdefghi", 9)]
    [TestCase("ABCDEFGHIJKLMNOPQ", 17)]
    public static void LiteralProducesGuardedOverlappingCopy(string source, int byteLength)
    {
        WithCompiler(source, (compiler, entry, body, store) =>
        {
            var call = NewReadUtf8Call(compiler, store, source.Length);
            var statement = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            body.setBBProfileWeight(100);
            compiler.MethodHasSpecialIntrinsics = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(s_contentQueries, Is.EqualTo(1));
            Assert.That(call.IsSpecialIntrinsic(), Is.False);

            var lengthCheck = entry.Target;
            var fast = lengthCheck.Next!;
            var remainder = fast.Next!;
            Assert.That(lengthCheck.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(lengthCheck.TrueTarget, Is.SameAs(remainder));
            Assert.That(lengthCheck.FalseTarget, Is.SameAs(fast));
            Assert.That(lengthCheck.TrueEdge.Likelihood, Is.EqualTo(1.0));
            Assert.That(lengthCheck.FalseEdge.Likelihood, Is.EqualTo(0.0));
            Assert.That(fast.Target, Is.SameAs(remainder));
            Assert.That(fast.bbWeight, Is.Zero);
            Assert.That(lengthCheck.bbWeight, Is.EqualTo(100));
            Assert.That(remainder.bbWeight, Is.EqualTo(100));
            Assert.That(BasicBlock.sameEHRegion(lengthCheck, remainder), Is.True);
            Assert.That(BasicBlock.sameEHRegion(lengthCheck, fast), Is.True);
            Assert.That(statement, Is.SameAs(remainder.FirstStmt));
            Assert.That(statement.TreeList.Any(node => ReferenceEquals(node, call)), Is.False);

            var check = lengthCheck.LastStmt!.RootNode.AsUnOp().Op1.AsOp();
            Assert.That(check.Oper, Is.EqualTo(GT_LT));
            Assert.That(check.Flags & GTF_RELOP_JMP_USED, Is.Not.EqualTo(GTF_EMPTY));
            Assert.That(check.Op2.AsIntCon().IconValue, Is.EqualTo((nint)byteLength));
            Assert.That(lengthCheck.Statements.Any(stmt => stmt.RootNode is GenTreeLclVar local
                && local.Oper is GT_STORE_LCL_VAR && local.Data.IsIntegralConst(-1)), Is.True);

            var stores = fast.Statements.Where(stmt => stmt.RootNode.Oper is GT_STOREIND)
                .Select(stmt => stmt.RootNode.AsIndir()).ToArray();
            var chunkSize = stores[0].Type.Size;
            Assert.That(stores, Has.Length.EqualTo((byteLength + chunkSize - 1) / chunkSize));
            for (var i = 0; i < stores.Length; i++)
            {
                var offset = i == stores.Length - 1 ? byteLength - chunkSize : i * chunkSize;
                Assert.That(stores[i].Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)offset));
                Assert.That(stores[i].Data.Oper.IsConst, Is.True);
            }
            Assert.That(fast.LastStmt!.RootNode.AsLclVar().Data.AsIntCon().IconValue,
                Is.EqualTo((nint)byteLength));
        });
    }

    [Test]
    public static void NestedCallKeepsEarlierSideEffectsBeforeTheGuard()
    {
        WithCompiler("Hi", (compiler, entry, body, store) =>
        {
            var call = NewReadUtf8Call(compiler, store, 2);
            var effect = compiler.gtNewStoreLclVarNode(3, compiler.gtNewIconNode(TYP_INT, 42));
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, call, compiler.gtNewIconNode(TYP_INT, 1));
            var expression = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, effect, sum);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, expression));
            compiler.MethodHasSpecialIntrinsics = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var guard = entry.Target;
            var fast = guard.Next!;
            var remainder = fast.Next!;
            Assert.That(guard.Statements.Any(stmt => stmt.RootNode is GenTreeLclVar local
                && local.Oper is GT_STORE_LCL_VAR && local.LclNum == 3), Is.True);
            Assert.That(remainder.FirstStmt, Is.SameAs(original));
            Assert.That(original.TreeList.Any(node => ReferenceEquals(node, call)), Is.False);
            Assert.That(original.TreeList.Any(node => node.Oper is GT_ADD), Is.True);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(257)]
    public static void InvalidSourceLengthDoesNotQueryContent(int sourceLength)
    {
        WithCompiler("A", (compiler, entry, body, store) =>
        {
            var call = NewReadUtf8Call(compiler, store, sourceLength);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasSpecialIntrinsics = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_contentQueries, Is.Zero);
            Assert.That(call.IsSpecialIntrinsic(), Is.True);
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void MutableOrUnavailableObjectDoesNotExpand(bool immutable, bool available)
    {
        WithCompiler("Hi", (compiler, entry, body, store) =>
        {
            s_immutable = immutable;
            s_contentAvailable = available;
            var call = NewReadUtf8Call(compiler, store, 2);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasSpecialIntrinsics = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(body.FirstStmt, Is.SameAs(original));
            Assert.That(s_contentQueries, Is.EqualTo(immutable ? 1 : 0));
        });
    }

    [Test]
    public static void ExcessiveEncodedLengthLeavesOriginalCall()
    {
        WithCompiler(new string('\u20AC', 86), (compiler, entry, body, store) =>
        {
            var call = NewReadUtf8Call(compiler, store, 86);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasSpecialIntrinsics = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(body.LastStmt!.TreeList.Any(node => ReferenceEquals(node, call)), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SourceAndLengthRequireEqualValueNumbers(bool corruptSource)
    {
        WithCompiler("Hi", (compiler, entry, body, store) =>
        {
            var call = NewReadUtf8Call(compiler, store, 2);
            var argument = call.Args.GetUserArgByIndex(corruptSource ? 0 : 1)!.Node;
            argument._vnPair.Conservative = store.VNForIntCon(42);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasSpecialIntrinsics = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(body.FirstStmt, Is.SameAs(original));
            Assert.That(s_contentQueries, Is.Zero);
        });
    }

    [Test]
    public static void SizeOptimizationGatePreservesOriginalCall()
    {
        WithCompiler("Hi", (compiler, entry, body, store) =>
        {
            var call = NewReadUtf8Call(compiler, store, 2);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasSpecialIntrinsics = true;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_SIZE_OPT);

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(body.FirstStmt, Is.SameAs(original));
            Assert.That(s_contentQueries, Is.Zero);
        });
    }

    [Test]
    public static void UnrollThresholdRejectsLongButConvertibleLiteral()
    {
        WithCompiler(new string('A', 256), (compiler, entry, body, store) =>
        {
            compiler.opts.preferredVectorByteLength = 16;
            var threshold = compiler.GetUnrollThreshold(Compiler.Memcpy);
            Assert.That(threshold, Is.LessThan(256));
            var call = NewReadUtf8Call(compiler, store, threshold + 1);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasSpecialIntrinsics = true;

            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(body.FirstStmt, Is.SameAs(original));
            Assert.That(s_contentQueries, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RareBlockOrDisabledOptimizationsPreserveCall(bool minOpts)
    {
        WithCompiler("Hi", (compiler, entry, body, store) =>
        {
            var call = NewReadUtf8Call(compiler, store, 2);
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));

            compiler.MethodHasSpecialIntrinsics = true;
            body.setBBProfileWeight(minOpts ? 1 : 0);
            Assert.That(Expand(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_contentQueries, Is.Zero);
            Assert.That(call.IsSpecialIntrinsic(), Is.True);
        }, minOpts);
    }

    private static PhaseStatus Expand(Compiler compiler) => compiler.fgVNBasedIntrinsicExpansion();

    private static GenTreeCall NewReadUtf8Call(Compiler compiler, ValueNumStore store, int length)
    {
        var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)0x1234);
        call._callMoreFlags |= GTF_CALL_M_SPECIAL_INTRINSIC;

        var srcPtr = compiler.gtNewIconNode(TYP_BYREF, 0);
        srcPtr._vnPair.SetBoth(store.VNForHandle(0x1000, GTF_ICON_OBJ_HDL));
        var srcLen = compiler.gtNewIconNode(TYP_INT, length);
        srcLen._vnPair.SetBoth(store.VNForIntCon(length));

        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(srcPtr));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(srcLen));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_BYREF, 0)));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 2)));
        return call;
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var statement = compiler.gtNewStmt(tree);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);
        return statement;
    }

    private static void WithCompiler(string source, Action<Compiler, BasicBlock, BasicBlock, ValueNumStore> action,
        bool minOpts = false)
    {
        s_content = source;
        s_immutable = true;
        s_contentAvailable = true;
        s_contentQueries = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getMethodNameFromMetadata = &GetMethodName;
        vtable.Base.Base.isObjectImmutable = &IsImmutable;
        vtable.Base.getObjectContent = &GetContent;
#if DEBUG
        vtable.Base.Base.printObjectDescription = &GetDescription;
        vtable.Base.Base.getStringLiteral = &GetLiteral;
        vtable.Base.Base.runWithSPMIErrorTrap = &RunTrap;
#endif
        ICorJitInfo ee = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&ee);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.info = new Compiler.Info { compCompHnd = &ee };
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
#if DEBUG
        compiler.info.compFullName = nameof(VNIntrinsicExpansionTests);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        compiler.compHndBBtab = [];
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 4;
        compiler.lvaTable[0].Type = TYP_BYREF;
        compiler.lvaTable[1].Type = TYP_INT;
        compiler.lvaTable[2].Type = TYP_INT;
        compiler.lvaTable[3].Type = TYP_INT;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.codeGen = new CodeGen(compiler);
        var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
        var body = BasicBlock.New(compiler, BBJ_RETURN);
        body.RemoveFlags(BBF_INTERNAL);
        body.SetFlags(BBF_IMPORTED);
        entry.bbRefs = 1;
        entry.Next = body;
        body.Prev = entry;
        compiler.fgFirstBB = entry;
        compiler.fgLastBB = body;
        compiler.fgPredsComputed = true;
        JitTls.Compiler = compiler;
        entry.TargetEdge = compiler.fgAddRefPred(body, entry);
        var store = new ValueNumStore(compiler);
        compiler.vnStore = store;

        try
        {
            action(compiler, entry, body, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetMethodName(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method,
        byte** className, byte** namespaceName, byte** enclosingClasses, nint maxEnclosingClasses)
    {
        *namespaceName = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference("System.Text\0"u8));
        *className = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference("UTF8EncodingSealed\0"u8));
        enclosingClasses[0] = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference("UTF8Encoding\0"u8));
        return (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference("ReadUtf8\0"u8));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsImmutable(ICorJitInfo* self, CORINFO_OBJECT_STRUCT_* obj) => s_immutable ? (byte)1 : (byte)0;

#if DEBUG
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint GetDescription(ICorJitInfo* self, CORINFO_OBJECT_STRUCT_* obj,
        byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        var bytes = new Span<byte>(buffer, (int)bufferSize);
        bytes.Fill(0xCC);
        var written = Encoding.UTF8.GetBytes(s_content.AsSpan(), bytes[..^1]);
        bytes[written] = 0;

        if (requiredBufferSize != null)
        {
            *requiredBufferSize = written + 1;
        }

        return written;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte RunTrap(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        callback(state);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetLiteral(ICorJitInfo* self, CORINFO_MODULE_STRUCT_* module, int token,
        char* buffer, int bufferSize, int startIndex)
    {
        if (!s_contentAvailable)
        {
            return -1;
        }

        s_content.AsSpan(0, Math.Min(bufferSize, s_content.Length)).CopyTo(new Span<char>(buffer, bufferSize));
        return s_content.Length;
    }
#endif

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte GetContent(ICorJitInfo* self, CORINFO_OBJECT_STRUCT_* obj, byte* buffer,
        int bufferSize, int offset)
    {
        s_contentQueries++;
        var bytes = MemoryMarshal.AsBytes(s_content.AsSpan());
        if (!s_contentAvailable || (offset < 0) || (bufferSize > bytes.Length - offset))
        {
            return 0;
        }

        bytes.Slice(offset, bufferSize).CopyTo(new Span<byte>(buffer, bufferSize));
        return 1;
    }
}
