// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class GenTreePhi
{
    public struct UseList : IEnumerable<Use>
    {
        private Use? _firstUse;

        public UseList(Use? uses)
        {
            _firstUse = uses;
        }

        public readonly UseEnumerator GetEnumerator() => new UseEnumerator(_firstUse);

        readonly IEnumerator<Use> IEnumerable<Use>.GetEnumerator() => GetEnumerator();

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
