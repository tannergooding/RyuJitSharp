// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    internal static nint emitGetInsSC(instrDesc id)
    {
        if (id.idIsLargeCns())
        {
            return ((instrDescCns)id).idcCnsVal;
        }

        return id.idSmallCns();
    }

    internal nint emitGetInsSCForTarget(instrDesc id)
    {
        // ARM32 local-variable immediates depend on the active frame layout.
        if (Configuration.TARGET_ARM && id.idIsLclVar())
        {
            var compiler = _compiler;
            if (compiler is null)
            {
                FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.ARM32LocalVariableImmediateResolutionRequiresAnActiveCompiler
    );
                return default!;
            }
            var varNum = id.idAddr().iiaLclVar.lvaVarNum();
            var offs = unchecked((int)id.idAddr().iiaLclVar.lvaOffset());

            var adr = compiler.lvaFrameAddress(varNum, id.idIsLclFPBase(), out _, offs,
                CodeGen.instIsFP(id.idIns()));
            var displacement = unchecked(adr + offs);
            if (id.idIns() is INS_sub or INS_subw)
            {
                displacement = unchecked(-displacement);
            }

            return displacement;
        }

        return emitGetInsSC(id);
    }
}
