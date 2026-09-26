// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void writeLocalReg(GenTreeLclVar tree, uint localNumber, regNumber register)
    {
        assert((tree.LclNum == localNumber) == !tree.IsMultiReg);
        if (tree.LclNum == localNumber)
        {
            tree.RegNum = register;
        }
        else
        {
            assert(_compiler.lvaEnregMultiRegVars);
            ref var parent = ref _compiler.lvaGetDesc(tree.LclNum);
            assert(parent.lvPromoted);
            var registerIndex = checked((int)localNumber - parent.lvFieldLclStart);
            assert((uint)registerIndex < MAX_MULTIREG_COUNT);
            tree.SetRegNumByIdx(register, checked((byte)registerIndex));
        }
    }
}
