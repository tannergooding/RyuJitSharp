// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.CodeGen.GenIntCastDesc.CheckKind;
using static RyuJitSharp.CodeGen.GenIntCastDesc.ExtendKind;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public readonly struct GenIntCastDesc
    {
        public enum CheckKind
        {
            CHECK_NONE,
            CHECK_SMALL_INT_RANGE,
            CHECK_POSITIVE,
            CHECK_UINT_RANGE,
            CHECK_POSITIVE_INT_RANGE,
            CHECK_INT_RANGE,
        }

        public enum ExtendKind
        {
            COPY,
            ZERO_EXTEND_SMALL_INT,
            SIGN_EXTEND_SMALL_INT,
            ZERO_EXTEND_INT,
            SIGN_EXTEND_INT,
            LOAD_ZERO_EXTEND_SMALL_INT,
            LOAD_SIGN_EXTEND_SMALL_INT,
            LOAD_ZERO_EXTEND_INT,
            LOAD_SIGN_EXTEND_INT,
            LOAD_SOURCE,
        }

        private readonly uint _checkSrcSize;
        private readonly int _checkSmallIntMin;
        private readonly int _checkSmallIntMax;

        public GenIntCastDesc(GenTreeCast cast)
        {
#if !TARGET_AMD64
            throw new FatalJitException(CORJIT_SKIPPED, "Integer cast descriptions require AMD64.");
#else
            var src = cast.CastOp;
            var srcType = src.Type.ActualType;
            var srcUnsigned = cast.IsUnsigned;
            var srcSize = srcType.Size;
            var castType = cast.CastType;
            var castUnsigned = varTypeIsUnsigned(castType);
            var castSize = castType.Size;
            var dstSize = cast.Type.ActualType.Size;
            var overflow = cast.HasOverflowCheck;
            var castIsLoad = !src.IsUsedFromReg;

            assert(castIsLoad == src.IsUsedFromMemory);
            assert((srcSize == 4) || (srcSize == TYP_I_IMPL.Size));
            assert((dstSize == 4) || (dstSize == TYP_I_IMPL.Size));
            assert(dstSize == castType.ActualType.Size);

            if (castSize < 4)
            {
                if (overflow)
                {
                    Check = CHECK_SMALL_INT_RANGE;
                    _checkSrcSize = srcSize;
                    var castNumBits = (castSize * 8) - (castUnsigned ? 0 : 1);
                    _checkSmallIntMax = (1 << castNumBits) - 1;
                    _checkSmallIntMin = castUnsigned || srcUnsigned ? 0 : -_checkSmallIntMax - 1;
                    Extend = COPY;
                    ExtendSrcSize = dstSize;
                }
                else
                {
                    Check = CHECK_NONE;
                    // A small cast truncates and then widens that small value to its actual type.
                    Extend = castUnsigned ? ZERO_EXTEND_SMALL_INT : SIGN_EXTEND_SMALL_INT;
                    ExtendSrcSize = castSize;
                }
            }
            else if (castSize > srcSize)
            {
                assert((srcSize == 4) && (castSize == 8));
                if (overflow && !srcUnsigned && castUnsigned)
                {
                    Check = CHECK_POSITIVE;
                    _checkSrcSize = 4;
                    assert((srcType == TYP_INT) && (castType == TYP_ULONG));
                    Extend = ZERO_EXTEND_INT;
                    ExtendSrcSize = 4;
                }
                else
                {
                    Check = CHECK_NONE;
                    Extend = srcUnsigned ? ZERO_EXTEND_INT : SIGN_EXTEND_INT;
                    ExtendSrcSize = 4;
                }
            }
            else if (castSize < srcSize)
            {
                assert((srcSize == 8) && (castSize == 4));
                if (overflow)
                {
                    Check = castUnsigned ? CHECK_UINT_RANGE
                        : srcUnsigned ? CHECK_POSITIVE_INT_RANGE : CHECK_INT_RANGE;
                    _checkSrcSize = 8;
                }
                else
                {
                    Check = CHECK_NONE;
                }
                Extend = COPY;
                ExtendSrcSize = 4;
            }
            else
            {
                assert(castSize == srcSize);
                if (overflow && (srcUnsigned != castUnsigned))
                {
                    Check = CHECK_POSITIVE;
                    _checkSrcSize = srcSize;
                }
                else
                {
                    Check = CHECK_NONE;
                }
                Extend = COPY;
                ExtendSrcSize = srcSize;
            }

            if (castIsLoad)
            {
                // Spill temps already hold the actual-type value with the source's extension.
                var srcLoadType = src.IsUsedFromSpillTemp ? srcType : src.Type;
                switch (Extend)
                {
                    case ZERO_EXTEND_SMALL_INT:
                    {
                        assert(varTypeIsUnsigned(srcLoadType) || (srcLoadType.Size >= castType.Size));
                        Extend = LOAD_ZERO_EXTEND_SMALL_INT;
                        ExtendSrcSize = Math.Min(srcLoadType.Size, castType.Size);
                        break;
                    }

                    case SIGN_EXTEND_SMALL_INT:
                    {
                        assert(varTypeIsSigned(srcLoadType) || (srcLoadType.Size >= castType.Size));
                        Extend = LOAD_SIGN_EXTEND_SMALL_INT;
                        ExtendSrcSize = Math.Min(srcLoadType.Size, castType.Size);
                        break;
                    }

                    case ZERO_EXTEND_INT:
                    {
                        assert(varTypeIsUnsigned(srcLoadType) || (srcLoadType == TYP_INT));
                        Extend = varTypeIsSmall(srcLoadType) ? LOAD_ZERO_EXTEND_SMALL_INT : LOAD_ZERO_EXTEND_INT;
                        ExtendSrcSize = srcLoadType.Size;
                        break;
                    }

                    case SIGN_EXTEND_INT:
                    {
                        assert(varTypeIsSigned(srcLoadType) || (srcLoadType == TYP_INT));
                        Extend = varTypeIsSmall(srcLoadType) ? LOAD_SIGN_EXTEND_SMALL_INT : LOAD_SIGN_EXTEND_INT;
                        ExtendSrcSize = srcLoadType.Size;
                        break;
                    }

                    case COPY:
                    {
                        Extend = LOAD_SOURCE;
                        ExtendSrcSize = 0;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }
#endif
        }

        public CheckKind Check { get; }

        public uint CheckSrcSize
        {
            get
            {
                assert(Check != CHECK_NONE);
                return _checkSrcSize;
            }
        }

        public int CheckSmallIntMin
        {
            get
            {
                assert(Check == CHECK_SMALL_INT_RANGE);
                return _checkSmallIntMin;
            }
        }

        public int CheckSmallIntMax
        {
            get
            {
                assert(Check == CHECK_SMALL_INT_RANGE);
                return _checkSmallIntMax;
            }
        }

        public ExtendKind Extend { get; }

        public uint ExtendSrcSize { get; }
    }
}
