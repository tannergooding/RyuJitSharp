// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private sealed class HWIntrinsicImmOpHelper
    {
        private readonly CodeGen _codeGen;
        private readonly BasicBlock? _endLabel;
        private readonly BasicBlock? _nonZeroLabel;
        private int _immValue;
        private readonly int _immLowerBound;
        private readonly int _immUpperBound;
        private readonly regNumber _nonConstImmReg;
        private readonly regNumber _branchTargetReg;
        private readonly int _numInstrs;

        public HWIntrinsicImmOpHelper(CodeGen codeGen, GenTree immOp, GenTreeHWIntrinsic intrin,
            int numInstrs = 1)
        {
            assert(codeGen is not null);
            assert(varTypeIsIntegral(immOp.Type));
            _codeGen = codeGen;
            _numInstrs = numInstrs;
            _branchTargetReg = REG_NA;
            if (immOp.IsContainedIntOrIImmed)
            {
                _nonConstImmReg = REG_NA;
                _immValue = unchecked((int)immOp.AsIntCon().IconValue);
                _immLowerBound = _immValue;
                _immUpperBound = _immValue;
            }
            else
            {
                var category = HWIntrinsicInfo.lookupCategory(intrin.HWIntrinsicId);
                if (category == HW_Category_SIMDByIndexedElement)
                {
                    var info = new Arm64HWIntrinsic(intrin);
                    var_types indexedElementOpType;
                    if (info.NumOperands == 2)
                    {
                        indexedElementOpType = info.Op1.Type;
                    }
                    else if (info.NumOperands == 3)
                    {
                        indexedElementOpType = info.Op2.Type;
                    }
                    else
                    {
                        assert(info.NumOperands == 4);
                        indexedElementOpType = info.Op3.Type;
                    }
                    assert(varTypeIsSimd(indexedElementOpType));
                    var indexedElementSimdSize = indexedElementOpType.Size;
                    HWIntrinsicInfo.lookupImmBounds(intrin.HWIntrinsicId, indexedElementSimdSize,
                        intrin.SimdBaseType, 1, out _immLowerBound, out _immUpperBound);
                }
                else
                {
                    HWIntrinsicInfo.lookupImmBounds(intrin.HWIntrinsicId, intrin.SimdSize,
                        intrin.SimdBaseType, 1, out _immLowerBound, out _immUpperBound);
                }
                _nonConstImmReg = immOp.RegNum;
                _immValue = _immLowerBound;
                if (TestImmOpZeroOrOne())
                {
                    _nonZeroLabel = codeGen.genCreateTempLabel();
                }
                else
                {
                    _branchTargetReg = codeGen._internalRegisters.GetSingle(intrin);
                }
                _endLabel = codeGen.genCreateTempLabel();
            }
        }

        public HWIntrinsicImmOpHelper(CodeGen codeGen, regNumber immReg, int immLowerBound,
            int immUpperBound, GenTreeHWIntrinsic intrin, int numInstrs = 1)
        {
            assert(codeGen is not null);
            _codeGen = codeGen;
            _immValue = immLowerBound;
            _immLowerBound = immLowerBound;
            _immUpperBound = immUpperBound;
            _nonConstImmReg = immReg;
            _branchTargetReg = REG_NA;
            _numInstrs = numInstrs;
            if (TestImmOpZeroOrOne())
            {
                _nonZeroLabel = codeGen.genCreateTempLabel();
            }
            else
            {
                _branchTargetReg = codeGen._internalRegisters.GetSingle(intrin);
            }
            _endLabel = codeGen.genCreateTempLabel();
        }

        public bool Done => _immValue > _immUpperBound;
        public int ImmValue => _immValue;
        private bool NonConstImmOp => _nonConstImmReg != REG_NA;

        private bool TestImmOpZeroOrOne()
        {
            assert(NonConstImmOp);

            return _immLowerBound == 0 && _immUpperBound == 1;
        }

        public void EmitBegin()
        {
            if (NonConstImmOp)
            {
                var beginLabel = _codeGen.genCreateTempLabel();
                if (TestImmOpZeroOrOne())
                {
                    assert(_nonZeroLabel is not null);
                    Arm64IntrinsicEmitRegisterBranch(INS_cbnz, EA_4BYTE, _nonZeroLabel, _nonConstImmReg);
                }
                else
                {
                    assert(_numInstrs is 1 or 2);
                    // A case occupies one or two four-byte instructions followed by B.
                    Arm64IntrinsicEmitRegisterLabel(INS_adr, EA_8BYTE, beginLabel, _branchTargetReg);
                    _codeGen.Emitter.emitIns_R_R_R_I(INS_add, EA_8BYTE, _branchTargetReg,
                        _branchTargetReg, _nonConstImmReg, 3, INS_OPTS_LSL);
                    if (_numInstrs == 2)
                    {
                        _codeGen.Emitter.emitIns_R_R_R_I(INS_add, EA_8BYTE, _branchTargetReg,
                            _branchTargetReg, _nonConstImmReg, 2, INS_OPTS_LSL);
                    }
                    if (_immLowerBound != 0)
                    {
                        var lowerReduce = unchecked((nint)_immLowerBound << 3);
                        if (_numInstrs == 2)
                        {
                            lowerReduce = unchecked(lowerReduce + ((nint)_immLowerBound << 2));
                        }
                        _codeGen.Emitter.emitIns_R_R_I(INS_sub, EA_8BYTE, _branchTargetReg,
                            _branchTargetReg, lowerReduce);
                    }
                    _codeGen.Emitter.emitIns_R(INS_br, EA_8BYTE, _branchTargetReg);
                }
                _codeGen.genDefineInlineTempLabel(beginLabel);
            }
        }

        public void EmitCaseEnd()
        {
            assert(!Done);
            if (NonConstImmOp)
            {
                var isLastCase = _immValue == _immUpperBound;
                if (isLastCase)
                {
                    assert(_endLabel is not null);
                    _codeGen.genDefineInlineTempLabel(_endLabel);
                }
                else
                {
                    assert(_endLabel is not null);
                    _codeGen.Emitter.emitIns_J(INS_b, _endLabel);
                    if (TestImmOpZeroOrOne())
                    {
                        assert(_nonZeroLabel is not null);
                        _codeGen.genDefineInlineTempLabel(_nonZeroLabel);
                    }
                    else
                    {
                        var tempLabel = _codeGen.genCreateTempLabel();
                        _codeGen.genDefineInlineTempLabel(tempLabel);
                    }
                }
            }
            _immValue = unchecked(_immValue + 1);
        }
    }
}
#endif
