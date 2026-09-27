// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class TreeCloneOrderTests
{
    [TestCase(false, false, TYP_INT)]
    [TestCase(false, true, TYP_INT)]
    [TestCase(true, false, TYP_INT)]
    [TestCase(true, true, TYP_INT)]
    [TestCase(false, false, TYP_BYREF)]
    [TestCase(false, true, TYP_BYREF)]
    [TestCase(true, false, TYP_BYREF)]
    [TestCase(true, true, TYP_BYREF)]
    public static void IntegerClonesPreserveRuntimeMetadataAndNativeTargetCookiePolicy(
        bool expression, bool handle, var_types type)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var original = new GenTreeIntCon(type, 123) {
                CompileTimeHandle = 456,
            };
            if (handle)
            {
                original.Flags |= Globals.GTF_ICON_STATIC_HDL;
            }
#if DEBUG
            original.TargetHandle = 789;
#endif

            var copy = (expression ? compiler.gtCloneExpr(original) : compiler.gtClone(original, complexOK: true))
                as GenTreeIntCon ?? throw new AssertionException("Integer cloning must produce an integer node.");

            Assert.That(copy, Is.Not.SameAs(original));
            Assert.That(copy.Type, Is.EqualTo(type));
            Assert.That(copy.IconVal, Is.EqualTo(original.IconVal));
            Assert.That(copy.CompileTimeHandle, Is.EqualTo(original.CompileTimeHandle));
            Assert.That(copy.IconHandleFlag, Is.EqualTo(original.IconHandleFlag));
#if DEBUG
            var expectedCookie = expression ? original.TargetHandle : 0;
#if LATE_DISASM
            if (handle)
            {
                expectedCookie = 0;
            }
#endif
            Assert.That(copy.TargetHandle, Is.EqualTo(expectedCookie));
            Assert.That(original.TargetHandle, Is.EqualTo((nint)789));
#endif
        });
    }

    [TestCase("store-indirect")]
    [TestCase("store-local")]
    [TestCase("field-instance")]
    [TestCase("field-static")]
    [TestCase("conditional")]
    [TestCase("bounds-check")]
    [TestCase("array-length")]
    [TestCase("address-base")]
    [TestCase("address-index")]
    [TestCase("condition-code")]
    [TestCase("hardware-intrinsic")]
    public static void SimpleAndHardwareNodesAreClonedBeforeTheirOperands(string shape)
    {
        SsaLivenessTests.WithCompiler(4, compiler => {
            var original = CreateTree(compiler, shape);
            original.Flags |= GTF_DONT_CSE;
            original._vnPair = new ValueNumPair(123, 456);
            original.SetCosts(7, 11);
            var originals = PreOrder(compiler, original);
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif

            var copy = compiler.gtCloneExpr(original);
            var copies = PreOrder(compiler, copy);

            Assert.That(copies, Has.Count.EqualTo(originals.Count));
            Assert.That(copy.Flags, Is.EqualTo(original.Flags));
            Assert.That(copy._vnPair, Is.EqualTo(original._vnPair));
            Assert.That(copy.CostEx, Is.EqualTo(7));
            Assert.That(copy.CostSz, Is.EqualTo(11));

            for (var i = 0; i < copies.Count; i++)
            {
                Assert.That(copies[i], Is.Not.SameAs(originals[i]));
                Assert.That(copies[i].Oper, Is.EqualTo(originals[i].Oper));
                Assert.That(copies[i].Type, Is.EqualTo(originals[i].Type));
#if DEBUG
                Assert.That(copies[i].TreeId, Is.EqualTo(nextId + i));
#endif
            }
#if DEBUG
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId + originals.Count));
#endif
            if (original is GenTreeHWIntrinsic hardware)
            {
                var hardwareCopy = copy.AsHWIntrinsic();
                Assert.That(hardwareCopy.HWIntrinsicId, Is.EqualTo(hardware.HWIntrinsicId));
                Assert.That(hardwareCopy.SimdBaseType, Is.EqualTo(hardware.SimdBaseType));
                Assert.That(hardwareCopy.SimdSize, Is.EqualTo(hardware.SimdSize));
                var first = hardware.GetOp(1);
                hardwareCopy.SetOp(1, compiler.gtNewIconNode(TYP_INT, 99));
                Assert.That(hardware.GetOp(1), Is.SameAs(first));
            }
            else if (original is GenTreeQmark conditional)
            {
                Assert.That(copy.AsQmark().ThenNodeLikelihood, Is.EqualTo(conditional.ThenNodeLikelihood));
            }
            else if (original is GenTreeOpCC conditionCode)
            {
                Assert.That(copy.AsOpCC().Condition, Is.EqualTo(conditionCode.Condition));
            }
        });
    }

    [TestCase(GT_CMPXCHG)]
    [TestCase(GT_SELECT)]
    public static void SpecialConstructorArgumentsAreStillClonedBeforeTheirParent(genTreeOps oper)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            GenTree original = oper == GT_CMPXCHG
                ? new GenTreeCmpXchg(TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8),
                    compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2))
                : new GenTreeConditional(GT_SELECT, TYP_INT, compiler.gtNewIconNode(TYP_INT, 1),
                    compiler.gtNewIconNode(TYP_INT, 2), compiler.gtNewIconNode(TYP_INT, 3));
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif

            var copy = compiler.gtCloneExpr(original);
            var originals = PreOrder(compiler, original);
            var copies = PreOrder(compiler, copy);

            Assert.That(copies, Has.Count.EqualTo(4));
            for (var i = 0; i < copies.Count; i++)
            {
                Assert.That(copies[i], Is.Not.SameAs(originals[i]));
#if DEBUG
                Assert.That(copies[i].TreeId, Is.EqualTo(nextId + (i == 0 ? 3 : i - 1)));
#endif
            }
        });
    }

    [Test]
    public static void ArrayElementClonesIndicesBeforeObjectAndParent()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            var original = new GenTreeArrElem(TYP_BYREF, array, 4, [first, second]);
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif

            var copy = compiler.gtCloneExpr(original).AsArrElem();

            Assert.That(copy.ArrObj, Is.Not.SameAs(array));
            Assert.That(copy.ArrInds[0], Is.Not.SameAs(first));
            Assert.That(copy.ArrInds[1], Is.Not.SameAs(second));
            Assert.That(copy.ArrRank, Is.EqualTo(original.ArrRank));
            Assert.That(copy.ArrElemSize, Is.EqualTo(original.ArrElemSize));
