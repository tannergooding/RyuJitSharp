// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTreePhi
{
    public sealed class Use
    {
        private GenTree _node;
        private Use? _next;

        public Use(GenTree node, Use? next = null)
        {
            assert(node.Oper is GT_PHI_ARG);
            _node = node;
            _next = next;
        }

        public Use? Next
        {
            get
            {
                return _next;
            }

            set
            {
                _next = value;
            }
        }

#nullable disable
        public ref Use NextRef => ref _next;
#nullable restore

        public GenTree Node
        {
            get
            {
                assert(_node.Oper is GT_PHI_ARG);
                return _node;
            }

            set
            {
                assert(value.Oper is GT_PHI_ARG);
                _node = value;
            }
        }

#nullable disable
        public ref GenTree NodeRef => ref _node;
#nullable restore
    }
}
