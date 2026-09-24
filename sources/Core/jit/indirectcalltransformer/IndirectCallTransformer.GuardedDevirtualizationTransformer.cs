// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class IndirectCallTransformer
{
    public sealed unsafe class GuardedDevirtualizationTransformer : Transformer
    {
        private int _returnTemp = BAD_VAR_NUM;
        private Statement? _lastStmt;
        private bool _checkFallsThrough;
        private bool _returnValueUnused;

        public GuardedDevirtualizationTransformer(Compiler compiler, BasicBlock block, Statement stmt)
            : base(compiler, block, stmt, stmt.RootNode.AsCall())
        {
        }

        public override void Run()
        {
            _origCall = GetCall(_stmt);
#if DEBUG
            JITDUMP($"\n----------------\n\n*** {Name} contemplating [{_origCall.TreeId:D6}] in {FMT_BB(_currBlock.bbNum)} \n");
#endif
            _likelihood = _origCall.GetGdvCandidateInfo(0).likelihood;
            assert((_likelihood >= 0) && (_likelihood <= 100));
            JITDUMP($"Likelihood of correct guess is {_likelihood}\n");

            // Native only chains single, non-exact guesses.
            var canChainGdv = (GetChecksCount() == 1) && ((_origCall._callMoreFlags & GTF_CALL_M_GUARDED_DEVIRT_EXACT) == 0);
            if (canChainGdv)
            {
                _compiler.Metrics.GDV++;
                if (GetChecksCount() > 1)
                {
                    _compiler.Metrics.MultiGuessGDV++;
                }

                var isChainedGdv = (_origCall._callMoreFlags & GTF_CALL_M_GUARDED_DEVIRT_CHAIN) != 0;
                if (isChainedGdv)
                {
                    JITDUMP("Expansion will chain to the previous GDV\n");
                }

                Transform();
                if (isChainedGdv)
                {
                    _compiler.Metrics.ChainedGDV++;
                    TransformForChainedGdv();
                }

                ScoutForChainedGdv();
            }
            else
            {
                JITDUMP("Expansion will not chain to the previous GDV due to multiple type checks\n");
                Transform();
            }
        }

        protected override string Name => "GuardedDevirtualization";

        protected override GenTreeCall GetCall(Statement callStmt)
        {
            assert(callStmt.RootNode.Oper.IsCall);
            return callStmt.RootNode.AsCall();
        }

        protected override void ClearFlag()
        {
            // CreateElse removes GDV information after all candidates are used.
        }

        protected override byte GetChecksCount() => _origCall.InlineCandidatesCount;

        protected override void ChainFlow()
        {
            assert(_compiler.fgPredsComputed);
            // Edges are established while creating each candidate's blocks.
        }

        protected override void SetWeights()
        {
            assert(_remainderBlock is not null);
            _remainderBlock.inheritWeight(_currBlock);
        }

        protected override void CreateCheck(byte checkIdx)
        {
            if (checkIdx == 0)
            {
                _checkBlock = _currBlock;
                _checkFallsThrough = false;
            }
            else
            {
                assert(_checkBlock is not null);
                assert(_thenBlock is not null);
                var prevCheckBlock = _checkBlock;
                _checkBlock = CreateAndInsertBasicBlock(BBJ_ALWAYS, _thenBlock, _currBlock);
                _checkFallsThrough = false;
                assert(prevCheckBlock.Kind is BBJ_ALWAYS);
                assert(prevCheckBlock.JumpsToNext);
                var prevCheckThenEdge = prevCheckBlock.TargetEdge;
                var checkLikelihood = Math.Max(0.0, 1.0 - prevCheckThenEdge.Likelihood);
                JITDUMP($"Level {checkIdx} Check block {FMT_BB(_checkBlock.bbNum)} success likelihood {FMT_WT(checkLikelihood)}\n");
                var prevCheckCheckEdge = _compiler.fgAddRefPred(_checkBlock, prevCheckBlock);
                prevCheckCheckEdge.Likelihood = checkLikelihood;
                _checkBlock.inheritWeight(prevCheckBlock);
                _checkBlock.scaleBBWeight(checkLikelihood);
                prevCheckBlock.SetCond(prevCheckCheckEdge, prevCheckThenEdge);
            }

            var thisArg = _origCall.Args.ThisArg;
            assert(thisArg is not null);
            SplitCall(_checkBlock, ref thisArg.EarlyNodeRef);
            var thisTree = _compiler.gtCloneExpr(thisArg.Node);

            // Chaining must also copy any receiver spill: the cold path bypasses
            // the check block and otherwise would miss that definition.
            _lastStmt = _checkBlock.LastStmt;
            var isLastCheck = checkIdx == _origCall.InlineCandidatesCount - 1;
            if (isLastCheck && ((_origCall._callMoreFlags & GTF_CALL_M_GUARDED_DEVIRT_EXACT) != 0))
            {
                assert(_checkBlock.Kind is BBJ_ALWAYS);
                _checkFallsThrough = true;
                return;
            }

            var guardedInfo = _origCall.GetGdvCandidateInfo(checkIdx);
            GenTree compare;
            if (guardedInfo.guardedClassHandle != NO_CLASS_HANDLE)
            {
                var methodTable = _compiler.gtNewMethodTableLookup(thisTree);
                var targetMethodTable = _compiler.gtNewIconEmbClsHndNode(guardedInfo.guardedClassHandle);
                compare = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, targetMethodTable, methodTable);
                _compiler.Metrics.ClassGDV++;
            }
            else
            {
                assert(_origCall.IsVirtualVtable || _origCall.IsDelegateInvoke);
                if (_origCall.IsVirtualVtable)
                {
                    var tarTree = _compiler.fgExpandVirtualVtableCallTarget(_origCall);
                    var methHnd = guardedInfo.guardedMethodHandle;
                    CORINFO_CONST_LOOKUP lookup;
                    _compiler.info.compCompHnd->getFunctionEntryPoint(methHnd, &lookup);
                    var compareTarTree = CreateTreeForLookup(methHnd, lookup);
                    assert(compareTarTree is not null);
                    compare = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, compareTarTree, tarTree);
                }
                else
                {
                    var offset = _compiler.gtNewIconNode(TYP_I_IMPL, _compiler.eeGetEEInfo().offsetOfDelegateFirstTarget);
                    GenTree tarTree = _compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, thisTree, offset);
                    tarTree = _compiler.gtNewIndir(TYP_I_IMPL, tarTree, GTF_IND_INVARIANT);
                    var methHnd = guardedInfo.guardedMethodHandle;
                    CORINFO_CONST_LOOKUP lookup;
                    _compiler.info.compCompHnd->getFunctionFixedEntryPoint(methHnd, false, &lookup);
                    var compareTarTree = CreateTreeForLookup(methHnd, lookup);
                    assert(compareTarTree is not null);
                    compare = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, compareTarTree, tarTree);
                }

                _compiler.Metrics.MethodGDV++;
            }

            var jmpTree = _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, compare);
            var jmpStmt = _compiler.fgNewStmtFromTree(jmpTree, di: _stmt.DebugInfo);
            _compiler.fgInsertStmtAtEnd(_checkBlock, jmpStmt);
        }

        protected override void FixupRetExpr()
        {
            var inlineInfo = _origCall.GetGdvCandidateInfo(0);
            var retExpr = inlineInfo.retExpr;
            if (retExpr is null)
            {
                return;
            }

            var noReturnValue = _origCall.Type is TYP_VOID;
            if (!noReturnValue)
            {
                var nextStmt = _stmt.NextStmt;
                if (nextStmt is not null)
                {
                    var fld = _compiler.gtFindLink(nextStmt, retExpr);
                    var parent = fld.parent;
                    if ((parent is not null) && (parent.Oper is GT_COMMA) && (parent.AsOp().Op1 == retExpr))
                    {
                        _returnValueUnused = true;
#if DEBUG
                        JITDUMP($"GT_RET_EXPR [{retExpr.TreeId:D6}] value is unused\n");
#endif
                    }
                }
            }

            if (noReturnValue)
            {
#if DEBUG
                JITDUMP($"Linking GT_RET_EXPR [{retExpr.TreeId:D6}] for VOID return to NOP\n");
#endif
                retExpr.SubstExpr = _compiler.gtNewNothingNode();
            }
            else if (_returnValueUnused)
            {
#if DEBUG
                JITDUMP($"Linking GT_RET_EXPR [{retExpr.TreeId:D6}] for UNUSED return to NOP\n");
#endif
                retExpr.SubstExpr = _compiler.gtNewNothingNode();
            }
            else
            {
                // Candidate zero need not carry the importer's spill temp.
                _returnTemp = BAD_VAR_NUM;
                for (byte i = 0; i < _origCall.InlineCandidatesCount; i++)
                {
                    var spillTemp = _origCall.GetGdvCandidateInfo(i).preexistingSpillTemp;
                    if (spillTemp != BAD_VAR_NUM)
                    {
                        assert((_returnTemp == BAD_VAR_NUM) || (_returnTemp == spillTemp));
                        _returnTemp = spillTemp;
                    }
                }

                if (_returnTemp != BAD_VAR_NUM)
                {
                    JITDUMP($"Reworking call(s) to return value via a existing return temp V{_returnTemp:D2}\n");
                    // Multiple paths define the temp, so a specialized hot-path
                    // return type must not refine the fallback's declared type.
                    ref var returnTempLcl = ref _compiler.impInlineRoot.lvaGetDesc(_returnTemp);
                    if (returnTempLcl.lvSingleDef)
                    {
                        JITDUMP($"Return temp V{_returnTemp:D2} is no longer a single def temp\n");
                        returnTempLcl.lvSingleDef = false;
                    }
                }
                else
                {
                    _returnTemp = _compiler.lvaGrabTemp(false, "guarded devirt return temp");
                    JITDUMP($"Reworking call(s) to return value via a new temp V{_returnTemp:D2}\n");
                    if (varTypeIsSmall(_origCall._returnType))
                    {
                        assert(_origCall.NormalizesSmallTypesOnReturn);
                        _compiler.lvaGetDesc(_returnTemp).Type = _origCall._returnType;
                    }
                }

                if (varTypeIsStruct(_origCall.Type))
                {
                    _compiler.lvaSetStruct(_returnTemp, _origCall._retClsHnd, false);
                }

                var tempTree = _compiler.gtNewLclvNode(_origCall.Type, _returnTemp);
#if DEBUG
                JITDUMP($"Linking GT_RET_EXPR [{retExpr.TreeId:D6}] to refer to temp V{_returnTemp:D2}\n");
#endif
                retExpr.SubstExpr = tempTree;
            }
        }

        private void DevirtualizeCall(BasicBlock block, byte candidateId)
        {
            var inlineInfo = _origCall.GetGdvCandidateInfo(candidateId);
            var clsHnd = inlineInfo.guardedClassHandle;
            var thisTemp = _compiler.lvaGrabTemp(false, "guarded devirt this exact temp");
            assert(_origCall.Args.ThisArg is not null);
            var clonedObj = _compiler.gtCloneExpr(_origCall.Args.ThisArg.Node);
            GenTree newThisObj;
            if (_origCall.IsDelegateInvoke)
            {
                var offset = _compiler.gtNewIconNode(TYP_I_IMPL, _compiler.eeGetEEInfo().offsetOfDelegateInstance);
                newThisObj = _compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, clonedObj, offset);
                newThisObj = _compiler.gtNewIndir(TYP_REF, newThisObj);
            }
            else
            {
                newThisObj = clonedObj;
            }

            var store = _compiler.gtNewTempStore(thisTemp, newThisObj);
            if (clsHnd != NO_CLASS_HANDLE)
            {
                _compiler.lvaSetClass(thisTemp, clsHnd, true);
            }
            else
            {
                _compiler.lvaSetClass(thisTemp, _compiler.info.compCompHnd->getMethodClass(inlineInfo.guardedMethodHandle));
            }

            _compiler.fgInsertStmtAtEnd(block, _compiler.gtNewStmt(store));
            var call = _compiler.gtCloneCandidateCall(_origCall);
            assert(call.Args.ThisArg is not null);
            call.Args.ThisArg.EarlyNode = _compiler.gtNewLclvNode(TYP_REF, thisTemp);
