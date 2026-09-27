// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class MaskConversionTests
{
    [TestCase(1.0, true)]
    [TestCase(20.0, false)]
    [TestCase(30.0, false)]
    public static void WeightedCostsPreserveTiesAndRewriteAllDefinitions(double vectorStoreWeight, bool profitable)
    {
        WithCompiler(compiler => {
            var mask = compiler.gtNewLclvNode(TYP_MASK, 1);
            var maskStore = compiler.gtNewStoreLclVarNode(0, ToVector(mask));
            var first = Append(compiler, maskStore, 10);
            var vector = compiler.gtNewLclvNode(TYP_SIMD16, 3);
            var vectorStore = compiler.gtNewStoreLclVarNode(0, vector);
            _ = Append(compiler, vectorStore, vectorStoreWeight);
            var use = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var useStore = compiler.gtNewStoreLclVarNode(2, ToMask(use));
            var last = Append(compiler, useStore, 10);

            Assert.That(Optimize(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaTable[0].Type, Is.EqualTo(profitable ? TYP_MASK : TYP_SIMD16));
            Assert.That(maskStore.Type, Is.EqualTo(profitable ? TYP_MASK : TYP_SIMD16));
            Assert.That(use.Type, Is.EqualTo(profitable ? TYP_MASK : TYP_SIMD16));

            if (profitable)
            {
                Assert.That(maskStore.Data, Is.SameAs(mask));
                Assert.That(useStore.Data, Is.SameAs(use));
                Assert.That(vectorStore.Data.IsConvertVectorToMask, Is.True);
                var conversion = vectorStore.Data.AsHWIntrinsic();
                Assert.That(conversion.GetOp(1), Is.SameAs(vector));
                Assert.That(conversion.SimdBaseType, Is.EqualTo(TYP_INT));
                Assert.That(conversion.SimdSize, Is.EqualTo(16));
                Assert.That(first.LocalsTreeList.ToArray(), Is.EqualTo((GenTree[])[mask, maskStore]));
                Assert.That(last.LocalsTreeList.ToArray(), Is.EqualTo((GenTree[])[use, useStore]));
            }
            else
            {
                Assert.That(maskStore.Data.IsConvertMaskToVector, Is.True);
                Assert.That(useStore.Data.IsConvertVectorToMask, Is.True);
                Assert.That(vectorStore.Data, Is.SameAs(vector));
            }
        });
    }

    [TestCase("exposed")]
    [TestCase("field")]
    [TestCase("parameter")]
    [TestCase("osr")]
    [TestCase("vector-use")]
    [TestCase("partial-use")]
    [TestCase("address")]
    [TestCase("base-type")]
    [TestCase("size")]
    public static void InvalidLocalsRetainTheirFullVectorRepresentation(string reason)
    {
        WithCompiler(compiler => {
            var store = compiler.gtNewStoreLclVarNode(0, ToVector(compiler.gtNewLclvNode(TYP_MASK, 1)));
            _ = Append(compiler, store, 10);
            GenTree use = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            GenTree converted = ToMask(use, reason == "base-type" ? TYP_FLOAT : TYP_INT, reason == "size" ? (byte)32 : (byte)16);

            switch (reason)
            {
                case "exposed":
                {
                    compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.ESCAPE_ADDRESS);
                    break;
                }

                case "field":
                {
                    compiler.lvaTable[0].lvIsStructField = true;
                    break;
                }

                case "parameter":
                {
                    compiler.lvaTable[0].lvIsParam = true;
                    break;
                }

                case "osr":
                {
                    compiler.lvaTable[0].lvIsOSRLocal = true;
                    break;
                }

                case "vector-use":
                {
                    _ = Append(compiler, compiler.gtNewStoreLclVarNode(3, use), 1);
                    use = compiler.gtNewLclvNode(TYP_SIMD16, 0);
                    converted = ToMask(use);
                    break;
                }

                case "partial-use":
                {
                    use = compiler.gtNewLclFldNode(TYP_SIMD16, 0, 0);
                    converted = ToMask(use);
                    break;
                }

                case "address":
                {
                    _ = Append(compiler, compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0), 1);
                    break;
                }
            }

            var useStore = compiler.gtNewStoreLclVarNode(2, converted);
            _ = Append(compiler, useStore, 10);
            _ = Optimize(compiler);

            Assert.That(compiler.lvaTable[0].Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(store.Data.IsConvertMaskToVector, Is.True);
            Assert.That(useStore.Data, Is.SameAs(converted));
            Assert.That(use.Type, Is.EqualTo(TYP_SIMD16));
        });
    }

    [TestCase("minopts")]
    [TestCase("unused")]
    [TestCase("no-local-conversions")]
#if DEBUG
    [TestCase("disabled")]
#endif
    public static void PhaseGatesDoNotRewriteLocals(string reason)
    {
        WithCompiler(compiler => {
            var store = compiler.gtNewStoreLclVarNode(0, reason == "no-local-conversions"
                ? compiler.gtNewLclvNode(TYP_SIMD16, 3)
                : ToVector(compiler.gtNewLclvNode(TYP_MASK, 1)));
            _ = Append(compiler, store, 1);
            compiler.compMaskConvertUsed = reason != "unused";
#if DEBUG
            Config(ref JitConfig) = reason == "disabled" ? 0 : 1;
#endif
            Assert.That(Optimize(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaTable[0].Type, Is.EqualTo(TYP_SIMD16));
        }, minopts: reason == "minopts");
    }

    private static GenTreeHWIntrinsic ToVector(GenTree operand)
        => new(TYP_SIMD16, NI_AVX512_ConvertMaskToVector, TYP_INT, 16, operand);

    private static GenTreeHWIntrinsic ToMask(GenTree operand, var_types baseType = TYP_INT, byte size = 16)
        => new(TYP_MASK, NI_AVX512_ConvertVectorToMask, baseType, size, operand);

    private static Statement Append(Compiler compiler, GenTree root, double weight)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        block.bbWeight = weight;
        if (compiler.fgLastBB is BasicBlock previous)
        {
            previous.Next = block;
            block.Prev = previous;
        }
        else
        {
            compiler.fgFirstBB = block;
        }
        compiler.fgLastBB = block;
        var statement = compiler.gtNewStmt(root);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.fgSequenceLocals(statement);

        return statement;
    }

    private static void WithCompiler(Action<Compiler> action, bool minopts = false)
    {
        var previousConfig = JitConfig;
#if DEBUG
        Config(ref JitConfig) = 1;
#endif
        try
        {
            FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
                compiler.lvaTable = [
                    new LclVarDsc { Type = TYP_SIMD16 },
                    new LclVarDsc { Type = TYP_MASK, lvIsParam = true },
                    new LclVarDsc { Type = TYP_MASK },
                    new LclVarDsc { Type = TYP_SIMD16, lvIsParam = true },
                ];
                compiler.lvaCount = 4;
                compiler.compMaskConvertUsed = true;
                action(compiler);
            }, minopts);
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgOptimizeMaskConversions")]
    private static extern PhaseStatus Optimize(Compiler compiler);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDoOptimizeMaskConversions")]
    private static extern ref int Config(ref JitConfigValues config);
#endif
}
