// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class IndirectCallTransformer
{
    public sealed class FatPointerCallTransformer : Transformer
    {
        private const int FAT_POINTER_MASK = 0x2;

        private GenTree _fptrAddress;
        private readonly var_types _pointerType;
        private readonly bool _doesReturnValue;

        public FatPointerCallTransformer(Compiler compiler, BasicBlock block, Statement stmt)
            : base(compiler, block, stmt, (stmt.RootNode.Oper is GT_STORE_LCL_VAR
                ? stmt.RootNode.AsLclVar().Data : stmt.RootNode).AsCall())
        {
            _doesReturnValue = stmt.RootNode.Oper is GT_STORE_LCL_VAR;
            assert(_origCall.ControlExpr is not null);
            _fptrAddress = _origCall.ControlExpr;
            _pointerType = _fptrAddress.Type;
        }

        protected override string Name => "FatPointerCall";

        protected override GenTreeCall GetCall(Statement callStmt)
        {
            var tree = callStmt.RootNode;

            if (_doesReturnValue)
            {
                assert(tree.Oper is GT_STORE_LCL_VAR);
                return tree.AsLclVar().Data.AsCall();
            }

            return tree.AsCall();
        }

        protected override void ClearFlag()
        {
            _origCall.IsFatPointerCandidate = false;
        }

        protected override void FixupRetExpr()
        {
            // Fat-pointer return values were already spilled by the importer.
        }

        protected override void CreateCheck(byte checkIdx)
        {
            assert(checkIdx == 0);

            if (_origCall.IsGenericVirtual(_compiler))
            {
                SplitCall(_currBlock, ref _origCall.ControlExprRef);
                assert(_origCall.ControlExpr is not null);
                _fptrAddress = _origCall.ControlExpr;
            }

            _checkBlock = CreateAndInsertBasicBlock(BBJ_ALWAYS, _currBlock, _currBlock);
            var fatPointerMask = new GenTreeIntCon(TYP_I_IMPL, FAT_POINTER_MASK);
            var fptrAddressCopy = _compiler.gtCloneExpr(_fptrAddress);
            assert(fptrAddressCopy is not null);
            var fatPointerAnd = _compiler.gtNewBinaryNode(GT_AND, TYP_I_IMPL, fptrAddressCopy, fatPointerMask);
            var zero = new GenTreeIntCon(TYP_I_IMPL, 0);
            var fatPointerCmp = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, fatPointerAnd, zero);
            var jmpTree = _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, fatPointerCmp);
            var jmpStmt = _compiler.fgNewStmtFromTree(jmpTree, di: _stmt.DebugInfo);
            _compiler.fgInsertStmtAtEnd(_checkBlock, jmpStmt);
        }

        protected override void CreateThen(byte checkIdx)
        {
            assert(_remainderBlock is not null);
            assert(_checkBlock is not null);
            _thenBlock = CreateAndInsertBasicBlock(BBJ_ALWAYS, _checkBlock, _currBlock);
            var copyOfOriginalStmt = _compiler.gtCloneStmt(_stmt);
            _compiler.fgInsertStmtAtEnd(_thenBlock, copyOfOriginalStmt);
        }

        protected override void CreateElse()
        {
            assert(_thenBlock is not null);
            _elseBlock = CreateAndInsertBasicBlock(BBJ_ALWAYS, _thenBlock, _currBlock);
            var fixedFptrAddress = GetFixedFptrAddress();
            var actualCallAddress = _compiler.gtNewIndir(_pointerType, fixedFptrAddress, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
            var hiddenArgument = GetHiddenArgument(fixedFptrAddress);
            var fatStmt = CreateFatCallStmt(actualCallAddress, hiddenArgument);
            _compiler.fgInsertStmtAtEnd(_elseBlock, fatStmt);
        }

        private GenTreeOp GetFixedFptrAddress()
        {
            var fptrAddressCopy = _compiler.gtCloneExpr(_fptrAddress);
            assert(fptrAddressCopy is not null);
            var fatPointerMask = new GenTreeIntCon(TYP_I_IMPL, FAT_POINTER_MASK);
            return _compiler.gtNewBinaryNode(GT_SUB, _pointerType, fptrAddressCopy, fatPointerMask);
        }

        private GenTreeIndir GetHiddenArgument(GenTree fixedFptrAddress)
        {
            var fixedFptrAddressCopy = _compiler.gtCloneExpr(fixedFptrAddress);
            assert(fixedFptrAddressCopy is not null);
            var wordSize = new GenTreeIntCon(TYP_I_IMPL, TYP_I_IMPL.Size);
            var hiddenArgumentPtr = _compiler.gtNewBinaryNode(GT_ADD, _pointerType, fixedFptrAddressCopy, wordSize);
            return _compiler.gtNewIndir(fixedFptrAddressCopy.Type, hiddenArgumentPtr, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
        }

        private Statement CreateFatCallStmt(GenTree actualCallAddress, GenTree hiddenArgument)
        {
            var fatStmt = _compiler.gtCloneStmt(_stmt);
            var fatCall = GetCall(fatStmt);
            fatCall.ControlExpr = actualCallAddress;
            _ = fatCall.Args.InsertInstParam(_compiler, hiddenArgument);
            return fatStmt;
        }
    }
}