#if DEBUG
            call.IsGuarded = true;
            JITDUMP($"Direct call [{call.TreeId:D6}] in block {FMT_BB(block.bbNum)}\n");
#endif

            var methodHnd = inlineInfo.guardedMethodHandle;
            var thisObj = clsHnd != NO_CLASS_HANDLE ? call.Args.ThisArg.EarlyNode : newThisObj;
            _ = _compiler.gtGetClassHandle(thisObj, out var objClassIsExact, out var objIsNonNull);
            var derivedMethodAttribs = _compiler.info.compCompHnd->getMethodAttribs(methodHnd);
            var exactContext = inlineInfo.exactContextHandle;
            CORINFO_SIG_INFO derivedSig;
            _compiler.info.compCompHnd->getMethodSig(methodHnd, &derivedSig);
            var dcInfo = new Compiler.DevirtualizedCallInfo {
                tokenLookupContext = exactContext,
                methSig = ref derivedSig,
                objIsNonNull = objIsNonNull,
                hadImplicitNullCheck = _origCall.IsVirtual,
                isDelegateCall = _origCall.IsDelegateInvoke,
                isExplicitTailCall = (call._callMoreFlags & GTF_CALL_M_EXPLICIT_TAILCALL) != 0,
                objClassIsExact = (clsHnd != NO_CLASS_HANDLE) && objClassIsExact,
                objClassIsFinal = false,
                ilOffset = inlineInfo.ilOffset,
                instParamLookup = ref inlineInfo.guardedMethodInstParamLookup,
            };

            if (clsHnd != NO_CLASS_HANDLE)
            {
                dcInfo.resolvedToken = ref inlineInfo.guardedMethodResolvedToken;
                dcInfo.unboxedResolvedToken = ref inlineInfo.guardedMethodUnboxedResolvedToken;
            }

            _compiler.impTransformDevirtualizedCall(call, ref methodHnd, ref derivedMethodAttribs, in dcInfo, block,
                out _, inlineInfo.originalMethodHandle);
            assert(!call.IsVirtual && !call.IsDelegateInvoke);
            var unboxedMethodHnd = inlineInfo.guardedMethodUnboxedResolvedToken.hMethod;
            var unboxedEntryMismatch = (unboxedMethodHnd is not null) && (methodHnd != unboxedMethodHnd);

            if (!inlineInfo.isInlineable || unboxedEntryMismatch)
            {
                if (unboxedEntryMismatch)
                {
                    JITDUMP("Devirtualization was unable to use the unboxed entry; so marking call (to boxed entry) as not inlineable\n");
                }
                else
                {
                    JITDUMP("Target of this GDV candidate is not inlineable; leaving the devirtualized call as a plain direct call\n");
                    _compiler.Metrics.NoInlineGDV++;
                }

                call.Flags &= ~GTF_CALL_INLINE_CANDIDATE;
                call.ClearInlineInfo();
                if (_returnTemp != BAD_VAR_NUM)
                {
                    var returnStore = _compiler.gtNewTempStore(_returnTemp, call);
                    _compiler.fgInsertStmtAtEnd(block, _compiler.gtNewStmt(returnStore));
                }
                else
                {
                    _compiler.fgInsertStmtAtEnd(block, _compiler.gtNewStmt(call, _stmt.DebugInfo));
                }
            }
            else
            {
                // Only an inlineable candidate should consume the enumerator map.
                if (_compiler.hasImpEnumeratorGdvLocalMap)
                {
                    var map = _compiler.ImpEnumeratorGdvLocalMap;
                    if (map.TryGetValue(_origCall, out var enumeratorLcl))
                    {
#if DEBUG
                        JITDUMP($"Flagging [{call.TreeId:D6}] for enumerator cloning via V{enumeratorLcl:D2}\n");
#endif
                        _ = map.Remove(_origCall);
                        map[call] = enumeratorLcl;
                    }
                }

                _compiler.fgInsertStmtAtEnd(block, _compiler.gtNewStmt(call, _stmt.DebugInfo));
                var oldRetExpr = inlineInfo.retExpr;
                inlineInfo.clsHandle = _compiler.info.compCompHnd->getMethodClass(methodHnd);
                inlineInfo.exactContextHandle = exactContext;
                inlineInfo.preexistingSpillTemp = _returnTemp;
                call.SingleInlineCandidateInfo = inlineInfo;

                if (oldRetExpr is not null)
                {
                    inlineInfo.retExpr = _compiler.gtNewInlineCandidateReturnExpr(call, call.Type);
                    GenTree newRetExpr = inlineInfo.retExpr;
                    if (_returnTemp != BAD_VAR_NUM)
                    {
                        newRetExpr = _compiler.gtNewTempStore(_returnTemp, newRetExpr);
                    }
                    else
                    {
                        assert((_origCall.Type is TYP_VOID) || _returnValueUnused);
                        newRetExpr = _compiler.gtUnusedValNode(newRetExpr);
                    }

                    _compiler.fgInsertStmtAtEnd(block, _compiler.gtNewStmt(newRetExpr));
                }
            }
        }

        protected override void CreateThen(byte checkIdx)
        {
            assert(_checkBlock is not null);
            assert(_remainderBlock is not null);
            var thenLikelihood = _origCall.GetGdvCandidateInfo(checkIdx).likelihood;
            var baseLikelihood = 0;
            for (byte i = 0; i < checkIdx; i++)
            {
                baseLikelihood += _origCall.GetGdvCandidateInfo(i).likelihood;
            }

            assert(baseLikelihood < 100);
            baseLikelihood = 100 - baseLikelihood;
            // Conditional likelihood divides by the mass left after earlier
            // failed guesses: e.g. a 30% second guess after 50% becomes 0.6.
            var adjustedThenLikelihood = Math.Min((double)thenLikelihood / baseLikelihood, 100.0);
            JITDUMP($"For check in {FMT_BB(_checkBlock.bbNum)}: orig likelihood {FMT_WT(thenLikelihood / 100.0)}, base likelihood {FMT_WT(baseLikelihood / 100.0)}, adjusted likelihood {FMT_WT(adjustedThenLikelihood)}\n");

            _thenBlock = CreateAndInsertBasicBlock(BBJ_ALWAYS, _checkBlock, _currBlock);
            _thenBlock.inheritWeight(_checkBlock);
            _thenBlock.scaleBBWeight(adjustedThenLikelihood);
            var thenRemainderEdge = _compiler.fgAddRefPred(_remainderBlock, _thenBlock);
            _thenBlock.TargetEdge = thenRemainderEdge;

            assert(_checkBlock.Kind is BBJ_ALWAYS);
            var checkThenEdge = _compiler.fgAddRefPred(_thenBlock, _checkBlock);
            _checkBlock.TargetEdge = checkThenEdge;
            assert(_checkBlock.JumpsToNext);
            // This edge becomes one arm of a conditional unless the last exact
            // check falls through, in which case its likelihood must remain 1.
            checkThenEdge.Likelihood = adjustedThenLikelihood;
            DevirtualizeCall(_thenBlock, checkIdx);
        }

        protected override void CreateElse()
        {
            assert(_thenBlock is not null);
            assert(_checkBlock is not null);
            assert(_remainderBlock is not null);
            _elseBlock = CreateAndInsertBasicBlock(BBJ_ALWAYS, _thenBlock, _currBlock);
            assert(_checkBlock.Kind is BBJ_ALWAYS);
            var checkThenEdge = _checkBlock.TargetEdge;
            var elseLikelihood = Math.Max(0.0, 1.0 - checkThenEdge.Likelihood);
            if (!_checkFallsThrough)
            {
                assert(_checkBlock.JumpsToNext);
                var checkElseEdge = _compiler.fgAddRefPred(_elseBlock, _checkBlock);
                checkElseEdge.Likelihood = elseLikelihood;
                _checkBlock.SetCond(checkElseEdge, checkThenEdge);
            }
            else
            {
                // Keep native's unreachable fallback for later dead-block removal.
                assert((_origCall._callMoreFlags & GTF_CALL_M_GUARDED_DEVIRT_EXACT) != 0);
                assert(checkThenEdge.Likelihood == 1.0);
            }

            var elseRemainderEdge = _compiler.fgAddRefPred(_remainderBlock, _elseBlock);
            _elseBlock.TargetEdge = elseRemainderEdge;
            _origCall.ClearInlineInfo();
            _elseBlock.inheritWeight(_checkBlock);
            _elseBlock.scaleBBWeight(elseLikelihood);
            var call = _origCall;
            var newStmt = _compiler.gtNewStmt(call, _stmt.DebugInfo);
            call.Flags &= ~GTF_CALL_INLINE_CANDIDATE;
#if DEBUG
            call.IsGuarded = true;
            JITDUMP($"Residual call [{call.TreeId:D6}] moved to block {FMT_BB(_elseBlock.bbNum)}\n");
#endif
            if (_returnTemp != BAD_VAR_NUM)
            {
                newStmt.RootNode = _compiler.gtNewTempStore(_returnTemp, call);
            }

            _compiler.fgInsertStmtAtEnd(_elseBlock, newStmt);
            _stmt.RootNode = _compiler.gtNewNothingNode();
        }

        private void TransformForChainedGdv()
        {
            assert(_checkBlock is not null);
            assert(_elseBlock is not null);
            assert(_thenBlock is not null);
            var coldBlock = _checkBlock.Prev;
            assert(coldBlock is not null);
            if ((coldBlock.Kind is not BBJ_ALWAYS) || !coldBlock.JumpsToNext)
            {
                JITDUMP($"Unexpected flow from cold path {FMT_BB(coldBlock.bbNum)}\n");
                return;
            }

            var hotBlock = coldBlock.Prev;
            assert(hotBlock is not null);
            if ((hotBlock.Kind is not BBJ_ALWAYS) || (hotBlock.Target != _checkBlock))
            {
                JITDUMP($"Unexpected flow from hot path {FMT_BB(hotBlock.bbNum)}\n");
                return;
            }

            JITDUMP($"Hot pred block is {FMT_BB(hotBlock.bbNum)} and cold pred block is {FMT_BB(coldBlock.bbNum)}\n");
            assert(_lastStmt is not null);
            var afterLastStmt = _lastStmt.NextStmt;
            for (var checkStmt = _checkBlock.FirstStmt; checkStmt != afterLastStmt;)
            {
                assert(checkStmt is not null);
                var nextStmt = checkStmt.NextStmt;
                var clonedStmt = _compiler.gtCloneStmt(checkStmt);
                _compiler.fgInsertStmtAtEnd(hotBlock, clonedStmt);
                checkStmt = nextStmt;
            }

            for (var checkStmt = _checkBlock.FirstStmt; checkStmt != afterLastStmt;)
            {
                assert(checkStmt is not null);
                var nextStmt = checkStmt.NextStmt;
                _compiler.fgUnlinkStmt(_checkBlock, checkStmt);
                _compiler.fgInsertStmtAtEnd(coldBlock, checkStmt);
                checkStmt = nextStmt;
            }

            _compiler.fgRedirectEdge(ref coldBlock.TargetEdgeRef, _elseBlock);
            if (coldBlock.hasProfileWeight)
            {
                var coldElseEdge = _compiler.fgGetPredForBlock(_elseBlock, coldBlock);
                assert(coldElseEdge is not null);
                var newCheckWeight = _checkBlock.bbWeight - coldElseEdge.LikelyWeight;
                if (newCheckWeight < 0)
                {
                    if (_compiler.fgPgoConsistent)
                    {
                        var isReasonableUnderflow = Compiler.fgProfileWeightsEqual(newCheckWeight, 0.0);
                        assert(isReasonableUnderflow);
                        if (!isReasonableUnderflow)
                        {
                            JITDUMP($"Profile data could not be locally repaired. Data {(_compiler.fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                            if (_compiler.fgPgoConsistent)
                            {
                                _compiler.Metrics.ProfileInconsistentChainedGDV++;
                                _compiler.fgPgoConsistent = false;
                            }
                        }
                    }

                    newCheckWeight = 0;
                }

                _checkBlock.setBBProfileWeight(newCheckWeight);
                var checkElseEdge = _compiler.fgGetPredForBlock(_elseBlock, _checkBlock);
                assert(checkElseEdge is not null);
                _elseBlock.setBBProfileWeight(checkElseEdge.LikelyWeight + coldElseEdge.LikelyWeight);
                var checkThenEdge = _compiler.fgGetPredForBlock(_thenBlock, _checkBlock);
                assert(checkThenEdge is not null);
                _thenBlock.setBBProfileWeight(checkThenEdge.LikelyWeight);
            }
        }

        private void ScoutForChainedGdv()
        {
            var gdvChainLikelihood = unchecked((uint)JitConfig.JitGuardedDevirtualizationChainLikelihood);
            if (_likelihood < gdvChainLikelihood)
            {
                return;
            }

            JITDUMP($"Scouting for possible GDV chain as likelihood {_likelihood} >= {gdvChainLikelihood}\n");
            var maxStatementDup = unchecked((uint)JitConfig.JitGuardedDevirtualizationChainStatements);
            uint chainStatementDup = 0;
            uint chainNodeDup = 0;
            assert(_remainderBlock is not null);
            foreach (var nextStmt in _remainderBlock.Statements)
            {
#if DEBUG
                JITDUMP($" Scouting {FMT_STMT(nextStmt.Id)}\n");
#endif
                var root = nextStmt.RootNode;
                if (root.Oper.IsCall)
                {
                    var call = root.AsCall();
                    if (call.IsGuardedDevirtualizationCandidate && (call.GetGdvCandidateInfo(0).likelihood >= gdvChainLikelihood))
                    {
#if DEBUG
                        JITDUMP($"GDV call at [{call.TreeId:D6}] has likelihood {call.GetGdvCandidateInfo(0).likelihood} >= {gdvChainLikelihood}; chaining ({chainStatementDup} stmts, {chainNodeDup} nodes to dup).\n");
#endif
                        call._callMoreFlags |= GTF_CALL_M_GUARDED_DEVIRT_CHAIN;
                        break;
                    }
                }

                if (chainStatementDup >= maxStatementDup)
                {
                    JITDUMP($"  reached max statement dup limit of {maxStatementDup}, bailing out\n");
                    break;
                }

                var visitor = new ClonabilityVisitor();
                _ = visitor.WalkTree(ref nextStmt.RootNodeRef, null);
                if (visitor.UnclonableNode is not null)
                {
#if DEBUG
                    JITDUMP($"  node [{visitor.UnclonableNode.TreeId:D6}] can't be cloned\n");
#endif
                    break;
                }

                chainStatementDup++;
                chainNodeDup += visitor.NodeCount;
            }
        }

        private struct ClonabilityVisitor : IGenTreeVisitor<ClonabilityVisitor>
        {
            public static bool DoPreOrder => true;
            private readonly GenTreeStack _ancestors;
            public GenTree? UnclonableNode;
            public uint NodeCount;

            public ClonabilityVisitor()
            {
                _ancestors = [];
            }

            public Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
            {
                var node = use;
                if (node.Oper.IsCall)
                {
                    var call = node.AsCall();
                    if (call.IsInlineCandidate && !call.IsGuardedDevirtualizationCandidate)
                    {
                        UnclonableNode = node;
                        return Compiler.WALK_ABORT;
                    }
                }
                else if (node.Oper is GT_RET_EXPR)
                {
                    var retExpr = node.AsRetExpr();
                    if (retExpr.SubstExpr is not null)
                    {
#if DEBUG
                        assert(retExpr.InlineCandidate.AsCall().IsGuarded);
#endif
                        use = retExpr.SubstExpr;
                        return Compiler.WALK_CONTINUE;
                    }

                    UnclonableNode = node;
                    return Compiler.WALK_ABORT;
                }

                NodeCount++;
                return Compiler.WALK_CONTINUE;
            }

            public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => Compiler.WALK_CONTINUE;

            public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
                => IGenTreeVisitor<ClonabilityVisitor>.WalkTree(ref this, ref use, user, _ancestors);
        }

        private GenTree? CreateTreeForLookup(CORINFO_METHOD_HANDLE methHnd, in CORINFO_CONST_LOOKUP lookup)
        {
            switch (lookup.accessType)
            {
                case IAT_VALUE:
                {
                    return CreateFunctionTargetAddr(methHnd, lookup);
                }

                case IAT_PVALUE:
                {
                    var tree = CreateFunctionTargetAddr(methHnd, lookup);
                    return _compiler.gtNewIndir(TYP_I_IMPL, tree, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
                }

                case IAT_PPVALUE:
                {
                    noway_assert(false, "!\"Unexpected IAT_PPVALUE\"");
                    return null;
                }

                case IAT_RELPVALUE:
                {
                    var addr = CreateFunctionTargetAddr(methHnd, lookup);
                    GenTree tree = CreateFunctionTargetAddr(methHnd, lookup);
                    tree = _compiler.gtNewIndir(TYP_I_IMPL, tree, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
                    return _compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, tree, addr);
                }

                default:
                {
                    noway_assert(false, "!\"Bad accessType\"");
                    return null;
                }
            }
        }

        private GenTreeIntCon CreateFunctionTargetAddr(CORINFO_METHOD_HANDLE methHnd, in CORINFO_CONST_LOOKUP lookup)
        {
            var con = _compiler.gtNewIconHandleNode((nint)lookup.addr, GTF_ICON_FTN_ADDR);
#if DEBUG
            con.TargetHandle = (nint)methHnd;
#endif
            return con;
        }
    }
}
