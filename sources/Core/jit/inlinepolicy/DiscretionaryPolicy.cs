// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Globalization;
using System.IO;

namespace RyuJitSharp;

public partial class DiscretionaryPolicy : DefaultPolicy
{
    protected const int MAX_ARGS = 6;
    protected const int SIZE_SCALE = 10;
    protected double _profileFrequency;
    protected uint _blockCount;
    protected uint _maxstack;
    protected uint _argCount;
    protected CorInfoType[] _argType = new CorInfoType[MAX_ARGS];
    protected nuint[] _argSize = new nuint[MAX_ARGS];
    protected uint _localCount;
    protected CorInfoType _returnType;
    protected nuint _returnSize;
    protected uint _argAccessCount;
    protected uint _localAccessCount;
    protected uint _intConstantCount;
    protected uint _floatConstantCount;
    protected uint _intLoadCount;
    protected uint _floatLoadCount;
    protected uint _intStoreCount;
    protected uint _floatStoreCount;
    protected uint _simpleMathCount;
    protected uint _complexMathCount;
    protected uint _overflowMathCount;
    protected uint _intArrayLoadCount;
    protected uint _floatArrayLoadCount;
    protected uint _refArrayLoadCount;
    protected uint _structArrayLoadCount;
    protected uint _intArrayStoreCount;
    protected uint _floatArrayStoreCount;
    protected uint _refArrayStoreCount;
    protected uint _structArrayStoreCount;
    protected uint _structOperationCount;
    protected uint _objectModelCount;
    protected uint _fieldLoadCount;
    protected uint _fieldStoreCount;
    protected uint _staticFieldLoadCount;
    protected uint _staticFieldStoreCount;
    protected uint _loadAddressCount;
    protected uint _throwCount;
    protected uint _returnCount;
    protected uint _callCount;
    protected uint _callSiteWeight;
    protected int _modelCodeSizeEstimate;
    protected int _perCallInstructionEstimate;
    protected bool _hasProfileWeights;
    protected bool _isClassCtor;
    protected bool _isSameThis;
    protected bool _callerHasNewArray;
    protected bool _callerHasNewObj;
    protected bool _calleeHasGCStruct;

    public DiscretionaryPolicy(Compiler compiler, bool isPrejitRoot)
        : base(compiler, isPrejitRoot)
    {
    }

#if DEBUG
    public override string Name => nameof(DiscretionaryPolicy);
#endif

    public override void NoteBool(InlineObservation observation, bool value)
    {
        switch (observation)
        {
            case InlineObservation.CALLEE_IS_CLASS_CTOR:
            {
                _isClassCtor = value;
                break;
            }

            case InlineObservation.CALLSITE_IS_SAME_THIS:
            {
                _isSameThis = value;
                break;
            }

            case InlineObservation.CALLER_HAS_NEWARRAY:
            {
                _callerHasNewArray = value;
                break;
            }

            case InlineObservation.CALLER_HAS_NEWOBJ:
            {
                _callerHasNewObj = value;
                break;
            }

            case InlineObservation.CALLEE_HAS_GC_STRUCT:
            {
                _calleeHasGCStruct = value;
                break;
            }

            case InlineObservation.CALLSITE_RARE_GC_STRUCT:
            {
                break;
            }

            case InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS:
            {
                _hasProfileWeights = value;
                break;
            }

            default:
            {
                base.NoteBool(observation, value);
                break;
            }
        }
    }

    public override void NoteInt(InlineObservation observation, int value)
    {
        unchecked
        {
            switch (observation)
            {
                case InlineObservation.CALLEE_IL_CODE_SIZE:
                {
                    assert(IsForceInlineKnown);
                    assert(value != 0);
                    _codeSize = value;

                    if (IsForceInline)
                    {
                        SetCandidate(InlineObservation.CALLEE_IS_FORCE_INLINE);
                    }
                    else
                    {
                        SetCandidate(InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);
                    }
                    break;
                }

                case InlineObservation.CALLEE_OPCODE:
                {
                    ComputeOpcodeBin((OPCODE)value);
                    base.NoteInt(observation, value);
                    break;
                }

                case InlineObservation.CALLEE_MAXSTACK:
                {
                    _maxstack = (uint)value;
                    break;
                }

                case InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS:
                {
                    _blockCount = (uint)value;
                    break;
                }

                case InlineObservation.CALLSITE_WEIGHT:
                {
                    _callSiteWeight = (uint)value;
                    break;
                }

                default:
                {
                    base.NoteInt(observation, value);
                    break;
                }
            }
        }
    }

    public override void NoteDouble(InlineObservation observation, double value)
    {
        assert(observation is InlineObservation.CALLSITE_PROFILE_FREQUENCY);
        assert(value >= 0.0);
        _profileFrequency = value;
    }

