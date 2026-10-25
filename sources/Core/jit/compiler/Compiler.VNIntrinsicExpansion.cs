// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Based on the RyuJIT compiler from dotnet/runtime, helperexpansion.cpp.

using System;
using System.Runtime.InteropServices;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private readonly struct VNIntrinsicExpansionVisitor(Compiler compiler)
        : IHelperExpansionVisitor<VNIntrinsicExpansionVisitor>
    {
        public unsafe bool Expand(ref BasicBlock block, Statement statement, GenTreeCall call)
            => compiler.fgVNBasedIntrinsicExpansionForCall(ref block, statement, call);
    }

    public unsafe PhaseStatus fgVNBasedIntrinsicExpansion()
    {
        if (!MethodHasSpecialIntrinsics || opts.OptimizationDisabled)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (opts.jitFlags->IsSet(JitFlags.JIT_FLAG_SIZE_OPT))
        {
            JITDUMP("Optimized for size - bail out.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var visitor = new VNIntrinsicExpansionVisitor(this);
        return fgExpandHelper(ref visitor, skipRarelyRunBlocks: true);
    }

    private unsafe bool fgVNBasedIntrinsicExpansionForCall(ref BasicBlock block, Statement stmt, GenTreeCall call)
    {
        if (!call.IsSpecialIntrinsic())
        {
            return false;
        }

        if (lookupNamedIntrinsic(call._callMethHnd) is NI_System_Text_UTF8Encoding_UTF8EncodingSealed_ReadUtf8)
        {
            return fgVNBasedIntrinsicExpansionForCall_ReadUtf8(ref block, stmt, call);
        }

        return false;
    }

    private unsafe bool fgVNBasedIntrinsicExpansionForCall_ReadUtf8(ref BasicBlock block, Statement stmt, GenTreeCall call)
    {
        assert(call.Args.CountUserArgs() == 4);

        var srcPtr = call.Args.GetUserArgByIndex(0)!.Node;
        var srcLen = call.Args.GetUserArgByIndex(1)!.Node;
        if (!srcLen._vnPair.BothEqual() || !vnStore!.IsVNInt32Constant(srcLen._vnPair.Liberal))
        {
            JITDUMP("ReadUtf8: srcLen is not constant\n");
            return false;
        }

        const int maxU16BufferSizeInChars = 256;
        var srcLenCnsU16 = unchecked((uint)vnStore.GetConstantInt32(srcLen._vnPair.Liberal));
        if ((srcLenCnsU16 == 0) || (srcLenCnsU16 > maxU16BufferSizeInChars))
        {
            JITDUMP("ReadUtf8: srcLenCns is 0 or > MaxPossibleUnrollThreshold\n");
            return false;
        }

        if (!GetImmutableDataFromAddress(srcPtr, (int)(srcLenCnsU16 * sizeof(ushort)), out var sourceData) ||
            sourceData is null)
        {
            JITDUMP("ReadUtf8: source data is not available at compile time\n");
            return false;
        }

        var bufferU16 = MemoryMarshal.Cast<byte, ushort>(sourceData);

        const int maxU8BufferSizeInBytes = 256;
        Span<byte> bufferU8 = stackalloc byte[maxU8BufferSizeInBytes];
        var srcLenU8 = ConvertReadUtf8Constant(bufferU16[..(int)srcLenCnsU16], bufferU8);
        if (srcLenU8 <= 0)
        {
            JITDUMP("ReadUtf8: minipal_convert_utf16_to_utf8 returned <= 0\n");
            return false;
        }

        assert(srcLenU8 <= maxU8BufferSizeInBytes);
        if (srcLenU8 > GetUnrollThreshold(Memcpy))
        {
            JITDUMP("ReadUtf8: srcLenU8 is out of unrollable range\n");
            return false;
        }

        var debugInfo = stmt.DebugInfo;
        var resultLclNum = SplitAtTreeAndReplaceItWithLocal(block, stmt, call, out var prevBb, out block);
        call._callMoreFlags &= ~GTF_CALL_M_SPECIAL_INTRINSIC;

        var srcLenU8Node = gtNewIconNode(TYP_INT, srcLenU8);
        fgValueNumberTreeConst(srcLenU8Node);

        var lengthCheckBb = fgNewBBafter(BBJ_COND, prevBb, extendRegion: true);
        lengthCheckBb.SetFlags(BBF_INTERNAL);

        var bytesWrittenDefaultVal = gtNewStoreLclVarNode(resultLclNum, gtNewIconNode(TYP_INT, -1));
        fgInsertStmtAtEnd(lengthCheckBb, fgNewStmtFromTree(bytesWrittenDefaultVal, di: debugInfo));

        var dstLen = call.Args.GetUserArgByIndex(3)!.Node;
        var clonedDstLen = gtCloneExpr(dstLen);
        if (clonedDstLen is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.CannotCloneReadUtf8DestinationLength);
            return default!;
        }

        var lengthCheck = gtNewBinaryNode(GT_LT, TYP_INT, clonedDstLen, srcLenU8Node);
        lengthCheck.Flags |= GTF_RELOP_JMP_USED;
        var lengthCheckStmt = fgNewStmtFromTree(gtNewUnaryNode(GT_JTRUE, TYP_VOID, lengthCheck), di: debugInfo);
        fgInsertStmtAtEnd(lengthCheckBb, lengthCheckStmt);
        lengthCheckBb.bbCodeOffs = block.bbCodeOffsEnd;
        lengthCheckBb.bbCodeOffsEnd = block.bbCodeOffsEnd;

        var fastpathBb = fgNewBBafter(BBJ_ALWAYS, lengthCheckBb, extendRegion: true);
        fastpathBb.SetFlags(BBF_INTERNAL);

        var maxLoadType = roundDownMaxType(srcLenU8);
        var chunkSize = maxLoadType.Size;
        assert(chunkSize > 0);
        var iterations = (srcLenU8 + chunkSize - 1) / chunkSize;
        var dstPtr = call.Args.GetUserArgByIndex(2)!.Node;

        for (var i = 0; i < iterations; i++)
        {
            var offset = i == iterations - 1 ? srcLenU8 - chunkSize : i * chunkSize;
            var offsetNode = gtNewIconNode(TYP_I_IMPL, offset);
            fgValueNumberTreeConst(offsetNode);

            var utf8cnsChunkNode = gtNewGenericCon(maxLoadType, bufferU8.Slice(offset, chunkSize));
            fgValueNumberTreeConst(utf8cnsChunkNode);

            var clonedDstPtr = gtCloneExpr(dstPtr);
            if (clonedDstPtr is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.CannotCloneReadUtf8DestinationPointer);
                return default!;
            }

            var dstAddOffsetNode = gtNewBinaryNode(GT_ADD, dstPtr.Type, clonedDstPtr, offsetNode);
            var storeInd = gtNewStoreIndNode(maxLoadType, dstAddOffsetNode, utf8cnsChunkNode);
            fgInsertStmtAtEnd(fastpathBb, fgNewStmtFromTree(storeInd, di: debugInfo));
        }

        var clonedSrcLen = gtCloneExpr(srcLenU8Node);
        if (clonedSrcLen is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.CannotCloneReadUtf8ByteLength);
            return default!;
        }

        var finalStmt = fgNewStmtFromTree(gtNewStoreLclVarNode(resultLclNum, clonedSrcLen), di: debugInfo);
        fgInsertStmtAtEnd(fastpathBb, finalStmt);
        fastpathBb.bbCodeOffs = block.bbCodeOffsEnd;
        fastpathBb.bbCodeOffsEnd = block.bbCodeOffsEnd;

        fgRedirectEdge(ref prevBb.TargetEdgeRef, lengthCheckBb);
        lengthCheckBb.inheritWeight(prevBb);
        assert(prevBb.JumpsToNext);

        var trueEdge = fgAddRefPred(block, lengthCheckBb);
        var falseEdge = fgAddRefPred(fastpathBb, lengthCheckBb);
        lengthCheckBb.SetCond(trueEdge, falseEdge);
        trueEdge.Likelihood = 1.0;
        falseEdge.Likelihood = 0.0;
        if (lengthCheckBb.hasProfileWeight)
        {
            fastpathBb.setBBProfileWeight(falseEdge.LikelyWeight);
        }

        fastpathBb.TargetEdge = fgAddRefPred(block, fastpathBb);
        assert(fastpathBb.JumpsToNext);
        block.inheritWeight(prevBb);

        assert(BasicBlock.sameEHRegion(prevBb, block));
        assert(BasicBlock.sameEHRegion(prevBb, lengthCheckBb));
        assert(BasicBlock.sameEHRegion(prevBb, fastpathBb));

        if (fgCanCompactBlock(prevBb))
        {
            fgCompactBlock(prevBb);
        }

        JITDUMP("ReadUtf8: succesfully expanded!\n");
        return true;
    }

    internal static int ConvertReadUtf8Constant(ReadOnlySpan<ushort> source, Span<byte> destination)
    {
        var written = 0;
        for (var index = 0; index < source.Length; index++)
        {
            var codePoint = (int)source[index];
            var replacement = false;
            if (char.IsHighSurrogate((char)codePoint) && (index + 1 < source.Length) &&
                char.IsLowSurrogate((char)source[index + 1]))
            {
                codePoint = char.ConvertToUtf32((char)codePoint, (char)source[++index]);
            }
            else if (char.IsSurrogate((char)codePoint))
            {
                // minipal's default replacement fallback emits one U+FFFD per unmatched UTF-16 code unit.
                codePoint = 0xFFFD;
                replacement = true;
            }

            var size = codePoint < 0x80 ? 1 : codePoint < 0x800 ? 2 : codePoint < 0x10000 ? 3 : 4;
            if (size > destination.Length - written)
            {
                // GetBytes backs up a pending fallback character, not the input pointer.
                // When the final input was an unmatched surrogate, it returns the written prefix.
                if (replacement && (index == source.Length - 1))
                {
                    return written;
                }

                return 0;
            }

            if (size is 1)
            {
                destination[written] = (byte)codePoint;
            }
            else if (size is 2)
            {
                destination[written] = (byte)(0xC0 | (codePoint >> 6));
                destination[written + 1] = (byte)(0x80 | (codePoint & 0x3F));
            }
            else if (size is 3)
            {
                destination[written] = (byte)(0xE0 | (codePoint >> 12));
                destination[written + 1] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                destination[written + 2] = (byte)(0x80 | (codePoint & 0x3F));
            }
            else
            {
                destination[written] = (byte)(0xF0 | (codePoint >> 18));
                destination[written + 1] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
                destination[written + 2] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                destination[written + 3] = (byte)(0x80 | (codePoint & 0x3F));
            }

            written += size;
        }

        return written;
    }
}
