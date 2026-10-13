// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    private void recordRiscVInsI(instruction ins, emitAttr attr, nint immediate)
    {
        RequireRiscV64Target();

        var code = emitInsCodeRiscV64(ins);

        switch (ins)
        {
            case INS_fence:
            {
                code |= (unchecked((uint)immediate) & 0xFFu) << 20;
                break;
            }

            case INS_j:
            {
                assert(immediate >= -1048576 && immediate < 1048576);
                code |= (unchecked((uint)(immediate >> 12)) & 0xFFu) << 12;
                code |= (unchecked((uint)(immediate >> 11)) & 0x1u) << 20;
                code |= (unchecked((uint)(immediate >> 1)) & 0x3FFu) << 21;
                code |= (unchecked((uint)(immediate >> 20)) & 0x1u) << 31;
                break;
            }

            default:
            {
                NO_WAY("illegal ins within emitIns_I!");
                return;
            }
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idAddr().iiaInstrEncode = code;
        id.idCodeSize(4);

        dispIns(id);
        appendToCurIG(id);
    }
}
