// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanIntervalDiagnosticsTests
{
    [Test]
    public static void EmptyBlocksRetainNativeHeadersSeparatorsAndTrailingLines()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 2);
            var firstHeader = Capture(() => blocks[0].dspBlockHeader());
            var secondHeader = Capture(() => blocks[1].dspBlockHeader());
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                firstHeader + $"====={newline}{newline}" +
                secondHeader + $"====={newline}{newline}{newline}{newline}"));
            Assert.That(CurrentBlockSequenceNumber(allocator), Is.EqualTo(2));
        });
    }

    [Test]
    public static void LocalNumberingLastUseAndOperandSeparatorsMatchNativeDump()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_INT, lvLRACandidate = true },
                new LclVarDsc { Type = TYP_INT },
            ];
            compiler.lvaCount = 2;

            var candidate = new GenTreeLclVar(TYP_INT, 0) { _seqNum = 2 };
            candidate.Flags |= GTF_VAR_DEATH;
            var constant = compiler.gtNewIconNode(TYP_INT, 42);
            constant._seqNum = 4;
            var add = new GenTreeOp(GT_ADD, TYP_INT, candidate, constant) { _seqNum = 6 };
            var store = new GenTreeLclVar(TYP_INT, 1, add) { _seqNum = 8 };
            var memoryLocal = new GenTreeLclVar(TYP_INT, 1) { _seqNum = 10 };
            block.InsertAtEnd(candidate);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(add);
            block.InsertAtEnd(store);
            block.InsertAtEnd(memoryLocal);

            var header = Capture(() => block.dspBlockHeader());
            var constantNameAndValue = Capture(() => DisplayLeaf(compiler, constant));
            var addName = Capture(() => compiler.gtDispNodeName(add));
            var newline = Environment.NewLine;
            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                header + $"====={newline}" +
                $"  N002. {Destination("")}  V00(t{candidate.TreeId}*){newline}" +
                $"  N004. {Destination($"t{constant.TreeId}")}{constantNameAndValue}{newline}" +
                $"  N006. {Destination($"t{add.TreeId}")}{addName}; t{candidate.TreeId}*,t{constant.TreeId}{newline}" +
                $"  N008. {Destination("")}  V01 MEM; t{add.TreeId}{newline}" +
                $"  N010. {Destination($"t{memoryLocal.TreeId}")}  V01 MEM{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedOperandPrintsItsOwnIdOnlyWhenItProducesRegisters(bool childProducesRegister)
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            var child = compiler.gtNewIconNode(TYP_INT, 7);
            child._seqNum = 2;
            if (!childProducesRegister)
            {
                child.IsContained = true;
            }

            var wrapper = new GenTreeUnOp(GT_BITCAST, TYP_INT, child) { IsContained = true, _seqNum = 4 };
            var other = compiler.gtNewIconNode(TYP_INT, 8);
            other._seqNum = 6;
            var add = new GenTreeOp(GT_ADD, TYP_INT, wrapper, other) { _seqNum = 8 };
            block.InsertAtEnd(child);
            block.InsertAtEnd(wrapper);
            block.InsertAtEnd(other);
            block.InsertAtEnd(add);

            var header = Capture(() => block.dspBlockHeader());
            var childText = Capture(() => DisplayLeaf(compiler, child));
            var wrapperName = Capture(() => compiler.gtDispNodeName(wrapper));
            var otherText = Capture(() => DisplayLeaf(compiler, other));
            var addName = Capture(() => compiler.gtDispNodeName(add));
            var childDestination = childProducesRegister ? $"t{child.TreeId}" : "";
            var wrapperDestination = childProducesRegister ? $"t{wrapper.TreeId}" : "";
            var addOperands = childProducesRegister ? $"t{wrapper.TreeId},t{other.TreeId}" : $"t{other.TreeId}";
            var newline = Environment.NewLine;
            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                header + $"====={newline}" +
                $"  N002. {Destination(childDestination)}{childText}{newline}" +
                $"  N004. {Destination(wrapperDestination)}{wrapperName}{(childProducesRegister ? $"; t{child.TreeId}" : "")}{newline}" +
                $"  N006. {Destination($"t{other.TreeId}")}{otherText}{newline}" +
                $"  N008. {Destination($"t{add.TreeId}")}{addName}; {addOperands}{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [Test]
    public static void SequenceNumbersUseTheNativeUnsignedDisplayWidth()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            var constant = compiler.gtNewIconNode(TYP_INT, 7);
            constant._seqNum = -1;
            block.InsertAtEnd(constant);
            var header = Capture(() => block.dspBlockHeader());
            var leaf = Capture(() => DisplayLeaf(compiler, constant));
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                header + $"====={newline}" +
                $"  N4294967295. {Destination($"t{constant.TreeId}")}{leaf}{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [Test]
    public static void ResolutionBlockPrintsItsOriginalEdge()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 2);
            _ = StartBlockSequence(allocator);
            BbNumMaxBeforeResolution(allocator) = (uint)blocks[0].bbNum;
            allocator.getSplitBBNumToTargetBBNumMap()[(uint)blocks[1].bbNum] =
                new LinearScan.SplitEdgeInfo {
                    fromBBNum = (uint)blocks[0].bbNum,
                    toBBNum = (uint)blocks[0].bbNum,
                };
            var firstHeader = Capture(() => blocks[0].dspBlockHeader());
            var secondHeader = Capture(() => blocks[1].dspBlockHeader());
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDumpPre(allocator));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP BEFORE LSRA{newline}" +
                firstHeader + $"====={newline}{newline}" +
                secondHeader + $"====={newline}" +
                $"New block introduced for resolution from {FMT_BB(blocks[0].bbNum)} to {FMT_BB(blocks[0].bbNum)}{newline}" +
                $"{newline}{newline}{newline}"));
            Assert.That(CurrentBlockSequenceNumber(allocator), Is.EqualTo(2));
        });
    }

    [Test]
    public static void ResolutionBlockWithoutSplitMappingFailsRatherThanDroppingItsDiagnostic()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 2);
            _ = StartBlockSequence(allocator);
            BbNumMaxBeforeResolution(allocator) = (uint)blocks[0].bbNum;

            _ = Assert.Throws<FatalJitException>(() => _ = Capture(() => TupleStyleDumpPre(allocator)));
        });
    }

    [Test]
    public static void RefPositionsPreserveIncomingReferencesBlockBoundariesAndNodeOrder()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 2);
            var firstNode = compiler.gtNewIconNode(TYP_INT, 7);
            firstNode._seqNum = 4;
            blocks[0].InsertAtEnd(firstNode);
            var secondNode = compiler.gtNewIconNode(TYP_INT, 9);
            secondNode._seqNum = 8;
            blocks[1].InsertAtEnd(secondNode);

            var local = new Interval(TYP_INT, SRBM_ALLINT_INIT) {
                isLocalVar = true, varNum = 3, intervalIndex = 1,
            };
            var value = new Interval(TYP_INT, SRBM_ALLINT_INIT) {
                intervalIndex = 2,
            };
            var preferred = new Interval(TYP_INT, SRBM_ALLINT_INIT) {
                intervalIndex = 3,
            };
            value.relatedInterval = preferred;

            _ = AddRef(allocator, RefType.RefTypeParamDef, 0, local);
            _ = AddRef(allocator, RefType.RefTypeBB, 2);
            var physicalRegister = allocator.physRegs[(int)regNumber.REG_RAX];
            physicalRegister.init(regNumber.REG_RAX);
            AddRef(allocator, RefType.RefTypeFixedReg, 4).setReg(physicalRegister);
            var use = AddRef(allocator, RefType.RefTypeUse, 4, value);
            use.registerAssignment = SRBM_RAX;
            use.isFixedRegRef = true;
            use.isLocalDefUse = true;
            use.lastUse = true;
            var definition = AddRef(allocator, RefType.RefTypeDef, 5, value);
            definition.registerAssignment = SRBM_RCX;
            definition.isFixedRegRef = true;
            definition.isLocalDefUse = true;
            definition.lastUse = true;
            var kill = AddRef(allocator, RefType.RefTypeKill, 5);
            kill.killedRegisters = RBM_RAX;
            var secondKill = AddRef(allocator, RefType.RefTypeKill, 5);
            secondKill.killedRegisters = RBM_RDX;
            _ = AddRef(allocator, RefType.RefTypeExpUse, 7, local);
            _ = AddRef(allocator, RefType.RefTypeDummyDef, 7, local);
            _ = AddRef(allocator, RefType.RefTypeBB, 7);
            _ = AddRef(allocator, RefType.RefTypeDef, 8, value);

            var firstHeader = Capture(() => blocks[0].dspBlockHeader());
            var secondHeader = Capture(() => blocks[1].dspBlockHeader());
            var firstLeaf = Capture(() => DisplayLeaf(compiler, firstNode));
            var secondLeaf = Capture(() => DisplayLeaf(compiler, secondNode));
            var firstKill = Capture(() => compiler.dumpRegMask(RBM_RAX));
            var lastKill = Capture(() => compiler.dumpRegMask(RBM_RDX));
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDump(allocator, LinearScan.LsraTupleDumpMode.LSRA_DUMP_REFPOS));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP WITH REF POSITIONS{newline}" +
                $"Incoming Parameters:  V03{newline}" +
                firstHeader + $"====={newline}" +
                $"  N004. {Destination("")}{firstLeaf}" +
                $"{newline}                               Use:<I2>(#3) Fixed:rax(#2) LocalDefUse *" +
                $"{newline}        Def:<I2>(#4) rcx LocalDefUse * Pref:<I3>" +
                $"{newline}        Kill: {firstKill} {lastKill} {newline}" +
                $"{newline}" +
                $"  Exposed use of V03 at #7{newline}" +
                $"  Dummy def of V03 at #8{newline}" +
                secondHeader + $"====={newline}" +
                $"  N008. {Destination("")}{secondLeaf}" +
                $"{newline}        Def:<I2>(#10) Pref:<I3>{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [Test]
    public static void RefPositionBoundaryLeavesFollowingBlockForNextSequenceEntry()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 3);
            _ = AddRef(allocator, RefType.RefTypeBB, 0);
            _ = AddRef(allocator, RefType.RefTypeBB, 1);
            _ = AddRef(allocator, RefType.RefTypeBB, 2);
            var newline = Environment.NewLine;
            var headers = new StringBuilder();
            foreach (var block in blocks)
            {
                _ = headers.Append(Capture(() => block.dspBlockHeader()));
                _ = headers.Append("=====").Append(newline).Append(newline);
            }

            var text = Capture(() => TupleStyleDump(allocator, LinearScan.LsraTupleDumpMode.LSRA_DUMP_REFPOS));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP WITH REF POSITIONS{newline}" +
                $"Incoming Parameters: {newline}" +
                headers + $"{newline}{newline}"));
        });
    }

    [TestCase(false, ' ')]
    [TestCase(true, 'S')]
    public static void PostAssignmentRendersRegisterAndSpillPrefix(bool spill, char prefix)
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            var node = compiler.gtNewIconNode(TYP_INT, 7);
            node._seqNum = 2;
            node.RegNum = regNumber.REG_RAX;
            if (spill)
            {
                node.Flags |= GTF_SPILL;
            }
            block.InsertAtEnd(node);
            _ = AddRef(allocator, RefType.RefTypeBB, 0);

            var header = Capture(() => block.dspBlockHeader());
            var leaf = Capture(() => DisplayLeaf(compiler, node));
            var newline = Environment.NewLine;
            var text = Capture(() => TupleStyleDump(allocator, LinearScan.LsraTupleDumpMode.LSRA_DUMP_POST));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP WITH REGISTER ASSIGNMENTS{newline}" +
                $"Incoming Parameters: {newline}" +
                header + $"====={newline}" +
                $"{prefix} N002. {Destination("rax")}{leaf}{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [TestCase(false, '*')]
    [TestCase(true, '$')]
    public static void PostNodeWithoutDestinationDisplaysItsAssignedRegister(bool spill, char prefix)
    {
        WithAllocator((compiler, allocator) => {
            var node = compiler.gtNewIconNode(TYP_INT, 7);
            node._seqNum = 2;
            node.RegNum = regNumber.REG_RAX;
            if (spill)
            {
                node.Flags |= GTF_SPILL;
            }
            var leaf = Capture(() => DisplayLeaf(compiler, node));

            var text = Capture(() => LsraDispNode(allocator, node, LinearScan.LsraTupleDumpMode.LSRA_DUMP_POST, false));

            Assert.That(text, Is.EqualTo($"{prefix} N002. {Destination("rax")}{leaf}"));
        });
    }

    [Test]
    public static void PostIncomingParametersDistinguishAbiRegistersMappingsAndStack()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_INT, lvIsRegArg = true, lvRegister = true, RegNum = regNumber.REG_RBX },
                new LclVarDsc { Type = TYP_INT, lvIsParamRegTarget = true, RegNum = regNumber.REG_STK },
                new LclVarDsc { Type = TYP_INT, RegNum = regNumber.REG_STK },
            ];
            compiler.lvaCount = 3;
            compiler.info.compArgsCount = 1;
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(regNumber.REG_RCX, 0, 8)),
            ];
            compiler._paramRegLocalMappings = [
                new ParameterRegisterLocalMapping(AbiPassingSegment.InRegister(regNumber.REG_RDX, 0, 8), 1, 0),
            ];

            foreach (var (localNumber, assignment) in new[] {
                (0u, SRBM_RBX),
                (1u, SRBM_RAX),
                (2u, SRBM_NONE),
            })
            {
                var interval = new Interval(TYP_INT, SRBM_ALLINT_INIT) {
                    isLocalVar = true, varNum = localNumber,
                };
                var reference = AddRef(allocator, RefType.RefTypeParamDef, 0, interval);
                reference.registerAssignment = assignment;
            }
            _ = AddRef(allocator, RefType.RefTypeBB, 0);

            var header = Capture(() => block.dspBlockHeader());
            var newline = Environment.NewLine;
            var text = Capture(() => TupleStyleDump(allocator, LinearScan.LsraTupleDumpMode.LSRA_DUMP_POST));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP WITH REGISTER ASSIGNMENTS{newline}" +
                $"Incoming Parameters:  V00(rcx=>rbx) V01(rdx=>rax) V02(STK){newline}" +
                header + $"====={newline}{newline}{newline}{newline}"));
        });
    }

    [Test]
    public static void PostVariableMapsKeepPredecessorAndResolutionEdgeAssignments()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 3);
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = 0 }];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedFixed = true;
            compiler.lvaTrackedToVarNum = [0];

            _ = StartBlockSequence(allocator);
            var firstNumber = (uint)blocks[0].bbNum;
            var secondNumber = (uint)blocks[1].bbNum;
            var thirdNumber = (uint)blocks[2].bbNum;
            BbNumMaxBeforeResolution(allocator) = secondNumber;
            allocator.getSplitBBNumToTargetBBNumMap()[thirdNumber] =
                new LinearScan.SplitEdgeInfo { fromBBNum = firstNumber, toBBNum = secondNumber };
            BlockInfo(allocator)[blocks[1].bbNum].predBBNum = firstNumber;
            allocator.initVarRegMaps();
            allocator.setOutVarRegForBB(firstNumber, 0, regNumber.REG_RAX);
            allocator.setInVarRegForBB(secondNumber, 0, regNumber.REG_RBX);
            allocator.setOutVarRegForBB(secondNumber, 0, regNumber.REG_RCX);

            var firstHeader = Capture(() => blocks[0].dspBlockHeader());
            var secondHeader = Capture(() => blocks[1].dspBlockHeader());
            var thirdHeader = Capture(() => blocks[2].dspBlockHeader());
            var newline = Environment.NewLine;
            var text = Capture(() => TupleStyleDump(allocator, LinearScan.LsraTupleDumpMode.LSRA_DUMP_POST));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP WITH REGISTER ASSIGNMENTS{newline}" +
                $"Incoming Parameters: {newline}" +
                firstHeader + $"====={newline}Var=Reg end of {FMT_BB(blocks[0].bbNum)}: V00=rax {newline}{newline}" +
                secondHeader + $"====={newline}" +
                $"Predecessor for variable locations: {FMT_BB(blocks[0].bbNum)}{newline}" +
                $"Var=Reg beg of {FMT_BB(blocks[1].bbNum)}: V00=rbx {newline}" +
                $"Var=Reg end of {FMT_BB(blocks[1].bbNum)}: V00=rcx {newline}{newline}" +
                thirdHeader + $"====={newline}" +
                $"New block introduced for resolution from {FMT_BB(blocks[0].bbNum)} to {FMT_BB(blocks[1].bbNum)}{newline}" +
                $"Var=Reg end of {FMT_BB(blocks[2].bbNum)}: V00=rbx {newline}" +
                $"{newline}{newline}{newline}"));
        }, enregister: true);
    }

    [Test]
    public static void PostCandidateLocalMarksReloadAndPreservesLastUse()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, lvLRACandidate = true, _varIndex = 0 }];
            compiler.lvaCount = 1;
            var node = new GenTreeLclVar(TYP_INT, 0) { _seqNum = 2, RegNum = regNumber.REG_RAX };
            node.Flags |= GTF_VAR_DEATH | GTF_SPILLED;
            block.InsertAtEnd(node);
            var header = Capture(() => block.dspBlockHeader());
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDump(allocator, LinearScan.LsraTupleDumpMode.LSRA_DUMP_POST));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP WITH REGISTER ASSIGNMENTS{newline}" +
                $"Incoming Parameters: {newline}" +
                header + $"====={newline}" +
                $"  N002. {Destination("")}  V00(rax*)R{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [Test]
    public static void RefPositionCandidateLocalUsesItsIntervalIndex()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, lvLRACandidate = true, _varIndex = 0 }];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            allocator.localVarIntervals = [new Interval(TYP_INT, SRBM_ALLINT_INIT) { intervalIndex = 12 }];
            var node = new GenTreeLclVar(TYP_INT, 0) { _seqNum = 2 };
            block.InsertAtEnd(node);
            _ = AddRef(allocator, RefType.RefTypeBB, 0);
            var header = Capture(() => block.dspBlockHeader());
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDump(allocator, LinearScan.LsraTupleDumpMode.LSRA_DUMP_REFPOS));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP WITH REF POSITIONS{newline}" +
                $"Incoming Parameters: {newline}" +
                header + $"====={newline}" +
                $"  N002. {Destination("")}  V00(L12){newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [Test]
    public static void PostMultiRegisterOperandRepeatsLastUseForEachRegister()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            compiler.lvaTable = [new LclVarDsc { Type = TYP_STRUCT, lvFieldCnt = 2 }];
            compiler.lvaCount = 1;
            var node = new GenTreeLclVar(TYP_STRUCT, 0) { _seqNum = 2 };
            node.Flags |= GTF_VAR_MULTIREG | GTF_VAR_DEATH;
            node.SetRegNumByIdx(regNumber.REG_RAX, 0);
            node.SetRegNumByIdx(regNumber.REG_RDX, 1);
            block.InsertAtEnd(node);
            var header = Capture(() => block.dspBlockHeader());
            var newline = Environment.NewLine;

            var text = Capture(() => TupleStyleDump(allocator, LinearScan.LsraTupleDumpMode.LSRA_DUMP_POST));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP WITH REGISTER ASSIGNMENTS{newline}" +
                $"Incoming Parameters: {newline}" +
                header + $"====={newline}" +
                $"  N002. {Destination("rax*,rdx*")}  V00 MEM{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    [Test]
    public static void PostStackValueAndAssignedOperandRetainNativeSeparators()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, 1)[0];
            var stacked = compiler.gtNewIconNode(TYP_INT, 3);
            stacked._seqNum = 2;
            var assigned = compiler.gtNewIconNode(TYP_INT, 5);
            assigned._seqNum = 4;
            assigned.RegNum = regNumber.REG_RAX;
            var add = new GenTreeOp(GT_ADD, TYP_INT, stacked, assigned) {
                _seqNum = 6, RegNum = regNumber.REG_RBX,
            };
            block.InsertAtEnd(stacked);
            block.InsertAtEnd(assigned);
            block.InsertAtEnd(add);

            var header = Capture(() => block.dspBlockHeader());
            var stackedLeaf = Capture(() => DisplayLeaf(compiler, stacked));
            var assignedLeaf = Capture(() => DisplayLeaf(compiler, assigned));
            var addName = Capture(() => compiler.gtDispNodeName(add));
            var newline = Environment.NewLine;
            var text = Capture(() => TupleStyleDump(allocator, LinearScan.LsraTupleDumpMode.LSRA_DUMP_POST));

            Assert.That(text, Is.EqualTo(
                $"TUPLE STYLE DUMP WITH REGISTER ASSIGNMENTS{newline}" +
                $"Incoming Parameters: {newline}" +
                header + $"====={newline}" +
                $"  N002. {Destination("STK")}{stackedLeaf}{newline}" +
                $"  N004. {Destination("rax")}{assignedLeaf}{newline}" +
                $"  N006. {Destination("rbx")}{addName}; STK,rax{newline}" +
                $"{newline}{newline}{newline}"));
        });
    }

    private static RefPosition AddRef(LinearScan allocator, RefType type, uint location, Interval? interval = null)
    {
        var reference = new RefPosition(1, location, null, type) {
            rpNum = (uint)allocator.refPositions.Count,
        };
        if (interval is not null)
        {
            reference.setInterval(interval);
        }
        allocator.refPositions.Add(reference);
        return reference;
    }

    private static string Destination(string operand)
        => operand.Length == 0 ? "                 " : $"{operand,-15} =";

    private static void DisplayLeaf(Compiler compiler, GenTree tree)
    {
        compiler.gtDispNodeName(tree);
        var indentStack = new IndentStack(compiler);
        compiler.gtDispLeaf(tree, ref indentStack);
    }

    private static BasicBlock[] CreateBlocks(Compiler compiler, int count)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index != 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = count;
        compiler.fgBBNumMax = blocks[^1].bbNum;
        compiler.fgPredsComputed = true;
        return blocks;
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            s_jitstdout = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "tupleStyleDumpPre")]
    private static extern void TupleStyleDumpPre(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "tupleStyleDump")]
    private static extern void TupleStyleDump(LinearScan allocator, LinearScan.LsraTupleDumpMode mode);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "lsraDispNode")]
    private static extern void LsraDispNode(LinearScan allocator, GenTree tree, LinearScan.LsraTupleDumpMode mode, bool hasDestination);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "startBlockSequence")]
    private static extern BasicBlock StartBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint BbNumMaxBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockSequenceNumber")]
    private static extern ref int CurrentBlockSequenceNumber(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[] BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool enregister = false)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = enregister ? CLFLG_MINOPT | CLFLG_REGVAR : CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.RegSet.rsClearRegsModified();
        try
        {
            action(compiler, new LinearScan(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
