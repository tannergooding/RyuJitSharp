// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using static RyuJitSharp.ICorDebugInfo.VarLocType;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private uint genStackLevel;

    public uint getCurrentStackLevel() => genStackLevel;

    public siVarLoc getSiVarLoc(in LclVarDsc varDsc, int offset, int stackLevel)
    {
        offset = unchecked(offset + varDsc.StackOffset);
        regNumber baseReg;

        if (!varDsc.lvFramePointerBased)
        {
            baseReg = REG_SPBASE;
            offset = unchecked(offset + stackLevel);
        }
        else
        {
            baseReg = REG_FPBASE;
        }

        return new siVarLoc(in varDsc, baseReg, offset, IsFramePointerUsed);
    }

    public struct siVarLoc
    {
        // Use the EE's storage so a location retains its native 32-bit register fields,
        // even though the compiler's managed regNumber has a byte underlying type.
        private ICorDebugInfo.VarLoc _location;

        public ICorDebugInfo.VarLocType vlType
        {
            readonly get
            {
                return _location.vlType;
            }
            set
            {
                _location.vlType = value;
            }
        }

        [UnscopedRef]
        public ref ICorDebugInfo.vlReg vlReg => ref _location.vlReg;

        [UnscopedRef]
        public ref ICorDebugInfo.vlStk vlStk => ref _location.vlStk;

        [UnscopedRef]
        public ref ICorDebugInfo.vlRegReg vlRegReg => ref _location.vlRegReg;

        [UnscopedRef]
        public ref ICorDebugInfo.vlRegStk vlRegStk => ref _location.vlRegStk;

        [UnscopedRef]
        public ref ICorDebugInfo.vlStkReg vlStkReg => ref _location.vlStkReg;

        [UnscopedRef]
        public ref ICorDebugInfo.vlStk2 vlStk2 => ref _location.vlStk2;

        [UnscopedRef]
        public ref ICorDebugInfo.vlFPstk vlFPstk => ref _location.vlFPstk;

        [UnscopedRef]
        public ref ICorDebugInfo.vlFixedVarArg vlFixedVarArg => ref _location.vlFixedVarArg;

        public siVarLoc(in LclVarDsc varDsc, regNumber baseReg, int offset, bool isFramePointerUsed)
        {
            this = default;

            if (varDsc.lvIsInReg)
            {
                siFillRegisterVarLoc(in varDsc, varDsc.GetRegisterType().ActualType, baseReg, offset,
                    isFramePointerUsed);
            }
            else
            {
                siFillStackVarLoc(in varDsc, varDsc.Type.ActualType, baseReg, offset, isFramePointerUsed);
            }
        }

        public bool vlIsInReg(regNumber reg)
        {
            var debugReg = (ICorDebugInfo.RegNum)reg;

            switch (vlType)
            {
                case VLT_REG:
                {
                    return vlReg.vlrReg == debugReg;
                }
                case VLT_REG_REG:
                {
                    return (vlRegReg.vlrrReg1 == debugReg) || (vlRegReg.vlrrReg2 == debugReg);
                }
                case VLT_REG_STK:
                {
                    return vlRegStk.vlrsReg == debugReg;
                }
                case VLT_STK_REG:
                {
                    return vlStkReg.vlsrReg == debugReg;
                }
                case VLT_STK:
                case VLT_STK2:
                case VLT_FPSTK:
                {
                    return false;
                }
                default:
                {
                    assert(false, "Bad locType");
                    return false;
                }
            }
        }

        public bool vlIsOnStack(regNumber reg, int offset)
        {
            ICorDebugInfo.RegNum actualReg;
            int actualOffset;

            switch (vlType)
            {
                case VLT_REG_STK:
                {
                    actualReg = vlRegStk.vlrsStk.vlrssBaseReg;
                    actualOffset = vlRegStk.vlrsStk.vlrssOffset;
                    break;
                }
                case VLT_STK_REG:
                {
                    actualReg = vlStkReg.vlsrStk.vlsrsBaseReg;
                    actualOffset = vlStkReg.vlsrStk.vlsrsOffset;
                    break;
                }
                case VLT_STK:
                {
                    actualReg = vlStk.vlsBaseReg;
                    actualOffset = vlStk.vlsOffset;
                    break;
                }
                case VLT_STK2:
                {
                    actualReg = vlStk2.vls2BaseReg;
                    actualOffset = vlStk2.vls2Offset;
                    break;
                }
                case VLT_REG:
                case VLT_REG_FP:
                case VLT_REG_REG:
                case VLT_FPSTK:
                {
                    return false;
                }
                default:
                {
                    assert(false, "Bad locType");
                    return false;
                }
            }

            if (actualReg == ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP)
            {
                actualReg = (ICorDebugInfo.RegNum)REG_SPBASE;
            }

            return (actualReg == (ICorDebugInfo.RegNum)reg) &&
                ((actualOffset == offset) || ((vlType == VLT_STK2) && (actualOffset == unchecked(offset - 4))));
        }

        public readonly bool vlIsOnStack() => vlType is VLT_STK or VLT_STK2 or VLT_FPSTK;

        public static ICorDebugInfo.RegNum mapRegNumToDebugRegNum(regNumber reg)
        {
#if TARGET_AMD64 || TARGET_ARM64
            const uint fpRegDebugNumBase = (uint)ICorDebugInfo.RegNum.REGNUM_FP_FIRST;
#if TARGET_AMD64
            const uint maxEncodableFpRegs = 16; // Only XMM0-XMM15.
#else
            const uint maxEncodableFpRegs = 32; // V0-V31.
#endif
#else
            // Other targets use zero-based FP indices in the debug register number.
            const uint fpRegDebugNumBase = 0;
#endif

            if (genIsValidIntReg(reg))
            {
                return (ICorDebugInfo.RegNum)reg;
            }

            if (genIsValidFloatReg(reg))
            {
                var fpIndex = (uint)(reg - REG_FP_FIRST);
#if TARGET_AMD64 || TARGET_ARM64
                if (fpIndex >= maxEncodableFpRegs)
                {
                    return ICorDebugInfo.RegNum.REGNUM_COUNT;
                }
#endif
                return (ICorDebugInfo.RegNum)(fpRegDebugNumBase + fpIndex);
            }

            return ICorDebugInfo.RegNum.REGNUM_COUNT;
        }

        public void storeVariableInRegisters(regNumber reg, regNumber otherReg)
        {
            if (otherReg == REG_NA)
            {
                if (genIsValidFloatReg(reg))
                {
#if TARGET_AMD64 || TARGET_ARM64
                    var debugReg = mapRegNumToDebugRegNum(reg);
                    if (debugReg == ICorDebugInfo.RegNum.REGNUM_COUNT)
                    {
                        vlType = VLT_INVALID;
                        return;
                    }

                    vlType = VLT_REG_FP;
                    vlReg.vlrReg = debugReg;
#else
                    vlType = VLT_REG_FP;
                    vlReg.vlrReg = (ICorDebugInfo.RegNum)(reg - REG_FP_FIRST);
#endif
                }
                else if (genIsValidIntReg(reg))
                {
                    vlType = VLT_REG;
                    vlReg.vlrReg = (ICorDebugInfo.RegNum)reg;
                }
                else
                {
                    vlType = VLT_INVALID;
                }
            }
            else
            {
#if TARGET_AMD64 || TARGET_ARM64
                var debugReg1 = mapRegNumToDebugRegNum(reg);
                var debugReg2 = mapRegNumToDebugRegNum(otherReg);
                if ((debugReg1 == ICorDebugInfo.RegNum.REGNUM_COUNT) ||
                    (debugReg2 == ICorDebugInfo.RegNum.REGNUM_COUNT))
                {
                    vlType = VLT_INVALID;
                    return;
                }

                vlType = VLT_REG_REG;
                vlRegReg.vlrrReg1 = debugReg1;
                vlRegReg.vlrrReg2 = debugReg2;
#else
                if (!genIsValidIntReg(reg) || !genIsValidIntReg(otherReg))
                {
                    vlType = VLT_INVALID;
                    return;
                }

                vlType = VLT_REG_REG;
                vlRegReg.vlrrReg1 = (ICorDebugInfo.RegNum)reg;
                vlRegReg.vlrrReg2 = (ICorDebugInfo.RegNum)otherReg;
#endif
            }
        }

        public void storeVariableOnStack(regNumber stackBaseReg, int varStackOffset)
        {
            vlType = VLT_STK;
            vlStk.vlsBaseReg = (ICorDebugInfo.RegNum)stackBaseReg;
            vlStk.vlsOffset = varStackOffset;
        }

        public static bool Equals(siVarLoc? lhs, siVarLoc? rhs)
        {
            if (!lhs.HasValue || !rhs.HasValue)
            {
                return lhs.HasValue == rhs.HasValue;
            }

            return Equals(lhs.Value, rhs.Value);
        }

        public static bool Equals(siVarLoc lhs, siVarLoc rhs)
        {
            if (lhs.vlType != rhs.vlType)
            {
                return false;
            }

            return lhs.vlType switch
            {
                VLT_STK or VLT_STK_BYREF => (lhs.vlStk.vlsBaseReg == rhs.vlStk.vlsBaseReg) &&
                    (lhs.vlStk.vlsOffset == rhs.vlStk.vlsOffset),
                VLT_STK2 => (lhs.vlStk2.vls2BaseReg == rhs.vlStk2.vls2BaseReg) &&
                    (lhs.vlStk2.vls2Offset == rhs.vlStk2.vls2Offset),
                VLT_REG or VLT_REG_FP or VLT_REG_BYREF => lhs.vlReg.vlrReg == rhs.vlReg.vlrReg,
                VLT_REG_REG => (lhs.vlRegReg.vlrrReg1 == rhs.vlRegReg.vlrrReg1) &&
                    (lhs.vlRegReg.vlrrReg2 == rhs.vlRegReg.vlrrReg2),
                VLT_REG_STK => (lhs.vlRegStk.vlrsReg == rhs.vlRegStk.vlrsReg) &&
                    (lhs.vlRegStk.vlrsStk.vlrssBaseReg == rhs.vlRegStk.vlrsStk.vlrssBaseReg) &&
                    (lhs.vlRegStk.vlrsStk.vlrssOffset == rhs.vlRegStk.vlrsStk.vlrssOffset),
                VLT_STK_REG => (lhs.vlStkReg.vlsrReg == rhs.vlStkReg.vlsrReg) &&
                    (lhs.vlStkReg.vlsrStk.vlsrsBaseReg == rhs.vlStkReg.vlsrStk.vlsrsBaseReg) &&
                    (lhs.vlStkReg.vlsrStk.vlsrsOffset == rhs.vlStkReg.vlsrStk.vlsrsOffset),
                VLT_FPSTK => lhs.vlFPstk.vlfReg == rhs.vlFPstk.vlfReg,
                VLT_FIXED_VA => lhs.vlFixedVarArg.vlfvOffset == rhs.vlFixedVarArg.vlfvOffset,
                VLT_COUNT or VLT_INVALID => true,
                _ => throw new FatalJitException("Invalid variable location kind."),
            };
        }

        private void siFillStackVarLoc(in LclVarDsc varDsc, var_types type, regNumber baseReg, int offset,
            bool isFramePointerUsed)
        {
#if DEBUG
            assert(offset != BAD_STK_OFFS);
#endif
            switch (type)
            {
                case TYP_INT:
                case TYP_REF:
                case TYP_BYREF:
                case TYP_FLOAT:
                case TYP_STRUCT:
#if TARGET_64BIT
                case TYP_LONG:
                case TYP_DOUBLE:
#endif
#if FEATURE_SIMD
                case TYP_SIMD8:
                case TYP_SIMD12:
                case TYP_SIMD16:
#if TARGET_XARCH
                case TYP_SIMD32:
                case TYP_SIMD64:
#endif
#endif
#if FEATURE_MASKED_HW_INTRINSICS
                case TYP_MASK:
#endif
                {
#if FEATURE_IMPLICIT_BYREFS
                    if (varDsc.IsImplicitByRef)
                    {
                        assert(varDsc.lvIsParam);
                        assert(varDsc.Type is TYP_I_IMPL or TYP_BYREF);
                        vlType = VLT_STK_BYREF;
                    }
                    else
#endif
                    {
                        vlType = VLT_STK;
                    }

                    vlStk.vlsBaseReg = (ICorDebugInfo.RegNum)baseReg;
                    vlStk.vlsOffset = offset;
                    if (!isFramePointerUsed && (baseReg == REG_SPBASE))
                    {
                        vlStk.vlsBaseReg = ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP;
                    }
                    break;
                }
#if !TARGET_64BIT
                case TYP_LONG:
                case TYP_DOUBLE:
                {
                    vlType = VLT_STK2;
                    vlStk2.vls2BaseReg = (ICorDebugInfo.RegNum)baseReg;
                    vlStk2.vls2Offset = offset;
                    if (!isFramePointerUsed && (baseReg == REG_SPBASE))
                    {
                        vlStk2.vls2BaseReg = ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP;
                    }
                    break;
                }
#endif
                default:
                {
                    throw new FatalJitException("Invalid stack variable type.");
                }
            }
        }

        private void siFillRegisterVarLoc(in LclVarDsc varDsc, var_types type, regNumber baseReg, int offset,
            bool isFramePointerUsed)
        {
            switch (type)
            {
                case TYP_INT:
                case TYP_REF:
                case TYP_BYREF:
#if TARGET_64BIT
                case TYP_LONG:
#endif
                {
                    vlType = VLT_REG;
                    vlReg.vlrReg = (ICorDebugInfo.RegNum)varDsc.RegNum;
                    break;
                }
#if !TARGET_64BIT
                case TYP_LONG:
                {
                    if (varDsc.OtherReg != REG_STK)
                    {
                        vlType = VLT_REG_REG;
                        vlRegReg.vlrrReg1 = (ICorDebugInfo.RegNum)varDsc.RegNum;
                        vlRegReg.vlrrReg2 = (ICorDebugInfo.RegNum)varDsc.OtherReg;
                    }
                    else
                    {
                        vlType = VLT_REG_STK;
                        vlRegStk.vlrsReg = (ICorDebugInfo.RegNum)varDsc.RegNum;
                        vlRegStk.vlrsStk.vlrssBaseReg = (ICorDebugInfo.RegNum)baseReg;
                        if (isFramePointerUsed && (baseReg == REG_SPBASE))
                        {
                            vlRegStk.vlrsStk.vlrssBaseReg = ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP;
                        }
                        vlRegStk.vlrsStk.vlrssOffset = unchecked(offset + sizeof(int));
                    }
                    break;
                }
#else
                case TYP_FLOAT:
                case TYP_DOUBLE:
                {
                    var debugReg = mapRegNumToDebugRegNum(varDsc.RegNum);
                    if (debugReg == ICorDebugInfo.RegNum.REGNUM_COUNT)
                    {
                        vlType = VLT_INVALID;
                        break;
                    }

                    vlType = VLT_REG_FP;
                    vlReg.vlrReg = debugReg;
                    break;
                }
#endif
#if !TARGET_64BIT
                case TYP_FLOAT:
                case TYP_DOUBLE:
                {
                    if (isFloatRegType(type))
                    {
                        vlType = VLT_FPSTK;
                        vlFPstk.vlfReg = (int)varDsc.RegNum;
                    }
                    break;
                }
#endif
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
                    var debugReg = mapRegNumToDebugRegNum(varDsc.RegNum);
                    if (debugReg == ICorDebugInfo.RegNum.REGNUM_COUNT)
                    {
                        vlType = VLT_INVALID;
                        break;
                    }

                    vlType = VLT_REG_FP;
                    vlReg.vlrReg = debugReg;
                    break;
                }
#endif
                default:
                {
                    throw new FatalJitException("Invalid register variable type.");
                }
            }
        }
    }
}
