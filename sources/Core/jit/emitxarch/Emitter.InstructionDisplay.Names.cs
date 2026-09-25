// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private string emitDisplayName(instrDesc id) => codeGen.genInsDisplayName(id);

#else
    private string emitDisplayName(instrDesc id) =>
        throw new FatalJitException(CORJIT_SKIPPED, "x86 instruction-display mnemonic port is not implemented.");
#endif

    private static string emitSizeString(emitAttr attr) => attr switch
    {
        EA_UNKNOWN => "",
        EA_1BYTE => "byte  ptr ",
        EA_2BYTE => "word  ptr ",
        EA_4BYTE => "dword ptr ",
        EA_8BYTE => "qword ptr ",
        EA_16BYTE => "xmmword ptr ",
        EA_32BYTE => "ymmword ptr ",
        EA_64BYTE => "zmmword ptr ",
        EA_GCREF => "gword ptr ",
        EA_BYREF => "bword ptr ",
        _ when EA_IS_DSP_RELOC(attr) => "rword ptr ",
        _ => throw new FatalJitException("Invalid display operand size."),
    };
}
#endif
