// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public const int EXPSET_SZ = 64;

    /// <summary>We use the following format when printing the BasicBlock number: bbNum </summary>
    public static string FMT_BB(int bbNum) => $"BB{bbNum:D2}";

    /// <summary>Use this format for loop indices</summary>
    public const string FMT_LP = "L{0:D2}";

    /// <summary>Use this format for profile weights</summary>
    public static string FMT_WT(weight_t weight) => $"{weight:G7}";

    /// <summary>Use this format for profile weights where we want to conserve horizontal space, at the expense of displaying less precision.</summary>
    public static string FMT_WT_NARROW(weight_t weight) => $"{weight:G3}";

    /// <summary>how much a normal execute once block weighs</summary>
    public const weight_t BB_UNITY_WEIGHT = BB_UNITY_WEIGHT_UNSIGNED;

    /// <summary>how much a normal execute once block weighs</summary>
    public const int BB_UNITY_WEIGHT_UNSIGNED = 100;

    /// <summary>synthetic profile scale factor for loops</summary>
    public const weight_t BB_LOOP_WEIGHT_SCALE = 8.0;

    public const weight_t BB_ZERO_WEIGHT = 0.0;

    /// <summary>Upper bound for cold weights; used during block layout</summary>
    public const weight_t BB_COLD_WEIGHT = 0.01;

    /// <summary>maximum finite weight -- needs rethinking.</summary>
    public const weight_t BB_MAX_WEIGHT = float.MaxValue;

    /// <summary>base# to use when we have none</summary>
    public const int NO_BASE_TMP = -1;

    public const ushort MAX_XCPTN_INDEX = ushort.MaxValue - 1;

#if DEBUG
    public static FlowEdge? ShuffleHelper(uint hash, FlowEdge? res)
    {
        var head = res;

        for (FlowEdge? prev = null; res is not null; prev = res, res = res.NextPredEdge)
        {
            var blockNum = unchecked((uint)res.SourceBlock.bbNum);
            var blkHash = hash ^ (blockNum << 16) ^ blockNum;

            if ((((blkHash % 1879) & 1) != 0) && (prev is not null))
            {
                assert(head is not null);
                prev.NextPredEdge = head;
                var resNext = res.NextPredEdge;
                var headNext = head.NextPredEdge;
                head.NextPredEdge = resNext;
                res.NextPredEdge = headNext;
                (head, res) = (res, head);
            }
        }

        return head;
    }

    public static uint SsaStressHashHelper()
    {
        var hash = unchecked((uint)JitConfig.JitSsaStress);

        if (hash == 0)
        {
            return hash;
        }

        if (hash == 1)
        {
            var compiler = JitTls.Compiler;
            assert(compiler is not null);

            return unchecked((uint)compiler.info.compMethodHash());
        }

        return (hash >> 16) == 0 ? (hash << 16) | hash : hash;
    }
#endif
}
