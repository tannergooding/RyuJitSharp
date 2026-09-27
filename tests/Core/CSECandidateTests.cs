// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSECandidateTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_Init")]
    private static extern void Initialize(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_Index")]
    private static extern int Index(Compiler compiler, GenTree tree, Statement statement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_Locate")]
    private static extern bool Locate(Compiler compiler, CSE_HeuristicCommon heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optCSEstop")]
    private static extern void Stop(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_InitDataFlow")]
    private static extern void InitializeDataFlow(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_DataFlow")]
    private static extern void RunDataFlow(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_Availability")]
    private static extern void Classify(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEhash")]
    private static extern ref CSEdsc?[] Hash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEtab")]
    private static extern ref CSEdsc?[] Table(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEhashSize")]
    private static extern ref nint HashSize(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEhashMaxCountBeforeResize")]
    private static extern ref nint HashLimit(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitConstCSE")]
    private static extern ref int ConstantMode(ref JitConfigValues config);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CandidateCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "cseLivenessTraits")]
    private static extern ref BitVecTraits? LivenessTraits(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optPrintCSEDataFlowSet")]
    private static extern void PrintDataFlowSet(Compiler compiler, ReadOnlySpan<nint> set, bool includeBits);

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, false, true)]
    public static void DataFlowSetDisplaysAvailabilityAndIgnoresTheVisitedBit(
        bool includeBits, bool multiword, bool empty)
    {
        WithCompiler(compiler => {
            var count = multiword ? 64 : 3;
            CandidateCount(compiler) = count;
            var bitCount = (count * 2) + 1;
            var traits = new BitVecTraits(compiler, bitCount);
            LivenessTraits(compiler) = traits;
            var set = BitVecOps.MakeEmpty(traits);

            if (!empty)
            {
                BitVecOps.AddElemD(traits, set, 0);
                BitVecOps.AddElemD(traits, set, 1);
                BitVecOps.AddElemD(traits, set, (count - 1) * 2);

                if (multiword)
                {
                    BitVecOps.AddElemD(traits, set, 127);
                    BitVecOps.AddElemD(traits, set, 128);
                }
            }

            var before = (nint[])set.Clone();
            var output = CodeGenLifeTransitionTests.Capture(() => PrintDataFlowSet(compiler, set, includeBits));
            var bits = empty ? "0" : multiword ? $"1C{new string('0', 30)}3" : "13";
            var bitsPerWord = IntPtr.Size * 8;
            var wordCount = (bitCount + bitsPerWord - 1) / bitsPerWord;
            var digits = wordCount * IntPtr.Size * 2;
            var names = empty ? "" : multiword ? "CSE #01.c, CSE #64.c" : "CSE #01.c, CSE #03";
            var expected = includeBits ? $"{bits.PadLeft(digits, '0')} {names}" : names;

            Assert.That(output, Is.EqualTo(expected));
            Assert.That(set, Is.EqualTo(before));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CandidateDiagnosticRetainsValueNumberAndSharedConstantKeyForms(bool shared)
    {
        WithCompiler(compiler => {
            ConstantMode(ref Globals.JitConfig) = shared ? Globals.CONST_CSE_ENABLE_ALL : Globals.CONST_CSE_DISABLE_ALL;
            compiler.verbose = true;
            compiler.compCurBB = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.vnStore = new ValueNumStore(compiler);
            var vn = compiler.vnStore.VNForIntCon(0x12345678);
            Initialize(compiler);
            var first = compiler.gtNewIconNode(TYP_INT, 0x12345678);
            var second = compiler.gtNewIconNode(TYP_INT, 0x12345678);
            first._vnPair.SetBoth(vn);
            second._vnPair.SetBoth(vn);
            first.SetCosts(3, 4);
            second.SetCosts(3, 4);
            var firstStatement = compiler.gtNewStmt(first);
            var secondStatement = compiler.gtNewStmt(second);
            Assert.That(Index(compiler, first, firstStatement), Is.Zero);

            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(Index(compiler, second, secondStatement), Is.EqualTo(1)));
            var key = shared ? "K_12340000" : $"${vn:x}";
            var tree = CodeGenLifeTransitionTests.Capture(() => compiler.gtDispTree(second));

            Assert.That(output, Is.EqualTo(
                $"{Environment.NewLine}Candidate CSE #01, key={key} in BB01, [cost= 3, size= 4]: {Environment.NewLine}{tree}"));
            Assert.That(first._cseNum, Is.EqualTo(1));
            Assert.That(second._cseNum, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CandidateLimitDiagnosticDoesNotAssignAnIndex(bool verbose)
    {
        WithCompiler(compiler => {
            compiler.verbose = verbose;
            compiler.compCurBB = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.vnStore = new ValueNumStore(compiler);
            var vn = compiler.vnStore.VNForIntCon(142);
            Initialize(compiler);
            var first = compiler.gtNewIconNode(TYP_INT, 142);
            var second = compiler.gtNewIconNode(TYP_INT, 142);
            first._vnPair.SetBoth(vn);
            second._vnPair.SetBoth(vn);
            first.SetCosts(3, 3);
            second.SetCosts(3, 3);
            Assert.That(Index(compiler, first, compiler.gtNewStmt(first)), Is.Zero);
            CandidateCount(compiler) = 64;
            var statement = compiler.gtNewStmt(second);

            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(Index(compiler, second, statement), Is.Zero));
            var tree = CodeGenLifeTransitionTests.Capture(() => compiler.gtDispTree(second));

            Assert.That(output, Is.EqualTo(
                verbose ? $"Exceeded the MAX_CSE_CNT, not using tree:{Environment.NewLine}{tree}" : ""));
            Assert.That(second._cseNum, Is.Zero);
            Assert.That(CandidateCount(compiler), Is.EqualTo(64));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BetterExceptionCandidateReportsTheReplacedOccurrence(bool verbose)
    {
        WithCompiler(compiler => {
            compiler.verbose = verbose;
            compiler.compCurBB = BasicBlock.New(compiler, BBJ_RETURN);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            Initialize(compiler);
            var normal = store.VNForExpr(null, TYP_INT);
            var exception = store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc,
                store.VNForExpr(null, TYP_BYREF));
            var first = compiler.gtNewIndir(TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 17));
            var second = compiler.gtNewIndir(TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 17));
            first._vnPair.SetBoth(normal);
            second._vnPair.SetBoth(store.VNWithExc(normal, store.VNExcSetSingleton(exception)));
            Assert.That(Index(compiler, first, compiler.gtNewStmt(first)), Is.Zero);
            var statement = compiler.gtNewStmt(second);

            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(Index(compiler, second, statement), Is.Zero));
            var expected = $"Skipping CSE candidate for tree [{first.TreeId:D6}]; " +
                $"tree [{second.TreeId:D6}] is a better candidate with more exceptions{Environment.NewLine}";

            Assert.That(output, Is.EqualTo(verbose ? expected : ""));
            Assert.That(CandidateCount(compiler), Is.Zero);
            Assert.That(Array.Exists(Hash(compiler),
                descriptor => descriptor is not null && ReferenceEquals(descriptor.csdTreeList.tslTree, second)), Is.True);
        });
    }

    internal enum AvailabilityScenario
    {
        Abandoned,
        UnsatisfiedDefinition,
        UnsatisfiedUse,
        AcrossCall,
    }

    [TestCase(AvailabilityScenario.Abandoned,
        " Abandoned - CSE candidate has defs with different exception sets!")]
    [TestCase(AvailabilityScenario.UnsatisfiedDefinition,
        " Abandon - CSE candidate has defs with exception sets that do not satisfy some CSE use")]
    [TestCase(AvailabilityScenario.UnsatisfiedUse,
        " NO_CSE - This use has an exception set item that isn't contained in the defs!")]
    [TestCase(AvailabilityScenario.AcrossCall, " *** Now Live Across Call ***")]
    public static void AvailabilityReportsExceptionRejectionAndNewCallLiveness(
        AvailabilityScenario scenario, string message)
    {
        WithCompiler(compiler => {
            compiler.verbose = true;
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var definitionException = store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc,
                store.VNForExpr(null, TYP_BYREF));
            var requiredException = store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc,
                store.VNForExpr(null, TYP_BYREF));
            var definitionSet = store.VNExcSetSingleton(definitionException);
            var requiredSet = store.VNExcSetSingleton(requiredException);
            var acrossCall = scenario is AvailabilityScenario.AcrossCall;
            var isUse = acrossCall || scenario is AvailabilityScenario.UnsatisfiedUse;
            var tree = compiler.gtNewIndir(TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 17));
            tree._vnPair.SetBoth(store.VNWithExc(store.VNForExpr(null, TYP_INT),
                acrossCall ? ValueNumStore.VNForEmptyExcSet() : requiredSet));
            tree._cseNum = 1;
            var statement = compiler.gtNewStmt(tree);
            compiler.fgInsertStmtAtEnd(block, statement);
            compiler.fgSetStmtSeq(statement);
            var descriptor = new CSEdsc(tree, statement, block)
            {
                csdIndex = 1,
                defExcSetCurrent = definitionSet,
                defExcSetPromise = scenario switch
                {
                    AvailabilityScenario.Abandoned => ValueNumStore.NoVN,
                    AvailabilityScenario.UnsatisfiedDefinition => definitionSet,
                    _ => ValueNumStore.VNForEmptyExcSet(),
                },
            };
            Table(compiler) = [descriptor];
            CandidateCount(compiler) = 1;
            var traits = new BitVecTraits(compiler, 3);
            LivenessTraits(compiler) = traits;
            block.bbCseIn = BitVecOps.MakeEmpty(traits);

            if (isUse)
            {
                BitVecOps.AddElemD(traits, block.bbCseIn, 0);

                if (!acrossCall)
                {
                    BitVecOps.AddElemD(traits, block.bbCseIn, 1);
                }
            }

            var output = CodeGenLifeTransitionTests.Capture(() => Classify(compiler));

            Assert.That(output, Does.Contain(message + Environment.NewLine));
            Assert.That(descriptor.csdLiveAcrossCall, Is.EqualTo(acrossCall));
            Assert.That(descriptor.csdUseCount, Is.EqualTo(acrossCall ? 1 : 0));
            Assert.That(descriptor.csdDefCount, Is.Zero);
            Assert.That(tree._cseNum, Is.EqualTo(acrossCall ? 1 : 0));

            if (scenario is AvailabilityScenario.UnsatisfiedDefinition)
            {
                Assert.That(output, Does.Contain(">>> defExcSetCurrent is "));
                Assert.That(output, Does.Contain(">>> theLiberalExcSet is "));
                Assert.That(output, Does.Contain(">>> the intersectionExcSet is "));
                Assert.That(descriptor.defExcSetCurrent, Is.EqualTo(ValueNumStore.VNForEmptyExcSet()));
                Assert.That(descriptor.defExcSetPromise, Is.EqualTo(ValueNumStore.NoVN));
            }
        });
    }
#endif

    [Test]
    public static void DescriptorCountsDistinctAndRepeatedLocalsWithNativeEightLocalBudget()
    {
        WithCompiler(compiler =>
        {
            var first = compiler.gtNewLclvNode(TYP_INT, 1);
            var repeated = compiler.gtNewLclvNode(TYP_INT, 1);
            var third = compiler.gtNewLclvNode(TYP_INT, 2);
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, first,
                compiler.gtNewBinaryNode(GT_ADD, TYP_INT, repeated, third));
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var descriptor = new CSEdsc(tree, compiler.gtNewStmt(tree), block);

            descriptor.ComputeNumLocals(compiler);

            Assert.Multiple(() =>
            {
                Assert.That(descriptor.numDistinctLocals, Is.EqualTo(2));
                Assert.That(descriptor.numLocalOccurrences, Is.EqualTo(3));
                Assert.That(descriptor.csdTreeLast, Is.SameAs(descriptor.csdTreeList));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DuplicateCandidatesShareHashDescriptorAndRetainOccurrenceOrder(bool shared)
    {
        WithCompiler(compiler =>
        {
            ConstantMode(ref Globals.JitConfig) = shared ? Globals.CONST_CSE_ENABLE_ALL : Globals.CONST_CSE_DISABLE_ALL;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.compCurBB = block;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            Initialize(compiler);

            var vn = store.VNForIntCon(142);
            var first = compiler.gtNewIconNode(TYP_INT, 142);
            var second = compiler.gtNewIconNode(TYP_INT, 142);
            var third = compiler.gtNewIconNode(TYP_INT, 142);
            first._vnPair.SetBoth(vn);
            second._vnPair.SetBoth(vn);
            third._vnPair.SetBoth(vn);
            var firstStmt = compiler.gtNewStmt(first);
            var secondStmt = compiler.gtNewStmt(second);
            var thirdStmt = compiler.gtNewStmt(third);

            Assert.That(Index(compiler, first, firstStmt), Is.Zero);
            Assert.That(Index(compiler, second, secondStmt), Is.EqualTo(1));
            Assert.That(Index(compiler, third, thirdStmt), Is.EqualTo(1));
            Stop(compiler);

            var descriptor = Table(compiler)[0];
            Assert.That(descriptor, Is.Not.Null);
            var bucketDescriptor = Array.Find(Hash(compiler),
                bucket => ReferenceEquals(bucket, descriptor));
            Assert.Multiple(() =>
            {
                Assert.That(HashSize(compiler), Is.EqualTo((nint)128));
                Assert.That(HashLimit(compiler), Is.EqualTo((nint)512));
                Assert.That(descriptor!.csdTreeList.tslTree, Is.SameAs(first));
                Assert.That(descriptor.csdTreeList.tslNext!.tslTree, Is.SameAs(second));
                Assert.That(descriptor.csdTreeLast.tslTree, Is.SameAs(third));
                Assert.That(descriptor.csdTreeLast.tslNext, Is.Null);
                Assert.That(descriptor.csdIsSharedConst, Is.EqualTo(shared));
                Assert.That(bucketDescriptor, Is.SameAs(descriptor));
            });
        });
    }

    [Test]
    public static void CandidateIndexStopsAtNativeSixtyFourCandidateLimit()
    {
        WithCompiler(compiler =>
        {
            compiler.compCurBB = BasicBlock.New(compiler, BBJ_RETURN);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            Initialize(compiler);

            for (var index = 0; index <= 64; index++)
            {
                var value = index + 1000;
                var vn = store.VNForIntCon(value);
                var first = compiler.gtNewIconNode(TYP_INT, value);
                var second = compiler.gtNewIconNode(TYP_INT, value);
                first._vnPair.SetBoth(vn);
                second._vnPair.SetBoth(vn);
                Assert.That(Index(compiler, first, compiler.gtNewStmt(first)), Is.Zero);
                Assert.That(Index(compiler, second, compiler.gtNewStmt(second)),
                    Is.EqualTo(index < 64 ? index + 1 : 0));
            }

            Stop(compiler);
            Assert.That(Table(compiler), Has.Length.EqualTo(64));
        });
    }

    [Test]
    public static void HeuristicRejectsNonCandidatesAndAddressModeNodes()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var heuristic = new CSE_Heuristic(compiler);
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewLclvNode(TYP_INT, 1);
            left.SetCosts(3, 3);
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, right);
            tree._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            tree.SetCosts(3, 3);
            Assert.That(heuristic.ConsiderTree(tree, false), Is.True);

            tree.CanCse = false;
            Assert.That(heuristic.ConsiderTree(tree, false), Is.False);
            tree.CanCse = true;
            tree.Flags |= GenTreeFlags.GTF_ADDRMODE_NO_CSE;
            Assert.That(heuristic.ConsiderTree(tree, false), Is.False);
            Assert.That(heuristic.ConsiderTree(left, false), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IndirectionEligibilityUsesItsAddress(bool arrayElement)
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            GenTree address = arrayElement
                ? new GenTreeArrElem(TYP_BYREF, compiler.gtNewLclvNode(TYP_REF, 0), 4,
                    [compiler.gtNewIconNode(TYP_INT, 0)])
                : compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1234);
            var tree = compiler.gtNewIndir(TYP_INT, address);
            tree._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            tree.SetCosts(5, 4);

            Assert.That(new CSE_Heuristic(compiler).ConsiderTree(tree, false), Is.EqualTo(!arrayElement));
        });
    }

    [Test]
    public static void LocateVisitsEligibleTreesInStatementOrder()
    {
        WithCompiler(compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var vn = store.VNForExpr(null, TYP_INT);
            Initialize(compiler);

            GenTree NewExpression()
            {
                var operand = compiler.gtNewLclvNode(TYP_INT, 0);
                operand.SetCosts(3, 3);
                var expression = compiler.gtNewUnaryNode(GT_NEG, TYP_INT, operand);
                expression._vnPair.SetBoth(vn);
                expression.SetCosts(3, 3);
                return expression;
            }

            var first = NewExpression();
            var second = NewExpression();
            var firstStmt = compiler.gtNewStmt(first);
            var secondStmt = compiler.gtNewStmt(second);
            compiler.fgInsertStmtAtEnd(block, firstStmt);
            compiler.fgInsertStmtAtEnd(block, secondStmt);
            compiler.fgSetStmtSeq(firstStmt);
            compiler.fgSetStmtSeq(secondStmt);

            Assert.That(Locate(compiler, new CSE_Heuristic(compiler)), Is.True);
            var descriptor = Table(compiler)[0]!;
            Assert.Multiple(() =>
            {
                Assert.That(first._cseNum, Is.EqualTo(1));
                Assert.That(second._cseNum, Is.EqualTo(1));
                Assert.That(descriptor.csdTreeList.tslTree, Is.SameAs(first));
                Assert.That(descriptor.csdTreeLast.tslTree, Is.SameAs(second));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AvailabilityLabelsFirstOccurrenceDefAndSecondUse(bool verbose)
    {
        WithCompiler(compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            compiler.compCurBB = block;
            compiler.vnStore = new ValueNumStore(compiler);
            var vn = compiler.vnStore.VNForIntCon(43);
            Initialize(compiler);

            var first = compiler.gtNewIconNode(TYP_INT, 43);
            var second = compiler.gtNewIconNode(TYP_INT, 43);
            first._vnPair.SetBoth(vn);
            second._vnPair.SetBoth(vn);
            first.SetCosts(3, 3);
            second.SetCosts(3, 3);
            var firstStmt = compiler.gtNewStmt(first);
            var secondStmt = compiler.gtNewStmt(second);
            compiler.fgInsertStmtAtEnd(block, firstStmt);
            compiler.fgInsertStmtAtEnd(block, secondStmt);
            compiler.fgSetStmtSeq(firstStmt);
            compiler.fgSetStmtSeq(secondStmt);

            Assert.That(Index(compiler, first, firstStmt), Is.Zero);
            Assert.That(Index(compiler, second, secondStmt), Is.EqualTo(1));
            Stop(compiler);
#if DEBUG
            compiler.verbose = verbose;
            var output = CodeGenLifeTransitionTests.Capture(() => {
#endif
                InitializeDataFlow(compiler);
                RunDataFlow(compiler);
                Classify(compiler);
#if DEBUG
            });

            if (verbose)
            {
                var zeros = new string('0', IntPtr.Size * 2);
                var gen = $"{new string('0', (IntPtr.Size * 2) - 1)}3 CSE #01.c";
                var expected = $"\nBlocks that generate CSE def/uses\nBB01 cseGen = {gen}\n" +
                    "\nPerforming DataFlow for ValnumCSE's\n" +
                    $"\nAfter performing DataFlow for ValnumCSE's\nBB01\n in: {zeros} \ngen: {gen}\nout: {gen}\n\n" +
                    "Labeling the CSEs with Use/Def information\n" +
                    $"BB01 [{first.TreeId:D6}] Def of CSE #01 [weight=1]\n" +
                    $"BB01 [{second.TreeId:D6}] Use of CSE #01 [weight=1]\n";
                Assert.That(output, Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
            }
            else
            {
                Assert.That(output, Is.Empty);
            }
#endif

            var descriptor = Table(compiler)[0]!;
            Assert.Multiple(() =>
            {
                Assert.That(first._cseNum, Is.EqualTo(-1));
                Assert.That(second._cseNum, Is.EqualTo(1));
                Assert.That(descriptor.csdDefCount, Is.EqualTo(1));
                Assert.That(descriptor.csdUseCount, Is.EqualTo(1));
                Assert.That(descriptor.IsViable(), Is.True);
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DataFlowPreservesAvailabilityButKillsCrossCallBit(bool prohibitCseIn)
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var callBlock = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = callBlock;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = callBlock;
            entry.bbRefs = 1;
            callBlock.bbRefs = 0;
            compiler.fgPredsComputed = true;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(callBlock, entry));
            callBlock.SetFlags(BBF_HAS_CALL);
            if (prohibitCseIn)
            {
                callBlock.SetFlags(BBF_NO_CSE_IN);
            }
            compiler.compCurBB = entry;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var vn = store.VNForIntCon(13);
            Initialize(compiler);
            var first = compiler.gtNewIconNode(TYP_INT, 13);
            var second = compiler.gtNewIconNode(TYP_INT, 13);
            first._vnPair.SetBoth(vn);
            second._vnPair.SetBoth(vn);
            Assert.That(Index(compiler, first, compiler.gtNewStmt(first)), Is.Zero);
            Assert.That(Index(compiler, second, compiler.gtNewStmt(second)), Is.EqualTo(1));
            Stop(compiler);

            InitializeDataFlow(compiler);
            RunDataFlow(compiler);

            var traits = new BitVecTraits(compiler, 3);
            Assert.Multiple(() =>
            {
                Assert.That(BitVecOps.IsMember(traits, entry.bbCseOut!, 0), Is.True);
                Assert.That(BitVecOps.IsMember(traits, entry.bbCseOut!, 1), Is.True);
                Assert.That(BitVecOps.IsMember(traits, callBlock.bbCseIn!, 0), Is.EqualTo(!prohibitCseIn));
                Assert.That(BitVecOps.IsMember(traits, callBlock.bbCseOut!, 0), Is.EqualTo(!prohibitCseIn));
                Assert.That(BitVecOps.IsMember(traits, callBlock.bbCseOut!, 1), Is.False);
            });
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = Globals.JitConfig;
        Globals.JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            Globals.JitConfig = previousConfig;
            JitTls.Compiler = previous;
        }
    }
}
