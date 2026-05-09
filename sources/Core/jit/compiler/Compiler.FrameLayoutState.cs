// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const FrameLayoutState NO_FRAME_LAYOUT = FrameLayoutState.NO_FRAME_LAYOUT;
    public const FrameLayoutState INITIAL_FRAME_LAYOUT = FrameLayoutState.INITIAL_FRAME_LAYOUT;
    public const FrameLayoutState PRE_REGALLOC_FRAME_LAYOUT = FrameLayoutState.PRE_REGALLOC_FRAME_LAYOUT;
    public const FrameLayoutState REGALLOC_FRAME_LAYOUT = FrameLayoutState.REGALLOC_FRAME_LAYOUT;
    public const FrameLayoutState TENTATIVE_FRAME_LAYOUT = FrameLayoutState.TENTATIVE_FRAME_LAYOUT;
    public const FrameLayoutState FINAL_FRAME_LAYOUT = FrameLayoutState.FINAL_FRAME_LAYOUT;

    public enum FrameLayoutState
    {
        NO_FRAME_LAYOUT,
        INITIAL_FRAME_LAYOUT,
        PRE_REGALLOC_FRAME_LAYOUT,
        REGALLOC_FRAME_LAYOUT,
        TENTATIVE_FRAME_LAYOUT,
        FINAL_FRAME_LAYOUT,
    }
}