    public override bool PropagateNeverToRuntime()
    {
        return _observation is not (InlineObservation.CALLEE_NOT_PROFITABLE_INLINE or InlineObservation.CALLEE_DOES_NOT_RETURN);
    }

    public override void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
    {
        MethodInfoObservations(methodInfo);
        EstimateCodeSize();
        EstimatePerformanceImpact();
        base.DetermineProfitability(methodInfo);
    }

    protected unsafe void MethodInfoObservations(in CORINFO_METHOD_INFO methodInfo)
    {
        _localCount = methodInfo.locals.numArgs;

        var args = methodInfo.args;
        var argCount = (uint)args.numArgs;
        _argCount = argCount;
        var pointerSize = TARGET_POINTER_SIZE;
        var i = 0;

        if (args.hasThis())
        {
            _argType[i] = CORINFO_TYPE_CLASS;
            _argSize[i] = unchecked((nuint)pointerSize);
            i++;
            _argCount++;
        }

        if (args.hasTypeArg())
        {
            _argType[i] = CORINFO_TYPE_NATIVEINT;
            _argSize[i] = unchecked((nuint)pointerSize);
            i++;
            _argCount++;
        }

        var argList = args.args;
        var comp = _rootCompiler.info.compCompHnd;

        for (var j = 0; (i < MAX_ARGS) && (j < argCount); i++, j++)
        {
            CORINFO_CLASS_HANDLE classHandle;
            CorInfoType type;

            fixed (CORINFO_SIG_INFO* pArgs = &methodInfo.args)
            {
                type = strip(comp->getArgType(pArgs, argList, &classHandle));
            }
            _argType[i] = type;

            if (type is CORINFO_TYPE_VALUECLASS)
            {
                assert(classHandle != null);
                _argSize[i] = unchecked((nuint)roundUp(comp->getClassSize(classHandle), pointerSize));
            }
            else
            {
                _argSize[i] = unchecked((nuint)pointerSize);
            }

            argList = comp->getArgNext(argList);
        }

        while (i < MAX_ARGS)
        {
            _argType[i] = CORINFO_TYPE_UNDEF;
            _argSize[i] = 0;
            i++;
        }

        _returnType = args.retType;

        if (_returnType is CORINFO_TYPE_VALUECLASS)
        {
            assert(args.retTypeClass != null);
            _returnSize = unchecked((nuint)roundUp(comp->getClassSize(args.retTypeClass), pointerSize));
        }
        else if (_returnType is CORINFO_TYPE_VOID)
        {
            _returnSize = 0;
        }
        else
        {
            _returnSize = unchecked((nuint)pointerSize);
        }
    }

    protected void EstimateCodeSize()
    {
        _calleeNativeSizeEstimate = DetermineNativeSizeEstimate();

        var sizeEstimate =
            -13.532 +
              0.359 * (int)_callsiteFrequency +
             -0.015 * _argCount +
             -1.553 * _argSize[5] +
              2.326 * _localCount +
              0.287 * _returnSize +
              0.561 * _intConstantCount +
              1.932 * _floatConstantCount +
             -0.822 * _simpleMathCount +
             -7.591 * _intArrayLoadCount +
              4.784 * _refArrayLoadCount +
             12.778 * _structArrayLoadCount +
              1.452 * _fieldLoadCount +
              8.811 * _staticFieldLoadCount +
              2.752 * _staticFieldStoreCount +
             -6.566 * _throwCount +
              6.021 * _callCount +
             -0.238 * (IsInstanceCtor ? 1 : 0) +
             -5.357 * (IsFromPromotableValueClass ? 1 : 0) +
             -7.901 * (_constantArgFeedsConstantTest > 0 ? 1 : 0) +
              0.065 * _calleeNativeSizeEstimate;

        _modelCodeSizeEstimate = (int)(SIZE_SCALE * sizeEstimate);
    }

    protected void EstimatePerformanceImpact()
    {
        var perCallSavingsEstimate =
            -7.35
            + (_callsiteFrequency is InlineCallsiteFrequency.BORING ? 0.76 : 0)
            + (_callsiteFrequency is InlineCallsiteFrequency.LOOP ? -2.02 : 0)
            + (_argType[0] is CORINFO_TYPE_CLASS ? 3.51 : 0)
            + (_argType[3] is CORINFO_TYPE_BOOL ? 20.7 : 0)
            + (_argType[4] is CORINFO_TYPE_CLASS ? 0.38 : 0)
            + (_returnType is CORINFO_TYPE_CLASS ? 2.32 : 0);

        _perCallInstructionEstimate = (int)(SIZE_SCALE * perCallSavingsEstimate);
    }

