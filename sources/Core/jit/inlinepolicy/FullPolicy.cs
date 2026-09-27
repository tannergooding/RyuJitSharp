// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public sealed class FullPolicy : DiscretionaryPolicy
{
    public FullPolicy(Compiler compiler, bool isPrejitRoot)
        : base(compiler, isPrejitRoot)
    {
    }

    public override string Name => nameof(FullPolicy);

    public override bool BudgetCheck()
    {
        return false;
    }

    public override void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
    {
        var strategy = _rootCompiler._inlineStrategy;
        assert(strategy is not null);

        if (unchecked((uint)_callsiteDepth) > unchecked((uint)strategy.MaxInlineDepth))
        {
            SetFailure(InlineObservation.CALLSITE_IS_TOO_DEEP);
            return;
        }

        if (unchecked((uint)_codeSize) > unchecked((uint)strategy.MaxInlineILSize))
        {
            SetFailure(InlineObservation.CALLEE_TOO_MUCH_IL);
            return;
        }

        SetCandidate(_isPrejitRoot
            ? InlineObservation.CALLEE_IS_PROFITABLE_INLINE
            : InlineObservation.CALLSITE_IS_PROFITABLE_INLINE);
    }
}
#endif