#if DEBUG
            Assert.That(copy.ArrInds[0].TreeId, Is.EqualTo(nextId));
            Assert.That(copy.ArrInds[1].TreeId, Is.EqualTo(nextId + 1));
            Assert.That(copy.ArrObj.TreeId, Is.EqualTo(nextId + 2));
            Assert.That(copy.TreeId, Is.EqualTo(nextId + 3));
#endif
        });
    }

    private static GenTree CreateTree(Compiler compiler, string shape)
    {
        switch (shape)
        {
            case "store-indirect":
            {
                var address = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8);
                var load = new GenTreeIndir(GT_IND, TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8));
                var add = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, load, compiler.gtNewIconNode(TYP_INT, 1));
                return new GenTreeStoreInd(TYP_INT, address, add);
            }

            case "store-local":
            {
                return new GenTreeLclVar(TYP_INT, 0,
                    compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                        compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2)));
            }

            case "field-instance":
            case "field-static":
            {
                return new GenTreeFieldAddr(TYP_BYREF,
                    shape == "field-instance" ? compiler.gtNewLclvNode(TYP_REF, 0) : null, null, 16);
            }

            case "conditional":
            {
                var colon = compiler.gtNewColonNode(TYP_INT,
                    compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
                return new GenTreeQmark(TYP_INT, compiler.gtNewIconNode(TYP_INT, 1), colon, 25);
            }

            case "bounds-check":
            {
                return new GenTreeBoundsChk(compiler.gtNewIconNode(TYP_INT, 1),
                    compiler.gtNewIconNode(TYP_INT, 2), SpecialCodeKind.SCK_RNGCHK_FAIL);
            }

            case "array-length":
            {
                return compiler.gtNewArrLen(TYP_INT, compiler.gtNewLclvNode(TYP_REF, 0), 8);
            }

            case "address-base":
            case "address-index":
            {
                var address = compiler.gtNewLclvNode(Globals.TYP_I_IMPL, 0);
                return new GenTreeAddrMode(Globals.TYP_I_IMPL,
                    shape == "address-base" ? address : null,
                    shape == "address-index" ? address : null, 1, 8);
            }

            case "condition-code":
            {
                return new GenTreeOpCC(GT_SELECTCC, TYP_INT, new GenCondition(GenCondition.EQ),
                    compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
            }

            case "hardware-intrinsic":
            {
                return new GenTreeHWIntrinsic(TYP_SIMD16, NamedIntrinsic.NI_Vector_Create, TYP_INT, 16,
                    compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2),
                    compiler.gtNewIconNode(TYP_INT, 3), compiler.gtNewIconNode(TYP_INT, 4));
            }

            default:
            {
                throw new AssertionException("Unknown clone shape.");
            }
        }
    }

    private static List<GenTree> PreOrder(Compiler compiler, GenTree root)
    {
        List<GenTree> nodes = [];
        _ = compiler.gtComplexityExceeds(root, uint.MaxValue, node => {
            nodes.Add(node);
            return 1;
        });

        return nodes;
    }
}
