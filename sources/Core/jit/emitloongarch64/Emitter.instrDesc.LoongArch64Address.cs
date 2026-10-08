// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        private BasicBlock? _loongArchBBlabel;
        private insGroup? _loongArchIGlabel;

        internal BasicBlock? LoongArchBBlabel
        {
            get
            {
                return _loongArchBBlabel;
            }
            set
            {
                _loongArchBBlabel = value;
            }
        }

        internal insGroup? LoongArchIGlabel
        {
            get
            {
                return _loongArchIGlabel;
            }
            set
            {
                _loongArchIGlabel = value;
            }
        }
    }
}
#endif