    public override int CodeSizeEstimate()
    {
        return _modelCodeSizeEstimate;
    }

#if DEBUG
    public override void DumpSchema(StreamWriter stream)
    {
        stream.Write("ILSize,CallsiteFrequency,InstructionCount,LoadStoreCount,BlockCount,Maxstack,ArgCount");

        for (var i = 0; i < MAX_ARGS; i++)
        {
            stream.Write($",Arg{i}Type");
        }

        for (var i = 0; i < MAX_ARGS; i++)
        {
            stream.Write($",Arg{i}Size");
        }

        stream.Write(",LocalCount,ReturnType,ReturnSize,ArgAccessCount,LocalAccessCount,IntConstantCount,FloatConstantCount");
        stream.Write(",IntLoadCount,FloatLoadCount,IntStoreCount,FloatStoreCount,SimpleMathCount,ComplexMathCount,OverflowMathCount");
        stream.Write(",IntArrayLoadCount,FloatArrayLoadCount,RefArrayLoadCount,StructArrayLoadCount");
        stream.Write(",IntArrayStoreCount,FloatArrayStoreCount,RefArrayStoreCount,StructArrayStoreCount");
        stream.Write(",StructOperationCount,ObjectModelCount,FieldLoadCount,FieldStoreCount,StaticFieldLoadCount,StaticFieldStoreCount");
        stream.Write(",LoadAddressCount,ThrowCount,ReturnCount,CallCount,CallSiteWeight");
        stream.Write(",IsForceInline,IsInstanceCtor,IsFromPromotableValueClass,HasSimd,LooksLikeWrapperMethod");
        stream.Write(",ArgFeedsConstantTest,IsMostlyLoadStore,ArgFeedsRangeCheck,ConstantArgFeedsConstantTest");
        stream.Write(",CalleeNativeSizeEstimate,CallsiteNativeSizeEstimate,ModelCodeSizeEstimate,ModelPerCallInstructionEstimate");
        stream.Write(",IsClassCtor,IsSameThis,CallerHasNewArray,CallerHasNewObj,CalleeDoesNotReturn,CalleeHasGCStruct,CallsiteDepth");
    }

    public override void DumpData(StreamWriter stream)
    {
        var first = true;
        WriteData(unchecked((uint)_codeSize), (uint)_callsiteFrequency, unchecked((uint)_instructionCount),
            unchecked((uint)_loadStoreCount), _blockCount, _maxstack, _argCount);

        foreach (var type in _argType)
        {
            WriteData((uint)type);
        }

        foreach (var size in _argSize)
        {
            WriteData(unchecked((uint)size));
        }

        WriteData(_localCount, (uint)_returnType, unchecked((uint)_returnSize), _argAccessCount, _localAccessCount,
            _intConstantCount, _floatConstantCount, _intLoadCount, _floatLoadCount, _intStoreCount, _floatStoreCount,
            _simpleMathCount, _complexMathCount, _overflowMathCount, _intArrayLoadCount, _floatArrayLoadCount,
            _refArrayLoadCount, _structArrayLoadCount, _intArrayStoreCount, _floatArrayStoreCount, _refArrayStoreCount,
            _structArrayStoreCount, _structOperationCount, _objectModelCount, _fieldLoadCount, _fieldStoreCount,
            _staticFieldLoadCount, _staticFieldStoreCount, _loadAddressCount, _returnCount, _throwCount, _callCount,
            _callSiteWeight, IsForceInline ? 1 : 0, IsInstanceCtor ? 1 : 0, IsFromPromotableValueClass ? 1 : 0,
            HasSimd ? 1 : 0, LooksLikeWrapperMethod ? 1 : 0, unchecked((uint)_argFeedsConstantTest),
            MethodIsMostlyLoadStore ? 1 : 0, unchecked((uint)_argFeedsRangeCheck), ConstArgFeedsIsKnownConst ? 1 : 0,
            ArgFeedsIsKnownConst ? 1 : 0, unchecked((uint)_constantArgFeedsConstantTest), _calleeNativeSizeEstimate,
            _callsiteNativeSizeEstimate, _modelCodeSizeEstimate, _perCallInstructionEstimate, _isClassCtor ? 1 : 0,
            _isSameThis ? 1 : 0, _callerHasNewArray ? 1 : 0, _callerHasNewObj ? 1 : 0, IsNoReturn ? 1 : 0,
            _calleeHasGCStruct ? 1 : 0, unchecked((uint)_callsiteDepth));

        void WriteData(params object[] values)
        {
            foreach (var value in values)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    stream.Write(',');
                }

                stream.Write(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }
    }
#endif
}
