// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgFixEntryFlowForOSR()
    {
        assert(fgEntryBB is not null);
        assert(fgOSREntryBB is not null);

        // Import starts at the OSR entry. Stepping through enclosing try entries
        // is deferred until fgPostImportationCleanup.
        fgCreateNewInitBB();
        assert(fgFirstBB is not null);
        assert(fgFirstBB.Kind is BBJ_ALWAYS);
        fgRedirectEdge(ref fgFirstBB.TargetEdgeRef, fgOSREntryBB);

        fgFirstBB.bbWeight = fgCalledCount;
        fgFirstBB.CopyFlags(fgEntryBB, BBF_PROF_WEIGHT);
        if (fgCalledCount == BB_ZERO_WEIGHT)
        {
            fgFirstBB.bbSetRunRarely();
        }

        JITDUMP($"OSR: redirecting flow at method entry from {FMT_BB(fgFirstBB.bbNum)} to OSR entry {FMT_BB(fgOSREntryBB.bbNum)} for the importer\n");

        // Redirecting the entry edge leaves a different original-entry loop
        // header's profile inconsistent.
        if ((fgEntryBB.bbPreds is not null) && (fgEntryBB != fgOSREntryBB))
        {
            JITDUMP($"OSR: profile data could not be locally repaired. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
            fgPgoConsistent = false;
        }
    }
}
