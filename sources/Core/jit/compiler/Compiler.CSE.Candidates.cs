// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, optcse.cpp.

using System;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    internal static int CseMinCost => MIN_CSE_COST;

    internal static bool optConstantCSEEnabled()
    {
        var config = JitConfig.JitConstCSE;
        return config is CONST_CSE_ENABLE_ALL or CONST_CSE_ENABLE_ALL_NO_SHARING
#if TARGET_ARM64 || TARGET_ARM || TARGET_RISCV64
            or CONST_CSE_ENABLE_ARM_RISCV64 or CONST_CSE_ENABLE_ARM_RISCV64_NO_SHARING
#endif
            ;
    }

    private static bool optSharedConstantCSEEnabled()
    {
        var config = JitConfig.JitConstCSE;
        return config is CONST_CSE_ENABLE_ALL
#if TARGET_ARM64 || TARGET_ARM || TARGET_RISCV64
            or CONST_CSE_ENABLE_ARM_RISCV64
#endif
            ;
    }

#if TARGET_XARCH
    private const int CSE_CONST_SHARED_LOW_BITS = 16;
#else
    private const int CSE_CONST_SHARED_LOW_BITS = 12;
#endif

    private static nuint CseSharedSignBit => (nuint)1 << (IntPtr.Size * 8 - 1);

    private static bool Is_Shared_Const_CSE(nuint key) => (key & CseSharedSignBit) != 0;

    private static nuint Encode_Shared_Const_CSE_Value(nuint key)
        => CseSharedSignBit | (key >> CSE_CONST_SHARED_LOW_BITS);

    private static nuint Decode_Shared_Const_CSE_Value(nuint key)
    {
        assert(Is_Shared_Const_CSE(key));
        return (key & ~CseSharedSignBit) << CSE_CONST_SHARED_LOW_BITS;
    }

    private static int getCSEAvailBit(int cseNum) => genCseNum2Bit(cseNum) * 2;

    private static int getCSEAvailCrossCallBit(int cseNum) => getCSEAvailBit(cseNum) + 1;

    private void optValnumCSE_Init()
    {
#if DEBUG
        optCSEtab = [];
#endif
        cseLivenessTraits = null;
        cseMaskTraits = null;
        optCSEhash = new CSEdsc[(int)s_optCSEhashSizeInitial];
        optCSEhashSize = s_optCSEhashSizeInitial;
        optCSEhashMaxCountBeforeResize = optCSEhashSize * s_optCSEhashBucketSize;
        optCSEhashCount = 0;
        optCSECandidateCount = 0;
        optDoCSE = false;
    }

    private static int optCSEKeyToHashIndex(nuint key, nint hashSize)
    {
        var hash = unchecked((uint)key);
#if TARGET_64BIT
        hash ^= (uint)(key >> 32);
#endif
        hash = unchecked(hash * (uint)(hashSize + 1));
        hash >>= 7;
        return (int)(hash % (uint)hashSize);
    }

    private void optCSEstop()
    {
        if (optCSECandidateCount == 0)
        {
            return;
        }

        optCSEtab = new CSEdsc[optCSECandidateCount];
        for (var i = 0; i < optCSEhashSize; i++)
        {
            for (var descriptor = optCSEhash[i]; descriptor is not null; descriptor = descriptor.csdNextInBucket)
            {
                if ((descriptor.csdIndex != 0) && (optCSEtab[descriptor.csdIndex - 1] is null))
                {
                    optCSEtab[descriptor.csdIndex - 1] = descriptor;
                }
            }
        }

        for (var i = 0; i < optCSECandidateCount; i++)
        {
            noway_assert(optCSEtab[i] is not null);
        }
    }

    private CSEdsc optCSEfindDsc(int index)
    {
        noway_assert((index > 0) && (index <= optCSECandidateCount));
        var descriptor = optCSEtab[index - 1];
        noway_assert(descriptor is not null);
        return descriptor;
    }

    private int optValnumCSE_Index(GenTree tree, Statement stmt)
    {
        assert(vnStore is not null);
        assert(compCurBB is not null);
        var enableSharedConstCSE = optSharedConstantCSEEnabled();
        var isSharedConst = false;
        var vnLib = tree._vnPair.Liberal;
        var vnLibNorm = vnStore.VNNormalValue(vnLib);
        nuint key;

        if ((tree.Oper is GT_COMMA) && !varTypeIsStruct(tree.Type))
        {
            var vnOp2Lib = tree.AsOp().Op2._vnPair.Liberal;
            key = (nuint)(vnOp2Lib != vnLib ? vnLib : vnLibNorm);
            assert(vnLibNorm == vnStore.VNNormalValue(vnOp2Lib));
        }
        else if (enableSharedConstCSE && tree.Oper.IsIntegralConst)
        {
            assert(vnStore.IsVNConstant(vnLibNorm));
            if (!tree.AsIntConCommon().ImmedValNeedsReloc(this) &&
                (tree.IsIntegralConst(0) || !varTypeIsGC(tree.Type)))
            {
                key = Encode_Shared_Const_CSE_Value(vnStore.CoercedConstantValue<nuint>(vnLibNorm));
                isSharedConst = true;
            }
            else
            {
                key = (nuint)vnLibNorm;
            }
        }
        else
        {
            key = (nuint)vnLibNorm;
        }

        assert(isSharedConst == Is_Shared_Const_CSE(key));
        var hashIndex = optCSEKeyToHashIndex(key, optCSEhashSize);
        var newCSE = false;
        CSEdsc? descriptor;

        for (descriptor = optCSEhash[hashIndex]; descriptor is not null; descriptor = descriptor.csdNextInBucket)
        {
            if (descriptor.csdHashKey != key)
            {
                continue;
            }

            var first = descriptor.csdTreeList;
            if ((tree.Oper is GT_CNS_INT) && (tree.Type != first.tslTree.Type))
            {
                continue;
            }

            if ((first.tslNext is null) && (compCurBB == first.tslBlock))
            {
                var previous = first.tslTree;
                var previousVN = previous._vnPair.Liberal;
                if (previousVN != vnLib)
                {
                    var previousExceptions = vnStore.VNExceptionSet(previousVN);
                    var currentExceptions = vnStore.VNExceptionSet(vnLib);
                    if ((previousExceptions != currentExceptions) &&
                        vnStore.VNExcIsSubset(currentExceptions, previousExceptions))
                    {
#if DEBUG
                        JITDUMP($"Skipping CSE candidate for tree [{previous.TreeId:D6}]; " +
                            $"tree [{tree.TreeId:D6}] is a better candidate with more exceptions\n");
#endif
                        previous._cseNum = NO_CSE;
                        first.tslStmt = stmt;
                        first.tslTree = tree;
                        tree._cseNum = checked((sbyte)descriptor.csdIndex);
                        return descriptor.csdIndex;
                    }
                }
            }

            if (first.tslNext is null)
            {
                descriptor.csdIsSharedConst = isSharedConst;
            }

            var occurrence = new treeStmtLst(tree, stmt, compCurBB);
            descriptor.csdTreeLast.tslNext = occurrence;
            descriptor.csdTreeLast = occurrence;
            optDoCSE = true;
            if (descriptor.csdIndex == 0)
            {
                newCSE = true;
                break;
            }

            tree._cseNum = checked((sbyte)descriptor.csdIndex);
            return descriptor.csdIndex;
        }

        if (!newCSE)
        {
            if (optCSECandidateCount < MAX_CSE_CNT)
            {
                if (optCSEhashCount == optCSEhashMaxCountBeforeResize)
                {
                    var newSize = optCSEhashSize * s_optCSEhashGrowthFactor;
                    var newHash = new CSEdsc[(int)newSize];
                    for (var i = 0; i < optCSEhashSize; i++)
                    {
                        var current = optCSEhash[i];
                        while (current is not null)
                        {
                            var next = current.csdNextInBucket;
                            var newIndex = optCSEKeyToHashIndex(current.csdHashKey, newSize);
                            current.csdNextInBucket = newHash[newIndex];
                            newHash[newIndex] = current;
                            current = next;
                        }
                    }
                    hashIndex = optCSEKeyToHashIndex(key, newSize);
                    optCSEhash = newHash;
                    optCSEhashSize = newSize;
                    optCSEhashMaxCountBeforeResize *= s_optCSEhashGrowthFactor;
                }

                optCSEhashCount++;
                descriptor = new CSEdsc(tree, stmt, compCurBB)
                {
                    csdHashKey = key,
                    csdConstDefVN = ValueNumStore.VNForNull(),
                    defExcSetPromise = ValueNumStore.VNForEmptyExcSet(),
                    defExcSetCurrent = ValueNumStore.VNForNull(),
                    csdNextInBucket = optCSEhash[hashIndex]
                };
                optCSEhash[hashIndex] = descriptor;
            }
            return 0;
        }

        if (optCSECandidateCount == MAX_CSE_CNT)
        {
#if DEBUG
            if (verbose)
            {
                jitprintf("Exceeded the MAX_CSE_CNT, not using tree:\n");
                gtDispTree(tree);
            }
#endif
            return 0;
        }

        var cseIndex = ++optCSECandidateCount;
        assert(descriptor is not null);
        descriptor.csdIndex = cseIndex;
        descriptor.csdTreeList.tslTree._cseNum = checked((sbyte)cseIndex);
        tree._cseNum = checked((sbyte)cseIndex);
        descriptor.ComputeNumLocals(this);

#if DEBUG
        if (verbose)
        {
            jitprintf($"\nCandidate {FMT_CSE(cseIndex)}, key=");

            if (!Is_Shared_Const_CSE(key))
            {
                vnPrint((int)key, 0);
            }
            else
            {
                var value = Decode_Shared_Const_CSE_Value(key);
                var displayValue = unchecked((nuint)dspOffset((nint)value));
                jitprintf($"K_{displayValue:x}");
            }

            jitprintf($" in {FMT_BB(compCurBB.bbNum)}, [cost={tree.CostEx,2}, size={tree.CostSz,2}]: \n");
            gtDispTree(tree);
        }
#endif
        return cseIndex;
    }

    private bool optValnumCSE_Locate(CSE_HeuristicCommon heuristic)
    {
        foreach (var block in Blocks)
        {
            compCurBB = block;
            foreach (var stmt in block.Statements)
            {
                if (stmt.IsPhiDefnStmt)
                {
                    continue;
                }

                var isReturn = stmt.RootNode.Oper is GT_RETURN;
                foreach (var tree in stmt.TreeList)
                {
                    if (!heuristic.ConsiderTree(tree, isReturn))
                    {
                        continue;
                    }

                    var cseIndex = optValnumCSE_Index(tree, stmt);
                    if (cseIndex != 0)
                    {
                        noway_assert(tree._cseNum == cseIndex);
                    }
                }
            }
        }

        if (!optDoCSE)
        {
            return false;
        }

        optCSEstop();
        return true;
    }
}
