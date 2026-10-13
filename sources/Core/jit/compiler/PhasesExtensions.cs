// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class PhasesExtensions
{
    extension(Phases phase)
    {
        public string Name
        {
            get
            {
                assert((uint)phase < (uint)PHASE_NUMBER_OF);
                return GetName(phase);
            }
        }

        public bool HasChildren
        {
            get
            {
                assert(s_hasChildren.Length == (int)(PHASE_NUMBER_OF));
                return s_hasChildren[(int)(phase)];
            }
        }

        public Phases Parent
        {
            get
            {
                assert(s_parents.Length == (int)(PHASE_NUMBER_OF));
                return s_parents[(int)(phase)];
            }
        }

        public bool ReportsIRSize
        {
            get
            {
                assert(s_reportsIRSize.Length == (int)(PHASE_NUMBER_OF));
                return s_reportsIRSize[(int)(phase)];
            }
        }
    }
}
