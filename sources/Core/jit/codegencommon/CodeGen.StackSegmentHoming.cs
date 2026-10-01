// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_WASM && (SWIFT_SUPPORT || TARGET_RISCV64 || TARGET_LOONGARCH64)
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genHomeStackSegment(int lclNum, in AbiPassingSegment segment, regNumber initReg,
        ref bool initRegStillZeroed)
    {
        fixed (bool* state = &initRegStillZeroed)
        {
            genHomeStackSegment(lclNum, in segment, initReg, state);
        }
    }

    public unsafe void genHomeStackSegment(int lclNum, in AbiPassingSegment segment, regNumber initReg)
    {
        genHomeStackSegment(lclNum, in segment, initReg, null);
    }

    private unsafe void genHomeStackSegment(int lclNum, in AbiPassingSegment segment, regNumber initReg,
        bool* initRegStillZeroed)
    {
        var loadType = TYP_UNDEF;
        switch (segment.Size)
        {
            case 1:
            {
                loadType = TYP_UBYTE;
                break;
            }
            case 2:
            {
                loadType = TYP_USHORT;
                break;
            }
            case 3:
            case 4:
            {
                loadType = TYP_INT;
                break;
            }
            case 5:
            case 6:
            case 7:
            case 8:
            {
                loadType = TYP_LONG;
                break;
            }
            default:
            {
                assert(false, "Unexpected segment size for struct parameter not passed implicitly by ref");
                return;
            }
        }

        var size = loadType.EmitSize;
        var loadOffset = segment.StackOffset;
        if (IsFramePointerUsed)
        {
            loadOffset -= genCallerSPtoFPdelta;
        }
        else
        {
            loadOffset -= genCallerSPtoInitialSPdelta;
        }

#if TARGET_XARCH
        Emitter.emitIns_R_AR(ins_Load(loadType), size, initReg, genFramePointerReg(), loadOffset);
#else
        genInstrWithConstant(ins_Load(loadType), size, initReg, genFramePointerReg(), loadOffset, initReg);
#endif
        Emitter.emitIns_S_R(ins_Store(loadType), size, initReg, lclNum, segment.Offset);
        if (initRegStillZeroed != null)
        {
            *initRegStillZeroed = false;
        }
    }
}
#endif
