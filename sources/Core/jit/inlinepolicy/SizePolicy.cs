// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public sealed class SizePolicy : DiscretionaryPolicy
{
    public SizePolicy(Compiler compiler, bool isPrejitRoot)
        : base(compiler, isPrejitRoot)
    {
    }

    public override string Name => nameof(SizePolicy);

    public override void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
    {
        MethodInfoObservations(methodInfo);
        EstimateCodeSize();

        var strategy = _rootCompiler._inlineStrategy;
        assert(strategy is not null);
        var initialSize = strategy.InitialSizeEstimate;
        var currentSize = strategy.CurrentSizeEstimate;
        var newSize = unchecked(currentSize + _modelCodeSizeEstimate);

        if (newSize <= initialSize)
        {
            _rootCompiler.JITLOG(LL_INFO100000,
                $"Inline profitable, root size estimate {newSize / SIZE_SCALE} is less than initial size {initialSize / SIZE_SCALE}\n");
            SetCandidate(_isPrejitRoot
                ? InlineObservation.CALLEE_IS_SIZE_DECREASING_INLINE
                : InlineObservation.CALLSITE_IS_SIZE_DECREASING_INLINE);
        }
        else if (_isPrejitRoot)
        {
            SetNever(InlineObservation.CALLEE_NOT_PROFITABLE_INLINE);
        }
        else
        {
            SetFailure(InlineObservation.CALLSITE_NOT_PROFITABLE_INLINE);
        }
    }
}
#endif
