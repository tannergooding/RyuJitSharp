// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

#if DEBUG
using System.IO;
using System.Text;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class StructPromotionTests
{
    [TestCase(CORINFO_TYPE_LONG, TYP_LONG)]
    [TestCase(CORINFO_TYPE_DOUBLE, TYP_DOUBLE)]
    [TestCase(CORINFO_TYPE_BYREF, TYP_BYREF)]
    public static void PromotesOriginalLocalsAcrossTableGrowthInFieldOffsetOrder(CorInfoType fieldType, var_types expected)
    {
        WithCompiler((compiler, metadata) => {
            metadata->FieldType = fieldType;
            compiler.lvaTable[2] = compiler.lvaTable[0];
            var original = compiler.lvaTable;

            Assert.That(Promote(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaTable, Is.Not.SameAs(original));
            Assert.That(compiler.lvaCount, Is.EqualTo(7));
            Assert.That(metadata->LayoutQueries, Is.EqualTo(1));
            for (var parent = 0; parent <= 2; parent += 2)
            {
                ref var local = ref compiler.lvaTable[parent];
                Assert.That(local.lvPromoted, Is.True);
                Assert.That(local.lvFieldCnt, Is.EqualTo(2));
                Assert.That(local.lvFieldLclStart, Is.EqualTo(parent == 0 ? 3 : 5));
                for (var index = 0; index < 2; index++)
                {
                    ref var field = ref compiler.lvaTable[local.lvFieldLclStart + index];
                    Assert.That(field.Type, Is.EqualTo(expected));
                    Assert.That(field.lvIsStructField, Is.True);
                    Assert.That(field.lvParentLcl, Is.EqualTo(parent));
                    Assert.That(field.lvFldOffset, Is.EqualTo(index * 8));
                    Assert.That(field.lvFldOrdinal, Is.EqualTo(1 - index));
                    Assert.That(field.lvIsTemp, Is.False);
                    Assert.That(field.lvOnFrame, Is.True);
                }
            }
            Assert.That(compiler.compFloatingPointUsed, Is.EqualTo(expected is TYP_DOUBLE));
            Assert.That(compiler.compLongUsed, Is.EqualTo(expected is TYP_LONG));
        });
    }

    [TestCase(1, 0, 8, 0, 8, CORINFO_TYPE_LONG, TYP_LONG)]
    [TestCase(1, 4, 4, 4, 4, CORINFO_TYPE_INT, TYP_INT)]
    [TestCase(2, 0, 8, 0, 8, CORINFO_TYPE_LONG, TYP_UNDEF)]
    [TestCase(1, 0, 8, 4, 8, CORINFO_TYPE_LONG, TYP_UNDEF)]
    [TestCase(1, 0, 8, 0, 4, CORINFO_TYPE_LONG, TYP_UNDEF)]
    [TestCase(1, 0, 16, 0, 16, CORINFO_TYPE_LONG, TYP_UNDEF)]
    [TestCase(1, 2, 4, 2, 4, CORINFO_TYPE_INT, TYP_UNDEF)]
    [TestCase(1, 0, 8, 0, 8, CORINFO_TYPE_VALUECLASS, TYP_UNDEF)]
    public static void PrimitiveWrapperPromotionRequiresMatchingAlignedStorage(
        int fieldCount, int wrapperOffset, int wrapperSize, int fieldOffset, int fieldSize,
        CorInfoType fieldType, var_types expected)
    {
        WithCompiler((compiler, _) => {
            var nodes = stackalloc CORINFO_TYPE_LAYOUT_NODE[2];
            nodes[0] = new CORINFO_TYPE_LAYOUT_NODE
            {
                type = CORINFO_TYPE_VALUECLASS,
                numFields = fieldCount,
                offset = wrapperOffset,
                size = wrapperSize,
                simdTypeHnd = NO_CLASS_HANDLE,
            };
            nodes[1] = new CORINFO_TYPE_LAYOUT_NODE
            {
                type = fieldType,
                offset = fieldOffset,
                size = fieldSize,
                simdTypeHnd = NO_CLASS_HANDLE,
            };

            var helper = compiler.structPromotionHelper ?? throw new AssertionException("Missing promotion helper.");
            Assert.That(TryPromoteValueClassAsPrimitive(helper, nodes, 2, 0), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void ClearsConservativeInlineTypeCacheBeforePromotion()
    {
        WithCompiler((compiler, metadata) => {
            var helper = compiler.structPromotionHelper ?? throw new AssertionException("Missing promotion helper.");
            metadata->IncompleteLayout = true;
            Assert.That(helper.CanPromoteStructType((CORINFO_CLASS_STRUCT_*)metadata), Is.False);
            metadata->IncompleteLayout = false;

            Assert.That(Promote(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(metadata->LayoutQueries, Is.EqualTo(2));
            Assert.That(compiler.lvaTable[0].lvPromoted, Is.True);
        });
    }

    [TestCase("option")]
    [TestCase("disabled")]
    [TestCase("varargs")]
    public static void DisabledPhasesLeaveTheHelperAndLocalsUntouched(string gate)
    {
        WithCompiler((compiler, metadata) => {
            compiler.opts.compFlags = gate == "option" ? 0 : CLFLG_STRUCTPROMOTE;
            compiler.fgNoStructPromotion = gate == "disabled";
            compiler.info.compIsVarArgs = gate == "varargs";
            compiler.structPromotionHelper = null;

            Assert.That(Promote(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(3));
            Assert.That(metadata->LayoutQueries, Is.Zero);
        });
    }

    [TestCase(TYP_SIMD16, false)]
    [TestCase(TYP_MASK, false)]
    [TestCase(TYP_STRUCT, true)]
    public static void RegisterStructsAreMarkedEvenAtTheTrackingLimit(var_types type, bool bitcast)
    {
        WithCompiler((compiler, metadata) => {
            MaxLocals(ref JitConfig) = compiler.lvaCount;
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[0].lvIsBitcastToSimd = bitcast;
            compiler.lvaTable[0].lvFieldAccessed = true;

            Assert.That(Promote(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaTable[0].lvRegStruct, Is.True);
            Assert.That(metadata->LayoutQueries, Is.Zero);
        });
    }

    [TestCase(3, 0)]
    [TestCase(4, 1)]
    public static void TrackingLimitIsRecheckedAfterEachOriginalLocal(int limit, int promoted)
    {
        WithCompiler((compiler, metadata) => {
            compiler.lvaTable[2] = compiler.lvaTable[0];
            MaxLocals(ref JitConfig) = limit;

            Assert.That(Promote(compiler), Is.EqualTo(promoted == 0
                ? PhaseStatus.MODIFIED_NOTHING : PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(3 + (2 * promoted)));
            Assert.That(compiler.lvaTable[0].lvPromoted, Is.EqualTo(promoted != 0));
            Assert.That(compiler.lvaTable[2].lvPromoted, Is.False);
            Assert.That(metadata->LayoutQueries, Is.EqualTo(promoted));
        });
    }

    [Test]
    public static void CustomLayoutsRemainUnpromoted()
    {
        WithCompiler((compiler, metadata) => {
            compiler.lvaTable[0].Layout = new ClassLayout(16);
            Assert.That(Promote(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(metadata->LayoutQueries, Is.Zero);
        });
    }

#if DEBUG
    [TestCase(false, false, "")]
    [TestCase(false, true, "")]
    [TestCase(true, false, "\u258C")]
    [TestCase(true, true, "*")]
    public static void OptionalIndentationMatchesNativeNullStack(bool present, bool ascii, string expected)
    {
        WithCompiler((compiler, _) => {
            compiler.asciiTrees = ascii;
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            var previous = s_jitstdout;
            s_jitstdout = writer;
            try
            {
                var indent = present ? new IndentStack(compiler) : default;
                indent.Print();
                writer.Flush();
                Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
            }
            finally
            {
                s_jitstdout = previous;
            }
        });
    }
#endif

    private struct Metadata
    {
        public CorInfoType FieldType;
        public int LayoutQueries;
        public bool IncompleteLayout;
    }

    private delegate void PromotionAction(Compiler compiler, Metadata* metadata);

    private static void WithCompiler(PromotionAction action)
    {
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        MaxLocals(ref JitConfig) = 128;
        try
        {
            FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
                Metadata metadata = new() { FieldType = CORINFO_TYPE_LONG };
                ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
                vtable.Base.Base.isValueClass = &IsValueClass;
                vtable.Base.Base.getClassAttribs = &GetClassAttributes;
                vtable.Base.Base.getClassSize = &GetClassSize;
                vtable.Base.Base.getTypeLayout = &GetTypeLayout;
#if DEBUG
                vtable.Base.Base.runWithSPMIErrorTrap = &InstructionRecordingTestSupport.UnavailableMethodMetadata;
#endif
                ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
                compiler.info.compCompHnd = &jitInfo;
                compiler.opts.compFlags = CLFLG_STRUCTPROMOTE;
                compiler.genReturnLocal = BAD_VAR_NUM;
                compiler.structPromotionHelper = new Compiler.StructPromotionHelper(compiler);
                compiler.lvaTable[0].Type = TYP_STRUCT;
                compiler.lvaTable[0].Layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)&metadata,
                    true, 16, TYP_STRUCT, "Pair", "Pair");
                action(compiler, &metadata);
            });
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsValueClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttributes(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => CorInfoFlag.CORINFO_FLG_VALUECLASS;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 16;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static GetTypeLayoutResult GetTypeLayout(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type,
        CORINFO_TYPE_LAYOUT_NODE* nodes, nint* count)
    {
        var metadata = (Metadata*)type;
        metadata->LayoutQueries++;
        *count = metadata->IncompleteLayout ? 1 : 3;
        nodes[0] = new CORINFO_TYPE_LAYOUT_NODE { size = 16, type = CORINFO_TYPE_VALUECLASS, numFields = 2 };
        nodes[1] = new CORINFO_TYPE_LAYOUT_NODE { parent = 0, offset = 8, size = 8, type = metadata->FieldType };
        nodes[2] = new CORINFO_TYPE_LAYOUT_NODE { parent = 0, offset = 0, size = 8, type = metadata->FieldType };
        return GetTypeLayoutResult.Success;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgPromoteStructs")]
    private static extern PhaseStatus Promote(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryPromoteValueClassAsPrimitive")]
    private static extern var_types TryPromoteValueClassAsPrimitive(Compiler.StructPromotionHelper helper,
        CORINFO_TYPE_LAYOUT_NODE* treeNodes, nint maxTreeNodes, nint index);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMaxLocalsToTrack")]
    private static extern ref int MaxLocals(ref JitConfigValues config);
}
