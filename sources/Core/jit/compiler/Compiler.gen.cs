// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    // Record the instr offset mapping to the generated code

    public LinkedList<IPmappingDsc> genIPmappings;

    public LinkedList<RichIPMapping> genRichIPmappings;

    // Managed RetVal - A side hash table meant to record the mapping from a
    // GT_CALL node to its debug info.  This info is used to emit sequence points
    // that can be used by debugger to determine the native offset at which the
    // managed RetVal will be available.
    //
    // In fact we can store debug info in a GT_CALL node.  This was ruled out in
    // favor of a side table for two reasons: 1) We need debug info for only those
    // GT_CALL nodes (created during importation) that correspond to an IL call and
    // whose return type is other than TYP_VOID. 2) GT_CALL node is a frequently used
    // structure and IL offset is needed only when generating debuggable code. Therefore
    // it is desirable to avoid memory size penalty in retail scenarios.

    public CallSiteDebugInfoTable? genCallSite2DebugInfoMap;

    /// <summary>Local number for the return value when applicable.</summary>
    public int genReturnLocal = BAD_VAR_NUM;

    /// <summary>jumped to when not optimizing for speed.</summary>
    public BasicBlock? genReturnBB;

#if SWIFT_SUPPORT
    /// <summary>Local number for the Swift error value when applicable.</summary>
    public int genReturnErrorLocal = BAD_VAR_NUM;
#endif

    public bool IsFramePointerUsed
    {
        get
        {
            assert(codeGen is not null);
            return codeGen.IsFramePointerUsed;
        }
    }

    /// <summary>Return the normalized index to use in the EXPSET_TP for the CSE with the given CSE index.</summary>
    /// <param name="cseNum"></param>
    /// <returns></returns>
    /// <remarks>Each GenTree has a `_cseNum` field. Zero is reserved to mean this node is not a CSE, positive values indicate CSE uses, and negative values indicate CSE defs. The caller must pass a non-zero positive value, as from GET_CSE_INDEX().</remarks>
    public static int genCseNum2Bit(int cseNum)
    {
        assert((cseNum > 0) && (cseNum <= MAX_CSE_CNT));
        return cseNum - 1;
    }
}
