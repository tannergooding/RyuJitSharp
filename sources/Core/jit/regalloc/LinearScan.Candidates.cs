// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    public bool WillEnregisterLocalVars() => _enregisterLocalVars;

    public bool IsRegCandidate(in LclVarDsc varDsc)
    {
        if (!WillEnregisterLocalVars())
        {
            return false;
        }

        assert(_compiler.compEnregLocals);

        if (!varDsc.lvTracked)
        {
            return false;
        }

#if LOWER_DECOMPOSE_LONGS
        if (varDsc.Type is TYP_LONG)
        {
            return false;
        }
#endif

        if (_compiler.compJmpOpUsed && varDsc.lvIsRegArg)
        {
            return false;
        }

        if (_compiler.lvaIsFieldOfDependentlyPromotedStruct(in varDsc))
        {
            return false;
        }

        if (varDsc.lvRefCnt() is 0)
        {
            ref var local = ref _compiler.lvaGetDesc(_compiler.lvaGetLclNum(in varDsc));
            local.setLvRefCntWtd(0);
            return false;
        }

        if (varDsc.lvDoNotEnregister)
        {
            return false;
        }

        switch (varDsc.Type.ActualType)
        {
            case TYP_FLOAT:
            case TYP_DOUBLE:
            {
                return !_compiler.opts.compDbgCode;
            }

            case TYP_INT:
            case TYP_LONG:
            case TYP_REF:
            case TYP_BYREF:
            {
                return true;
            }

#if FEATURE_SIMD
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
#if FEATURE_MASKED_HW_INTRINSICS
            case TYP_MASK:
#endif
            {
                return !varDsc.lvPromoted;
            }
#endif

            case TYP_STRUCT:
            {
                // Structs containing GC pointers still require stack liveness reporting.
                return _compiler.compEnregStructLocals && !varDsc.HasGCPtr;
            }

            default:
            {
                return false;
            }
        }
    }

    public bool IsContainableMemoryOp(GenTree node)
    {
        if (node.IsMemoryOp)
        {
            return true;
        }

        if (!node.Oper.IsLocal)
        {
            return false;
        }

        if (!WillEnregisterLocalVars())
        {
            return true;
        }

        return _compiler.lvaGetDesc(node.AsLclVarCommon().LclNum).lvDoNotEnregister;
    }

    private void checkForDNER(int localNumber, in LclVarDsc local)
    {
        if (local.lvDoNotEnregister)
        {
            return;
        }

        if (!_compiler.compEnregLocals)
        {
            _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.NoRegVars);
            return;
        }

        if (varTypeIsStruct(local.Type) && !local.lvPromoted)
        {
            if (!local.IsEnregisterableType)
            {
                _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.NotRegSizeStruct);
                return;
            }

            if (local.Type == TYP_STRUCT)
            {
                if (!local.lvRegStruct && !_compiler.compEnregStructLocals)
                {
                    _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.DontEnregStructs);
                    return;
                }

                if (local.lvIsMultiRegArgOrRet)
                {
                    // Prolog and return generators do not support SIMD/general-register moves.
                    _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.IsStructArg);
                    return;
                }

#if TARGET_ARM
                if (local.lvIsParam)
                {
                    _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.IsStructArg);
                    return;
                }
#endif
            }
        }

        if (local.lvPinned)
        {
            _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.PinningRef);
            return;
        }

        if (local.lvTracked && local.IsLiveInOutOfHandler)
        {
            if (!_compiler.IsEHVarARegCandidate(in local))
            {
                _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.LiveInOutOfHandler);
                return;
            }

#if JIT32_GCENCODER
            if (_compiler.lvaKeepAliveAndReportThis() && (localNumber == _compiler.info.compThisArg))
            {
                // EH exposure prevents keeping "this" in one register for the whole method.
                _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.LiveInOutOfHandler);
                return;
            }
#endif
        }
    }
}
