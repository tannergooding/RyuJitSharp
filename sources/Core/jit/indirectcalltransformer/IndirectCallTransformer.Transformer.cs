// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class IndirectCallTransformer
{
    public abstract class Transformer
    {
        protected readonly Compiler _compiler;
        protected readonly BasicBlock _currBlock;
        protected readonly Statement _stmt;
        protected BasicBlock? _remainderBlock;
        protected BasicBlock? _checkBlock;
        protected BasicBlock? _thenBlock;
        protected BasicBlock? _elseBlock;
        protected GenTreeCall _origCall;
        protected int _likelihood = 80;

        protected Transformer(Compiler compiler, BasicBlock block, Statement stmt, GenTreeCall call)
        {
            _compiler = compiler;
            _currBlock = block;
            _stmt = stmt;
            _origCall = call;
        }

        public virtual void Run()
        {
            Transform();
        }

        protected void Transform()
        {
#if DEBUG
            JITDUMP($"*** {Name}: transforming {FMT_STMT(_stmt.Id)}\n");
#endif
            FixupRetExpr();
            ClearFlag();
            CreateRemainder();
            assert(GetChecksCount() > 0);

            for (byte i = 0; i < GetChecksCount(); i++)
            {
                CreateCheck(i);
                CreateThen(i);
            }

            CreateElse();
            RemoveOldStatement();
            SetWeights();
            ChainFlow();
        }

        protected abstract string Name { get; }
        protected abstract void ClearFlag();
        protected abstract GenTreeCall GetCall(Statement callStmt);
        protected abstract void FixupRetExpr();

        protected void SplitCall(BasicBlock block, ref GenTree useToSpill)
        {
            // Preserve evaluation order through the last side effect, including
            // earlier loads that a later argument could change through an alias.
            ref var lastSideEffectUse = ref Unsafe.NullRef<GenTree>();

            foreach (ref var use in _origCall.UseEdges)
            {
                if ((use.Flags & GTF_SIDE_EFFECT) != 0)
                {
                    lastSideEffectUse = ref use;
                }
            }

            if (!Unsafe.IsNullRef(ref lastSideEffectUse))
            {
                foreach (ref var use in _origCall.UseEdges)
                {
                    var node = use;
                    if (((node.Flags & GTF_ALL_EFFECT) != 0) ||
                        (!_compiler.impIsInvariant(node) && _compiler.gtHasLocalsWithAddrOp(node)))
                    {
                        SpillUseToTemp(block, ref use);
                    }

                    if (Unsafe.AreSame(ref use, ref lastSideEffectUse))
                    {
                        break;
                    }
                }
            }

            if (!useToSpill.Oper.IsLocal)
            {
                SpillUseToTemp(block, ref useToSpill);
            }
        }

        protected unsafe void SpillUseToTemp(BasicBlock block, ref GenTree use)
        {
            var tmpNum = _compiler.lvaGrabTemp(true, "indirect call transform spill temp");
            var node = use;
            var store = _compiler.gtNewTempStore(tmpNum, node);

            if (node.Type is TYP_REF)
            {
                var cls = _compiler.gtGetClassHandle(node, out var isExact, out _);
                if (cls != NO_CLASS_HANDLE)
                {
                    _compiler.lvaSetClass(tmpNum, cls, isExact);
                }
            }

            var storeStmt = _compiler.fgNewStmtFromTree(store, di: _stmt.DebugInfo);
            _compiler.fgInsertStmtAtEnd(block, storeStmt);
            use = _compiler.gtNewLclVarNode(TYP_UNDEF, tmpNum);
        }

        private void CreateRemainder()
        {
            _remainderBlock = _compiler.fgSplitBlockAfterStatement(_currBlock, _stmt);
            _remainderBlock.SetFlags(BBF_INTERNAL);
            _remainderBlock.RemoveFlags(BBF_DONT_REMOVE);
            _compiler.fgRemoveRefPred(_currBlock.TargetEdge);
        }

        protected abstract void CreateCheck(byte checkIdx);

        protected BasicBlock CreateAndInsertBasicBlock(BBKinds jumpKind, BasicBlock insertAfter, BasicBlock? flagsSource)
        {
            var block = _compiler.fgNewBBafter(jumpKind, insertAfter, true);
            block.SetFlags(BBF_IMPORTED);

            if (flagsSource is not null)
            {
                block.CopyFlags(flagsSource, BBF_SPLIT_GAINED);
            }

            block.RemoveFlags(BBF_DONT_REMOVE);
            return block;
        }

        protected abstract void CreateThen(byte checkIdx);
        protected abstract void CreateElse();

        protected virtual byte GetChecksCount() => 1;

        private void RemoveOldStatement()
        {
            _compiler.fgRemoveStmt(_currBlock, _stmt);
        }

        protected virtual void SetWeights()
        {
            assert(_remainderBlock is not null);
            assert(_checkBlock is not null);
            assert(_thenBlock is not null);
            assert(_elseBlock is not null);
            _remainderBlock.inheritWeight(_currBlock);
            _checkBlock.inheritWeight(_currBlock);
            _thenBlock.inheritWeightPercentage(_currBlock, _likelihood);
            _elseBlock.inheritWeightPercentage(_currBlock, 100 - _likelihood);
        }

        protected virtual void ChainFlow()
        {
            assert(_compiler.fgPredsComputed);
            assert(_checkBlock is not null);
            assert(_thenBlock is not null);
            assert(_elseBlock is not null);
            assert(_remainderBlock is not null);

            if (_checkBlock != _currBlock)
            {
                assert(_currBlock.Kind is BBJ_ALWAYS);
                var newEdge = _compiler.fgAddRefPred(_checkBlock, _currBlock);
                _currBlock.TargetEdge = newEdge;
            }

            // Native uses equal edge likelihoods despite the 80/20 block weights.
            assert(_checkBlock.Kind is BBJ_ALWAYS);
            var thenEdge = _compiler.fgAddRefPred(_thenBlock, _checkBlock);
            thenEdge.Likelihood = 0.5;
            var elseEdge = _compiler.fgAddRefPred(_elseBlock, _checkBlock);
            elseEdge.Likelihood = 0.5;
            _checkBlock.SetCond(elseEdge, thenEdge);

            assert(_thenBlock.Kind is BBJ_ALWAYS);
            var thenRemainderEdge = _compiler.fgAddRefPred(_remainderBlock, _thenBlock);
            _thenBlock.TargetEdge = thenRemainderEdge;

            assert(_elseBlock.Kind is BBJ_ALWAYS);
            var elseRemainderEdge = _compiler.fgAddRefPred(_remainderBlock, _elseBlock);
            _elseBlock.TargetEdge = elseRemainderEdge;
        }
    }
}
