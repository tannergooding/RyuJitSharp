// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && JIT32_GCENCODER
using System;
using System.Numerics;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.rpdArgType_t;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial struct GCInfo
{
    private const uint PasMaskBits = 32;
    private const uint PasHighestMaskBit = 0x80000000;
    private const uint PasEnumStart = uint.MaxValue;
    private const uint PasEnumLast = uint.MaxValue - 1;
    private const uint PasEnumEnd = uint.MaxValue - 2;

    private const uint CpMaxCodeDelta = 0x23;
    private const uint CpMaxArgCount = 0x02;
    private const uint CpMaxArgMask = 0x00;

    private static readonly uint[] CallPatternTable =
    [
        0x0a000200, 0x0c000200, 0x0c000201, 0x0a000300, 0x0c000300, 0x0e000200, 0x10000200, 0x0b000200,
        0x0d000200, 0x08000200, 0x0c000301, 0x11000200, 0x0e000300, 0x12000200, 0x10000300, 0x11000300,
        0x0a000201, 0x0a000100, 0x0f000200, 0x13000200, 0x08000300, 0x15000200, 0x0d000201, 0x0c000100,
        0x0d000300, 0x23000200, 0x1b000200, 0x14000200, 0x0f000300, 0x0a000700, 0x09000200, 0x12000300,
        0x16000200, 0x07000200, 0x09000300, 0x0c000700, 0x0c000600, 0x0e000100, 0x1a000200, 0x18000200,
        0x17000200, 0x1f000200, 0x13000300, 0x0a000600, 0x0e000600, 0x08000201, 0x0b000300, 0x0a000301,
        0x07000100, 0x13000100, 0x09000301, 0x19000200, 0x11000700, 0x21000200, 0x0d000202, 0x10000100,
        0x0f000600, 0x14000300, 0x0c000500, 0x08000301, 0x20000200, 0x10000700, 0x0f000100, 0x1e000200,
        0x0c000400, 0x16000300, 0x12000600, 0x22000200, 0x1d000200, 0x0c000f00, 0x0e000700, 0x0a000400,
        0x09000201, 0x10000600, 0x15000300, 0x0a000101, 0x0a000b00, 0x0c000601, 0x09000700, 0x07000300,
    ];

    private static readonly uint[] CallCommonDelta = [6, 8, 10, 12];

    private static int lookupCallPattern(uint argCount, uint regMask, uint argMask, uint codeDelta)
    {
        if ((argCount <= CpMaxArgCount) && (argMask <= CpMaxArgMask))
        {
            var patternValue = (byte)argCount |
                ((uint)(byte)regMask << 8) |
                ((uint)(byte)argMask << 16) |
                ((uint)(byte)codeDelta << 24);
            var codeDeltaFits = (byte)codeDelta == codeDelta;
            var bestDelta = 0xFFu;
            var bestPattern = 0xFFu;

            assert((uint)sizeof(uint) == 4);

            for (var index = 0; index < CallPatternTable.Length; index++)
            {
                var currentValue = CallPatternTable[index];
                if ((patternValue == currentValue) && codeDeltaFits)
                {
                    return index;
                }

                if (((patternValue ^ currentValue) & 0x00FFFFFF) == 0)
                {
                    var delta = unchecked(codeDelta - (currentValue >> 24));
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        bestPattern = (uint)index;
                    }
                }
            }

            if (bestPattern != 0xFF)
            {
                return unchecked((int)((bestDelta << 8) | bestPattern));
            }
        }

        return -1;
    }

    private static uint gceEncodeCalleeSavedRegs(regMask registers)
    {
        var encodedRegisters = 0u;
        if ((registers & (regMask)RBM_EBX) != SRBM_NONE)
        {
            encodedRegisters |= 0x04;
        }
        if ((registers & (regMask)RBM_ESI) != SRBM_NONE)
        {
            encodedRegisters |= 0x02;
        }
        if ((registers & (regMask)RBM_EDI) != SRBM_NONE)
        {
            encodedRegisters |= 0x01;
        }

        return encodedRegisters;
    }

    private static unsafe byte* gceByrefPrefixI(regPtrDsc descriptor, byte* destination)
    {
        assert(descriptor.rpdArg || (descriptor.rpdCompiler.rpdDel == 0));
        if (!descriptor.rpdArg || (descriptor.rpdArgTypeGet() == rpdARG_PUSH))
        {
            if (descriptor.rpdGCtypeGet() == GCT_BYREF)
            {
                *destination++ = 0xBF;
            }
        }

        return destination;
    }

    private sealed class PendingArgsStack
    {
        private readonly uint _maxDepth;
        private readonly byte[]? _topArray;
        private uint _depth;
        private uint _bottomMask;
        private uint _byrefBottomMask;
        private uint _pointersInTopArray;

        internal PendingArgsStack(uint maxDepth)
        {
            _maxDepth = maxDepth;
            if (_maxDepth > PasMaskBits)
            {
                _topArray = new byte[checked((int)(_maxDepth - PasMaskBits))];
            }
        }

        internal uint pasCurDepth() => _depth;

        internal uint pasArgMask()
        {
            assert(_depth <= PasMaskBits);
            return _bottomMask;
        }

        internal uint pasByrefArgMask()
        {
            assert(_depth <= PasMaskBits);
            return _byrefBottomMask;
        }

        internal void pasPush(GCtype gcType)
        {
            assert(_depth < _maxDepth);
            if (_depth < PasMaskBits)
            {
                _bottomMask <<= 1;
                _byrefBottomMask <<= 1;
                if (gcType != GCT_NONE)
                {
                    _bottomMask |= 1;
                    if (gcType == GCT_BYREF)
                    {
                        _byrefBottomMask |= 1;
                    }
                }
            }
            else
            {
                var topArray = _topArray
                    ?? throw new InvalidOperationException("The pending-argument stack is missing its overflow storage.");
                topArray[checked((int)(_depth - PasMaskBits))] = (byte)gcType;
                if (gcType != GCT_NONE)
                {
                    _pointersInTopArray++;
                }
            }

            _depth++;
        }

        internal void pasPop(uint count)
        {
            assert(_depth >= count);
            while ((_depth > PasMaskBits) && (count != 0))
            {
                var topArray = _topArray
                    ?? throw new InvalidOperationException("The pending-argument stack is missing its overflow storage.");
                var topIndex = _depth - PasMaskBits - 1;
                if ((GCtype)topArray[checked((int)topIndex)] != GCT_NONE)
                {
                    _pointersInTopArray--;
                }
                _depth--;
                count--;
            }
            if (count == 0)
            {
                return;
            }

            assert(_pointersInTopArray == 0);
            assert(count <= PasMaskBits);
            if (count == PasMaskBits)
            {
                _bottomMask = 0;
                _byrefBottomMask = 0;
                _depth = 0;
            }
            else
            {
                _bottomMask >>= checked((int)count);
                _byrefBottomMask >>= checked((int)count);
                _depth -= count;
            }
        }

        internal void pasKill(uint gcCount)
        {
            assert(gcCount != 0);
            for (var currentPosition = _depth; (currentPosition > PasMaskBits) && (gcCount != 0);
                currentPosition--)
            {
                var topArray = _topArray
                    ?? throw new InvalidOperationException("The pending-argument stack is missing its overflow storage.");
                var currentIndex = currentPosition - PasMaskBits - 1;
                if ((GCtype)topArray[checked((int)currentIndex)] != GCT_NONE)
                {
                    topArray[checked((int)currentIndex)] = (byte)GCT_NONE;
                    _pointersInTopArray--;
                    gcCount--;
                }
            }

            assert(_pointersInTopArray == 0);
            assert(gcCount <= PasMaskBits);
            for (var bit = 1u; gcCount != 0; bit <<= 1)
            {
                assert(_bottomMask != 0);
                if ((_bottomMask & bit) != 0)
                {
                    _bottomMask &= ~bit;
                    _byrefBottomMask &= ~bit;
                    gcCount--;
                }
                else
                {
                    assert(bit != PasHighestMaskBit);
                }
            }
        }

        internal bool pasHasGCptrs()
        {
            if (_depth <= PasMaskBits)
            {
                return _bottomMask != 0;
            }

            return (_bottomMask != 0) || (_pointersInTopArray != 0);
        }

        internal uint pasEnumGCoffsCount()
        {
            assert((_depth > PasMaskBits) && pasHasGCptrs());
            var count = 0u;
            for (var bit = 1u; bit != 0; bit <<= 1)
            {
                if ((_bottomMask & bit) != 0)
                {
                    count++;
                }
            }

            return count + _pointersInTopArray;
        }

        internal uint pasEnumGCoffs(uint iterator, out uint offset)
        {
            offset = 0;
            if (iterator == PasEnumLast)
            {
                return PasEnumEnd;
            }

            var index = iterator == PasEnumStart ? _depth : iterator;
            for (; index > PasMaskBits; index--)
            {
                var topArray = _topArray
                    ?? throw new InvalidOperationException("The pending-argument stack is missing its overflow storage.");
                var argument = (GCtype)topArray[checked((int)(index - PasMaskBits - 1))];
                if (argument != GCT_NONE)
                {
                    offset = unchecked((_depth - index) * TARGET_POINTER_SIZE);
                    if (argument == GCT_BYREF)
                    {
                        offset |= ByrefOffsetFlag;
                    }

                    return index - 1;
                }
            }
            if (_bottomMask == 0)
            {
                return PasEnumEnd;
            }

            index = (iterator == PasEnumStart) || (iterator >= PasMaskBits) ? 0 : iterator;
            for (var bit = 1u << checked((int)index); bit != 0; index++, bit <<= 1)
            {
                if ((_bottomMask & bit) != 0)
                {
                    var level = _depth > PasMaskBits ? _depth - PasMaskBits : 0;
                    level += index;
                    offset = unchecked(level * TARGET_POINTER_SIZE);
                    if ((_byrefBottomMask & bit) != 0)
                    {
                        offset |= ByrefOffsetFlag;
                    }

                    var remainingMask = unchecked(0u - (bit << 1));
                    return (_bottomMask & remainingMask) != 0 ? index + 1 : PasEnumLast;
                }
            }

            assert(false);
            return PasEnumEnd;
        }
    }

    private sealed class NoGCRegionEncoder
    {
        private readonly unsafe byte* _destination;
        private uint _lastSize;
        private uint _lastEndOffset = uint.MaxValue;
        internal nuint TotalSize;

        internal unsafe NoGCRegionEncoder(byte* destination)
        {
            _destination = destination;
        }

        internal unsafe bool Emit(uint offset, uint size)
        {
            uint encodedSize;
            if (offset == _lastEndOffset)
            {
                TotalSize -= encodeUnsigned(null, _lastSize);
                encodedSize = unchecked(_lastSize + size);
            }
            else
            {
                TotalSize += encodeUnsigned(_destination == null ? null : _destination + (nint)TotalSize, offset);
                encodedSize = size;
            }

            TotalSize += encodeUnsigned(_destination == null ? null : _destination + (nint)TotalSize, encodedSize);
            _lastSize = encodedSize;
            _lastEndOffset = unchecked(offset + size);
            return true;
        }
    }

    private readonly unsafe nuint gcMakeRegPtrTable(byte* destination, int mask, InfoHdr header,
        uint codeSize, ref nuint argTabOffset)
    {
        assert((mask == 0) || (mask == -1));

        var compiler = Compiler;
        nuint totalSize = 0;
        var emitArgTabOffset = (header.varPtrTableSize != 0) ||
            (header.untrackedCnt > SET_UNTRACKED_MAX) || (header.noGCRegionCnt != 0);
        if ((mask != 0) && emitArgTabOffset)
        {
            var size = encodeUnsigned(destination, unchecked((uint)argTabOffset));
            destination += size;
            totalSize += size;
        }

#if VERIFY_GC_TABLES
        if (mask == -1)
        {
            *(ushort*)destination = 0xBEEF;
            destination += sizeof(ushort);
        }
        totalSize += sizeof(ushort);
#endif

#if DEBUG
        gcCountForHeader(out var untrackedCount, out var varPtrTableSize, out var noGCRegionCount);
        assert(untrackedCount == header.untrackedCnt);
        assert(varPtrTableSize == header.varPtrTableSize);
        assert(noGCRegionCount == header.noGCRegionCnt);
#endif

        if (header.noGCRegionCnt != 0)
        {
            var encoder = new NoGCRegionEncoder(mask != 0 ? destination : null);
            _ = _codeGen.Emitter.emitGenNoGCLst(
                (_, offset, size, _, _) => encoder.Emit(offset, size),
                skipMainPrologsAndEpilogs: true);
            totalSize += encoder.TotalSize;
            if (mask == -1)
            {
                destination += (nint)encoder.TotalSize;
            }
        }

        if (header.untrackedCnt != 0)
        {
            var lastOffset = 0;
            for (var varNum = 0; varNum < compiler.lvaCount; varNum++)
            {
                ref var variable = ref compiler.lvaTable[varNum];
                if (compiler.lvaIsFieldOfDependentlyPromotedStruct(in variable))
                {
                    continue;
                }

                if (varTypeIsGC(variable.Type))
                {
                    if (!gcIsUntrackedLocalOrNonEnregisteredArg(varNum))
                    {
                        continue;
                    }

                    var offset = variable.StackOffset;
#if DOUBLE_ALIGN
                    if (compiler.genDoubleAlign && variable.lvIsParam && !variable.lvIsRegArg)
                    {
                        offset = unchecked(offset + _codeGen.genTotalFrameSize);
                    }
#endif
                    assert((unchecked(~(int)OffsetMask) % sizeof(int)) == 0);
                    if (variable.Type == TYP_BYREF)
                    {
                        offset |= (int)ByrefOffsetFlag;
                    }
                    if (variable.lvPinned)
                    {
                        offset |= (int)PinnedOffsetFlag;
                    }

                    var encodedOffset = unchecked(lastOffset - offset);
                    lastOffset = offset;
                    var size = encodeSigned(mask == 0 ? null : destination, encodedOffset);
                    if (mask == -1)
                    {
                        destination += size;
                    }
                    totalSize += size;
                }
                else if ((variable.Type == TYP_STRUCT) && variable.lvOnFrame && variable.HasGCPtr)
                {
                    var layout = variable.Layout
                        ?? throw new InvalidOperationException("A struct local on the frame must have a layout.");
                    for (var slot = 0; slot < layout.SlotCount; slot++)
                    {
                        if (!layout.IsGCPtr(slot))
                        {
                            continue;
                        }

                        var offset = unchecked((int)((uint)variable.StackOffset +
                            ((uint)slot * TARGET_POINTER_SIZE)));
#if DOUBLE_ALIGN
                        if (compiler.genDoubleAlign && variable.lvIsParam && !variable.lvIsRegArg)
                        {
                            offset = unchecked(offset + _codeGen.genTotalFrameSize);
                        }
#endif
                        if (layout.GetGCPtrType(slot) == TYP_BYREF)
                        {
                            offset |= (int)ByrefOffsetFlag;
                        }

                        var encodedOffset = unchecked(lastOffset - offset);
                        lastOffset = offset;
                        var size = encodeSigned(mask == 0 ? null : destination, encodedOffset);
                        if (mask != 0)
                        {
                            destination += size;
                        }
                        totalSize += size;
                    }
                }
            }

#if DEBUG
            assert(RegSet.tmpGetAllFree());
#endif
            for (var temp = RegSet.tmpListBeg(); temp is not null; temp = RegSet.tmpListNxt(temp))
            {
                if (!varTypeIsGC(temp.tdTempType))
                {
                    continue;
                }

                var offset = temp.tdTempOffs;
                if (temp.tdTempType == TYP_BYREF)
                {
                    offset |= (int)ByrefOffsetFlag;
                }

                var encodedOffset = unchecked(lastOffset - offset);
                lastOffset = offset;
                var size = encodeSigned(mask == 0 ? null : destination, encodedOffset);
                if (mask != 0)
                {
                    destination += size;
                }
                totalSize += size;
            }
        }

#if VERIFY_GC_TABLES
        if (mask != 0)
        {
            *(ushort*)destination = 0xCAFE;
            destination += sizeof(ushort);
        }
        totalSize += sizeof(ushort);
#endif

        if (!compiler.info.compIsStatic)
        {
            _ = gcIsUntrackedLocalOrNonEnregisteredArg(compiler.info.compThisArg);
        }

        if (header.varPtrTableSize != 0)
        {
            var lastOffset = 0u;
            for (var variable = gcVarPtrList; variable is not null; variable = variable.vpdNext)
            {
                var lowBits = variable.vpdVarNum & OffsetMask;
                var signedOffset = unchecked((int)(variable.vpdVarNum & ~OffsetMask));
                var variableOffset = signedOffset < 0
                    ? unchecked((uint)-signedOffset)
                    : (uint)signedOffset;
                variableOffset |= lowBits;
                var beginOffset = variable.vpdBegOfs;
                var endOffset = variable.vpdEndOfs;
                if (endOffset == beginOffset)
                {
                    continue;
                }

                var encodedSize = encodeUnsigned(mask == 0 ? null : destination, variableOffset);
                var encodedBegin = encodeUDelta(mask == 0 ? null : destination + encodedSize,
                    beginOffset, lastOffset);
                var encodedEnd = encodeUDelta(mask == 0 ? null : destination + encodedSize + encodedBegin,
                    endOffset, beginOffset);
                var size = (uint)(encodedSize + encodedBegin + encodedEnd);
                if (mask != 0)
                {
                    destination += size;
                }
                totalSize += size;
                lastOffset = beginOffset;
            }
        }

        argTabOffset = totalSize;

#if VERIFY_GC_TABLES
        if (mask != 0)
        {
            *(ushort*)destination = 0xBABE;
            destination += sizeof(ushort);
        }
        totalSize += sizeof(ushort);
#endif

        if ((mask == 0) && emitArgTabOffset)
        {
            totalSize += encodeUnsigned(null, unchecked((uint)argTabOffset));
        }

        var lastCodeOffset = 0u;
        if (_codeGen.Interruptible)
        {
            assert(compiler.IsFullPtrRegMapRequired);
            var pointerRegisters = 0u;
            for (var descriptor = gcRegPtrList; descriptor is not null; descriptor = descriptor.rpdNext)
            {
                var baseAddress = destination;
                var nextOffset = descriptor.rpdOffs;
                var codeDelta = unchecked(nextOffset - lastCodeOffset);
                assert(unchecked((int)codeDelta) >= 0);

                if ((codeDelta >= 8) && (codeDelta <= (64 + 7)))
                {
                    var biggerDelta = ((codeDelta - 8) & 0x38) + 8;
                    *destination++ = (byte)(0xF0 | ((biggerDelta - 8) >> 3));
                    lastCodeOffset += biggerDelta;
                    codeDelta &= 0x07;
                }

                if (codeDelta > 7)
                {
                    *destination++ = 0xB8;
                    destination += encodeUnsigned(destination, codeDelta);
                    codeDelta = 0;
                    lastCodeOffset = nextOffset;
                }

                if (descriptor.rpdArg)
                {
                    if (descriptor.rpdArgTypeGet() == rpdARG_KILL)
                    {
                        if (codeDelta != 0)
                        {
                            assert((codeDelta & 0x07) == codeDelta);
                            *destination++ = (byte)(0xC0 | (byte)codeDelta);
                            lastCodeOffset = nextOffset;
                        }

                        *destination++ = 0xFD;
                        assert(descriptor.rpdCallData.rpdPtrArg != 0);
                        destination += encodeUnsigned(destination, descriptor.rpdCallData.rpdPtrArg);
                    }
                    else if ((descriptor.rpdCallData.rpdPtrArg < 6) &&
                        (descriptor.rpdGCtypeGet() != GCT_NONE))
                    {
                        destination = gceByrefPrefixI(descriptor, destination);
                        if ((descriptor.rpdArgTypeGet() == rpdARG_PUSH) ||
                            (descriptor.rpdCallData.rpdPtrArg != 0))
                        {
                            var isPop = descriptor.rpdArgTypeGet() == rpdARG_POP;
                            *destination++ = (byte)(0x80u | (byte)codeDelta |
                                ((uint)descriptor.rpdCallData.rpdPtrArg << 3) | (isPop ? 0x40u : 0u));
                            lastCodeOffset = nextOffset;
                        }
                        else
                        {
                            assert(false);
                        }
                    }
                    else if (descriptor.rpdGCtypeGet() == GCT_NONE)
                    {
                        assert((codeDelta & 0x07) == codeDelta);
                        *destination++ = (byte)(0xB0 | (byte)codeDelta);
                        lastCodeOffset = nextOffset;
                    }
                    else
                    {
                        if (codeDelta != 0)
                        {
                            assert((codeDelta & 0x07) == codeDelta);
                            *destination++ = (byte)(0xC0 | (byte)codeDelta);
                        }

                        var isPop = descriptor.rpdArgTypeGet() == rpdARG_POP;
                        destination = gceByrefPrefixI(descriptor, destination);
                        *destination++ = (byte)(0xF8 | (isPop ? 0x04 : 0));
                        destination += encodeUnsigned(destination, descriptor.rpdCallData.rpdPtrArg);
                        lastCodeOffset = nextOffset;
                    }
                }
                else
                {
                    var registerMask = unchecked((uint)descriptor.rpdCompiler.rpdDel) & pointerRegisters;
                    while (registerMask != 0)
                    {
                        var registerBit = registerMask & unchecked(0u - registerMask);
                        pointerRegisters &= ~registerBit;
                        var registerNumber = BitOperations.TrailingZeroCount(registerBit);
                        assert(registerNumber <= 7);
                        assert(registerNumber != 4);
                        assert((codeDelta & 0x07) == codeDelta);
                        *destination++ = (byte)((registerNumber << 3) | (byte)codeDelta);
                        registerMask -= registerBit;
                        lastCodeOffset = nextOffset;
                        codeDelta = 0;
                    }

                    registerMask = unchecked((uint)descriptor.rpdCompiler.rpdAdd) & ~pointerRegisters;
                    while (registerMask != 0)
                    {
                        var registerBit = registerMask & unchecked(0u - registerMask);
                        pointerRegisters |= registerBit;
                        var registerNumber = BitOperations.TrailingZeroCount(registerBit);
                        assert(registerNumber <= 7);
                        destination = gceByrefPrefixI(descriptor, destination);
                        if (descriptor.rpdIsThis)
                        {
                            *destination++ = 0xBC;
                            assert(registerMask == registerBit);
                        }

                        assert((codeDelta & 0x07) == codeDelta);
                        *destination++ = (byte)(0x40 | (registerNumber << 3) | (byte)codeDelta);
                        registerMask -= registerBit;
                        lastCodeOffset = nextOffset;
                        codeDelta = 0;
                    }
                }

                totalSize += unchecked((nuint)(destination - baseAddress));
                if (mask == 0)
                {
                    destination = baseAddress;
                }
            }

            *destination = 0xFF;
            if (mask == -1)
            {
                destination++;
            }
            totalSize++;
        }
        else if (_codeGen.IsFramePointerUsed)
        {
            if (compiler.lvaKeepAliveAndReportThis() &&
                compiler.lvaTable[compiler.info.compThisArg].lvRegister)
            {
                var thisRegisterMask = (regMask)genRegMask(
                    compiler.lvaTable[compiler.info.compThisArg].RegNum);
                var thisPointerRegisterEncoding = gceEncodeCalleeSavedRegs(thisRegisterMask) << 4;
                if (thisPointerRegisterEncoding != 0)
                {
                    totalSize++;
                    if (mask != 0)
                    {
                        *destination++ = (byte)thisPointerRegisterEncoding;
                    }
                }
            }

            assert(!compiler.IsFullPtrRegMapRequired);
            for (var call = gcCallDescList; call is not null; call = call.cdNext)
            {
                var baseAddress = destination;
                var nextOffset = call.cdOffs;
                var codeDelta = unchecked(nextOffset - lastCodeOffset);
                assert(unchecked((int)codeDelta) >= 0);
                lastCodeOffset = nextOffset;

                var gcRefRegisterMask = gceEncodeCalleeSavedRegs(call.cdGCrefRegs);
                var byrefRegisterMask = gceEncodeCalleeSavedRegs(call.cdByrefRegs);
                assert((gcRefRegisterMask & byrefRegisterMask) == 0);
                var registerMask = gcRefRegisterMask | byrefRegisterMask;
                var byref = ((byrefRegisterMask | call.cdByrefArgMask) != 0);

                if (call.cdArgCnt != 0)
                {
                    var argumentCount = (uint)call.cdArgCnt;
                    var argumentBytes = 0u;
                    byte* argumentBytesSize = null;
                    var argumentTable = call.cdArgTable
                        ?? throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC call argument table is missing.");

                    if (mask != 0)
                    {
                        *destination++ = 0xFB;
                        *destination++ = (byte)((byrefRegisterMask << 4) | registerMask);
                        *(uint*)destination = codeDelta;
                        destination += sizeof(uint);
                        *(uint*)destination = argumentCount;
                        destination += sizeof(uint);
                        argumentBytesSize = destination;
                        destination += sizeof(uint);
                    }

                    for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
                    {
                        var size = encodeUnsigned(destination, argumentTable[checked((int)argumentIndex)]);
                        argumentBytes += size;
                        if (mask != 0)
                        {
                            destination += size;
                        }
                    }

                    if (mask == 0)
                    {
                        destination = baseAddress + 2 + 3 * sizeof(uint) + argumentBytes;
                    }
                    else
                    {
                        assert(destination == argumentBytesSize + sizeof(uint) + argumentBytes);
                        *(uint*)argumentBytesSize = argumentBytes;
                    }
                }
                else if ((codeDelta < 16) && (codeDelta != 0) && (call.cdArgMask == 0) && !byref)
                {
                    *destination++ = (byte)((registerMask << 4) | (byte)codeDelta);
                }
                else if ((codeDelta < 0x79) && (call.cdArgMask <= 0x1F) && !byref)
                {
                    *destination++ = (byte)(0x80u | (byte)codeDelta);
                    *destination++ = (byte)(call.cdArgMask | (registerMask << 5));
                }
                else if ((codeDelta <= 0x01FF) && (call.cdArgMask <= 0x0FFF) && !byref)
                {
                    *destination++ = 0xFD;
                    *destination++ = (byte)call.cdArgMask;
                    *destination++ = (byte)(((call.cdArgMask >> 4) & 0xF0u) |
                        ((uint)(byte)codeDelta & 0x0Fu));
                    *destination++ = (byte)((registerMask << 5) | ((codeDelta >> 4) & 0x1F));
                }
                else if ((codeDelta <= 0x0FF) && (call.cdArgMask <= 0x01F))
                {
                    *destination++ = 0xF9;
                    *destination++ = (byte)codeDelta;
                    *destination++ = (byte)((registerMask << 5) | call.cdArgMask);
                    *destination++ = (byte)((byrefRegisterMask << 5) | call.cdByrefArgMask);
                }
                else if (!byref)
                {
                    *destination++ = 0xFE;
                    *destination++ = (byte)((byrefRegisterMask << 4) | registerMask);
                    *(uint*)destination = codeDelta;
                    destination += sizeof(uint);
                    *(uint*)destination = call.cdArgMask;
                    destination += sizeof(uint);
                }
                else
                {
                    *destination++ = 0xFA;
                    *destination++ = (byte)((byrefRegisterMask << 4) | registerMask);
                    *(uint*)destination = codeDelta;
                    destination += sizeof(uint);
                    *(uint*)destination = call.cdArgMask;
                    destination += sizeof(uint);
                    *(uint*)destination = call.cdByrefArgMask;
                    destination += sizeof(uint);
                }

                totalSize += unchecked((nuint)(destination - baseAddress));
                if (mask == 0)
                {
                    destination = baseAddress;
                }
            }

            *destination = 0xFF;
            if (mask == -1)
            {
                destination++;
            }
            totalSize++;
        }
        else
        {
            assert(compiler.IsFullPtrRegMapRequired);
            var pendingArguments = new PendingArgsStack(
                checked((uint)_codeGen.Emitter.emitMaxStackDepth));
            for (var descriptor = gcRegPtrList; descriptor is not null; descriptor = descriptor.rpdNext)
            {
                var baseAddress = destination;
                var nextOffset = descriptor.rpdOffs;
                var codeDelta = unchecked(nextOffset - lastCodeOffset);
                assert(unchecked((int)codeDelta) >= 0);

                if (descriptor.rpdIsThis)
                {
                    var thisRegisterMask = unchecked((uint)descriptor.rpdCompiler.rpdAdd);
                    assert((thisRegisterMask != 0) &&
                        ((thisRegisterMask & (thisRegisterMask - 1)) == 0));
                    var thisRegisterNumber = BitOperations.TrailingZeroCount(thisRegisterMask);
                    switch (thisRegisterNumber)
                    {
                        case 7:
                            *destination++ = 0xF4;
                            break;
                        case 6:
                            *destination++ = 0xF5;
                            break;
                        case 3:
                            *destination++ = 0xF6;
                            break;
                        case 5:
                            *destination++ = 0xF7;
                            break;
                    }
                }

                if (descriptor.rpdArg)
                {
                    if (descriptor.rpdArgTypeGet() == rpdARG_KILL)
                    {
                        pendingArguments.pasKill(descriptor.rpdCallData.rpdPtrArg);
                    }
                    else if (descriptor.rpdCall)
                    {
                        lastCodeOffset = nextOffset;
                        var callArgumentCount = (uint)descriptor.rpdCallData.rpdPtrArg;
                        var gcRefRegisterMask = 0u;
                        var byrefRegisterMask = 0u;
                        ReadOnlySpan<regNumber> calleeSaveOrder =
                            [REG_EDI, REG_ESI, REG_EBX, REG_EBP];
                        for (var index = 0; index < calleeSaveOrder.Length; index++)
                        {
                            var registerBit = 1u << (int)(calleeSaveOrder[index] - REG_INT_FIRST);
                            if ((descriptor.rpdCallData.rpdCallGCrefRegs & registerBit) != 0)
                            {
                                gcRefRegisterMask |= 1u << index;
                            }
                            if ((descriptor.rpdCallData.rpdCallByrefRegs & registerBit) != 0)
                            {
                                byrefRegisterMask |= 1u << index;
                            }
                        }

                        assert((gcRefRegisterMask & byrefRegisterMask) == 0);
                        var registerMask = gcRefRegisterMask | byrefRegisterMask;
                        pendingArguments.pasPop(callArgumentCount);

                        if ((pendingArguments.pasCurDepth() > PasMaskBits) &&
                            pendingArguments.pasHasGCptrs())
                        {
                            var pendingCount = pendingArguments.pasEnumGCoffsCount();
                            var pendingSize = 0u;
                            byte* pendingSizeField = null;
                            if (mask != 0)
                            {
                                *destination++ = 0xF8;
                                *destination++ = (byte)((byrefRegisterMask << 4) | registerMask);
                                *(uint*)destination = codeDelta;
                                destination += sizeof(uint);
                                *(uint*)destination = callArgumentCount;
                                destination += sizeof(uint);
                                *(uint*)destination = pendingCount;
                                destination += sizeof(uint);
                                pendingSizeField = destination;
                                destination += sizeof(uint);
                            }

                            var iterator = pendingArguments.pasEnumGCoffs(PasEnumStart, out var offset);
                            for (; pendingCount != 0;
                                iterator = pendingArguments.pasEnumGCoffs(iterator, out offset), pendingCount--)
                            {
                                var size = encodeUnsigned(destination, offset);
                                pendingSize += size;
                                if (mask != 0)
                                {
                                    destination += size;
                                }
                            }
                            assert(iterator == PasEnumEnd);

                            if (mask == 0)
                            {
                                destination = baseAddress + 2 + (4 * sizeof(uint)) + pendingSize;
                            }
                            else
                            {
                                assert(destination == pendingSizeField + sizeof(uint) + pendingSize);
                                *(uint*)pendingSizeField = pendingSize;
                            }
                        }
                        else
                        {
                            var argumentMask = 0u;
                            var byrefArgumentMask = 0u;
                            if (pendingArguments.pasHasGCptrs())
                            {
                                assert(pendingArguments.pasCurDepth() <= PasMaskBits);
                                argumentMask = pendingArguments.pasArgMask();
                                byrefArgumentMask = pendingArguments.pasByrefArgMask();
                            }

                            assert((registerMask != 0) || (argumentMask != 0) ||
                                (callArgumentCount != 0) || (pendingArguments.pasCurDepth() != 0));

                            var usePopEncoding = (callArgumentCount < 4) &&
                                (registerMask == 0) && (argumentMask == 0);
                            var emittedCall = false;
                            if (!usePopEncoding)
                            {
                                var pattern = lookupCallPattern(callArgumentCount, registerMask,
                                    argumentMask, codeDelta);
                                if (pattern != -1)
                                {
                                    if (pattern > 0xFF)
                                    {
                                        codeDelta = unchecked((uint)pattern >> 8);
                                        pattern &= 0xFF;
                                        if (codeDelta >= 16)
                                        {
                                            *destination++ = 0x40;
                                            destination += encodeUnsigned(destination, codeDelta);
                                            codeDelta = 0;
                                        }
                                        else
                                        {
                                            *destination++ = (byte)(0x40 | (byte)codeDelta);
                                        }
                                    }

                                    if ((byrefRegisterMask | byrefArgumentMask) != 0)
                                    {
                                        *destination++ = 0xF0;
                                        var interiorMask = (byrefArgumentMask << 4) | byrefRegisterMask;
                                        destination += encodeUnsigned(destination, interiorMask);
                                    }

                                    assert((pattern >= 0) && (pattern < 80));
                                    *destination++ = (byte)(0x80 | pattern);
                                    emittedCall = true;
                                }

                                if (!emittedCall && (callArgumentCount <= 7) && (argumentMask <= 7))
                                {
                                    var commonDeltaIndex = -1;
                                    var maxCommonDelta = CallCommonDelta[3];
                                    if (codeDelta > maxCommonDelta)
                                    {
                                        var skipDelta = codeDelta - maxCommonDelta;
                                        if (skipDelta > 15)
                                        {
                                            *destination++ = 0x40;
                                            destination += encodeUnsigned(destination, skipDelta);
                                        }
                                        else
                                        {
                                            *destination++ = (byte)(0x40 | (byte)skipDelta);
                                        }

                                        codeDelta = maxCommonDelta;
                                        commonDeltaIndex = 3;
                                    }
                                    else
                                    {
                                        for (var index = 0; index < CallCommonDelta.Length; index++)
                                        {
                                            if (codeDelta == CallCommonDelta[index])
                                            {
                                                commonDeltaIndex = index;
                                                break;
                                            }
                                        }

                                        var minCommonDelta = CallCommonDelta[0];
                                        if ((commonDeltaIndex == -1) && (codeDelta > minCommonDelta) &&
                                            (codeDelta < maxCommonDelta))
                                        {
                                            assert((minCommonDelta + 16) > maxCommonDelta);
                                            *destination++ = (byte)(0x40 | (byte)(codeDelta - minCommonDelta));
                                            codeDelta = minCommonDelta;
                                            commonDeltaIndex = 0;
                                        }
                                    }

                                    if (commonDeltaIndex != -1)
                                    {
                                        if ((byrefRegisterMask | byrefArgumentMask) != 0)
                                        {
                                            *destination++ = 0xF0;
                                            var interiorMask = (byrefArgumentMask << 4) | byrefRegisterMask;
                                            destination += encodeUnsigned(destination, interiorMask);
                                        }

                                        *destination++ = (byte)(0xD0 | registerMask);
                                        *destination++ = (byte)(((uint)commonDeltaIndex << 6) |
                                            (callArgumentCount << 3) | argumentMask);
                                        emittedCall = true;
}
                                }
                            }

                            if (!emittedCall)
                            {
                                if (codeDelta >= 16)
                                {
                                    var retainedDelta = usePopEncoding ? 15u : 0u;
                                    *destination++ = 0x40;
                                    destination += encodeUnsigned(destination, codeDelta - retainedDelta);
                                    codeDelta = retainedDelta;
                                }

                                if ((codeDelta > 0) || usePopEncoding)
                                {
                                    if (usePopEncoding)
                                    {
                                        if ((callArgumentCount != 0) || (codeDelta != 0))
                                        {
                                            *destination++ = (byte)(0x40 |
                                                (callArgumentCount << 4) | codeDelta);
                                        }
                                    }
                                    else
                                    {
                                        *destination++ = (byte)(0x40 | (byte)codeDelta);
                                    }
                                }

                                if (!usePopEncoding)
                                {
                                    if ((byrefRegisterMask | byrefArgumentMask) != 0)
                                    {
                                        *destination++ = 0xF0;
                                        var interiorMask = (byrefArgumentMask << 4) | byrefRegisterMask;
                                        destination += encodeUnsigned(destination, interiorMask);
                                    }

                                    *destination++ = (byte)(0xE0u | registerMask);
                                    destination += encodeUnsigned(destination, callArgumentCount);
                                    destination += encodeUnsigned(destination, argumentMask);
                                }
                            }
                        }
                    }
                    else
                    {
                        lastCodeOffset = nextOffset;
                        if (descriptor.rpdArgTypeGet() == rpdARG_POP)
                        {
                            assert(descriptor.rpdCallData.rpdPtrArg == 1);
                            if (codeDelta >= 16)
                            {
                                *destination++ = 0x40;
                                destination += encodeUnsigned(destination, codeDelta - 15);
                                codeDelta = 15;
                            }

                            *destination++ = (byte)(0x50 | (byte)codeDelta);
                            pendingArguments.pasPop(1);
                        }
                        else
                        {
                            if (codeDelta >= 32)
                            {
                                *destination++ = 0x40;
                                destination += encodeUnsigned(destination, codeDelta - 31);
                                codeDelta = 31;
                            }

                            assert(codeDelta < 32);
                            *destination++ = (byte)codeDelta;
                            pendingArguments.pasPush(descriptor.rpdGCtypeGet());
                        }
                    }
                }

                totalSize += unchecked((nuint)(destination - baseAddress));
                if (mask == 0)
                {
                    destination = baseAddress;
                }
            }

            assert(pendingArguments.pasCurDepth() == 0);
            *destination = 0xFF;
            if (mask == -1)
            {
                destination++;
            }
            totalSize++;
        }

#if VERIFY_GC_TABLES
        if (mask != 0)
        {
            *(ushort*)destination = 0xBEEB;
            destination += sizeof(ushort);
        }
        totalSize += sizeof(ushort);
#endif

        return totalSize;
    }
}
#endif
