// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    protected sealed class instrDescLclVarDecl : instrDesc
    {
        public uint lclCnt;
        public WasmValueType lclType;
        public uint lclBaseIndex;

        public override int NativeLogicalSize => DescriptorSizes.WasmLocalVarDecl;
    }

    protected sealed class instrDescValTypeImm : instrDesc
    {
        public WasmValueType valType;
        public uint imm;

        public override int NativeLogicalSize => DescriptorSizes.WasmValTypeImm;
    }

    protected sealed class instrDescV128Imm : instrDesc
    {
        private readonly byte[] _bytes = new byte[16];

        public override int NativeLogicalSize => DescriptorSizes.WasmV128Imm;

        public void idV128Const(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < _bytes.Length)
            {
                throw new ArgumentException("A Wasm v128 immediate must contain at least 16 bytes.", nameof(bytes));
            }

            bytes[.._bytes.Length].CopyTo(_bytes);
        }

        public ReadOnlySpan<byte> v128Bytes => _bytes;
    }

    protected sealed class instrDescMemargLane : instrDescCns
    {
        private byte _lane;

        public override int NativeLogicalSize => DescriptorSizes.WasmMemargLane;

        public void idLaneIdx(byte lane)
        {
            _lane = lane;
        }

        public byte lane => _lane;
    }

    private instrDescLclVarDecl emitNewInstrLclVarDecl(emitAttr attr, uint localCount, WasmValueType type, int localOffset)
    {
        var id = emitAllocAnyInstr<instrDescLclVarDecl>(DescriptorSizes.WasmLocalVarDecl, attr);
        id.lclCnt = localCount;
        id.lclType = type;

        if (_debugInfoSize > 0)
        {
            id.lclBaseIndex = unchecked(GetWasmArgsCount() + (uint)localOffset);
        }

        return id;
    }

    private instrDescValTypeImm emitNewInstrValTypeImm(emitAttr attr, WasmValueType type, uint immediate)
    {
        var id = emitAllocAnyInstr<instrDescValTypeImm>(DescriptorSizes.WasmValTypeImm, attr);
        id.valType = type;
        id.imm = immediate;
        return id;
    }

    public static bool isValidSimdElemSize(uint elementSize)
    {
        return elementSize is 1 or 2 or 4 or 8;
    }

    public static bool isValidVectorIndex(byte elementSize, byte index)
    {
        assert(isValidSimdElemSize(elementSize));

        return elementSize switch
        {
            1 => index < 16,
            2 => index < 8,
            4 => index < 4,
            8 => index < 2,
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, "Unexpected Wasm SIMD element size."),
        };
    }

    public static WasmValueType emitGetLclVarDeclType(instrDesc id)
    {
        assert(id.idIsLclVarDecl());
        return ((instrDescLclVarDecl)id).lclType;
    }

    public static uint emitGetLclVarDeclCount(instrDesc id)
    {
        assert(id.idIsLclVarDecl());
        return ((instrDescLclVarDecl)id).lclCnt;
    }

    public static WasmValueType emitGetValTypeImmType(instrDesc id)
    {
        assert(id.idIsValTypeImm());
        return ((instrDescValTypeImm)id).valType;
    }

    public static uint emitGetValTypeImmImm(instrDesc id)
    {
        assert(id.idIsValTypeImm());
        return ((instrDescValTypeImm)id).imm;
    }

    public static ReadOnlySpan<byte> emitGetV128ImmValue(instrDesc id)
    {
        assert(id.idIsV128Imm());
        return ((instrDescV128Imm)id).v128Bytes;
    }

    public static byte emitGetLaneImmValue(instrDesc id)
    {
        if (id.idIsMemargLaneImm())
        {
            return ((instrDescMemargLane)id).lane;
        }

        if (id.idInsFmt() == IF_LANE)
        {
            var lane = emitGetInsSC(id);
            assert((nuint)lane <= byte.MaxValue);
            return unchecked((byte)lane);
        }

        throw new FatalJitException(CORJIT_INTERNALERROR, "Unexpected Wasm lane immediate descriptor.");
    }

    private uint GetWasmArgsCount()
    {
        assert(_compiler is not null);
        unsafe
        {
            ref var func = ref _compiler.funCurrentFunc();

            if (func.funKind != FuncKind.FUNC_ROOT)
            {
                ref var ehDescriptor = ref _compiler.ehGetDsc(func.funEHIndex);
                return ehDescriptor.HasCatchHandler ? 3u : 2u;
            }

            uint count = 0;
            for (var argLocalNum = 0; argLocalNum < _compiler.info.compArgsCount; argLocalNum++)
            {
                ref readonly var abiInfo = ref _compiler.lvaGetParameterAbiInfo(argLocalNum);
                foreach (var segment in abiInfo.Segments)
                {
                    count = Math.Max(count, regNumberExtensions.WasmRegToIndex(segment.Register) + 1);
                }
            }

            return count;
        }
    }
}
#endif
