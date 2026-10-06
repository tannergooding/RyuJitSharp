// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // Native LinearScan::MaxInternalCount is five, which bounds the SIMD temporary array here.
    private const int MemmoveMaxInternalRegisterCount = 5;

    public void genCodeForMemmove(GenTreeBlk node)
    {
        var srcIndir = node.Data.AsIndir();
        assert(srcIndir.IsContained && !srcIndir.Addr.IsContained);

        var dst = genConsumeReg(node.Addr);
        var src = genConsumeReg(srcIndir.Addr);
        var size = node.Layout.Size;
        var simdSize = (uint)FP_REGSIZE_BYTES;

        // Eventually, emit CPYP+CPYM+CPYE on armv9 for large sizes here.
        // Keep individual ldr/str instructions and let peephole optimization form pairs (dotnet/runtime#64815).
        if (size >= simdSize)
        {
            var allFloatRegisters = new regMaskTP(SRBM_ALLFLOAT);
            var numberOfSimdRegs = InternalRegisters.Count(node, allFloatRegisters);
            assert(numberOfSimdRegs <= MemmoveMaxInternalRegisterCount);
            Span<regNumber> tempRegs = stackalloc regNumber[MemmoveMaxInternalRegisterCount];

            for (var index = 0; index < numberOfSimdRegs; index++)
            {
                tempRegs[(int)index] = InternalRegisters.Extract(node, allFloatRegisters);
            }

            EmitMemmoveSimdLoadStore(src, tempRegs, size, simdSize, load: true);
            EmitMemmoveSimdLoadStore(dst, tempRegs, size, simdSize, load: false);
        }
        else
        {
            assert((size > 0) && (size < simdSize));

            // Use overlapping accesses for the tail, e.g. size 9 uses offsets 0 and 1 for 8-byte loads.
            var loadStoreSize = 1u << BitOperations.Log2(size);
            if (loadStoreSize == size)
            {
                var allIntRegisters = new regMaskTP(SRBM_ALLINT);
                var tempReg = InternalRegisters.GetSingle(node, allIntRegisters);
                EmitMemmoveLoadStore(true, loadStoreSize, tempReg, src, 0);
                EmitMemmoveLoadStore(false, loadStoreSize, tempReg, dst, 0);
            }
            else
            {
                assert(InternalRegisters.Count(node) == 2);

                var allIntRegisters = new regMaskTP(SRBM_ALLINT);
                var tempReg1 = InternalRegisters.Extract(node, allIntRegisters);
                var tempReg2 = InternalRegisters.Extract(node, allIntRegisters);
                var tailOffset = size - loadStoreSize;

                EmitMemmoveLoadStore(true, loadStoreSize, tempReg1, src, 0);
                EmitMemmoveLoadStore(true, loadStoreSize, tempReg2, src, tailOffset);
                EmitMemmoveLoadStore(false, loadStoreSize, tempReg1, dst, 0);
                EmitMemmoveLoadStore(false, loadStoreSize, tempReg2, dst, tailOffset);
            }
        }
    }

    private void EmitMemmoveSimdLoadStore(
        regNumber addressReg,
        ReadOnlySpan<regNumber> tempRegs,
        uint size,
        uint simdSize,
        bool load)
    {
        uint offset = 0;
        var regIndex = 0;

        do
        {
            EmitMemmoveLoadStore(load, simdSize, tempRegs[regIndex++], addressReg, offset);
            offset += simdSize;
            if (size == offset)
            {
                break;
            }

            if ((size - offset) < simdSize)
            {
                // Overlap the previous access to cover the tail; keep using SIMD for simplicity.
                // TODO-CQ: Consider using a smaller SIMD register or GPR for the remainder.
                offset = size - simdSize;
            }
        } while (true);

    }

    private void EmitMemmoveLoadStore(
        bool load,
        uint regSize,
        regNumber tempReg,
        regNumber addressReg,
        uint offset)
    {
        var memType = regSize switch
        {
            1 => TYP_UBYTE,
            2 => TYP_USHORT,
            4 => TYP_INT,
            8 => TYP_LONG,
            16 => TYP_SIMD16,
            _ => TYP_UNDEF,
        };

        if (memType == TYP_UNDEF)
        {
            unreached();
            return;
        }

        var emitter = GetEmitter();
        var displacement = unchecked((int)offset);

        if (load)
        {
            emitter.emitIns_R_R_I(ins_Load(memType), emitTypeSize(memType), tempReg, addressReg, displacement);
        }
        else
        {
            emitter.emitIns_R_R_I(ins_Store(memType), emitTypeSize(memType), tempReg, addressReg, displacement);
        }
    }
}
