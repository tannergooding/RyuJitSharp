// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TRACK_ENREG_STATS
using System.IO;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct EnregisterStats
    {
        private uint _totalNumberOfVars;
        private uint _totalNumberOfStructVars;
        private uint _totalNumberOfEnregVars;
        private uint _totalNumberOfStructEnregVars;
        private uint _addrExposed;
        private uint _hiddenStructArg;
        private uint _vmNeedsStackAddr;
        private uint _localField;
        private uint _blockOp;
        private uint _dontEnregStructs;
        private uint _notRegSizeStruct;
        private uint _structArg;
        private uint _lclAddrNode;
        private uint _castTakesAddr;
        private uint _storeBlkSrc;
        private uint _swizzleArg;
        private uint _blockOpRet;
        private uint _returnSpCheck;
        private uint _callSpCheck;
        private uint _simdUserForcesDep;
        private uint _liveInOutHndlr;
        private uint _depField;
        private uint _noRegVars;
        private uint _wasmGcVisibility;
        private uint _pinningRef;
#if !TARGET_64BIT
        private uint _longParamField;
#endif
        private uint _parentExposed;
        private uint _tooConservative;
        private uint _escapeAddress;
        private uint _osrExposed;
        private uint _stressLclFld;
        private uint _dispatchRetBuf;
        private uint _wideIndir;
        private uint _stressPoisonImplicitByrefs;
        private uint _externallyVisibleImplicitly;
        private uint _smallTypePartialDef;

        public void RecordLocal(in LclVarDsc varDsc)
        {
            unchecked
            {
                _totalNumberOfVars++;
                if (varDsc.Type == TYP_STRUCT)
                {
                    _totalNumberOfStructVars++;
                }

                if (!varDsc.lvDoNotEnregister)
                {
                    _totalNumberOfEnregVars++;
                    if (varDsc.Type == TYP_STRUCT)
                    {
                        _totalNumberOfStructEnregVars++;
                    }
                }
                else
                {
                    switch (varDsc.DoNotEnregisterReason)
                    {
                        case DoNotEnregisterReason.AddrExposed:
                        {
                            _addrExposed++;
                            break;
                        }

                        case DoNotEnregisterReason.HiddenBufferStructArg:
                        {
                            _hiddenStructArg++;
                            break;
                        }

                        case DoNotEnregisterReason.DontEnregStructs:
                        {
                            _dontEnregStructs++;
                            break;
                        }

                        case DoNotEnregisterReason.NotRegSizeStruct:
                        {
                            _notRegSizeStruct++;
                            break;
                        }

                        case DoNotEnregisterReason.LocalField:
                        {
                            _localField++;
                            break;
                        }

                        case DoNotEnregisterReason.VMNeedsStackAddr:
                        {
                            _vmNeedsStackAddr++;
                            break;
                        }

                        case DoNotEnregisterReason.LiveInOutOfHandler:
                        {
                            _liveInOutHndlr++;
                            break;
                        }

                        case DoNotEnregisterReason.BlockOp:
                        {
                            _blockOp++;
                            break;
                        }

                        case DoNotEnregisterReason.IsStructArg:
                        {
                            _structArg++;
                            break;
                        }

                        case DoNotEnregisterReason.DepField:
                        {
                            _depField++;
                            break;
                        }

                        case DoNotEnregisterReason.NoRegVars:
                        {
                            _noRegVars++;
                            break;
                        }

#if !TARGET_64BIT
                        case DoNotEnregisterReason.LongParamField:
                        {
                            _longParamField++;
                            break;
                        }
#endif

                        case DoNotEnregisterReason.PinningRef:
                        {
                            _pinningRef++;
                            break;
                        }

                        case DoNotEnregisterReason.LclAddrNode:
                        {
                            _lclAddrNode++;
                            break;
                        }

                        case DoNotEnregisterReason.CastTakesAddr:
                        {
                            _castTakesAddr++;
                            break;
                        }

                        case DoNotEnregisterReason.StoreBlkSrc:
                        {
                            _storeBlkSrc++;
                            break;
                        }

                        case DoNotEnregisterReason.SwizzleArg:
                        {
                            _swizzleArg++;
                            break;
                        }

                        case DoNotEnregisterReason.BlockOpRet:
                        {
                            _blockOpRet++;
                            break;
                        }

                        case DoNotEnregisterReason.ReturnSpCheck:
                        {
                            _returnSpCheck++;
                            break;
                        }

                        case DoNotEnregisterReason.CallSpCheck:
                        {
                            _callSpCheck++;
                            break;
                        }

                        case DoNotEnregisterReason.simdUserForcesDep:
                        {
                            _simdUserForcesDep++;
                            break;
                        }

                        case DoNotEnregisterReason.WasmGCVisibility:
                        {
                            _wasmGcVisibility++;
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }

                    if (varDsc.DoNotEnregisterReason == DoNotEnregisterReason.AddrExposed)
                    {
                        // Adjusting an exposed 'this' can retain this reason after clearing exposure.
                        switch (varDsc.AddrExposedReason)
                        {
                            case AddressExposedReason.PARENT_EXPOSED:
                            {
                                _parentExposed++;
                                break;
                            }

                            case AddressExposedReason.TOO_CONSERVATIVE:
                            {
                                _tooConservative++;
                                break;
                            }

                            case AddressExposedReason.ESCAPE_ADDRESS:
                            {
                                _escapeAddress++;
                                break;
                            }

                            case AddressExposedReason.WIDE_INDIR:
                            {
                                _wideIndir++;
                                break;
                            }

                            case AddressExposedReason.OSR_EXPOSED:
                            {
                                _osrExposed++;
                                break;
                            }

                            case AddressExposedReason.STRESS_LCL_FLD:
                            {
                                _stressLclFld++;
                                break;
                            }

                            case AddressExposedReason.DISPATCH_RET_BUF:
                            {
                                _dispatchRetBuf++;
                                break;
                            }

                            case AddressExposedReason.STRESS_POISON_IMPLICIT_BYREFS:
                            {
                                _stressPoisonImplicitByrefs++;
                                break;
                            }

                            case AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY:
                            {
                                _externallyVisibleImplicitly++;
                                break;
                            }

                            case AddressExposedReason.SMALL_TYPE_PARTIAL_DEF:
                            {
                                _smallTypePartialDef++;
                                break;
                            }

                            default:
                            {
                                unreached();
                                break;
                            }
                        }
                    }
                }
            }
        }

        public readonly void Dump(TextWriter writer)
        {
            var nonStructVars = unchecked(s_enregisterStats._totalNumberOfVars - s_enregisterStats._totalNumberOfStructVars);
            var nonStructEnregVars = unchecked(s_enregisterStats._totalNumberOfEnregVars - s_enregisterStats._totalNumberOfStructEnregVars);
            var notEnreg = unchecked(s_enregisterStats._totalNumberOfVars - s_enregisterStats._totalNumberOfEnregVars);

            writer.Write("\nLocals enregistration statistics:\n");
            if (_totalNumberOfVars == 0)
            {
                writer.Write("No locals to report.\n");
                return;
            }
            writer.Write($"total number of locals: {unchecked((int)_totalNumberOfVars)}, " +
                $"number of enregistered: {unchecked((int)_totalNumberOfEnregVars)}, " +
                $"notEnreg: {unchecked((int)(_totalNumberOfVars - _totalNumberOfEnregVars))}, " +
                $"ratio: {formatFloat((float)_totalNumberOfEnregVars / _totalNumberOfVars, "F2")}\n");
            if (_totalNumberOfStructVars != 0)
            {
                writer.Write($"total number of struct locals: {unchecked((int)_totalNumberOfStructVars)}, " +
                    $"number of enregistered: {unchecked((int)_totalNumberOfStructEnregVars)}, " +
                    $"notEnreg: {unchecked((int)(_totalNumberOfStructVars - _totalNumberOfStructEnregVars))}, " +
                    $"ratio: {formatFloat((float)_totalNumberOfStructEnregVars / _totalNumberOfStructVars, "F2")}\n");
            }
            var primitiveLocals = unchecked(nonStructVars - nonStructEnregVars);
            if (primitiveLocals != 0)
            {
                writer.Write($"total number of primitive locals: {unchecked((int)nonStructVars)}, " +
                    $"number of enregistered: {unchecked((int)nonStructEnregVars)}, " +
                    $"notEnreg: {unchecked((int)primitiveLocals)}, " +
                    $"ratio: {formatFloat((float)nonStructEnregVars / nonStructVars, "F2")}\n");
            }
            if (notEnreg == 0)
            {
                writer.Write("All locals are enregistered.\n");
                return;
            }

            PrintStat(writer, "m_addrExposed", _addrExposed, notEnreg);
            PrintStat(writer, "m_hiddenStructArg", _hiddenStructArg, notEnreg);
            PrintStat(writer, "m_dontEnregStructs", _dontEnregStructs, notEnreg);
            PrintStat(writer, "m_notRegSizeStruct", _notRegSizeStruct, notEnreg);
            PrintStat(writer, "m_localField", _localField, notEnreg);
            PrintStat(writer, "m_VMNeedsStackAddr", _vmNeedsStackAddr, notEnreg);
            PrintStat(writer, "m_liveInOutHndlr", _liveInOutHndlr, notEnreg);
            PrintStat(writer, "m_blockOp", _blockOp, notEnreg);
            PrintStat(writer, "m_structArg", _structArg, notEnreg);
            PrintStat(writer, "m_depField", _depField, notEnreg);
            PrintStat(writer, "m_noRegVars", _noRegVars, notEnreg);
#if !TARGET_64BIT
            PrintStat(writer, "m_longParamField", _longParamField, notEnreg);
#endif
            PrintStat(writer, "m_PinningRef", _pinningRef, notEnreg);
            PrintStat(writer, "m_lclAddrNode", _lclAddrNode, notEnreg);
            PrintStat(writer, "m_castTakesAddr", _castTakesAddr, notEnreg);
            PrintStat(writer, "m_storeBlkSrc", _storeBlkSrc, notEnreg);
            PrintStat(writer, "m_swizzleArg", _swizzleArg, notEnreg);
            PrintStat(writer, "m_blockOpRet", _blockOpRet, notEnreg);
            PrintStat(writer, "m_returnSpCheck", _returnSpCheck, notEnreg);
            PrintStat(writer, "m_wasmGcVisibility", _wasmGcVisibility, notEnreg);
            PrintStat(writer, "m_callSpCheck", _callSpCheck, notEnreg);
            PrintStat(writer, "m_simdUserForcesDep", _simdUserForcesDep, notEnreg);

            writer.Write("\nAddr exposed details:\n");
            if (_addrExposed == 0)
            {
                writer.Write("\nNo address exposed locals to report.\n");
                return;
            }
            PrintStat(writer, "m_parentExposed", _parentExposed, _addrExposed);
            PrintStat(writer, "m_tooConservative", _tooConservative, _addrExposed);
            PrintStat(writer, "m_escapeAddress", _escapeAddress, _addrExposed);
            PrintStat(writer, "m_wideIndir", _wideIndir, _addrExposed);
            PrintStat(writer, "m_osrExposed", _osrExposed, _addrExposed);
            PrintStat(writer, "m_stressLclFld", _stressLclFld, _addrExposed);
            PrintStat(writer, "m_dispatchRetBuf", _dispatchRetBuf, _addrExposed);
            PrintStat(writer, "m_stressPoisonImplicitByrefs", _stressPoisonImplicitByrefs, _addrExposed);
            PrintStat(writer, "m_externallyVisibleImplicitly", _externallyVisibleImplicitly, _addrExposed);
            PrintStat(writer, "m_smallTypePartialDef", _smallTypePartialDef, _addrExposed);
        }

        private static void PrintStat(TextWriter writer, string name, uint value, uint total)
        {
            if (value != 0)
            {
                writer.Write($"{name} {unchecked((int)value)}, ratio: {formatFloat((float)value / total, "F2")}\n");
            }
        }
    }
}
#endif
