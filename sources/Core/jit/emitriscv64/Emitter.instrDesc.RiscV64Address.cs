// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        // Managed references cannot overlap the explicit native address union.
        private BasicBlock? _riscvBBlabel;
        private insGroup? _riscvIGlabel;

        internal BasicBlock? RiscVBBlabel
        {
            get
            {
                return _riscvBBlabel;
            }
            set
            {
                _riscvBBlabel = value;
            }
        }

        internal insGroup? RiscVIGlabel
        {
            get
            {
                return _riscvIGlabel;
            }
            set
            {
                _riscvIGlabel = value;
            }
        }
    }
}
#endif
