// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    public void gcVarPtrSetInit()
    {
        gcVarPtrSetCur = VarSetOps.MakeEmpty(Compiler);
        gcVarPtrList = null;
        gcVarPtrLast = null;
    }

    public void gcRegPtrSetInit()
    {
        gcRegGCrefSetCur = RBM_NONE;
        gcRegByrefSetCur = RBM_NONE;

        if (_codeGen.IsFullPtrRegMapRequired)
        {
            gcRegPtrList = null;
            gcRegPtrLast = null;
        }
        else
        {
            gcCallDescList = null;
            gcCallDescLast = null;
        }
    }
}
