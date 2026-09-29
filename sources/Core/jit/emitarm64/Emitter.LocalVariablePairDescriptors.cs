// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        private bool _idLclVarPair;

        public bool idIsLclVarPair()
        {
            return _idLclVarPair;
        }

        public void idSetIsLclVarPair()
        {
            _idLclVarPair = true;
        }
    }

    protected sealed class instrDescLclVarPair : instrDesc
    {
        public emitLclVarAddr iiaLclVar2;

        public override int NativeLogicalSize => DescriptorSizes.LocalVarPair;
    }

    protected sealed class instrDescLclVarPairCns : instrDescCns
    {
        public emitLclVarAddr iiaLclVar2;

        public override int NativeLogicalSize => DescriptorSizes.LocalVarPairConstant;
    }
}
#endif
