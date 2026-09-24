// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

internal readonly partial struct LocalAddressVisitor
{
    private readonly Compiler _compiler;

    internal LocalAddressVisitor(Compiler compiler)
    {
        _compiler = compiler;
    }

    internal readonly void UpdateEarlyRefCount(int lclNum, GenTree? node, GenTree? user)
    {
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        varDsc.incLvRefCntSaturating(1, RCS_EARLY);

        if (!_compiler.lvaIsImplicitByRefLocal(lclNum))
        {
            return;
        }

        // The weighted early count approximates uses as call arguments for
        // fgRetypeImplicitByRefArgs' decision to undo struct promotion.
        if ((node is not null) && (node.Oper is GT_LCL_VAR) && (user is not null) && (user.Oper is GT_CALL))
        {
            JITDUMP($"LocalAddressVisitor incrementing weighted ref count from {FMT_WT(varDsc.lvRefCntWtd(RCS_EARLY))} to {FMT_WT(varDsc.lvRefCntWtd(RCS_EARLY) + 1)} for implicit by-ref V{lclNum:D2} arg passed to call\n");
            varDsc.incLvRefCntWtd(1, RCS_EARLY);
        }
    }

    private static bool IsUnused(GenTree node, GenTree? user)
        => (user is null) || ((user.Oper is GT_COMMA) && (user.AsOp().Op1 == node));
}
