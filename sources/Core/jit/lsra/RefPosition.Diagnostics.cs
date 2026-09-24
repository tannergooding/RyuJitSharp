// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Globalization;

namespace RyuJitSharp;

public sealed partial class RefPosition
{
#if DEBUG
    public void dump(LinearScan linearScan)
    {
        jitprintf($"<RefPosition #{rpNum,-3} @{nodeLocation,-3}");
        jitprintf($" {refType} ");

        if (IsPhysRegRef())
        {
            getReg().tinyDump();
        }
        else if (referent is not null)
        {
            getInterval().tinyDump();
        }

        if ((refType is not RefType.RefTypeKill) && (treeNode is not null))
        {
            jitprintf(treeNode.Oper.Name);
            if (treeNode.IsMultiRegNode)
            {
                jitprintf($"[{multiRegIdx}]");
            }
            jitprintf(" ");
        }
        jitprintf($"BB{bbNum:D2} ");

        var compiler = linearScan.ReferenceDiagnosticCompiler;
        jitprintf("regmask=");
        if (refType is RefType.RefTypeKill)
        {
            compiler.dumpRegMask(getKilledRegisters());
        }
        else
        {
            var type = (refType is RefType.RefTypeBB or RefType.RefTypeKillGCRefs)
                ? TYP_INT
                : getRegisterType();
            compiler.dumpRegMask(registerAssignment, type);
        }

        jitprintf($" minReg={minRegCandidateCount}");
        if (lastUse)
        {
            jitprintf(" last");
        }
        if (reload)
        {
            jitprintf(" reload");
        }
        if (spillAfter)
        {
            jitprintf(" spillAfter");
        }
        if (singleDefSpill)
        {
            jitprintf(" singleDefSpill");
        }
        if (writeThru)
        {
            jitprintf(" writeThru");
        }
        if (moveReg)
        {
            jitprintf(" move");
        }
        if (copyReg)
        {
            jitprintf(" copy");
        }
        if (isFixedRegRef)
        {
            jitprintf(" fixed");
        }
        if (isLocalDefUse)
        {
            jitprintf(" local");
        }
        if (delayRegFree)
        {
            jitprintf(" delay");
        }
        if (outOfOrder)
        {
            jitprintf(" outOfOrder");
        }
        if (RegOptional())
        {
            jitprintf(" regOptional");
        }
        if (refType is not RefType.RefTypeKill)
        {
            var weight = linearScan.GetReferenceDiagnosticWeight(this);
            jitprintf($" wt={weight.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        jitprintf(">\n");
    }
#endif
}
