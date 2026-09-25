// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct FuncInfoDsc
{
    public FuncKind funKind;
    public byte funFlags;
    public ushort funEHIndex;

#if !HAS_FIXED_REGISTER_SET
    public regNumber funStackPointerReg;
    public regNumber funFramePointerReg;
#endif

    public readonly ref EHblkDsc GetEHDesc(Compiler compiler)
    {
        assert(funKind != FuncKind.FUNC_ROOT);
        return ref compiler.ehGetDsc(funEHIndex);
    }

    public readonly BasicBlock GetStartBlock(Compiler compiler)
    {
        if (funKind == FuncKind.FUNC_ROOT)
        {
            assert(compiler.fgFirstBB is not null);
            return compiler.fgFirstBB;
        }

        ref var descriptor = ref GetEHDesc(compiler);
        if (funKind == FuncKind.FUNC_FILTER)
        {
            assert(descriptor.HasFilter);
            return descriptor.ebdFilter;
        }

        assert(funKind == FuncKind.FUNC_HANDLER);
        return descriptor.ebdHndBeg;
    }
}
