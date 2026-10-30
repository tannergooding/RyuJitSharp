// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitComputeCodeSizes()
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.CodeSizeComputationMissingCompiler);
            return;
        }
        assert((compiler.fgFirstColdBlock is null) == (emitFirstColdIG is null));
        if (emitFirstColdIG is not null)
        {
            emitTotalHotCodeSize = unchecked((int)emitFirstColdIG.igOffs);
            emitTotalColdCodeSize = unchecked(emitTotalCodeSize - emitTotalHotCodeSize);
        }
        else
        {
            emitTotalHotCodeSize = emitTotalCodeSize;
            emitTotalColdCodeSize = 0;
        }
        compiler.info.compTotalHotCodeSize = emitTotalHotCodeSize;
        compiler.info.compTotalColdCodeSize = emitTotalColdCodeSize;
        if (Configuration.DEBUG && compiler.verbose)
        {
            jitprintf($"\nHot  code size = 0x{emitTotalHotCodeSize:X} bytes\n");
            jitprintf($"Cold code size = 0x{emitTotalColdCodeSize:X} bytes\n");
        }
    }

    public void emitEndFN()
    {
    }
}
