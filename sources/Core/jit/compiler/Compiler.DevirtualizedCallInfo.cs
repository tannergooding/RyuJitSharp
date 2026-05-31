// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Information needed to turn a resolved devirtualization target into a direct call.</summary>
    public unsafe ref struct DevirtualizedCallInfo
    {
        /// <summary>The class context or method context</summary>
        public CORINFO_CONTEXT_HANDLE tokenLookupContext;

        /// <summary>Resolved token for the target method, used by R2R.</summary>
        public ref CORINFO_RESOLVED_TOKEN resolvedToken;

        /// <summary>Resolved token for the unboxed entry, used by R2R.</summary>
        public ref CORINFO_RESOLVED_TOKEN unboxedResolvedToken;

        /// <summary>All the information needed for the instantiation parameter lookup.</summary>
        public ref CORINFO_LOOKUP instParamLookup;

        /// <summary>The devirted method signature.</summary>
        public ref CORINFO_SIG_INFO methSig;

        /// <summary>True if the receiver is known non-null.</summary>
        public bool objIsNonNull;

        /// <summary>True if the original call's null check was implicit.</summary>
        public bool hadImplicitNullCheck;

        /// <summary>True when transforming a delegate invoke into a direct call.</summary>
        public bool isDelegateCall;

        /// <summary>True for explicit tail calls.</summary>
        public bool isExplicitTailCall;

        /// <summary>True if the receiver type is exact.</summary>
        public bool objClassIsExact;

        /// <summary>True if the receiver type is final.</summary>
        public bool objClassIsFinal;

        /// <summary>IL offset of the original call.</summary>
        public IL_OFFSET ilOffset;
    }
}
