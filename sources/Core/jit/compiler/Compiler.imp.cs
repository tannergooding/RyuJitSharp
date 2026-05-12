// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Only present for inlinees</summary>
    public InlineInfo? impInlineInfo;

    /// <summary>Size of the full stack</summary>
    protected int impStkSize;

    protected NodeToUnsignedMap? impEnumeratorGdvLocalMap;

    protected VarToLikelyClassMap? impEnumeratorLikelyTypeMap;

    /// <summary>Statements for the BB being imported.</summary>
    protected Statement? impStmtList;

    /// <summary>The last statement for the current BB.</summary>
    protected Statement? impLastStmt;

#if DEBUG
    private int impCurOpcOffs;

    private string? impCurOpcName;

    private bool impNestedStackSpill;

    /// <summary>oldest stmt added for which we did not call SetLastILOffset().</summary>
    /// <remarks>For displaying instrs with generated native code (-n:B)</remarks>
    private Statement? impLastILoffsStmt;
#endif

    /// <summary>The context used for looking up tokens.</summary>
    private unsafe CORINFO_CONTEXT_HANDLE impTokenLookupContextHandle;

    /// <summary>Debug info of current statement being imported</summary>
    /// <remarks>
    ///   <para>It gets set to contain no IL location (!impCurStmtDI.GetLocation().IsValid) after it has been set in the appended trees.</para>
    ///   <para>Then it gets updated at IL instructions for which we have to report mapping info.</para>
    ///   <para>It will always contain the current inline context.</para>
    /// </remarks>
    private DebugInfo impCurStmtDI;

    /// <summary>list of BBs currently waiting to be imported.</summary>
    private PendingDsc? impPendingList;

    /// <summary>Freed up dscs that can be reused</summary>
    private PendingDsc? impPendingFree;

    // We keep a byte-per-block map (dynamically extended) in the top-level Compiler object of a compilation.
    private List<byte> impPendingBlockMembers;

    private bool impCanReimport;

    // When we compute a "spill clique" (see above) these byte-maps are allocated to have a byte per basic
    // block, and represent the predecessor and successor members of the clique currently being computed.
    // *** Access to these will need to be locked in a parallel compiler.

    private List<byte> impSpillCliquePredMembers;

    private List<byte> impSpillCliqueSuccMembers;

    private BlockListNode? impBlockListNodeFreeList;

    /// <summary>the temp below is valid and available</summary>
    public bool impBoxTempInUse;

    /// <summary>a temporary that is used for boxing</summary>
    public int impBoxTemp;

#if DEBUG
    public int impInlinedCodeSize;
#endif

    // The Compiler that is the root of the inlining tree of which "this" is a member.
    public Compiler impInlineRoot
    {
        get
        {
            var result = this;

            if (impInlineInfo is not null)
            {
                result = impInlineInfo.InlineRoot;
            }
            return result;
        }
    }

    // One might think it is worth caching these values, but results indicate that it isn't.
    // In addition, caching them causes SuperPMI to be unable to completely encapsulate an individual method context.

    public unsafe CORINFO_CLASS_HANDLE impRefAnyClass
    {
        get
        {
            var refAnyClass = info.compCompHnd->getBuiltinClass(CLASSID_TYPED_BYREF);
            assert(refAnyClass is not null);
            return refAnyClass;
        }
    }
    public unsafe CORINFO_CLASS_HANDLE impRuntimeArgumentHandle
    {
        get
        {
            var argIteratorClass = info.compCompHnd->getBuiltinClass(CLASSID_ARGUMENT_HANDLE);
            assert(argIteratorClass is not null);
            return argIteratorClass;
        }
    }
    public unsafe CORINFO_CLASS_HANDLE impTypeHandleClass
    {
        get
        {
            var typeHandleClass = info.compCompHnd->getBuiltinClass(CLASSID_TYPE_HANDLE);
            assert(typeHandleClass is not null);
            return typeHandleClass;
        }
    }
    public unsafe CORINFO_CLASS_HANDLE impStringClass
    {
        get
        {
            var stringClass = info.compCompHnd->getBuiltinClass(CLASSID_STRING);
            assert(stringClass is not null);
            return stringClass;
        }
    }

    public unsafe CORINFO_CLASS_HANDLE impObjectClass
    {
        get
        {
            var objectClass = info.compCompHnd->getBuiltinClass(CLASSID_SYSTEM_OBJECT);
            assert(objectClass is not null);
            return objectClass;
        }
    }

    /// <summary>Check whether two argument nodes are contiguous or not.</summary>
    /// <param name="op1"></param>
    /// <param name="op2"></param>
    /// <returns>if the argument node op1 is located before argument node op2, and they are located contiguously, then return true. Otherwise, return false.</returns>
    /// <remarks>Right now this can only check field and array. In future we should add more cases.</remarks>
    public bool areArgumentsContiguous(GenTree op1, GenTree op2)
    {
        var type = op1.Type;

        if (op2.Type != type)
        {
            return false;
        }

        assert(op1.Type is not TYP_STRUCT);

        var oper = op1.Oper;

        if (op2.Oper != oper)
        {
            return false;
        }

        if (oper.IsTrueIndir)
        {
            var op1IndirAddr = op1.AsIndir().Addr;
            var op2IndirAddr = op2.AsIndir().Addr;

            var indirAddrOper = op1IndirAddr.Oper;

            if (op2IndirAddr.Oper != indirAddrOper)
            {
                return false;
            }

            if (indirAddrOper is GT_INDEX_ADDR)
            {
                return areArrayElementsContiguous(op1IndirAddr.AsIndexAddr(), op2IndirAddr.AsIndexAddr());
            }
            if (indirAddrOper is GT_FIELD_ADDR)
            {
                return areFieldsContiguous(op1IndirAddr.AsFieldAddr(), op2IndirAddr.AsFieldAddr(), type.Size);
            }
        }
        else if (oper is GT_LCL_FLD)
        {
            return areLocalFieldsContiguous(op1.AsLclFld(), op2.AsLclFld(), type.Size);
        }
        return false;
    }

    /// <summary>Check whether two array element nodes are located contiguously or not.</summary>
    /// <param name="op1"></param>
    /// <param name="op2"></param>
    /// <returns>if the array element op1 is located before array element op2, and they are contiguous, then return true. Otherwise, return false.</returns>
    public bool areArrayElementsContiguous(GenTreeIndexAddr op1, GenTreeIndexAddr op2)
    {
        // TODO-CQ:
        //   Right this can only check array element with const number as index. In future,
        //   we should consider to allow this function to check the index using expression.

        var op1Index = op1.Index;
        var op2Index = op2.Index;

        if ((op1Index.Oper is not GT_CNS_INT) || (op2Index.Oper is not GT_CNS_INT))
        {
            return false;
        }

        if ((op1Index.AsIntCon().IconVal + 1) != op2Index.AsIntCon().IconVal)
        {
            return false;
        }

        var op1Arr = op1.Arr;
        var op2Arr = op2.Arr;

        if ((op1Arr.Oper is GT_IND) && (op2Arr.Oper is GT_IND))
        {
            var op1ArrIndirAddr = op1Arr.AsIndir().Addr;
            var op2ArrIndirAddr = op2Arr.AsIndir().Addr;

            if ((op1ArrIndirAddr.Oper is not GT_FIELD_ADDR) || (op2ArrIndirAddr.Oper is not GT_FIELD_ADDR))
            {
                return false;
            }

            return areFieldAddressesTheSame(op1ArrIndirAddr.AsFieldAddr(), op2ArrIndirAddr.AsFieldAddr());
        }
        else if ((op1Arr.Oper is GT_LCL_VAR) && (op2Arr.Oper is GT_LCL_VAR))
        {
            return (op1Arr.AsLclVar().LclNum == op2Arr.AsLclVar().LclNum);
        }

        return false;
    }

    /// <summary>Check if two field address nodes reference at the same location.</summary>
    /// <param name="op1">first field address</param>
    /// <param name="op2">second field address</param>
    /// <returns>If op1's parents node and op2's parents node are at the same location, return true. Otherwise, return false</returns>
    public unsafe bool areFieldAddressesTheSame(GenTreeFieldAddr op1, GenTreeFieldAddr op2)
    {
        assert((op1.Oper is GT_FIELD_ADDR) && (op2.Oper is GT_FIELD_ADDR));

        var op1ObjRef = op1.FldObj;
        var op2ObjRef = op2.FldObj;

        while ((op1ObjRef is not null) && (op2ObjRef is not null))
        {
            assert(varTypeIsI(op1ObjRef.Type.ActualType) && varTypeIsI(op2ObjRef.Type.ActualType));

            var oper = op1ObjRef.Oper;

            if (op2ObjRef.Oper != oper)
            {
                break;
            }

            if (((oper is GT_LCL_VAR) || op1ObjRef.IsLclVarAddr) && (op1ObjRef.AsLclVarCommon().LclNum == op2ObjRef.AsLclVarCommon().LclNum))
            {
                return true;
            }

            if (oper is GT_IND)
            {
                op1ObjRef = op1ObjRef.AsIndir().Addr;
                op2ObjRef = op2ObjRef.AsIndir().Addr;
                continue;
            }

            if (oper is GT_FIELD_ADDR)
            {
                var op1FieldAddr = op1ObjRef.AsFieldAddr();
                var op2FieldAddr = op2ObjRef.AsFieldAddr();

                if (op1FieldAddr.FldHnd == op2FieldAddr.FldHnd)
                {
                    op1ObjRef = op1FieldAddr.FldObj;
                    op2ObjRef = op2FieldAddr.FldObj;
                    continue;
                }
            }
            break;
        }

        return false;
    }

    /// <summary>Check whether two fields are contiguous.</summary>
    /// <param name="op1"></param>
    /// <param name="op2"></param>
    /// <param name="fldSize"></param>
    /// <returns>If the first field is located before second field, and they are located contiguously, then return true. Otherwise, return false.</returns>
    public bool areFieldsContiguous(GenTreeFieldAddr op1, GenTreeFieldAddr op2, int fldSize)
    {
        if ((op1.FldOffset + fldSize) != op2.FldOffset)
        {
            return false;
        }
        return areFieldAddressesTheSame(op1, op2);
    }

    /// <summary>Check whether two local field are contiguous</summary>
    /// <param name="op1"></param>
    /// <param name="op2"></param>
    /// <param name="fldSize"></param>
    /// <returns>If the first field is located before second field, and they are located contiguously, then return true. Otherwise, return false.</returns>
    public bool areLocalFieldsContiguous(GenTreeLclFld op1, GenTreeLclFld op2, int fldSize)
    {
        assert(op1.Type == op2.Type);
        return (op1.LclOffs + fldSize) == op2.LclOffs;
    }

#if FEATURE_SIMD
    /// <summary>Checking whether the field belongs to a simd struct or not. If it is, return the GenTree* for the struct node, also base type, field index and simd size. If it is not, just return  nullptr. Usually if the tree node is from a simd lclvar which is not used in any simd intrinsic, then we should return nullptr, since in this case we should treat simd struct as a regular struct. However if no matter what, you just want get simd struct node, you can set the ignoreUsedInSimdIntrinsic as true. Then there will be no IsUsedInSimdIntrinsic checking, and it will return simd struct node if the struct is a simd struct.</summary>
    /// <param name="tree">This node will be checked to see this is a field which belongs to a simd struct used for simd intrinsic or not.</param>
    /// <param name="index">if the tree is used for simd intrinsic, we will set this to the index number of this field.</param>
    /// <param name="simdSize">if the tree is used for simd intrinsic, set this to the simd struct size which this tree belongs to.</param>
    /// <param name="ignoreUsedInSimdIntrinsic">If this is set to true, then this function will ignore the UsedInSimdIntrinsic check.</param>
    /// <returns>A node which points the simd lclvar tree belongs to. If the tree is not the simd instrinic related field, return nullptr.</returns>
    public GenTreeLclFld? getSimdStructFromField(GenTree tree, out int index, out int simdSize, bool ignoreUsedInSimdIntrinsic = false)
    {
        index = 0;
        simdSize = 0;

        if (tree.Oper.IsTrueIndir)
        {
            var addr = tree.AsIndir().Addr;

            if (addr.Oper is not GT_FIELD_ADDR)
            {
                return null;
            }

            var fieldAddr = addr.AsFieldAddr();

            if (!fieldAddr.IsInstance)
            {
                return null;
            }

            var objRef = fieldAddr.FldObj;

            if (objRef.IsLclVarAddr)
            {
                var lclVarAddr = objRef.AsLclFld();
                ref var varDsc = ref lvaGetDesc(lclVarAddr.LclNum);

                if (varTypeIsSimd(varDsc.Type) && (varDsc.lvIsUsedInSimdIntrinsic || ignoreUsedInSimdIntrinsic))
                {
                    var elementType = tree.Type;
                    var fieldOffset = addr.AsFieldAddr().FldOffset;
                    var elementSize = elementType.Size;

                    if (varTypeIsArithmetic(elementType) && ((fieldOffset % elementSize) == 0))
                    {
                        simdSize = varDsc.lvExactSize;
                        index = fieldOffset / elementSize;
                        return lclVarAddr;
                    }
                }
            }
        }
        return null;
    }
#endif

    /// <summary>Add the statement to the current stmts list.</summary>
    /// <param name="stmt">the statement to add.</param>
    public void impAppendStmt(Statement stmt)
    {
        if (impStmtList is null)
        {
            // The stmt is the first in the list.
            impStmtList = stmt;
        }
        else
        {
            // Append the expression statement to the existing list.
            assert(impLastStmt is not null);
            impLastStmt.NextStmt = stmt;
            stmt.PrevStmt = impLastStmt;
        }
        impLastStmt = stmt;
    }

    /// <summary>Append the given statement to the current block's tree list.</summary>
    /// <param name="stmt">The statement to add.</param>
    /// <param name="chkLevel">[0..chkLevel) is the portion of the stack which we will check for interference with stmt and spilled if needed.</param>
    /// <param name="checkConsumedDebugInfo">Whether to check for consumption of impCurStmtDI. impCurStmtDI marks the debug info of the current boundary and is set when we start importing IL at that boundary. If this parameter is true, then the function checks if 'stmt' has been associated with the current boundary, and if so, clears it so that we do not attach it to more upcoming statements.</param>
    public void impAppendStmt(Statement stmt, int chkLevel, bool checkConsumedDebugInfo = true)
    {
        if (chkLevel == CHECK_SPILL_ALL)
        {
            chkLevel = stackState.esStackDepth;
        }

        if ((chkLevel is not 0) && (chkLevel != CHECK_SPILL_NONE))
        {
            assert(chkLevel <= stackState.esStackDepth);

            // If the statement being appended has any side-effects, check the stack to see if anything
            // needs to be spilled to preserve correct ordering.
            //
            var expr = stmt.RootNode;
            var oper = expr.Oper;
            var flags = expr.Flags & GTF_GLOB_EFFECT;

            // Stores to unaliased locals require special handling. Here, we look for trees that
            // can modify them and spill the references. In doing so, we make two assumptions:
            //
            // 1. All locals which can be modified indirectly are marked as address-exposed or with
            //    "lvHasLdAddrOp" -- we will rely on "impSpillSideEffects(spillGlobEffects: true)"
            //    below to spill them.
            // 2. Trees that assign to unaliased locals are always top-level (this avoids having to
            //    walk down the tree here), and are a subset of what is recognized here.
            //
            // If any of the above are violated (say for some temps), the relevant code must spill
            // things manually.

            ref var dstVarDsc = ref Unsafe.NullRef<LclVarDsc>();

            if (oper.IsLocalStore)
            {
                dstVarDsc = lvaGetDesc(expr.AsLclVarCommon().LclNum);
            }
            else if (oper is GT_CALL or GT_RET_EXPR) // The special case of calls with return buffers.
            {
                var call = (oper is GT_RET_EXPR) ? expr.AsRetExpr().InlineCandidate : expr.AsCall();

                if ((call.Type is TYP_VOID) && call.ShouldHaveRetBufArg)
                {
                    var args = call.Args;
                    assert(args.HasRetBuffer);

                    var retBuf = args.RetBufferArg.Node;
                    
                    assert(retBuf.Type is TYP_I_IMPL or TYP_BYREF);

                    if (retBuf.Oper is GT_LCL_ADDR)
                    {
                        dstVarDsc = ref lvaGetDesc(retBuf.AsLclVarCommon().LclNum);
                    }
                }
            }

            // In the case of GT_RET_EXPR any subsequent spills will appear in the wrong place -- after
            // the call. We need to move them to before the call
            //
            var lastStmt = impLastStmt;

            if (!Unsafe.IsNullRef(in dstVarDsc)&& !dstVarDsc.IsAddressExposed && !dstVarDsc.lvHasLdAddrOp)
            {
                impSpillLclRefs(lvaGetLclNum(dstVarDsc), chkLevel);

                if (expr.Oper.IsLocalStore)
                {
                    // For stores, limit the checking to what the value could modify/interfere with.
                    var value = expr.AsLclVarCommon().Data;
                    flags = value.Flags & GTF_GLOB_EFFECT;

                    // We don't mark indirections off of "aliased" locals with GLOB_REF, but they must still be
                    // considered as such in the interference checking.
                    if (((flags & GTF_GLOB_REF) is 0) && !impIsAddressInLocal(value) && gtHasLocalsWithAddrOp(value))
                    {
                        flags |= GTF_GLOB_REF;
                    }
                }
            }

            if (flags is not 0)
            {
                impSpillSideEffects((flags & (GTF_ASG | GTF_CALL)) is not 0, chkLevel, "impAppendStmt");
            }
            else
            {
                impSpillSpecialSideEff();
            }

            assert(impLastStmt is not null);

            if ((lastStmt != impLastStmt) && (oper is GT_RET_EXPR))
            {
                var call = expr.AsRetExpr().InlineCandidate;
                JITDUMP($"\nimpAppendStmt: after sinking a local struct store into inline candidate [{call.TreeId:D6}], we need to reorder subsequent spills.\n");

                // Move all newly appended statements to just before the call's statement.
                // First, find the statement containing the call.

                assert(lastStmt is not null);
                var insertBeforeStmt = lastStmt;

                while (insertBeforeStmt.RootNode != call)
                {
                    assert(insertBeforeStmt != impStmtList);
                    insertBeforeStmt = insertBeforeStmt.PrevStmt;
                    assert(insertBeforeStmt is not null);
                }

                var movingStmt = lastStmt.NextStmt;
                assert(movingStmt is not null);

                JITDUMP($"Moving {FMT_STMT(movingStmt.Id)} through {FMT_STMT(impLastStmt.Id)} before {FMT_STMT(insertBeforeStmt.Id)}\n");

                // We move these backwards, so must keep moving the insert point to keep them in order.
                while (impLastStmt != lastStmt)
                {
                    movingStmt = impExtractLastStmt();
                    impInsertStmtBefore(movingStmt, insertBeforeStmt);
                    insertBeforeStmt = movingStmt;
                }
            }
        }

        impAppendStmtCheck(stmt, chkLevel);
        impAppendStmt(stmt);

#if FEATURE_SIMD
        impMarkContiguousSimdFieldStores(stmt);
#endif

        // Once we set the current offset as debug info in an appended tree, we are
        // ready to report the following offsets. Note that we need to compare
        // offsets here instead of debug info, since we do not set the "is call"
        // bit in impCurStmtDI.
        assert(impLastStmt is not null);

        if (checkConsumedDebugInfo && (impLastStmt.DebugInfo.Location.Offset == impCurStmtDI.Location.Offset))
        {
            impCurStmtOffsSet(BAD_IL_OFFSET);
        }

#if DEBUG
        impLastILoffsStmt ??= stmt;

        if (verbose)
        {
            jitprintf("\n\n");
            gtDispStmt(stmt);
        }
#endif
    }

    /// <summary>Check that storing the given tree doesnt mess up the semantic order.</summary>
    /// <param name="stmt"></param>
    /// <param name="chkLevel"></param>
    /// <remarks>Note that this has only limited value as we can only check [0..chkLevel).</remarks>
    [Conditional("DEBUG")]
    public void impAppendStmtCheck(Statement stmt, int chkLevel)
    {
        if (chkLevel == CHECK_SPILL_ALL)
        {
            chkLevel = stackState.esStackDepth;
        }

        if (stackState.esStackDepth is 0 || chkLevel is 0 || chkLevel == CHECK_SPILL_NONE)
        {
            return;
        }
        var stack = stackState.esStack.AsSpan(0, chkLevel);

        var tree = stmt.RootNode;
        var flags = tree.Flags;

        // Calls can only be appended if there are no GTF_GLOB_EFFECT on the stack

        if ((flags & GTF_CALL) is not 0)
        {
            for (var level = 0; level < stack.Length; level++)
            {
                assert((stack[level].val.Flags & GTF_GLOB_EFFECT) is 0);
            }
        }

        var oper = tree.Oper;

        if (oper.IsStore)
        {
            // For a store to a local variable, all references of that variable have to be spilled.
            // If it is aliased, all calls and indirect accesses have to be spilled.
            if (oper.IsLocalStore)
            {
                var lclNum = tree.AsLclVarCommon().LclNum;

                for (var level = 0; level < stack.Length; level++)
                {
                    var stkTree = stack[level].val;
                    assert(!gtHasRef(stkTree, lclNum) || impIsInvariant(stkTree));
                    assert(!lvaTable[lclNum].IsAddressExposed || ((stkTree.Flags & GTF_SIDE_EFFECT) is 0));
                }
            }
            // If the access may be to global memory, all side effects have to be spilled.
            else if ((flags & GTF_GLOB_REF) is not 0)
            {
                for (var level = 0; level < stack.Length; level++)
                {
                    assert((stack[level].val.Flags & GTF_GLOB_REF) is 0);
                }
            }
        }
    }

    /// <summary>Append the given expression tree to the current block's tree list.</summary>
    /// <param name="tree">The tree that will be the root of the newly created statement.</param>
    /// <param name="chkLevel">[0..chkLevel) is the portion of the stack which we will check for interference with stmt and spill if needed.</param>
    /// <param name="di">Debug information to associate with the statement.</param>
    /// <param name="checkConsumedDebugInfo">Whether to check for consumption of impCurStmtDI. impCurStmtDI marks the debug info of the current boundary and is set when we start importing IL at that boundary. If this parameter is true, then the function checks if 'stmt' has been associated with the current boundary, and if so, clears it so that we do not attach it to more upcoming statements.</param>
    /// <returns>The newly created statement.</returns>
    public Statement impAppendTree(GenTree tree, int chkLevel, in DebugInfo di, bool checkConsumedDebugInfo = true)
    {
        // Allocate an 'expression statement' node
        var stmt = gtNewStmt(tree, di);

        // Append the statement to the current block's stmt list
        impAppendStmt(stmt, chkLevel, checkConsumedDebugInfo);
        return stmt;
    }

    /// <summary>"&amp;var" can be used either as TYP_BYREF or TYP_I_IMPL, but we set its type to TYP_BYREF when we create it. We know if it can be changed to TYP_I_IMPL only at the point where we use it</summary>
    /// <param name="tree"></param>
    public static void impBashVarAddrsToI(GenTree tree)
    {
        if (tree.Oper is GT_LCL_ADDR)
        {
            tree.Type = TYP_I_IMPL;
        }
    }

    public unsafe int impBoxPatternMatch(CORINFO_RESOLVED_TOKEN* pResolvedToken, byte* codeAddr, byte* codeEndp, BoxPatterns opts)
    {
        // TODO: Port Compiler.impBoxPatternMatch
        return -1;
    }

    /// <summary>check that the node's type is compatible with the signature's type using ECMA implicit argument coercion table.</summary>
    /// <param name="sigType">the type in the call signature</param>
    /// <param name="nodeType">the node type</param>
    /// <returns>true if they are compatible, false otherwise</returns>
    /// <remarks>
    ///   <para>it is currently allowing byref->long passing, should be fixed in VM</para>
    ///   <para>it can't check long -> native int case on 64-bit platforms, so the behavior is different depending on the target bitness</para>
    /// </remarks>
    public static bool impCheckImplicitArgumentCoercion(var_types sigType, var_types nodeType)
    {
        if (sigType == nodeType)
        {
            return true;
        }

        assert(AreContiguous(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT));

        if (sigType is >= TYP_BYTE and <= TYP_UINT)
        {
            if (nodeType is (>= TYP_BYTE and <= TYP_UINT) or TYP_I_IMPL)
            {
                return true;
            }
        }
        else if (sigType is TYP_LONG or TYP_ULONG)
        {
            if (nodeType is TYP_LONG)
            {
                return true;
            }
        }
        else if (sigType is TYP_FLOAT or TYP_DOUBLE)
        {
            if (nodeType is TYP_FLOAT or TYP_DOUBLE)
            {
                return true;
            }
        }
        else if (sigType is TYP_BYREF)
        {
            if (nodeType is TYP_I_IMPL)
            {
                return true;
            }

            // This condition tolerates such IL:
            // ;  V00 this              ref  this class-hnd
            // ldarg.0
            // call(byref)
            if (nodeType is TYP_REF)
            {
                return true;
            }
        }
        else if (varTypeIsStruct(sigType))
        {
            if (varTypeIsStruct(nodeType))
            {
                return true;
            }
        }

        // This condition should not be under `else` because `TYP_I_IMPL`
        // intersects with `TYP_LONG` or `TYP_INT`.
        if (sigType is TYP_I_IMPL or TYP_U_IMPL)
        {
            // Note that it allows `ldc.i8 1; call(nint)` on 64-bit platforms,
            // but we can't distinguish `nint` from `long` there.
            if (nodeType is TYP_I_IMPL or TYP_U_IMPL or TYP_INT or TYP_UINT)
            {
                return true;
            }

            // It tolerates IL that ECMA does not allow but that is commonly used.
            // Example:
            //   V02 loc1           struct <RTL_OSVERSIONINFOEX, 32>
            //   ldloca.s     0x2
            //   call(native int)
            if (nodeType is TYP_BYREF)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>screen inline candate based on info from the method header</summary>
    /// <param name="fncHandle">inline candidate method</param>
    /// <param name="methInfo">method info from VM</param>
    /// <param name="forceInline">true if method is marked with AggressiveInlining</param>
    /// <param name="inlineResult">ongoing inline evaluation</param>
    public unsafe void impCanInlineIL(CORINFO_METHOD_HANDLE fncHandle, CORINFO_METHOD_INFO* methInfo, bool forceInline, InlineResult inlineResult)
    {
        var codeSize = methInfo->ILCodeSize;

        // We shouldn't have made up our minds yet...
        assert(!inlineResult.IsDecided);

        if (methInfo->EHcount > 0)
        {
            if (!opts.compInlineMethodsWithEH)
            {
                inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_EH);
                return;
            }
        }

        if ((methInfo->ILCode is null) || (codeSize == 0))
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_NO_BODY);
            return;
        }

        // For now we don't inline varargs (import code can't handle it)

        if (methInfo->args.isVarArg())
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_MANAGED_VARARGS);
            return;
        }

        // Reject if it has too many locals.
        // This is currently an implementation limit due to fixed-size arrays in the
        // inline info, rather than a performance heuristic.

        inlineResult.NoteInt(InlineObservation.CALLEE_NUMBER_OF_LOCALS, methInfo->locals.numArgs);

        if (methInfo->locals.numArgs > MAX_INL_LCLS)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_TOO_MANY_LOCALS);
            return;
        }

        // Make sure there aren't too many arguments.
        // This is currently an implementation limit due to fixed-size arrays in the
        // inline info, rather than a performance heuristic.

        inlineResult.NoteInt(InlineObservation.CALLEE_NUMBER_OF_ARGUMENTS, methInfo->args.numArgs);

        if (methInfo->args.numArgs > MAX_INL_ARGS)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_TOO_MANY_ARGUMENTS);
            return;
        }

        // Note force inline state

        inlineResult.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, forceInline);

        // Note IL code size

        inlineResult.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, codeSize);

        if (inlineResult.IsFailure)
        {
            return;
        }

        // Make sure maxstack is not too big

        inlineResult.NoteInt(InlineObservation.CALLEE_MAXSTACK, methInfo->maxStack);

        if (inlineResult.IsFailure)
        {
            return;
        }
    }

    /// <summary>Create a DebugInfo instance with the specified IL offset and 'is call' bit, using the current stack to determine whether to set the 'stack empty' bit.</summary>
    /// <param name="offs">The IL offset for the DebugInfo.</param>
    /// <param name="isCall">Whether the created DebugInfo should have the IsCall bit set.</param>
    /// <returns>The DebugInfo instance.</returns>
    public DebugInfo impCreateDIWithCurrentStackInfo(IL_OFFSET offs, bool isCall)
    {
        assert(offs != BAD_IL_OFFSET);

        var sourceTypes = ICorDebugInfo.SOURCE_TYPE_INVALID;

        if (isCall)
        {
            sourceTypes |= ICorDebugInfo.CALL_INSTRUCTION;
        }
        if (stackState.esStackDepth <= 0)
        {
            sourceTypes |= ICorDebugInfo.STACK_EMPTY;
        }
        return new DebugInfo(compInlineContext, new ILLocation(offs, sourceTypes));
    }

    /// <summary>Set the "current debug info" to attach to statements that we are generating next.</summary>
    /// <param name="offs">the IL offset</param>
    /// <remarks>This function will be called in the main IL processing loop when it is determined that we have reached a location in the IL stream for which we want to report debug information. This is the main way we determine which statements to report debug info for to the EE: for other statements, they will have no debug information attached.</remarks>
    public void impCurStmtOffsSet(IL_OFFSET offs)
    {
        if (offs == BAD_IL_OFFSET)
        {
            impCurStmtDI = new DebugInfo(compInlineContext, new ILLocation());
        }
        else
        {
            impCurStmtDI = impCreateDIWithCurrentStackInfo(offs, isCall: false);
        }
    }

    /*****************************************************************************
     *
     *  Store the given start and end stmt in the given basic block. This is
     *  mostly called by impEndTreeList(BasicBlock *block). It is called
     *  directly only for handling CEE_LEAVEs out of finally-protected try's.
     */

    public void impEndTreeList(BasicBlock block, Statement firstStmt, Statement? lastStmt)
    {
        // Make the list circular, so that we can easily walk it backwards
        firstStmt.PrevStmt = lastStmt;

        // Store the tree list in the basic block
        block.FirstStmt = firstStmt;

        // The block should not already be marked as imported
        assert(!block.HasFlag(BBF_IMPORTED));

        block.SetFlags(BBF_IMPORTED);
    }

    public void impEndTreeList(BasicBlock block)
    {
        if (impStmtList is null)
        {
            // The block should not already be marked as imported.
            assert(!block.HasFlag(BBF_IMPORTED));

            // Empty block. Just mark it as imported.
            block.SetFlags(BBF_IMPORTED);
        }
        else
        {
            impEndTreeList(block, impStmtList, impLastStmt);
        }

#if DEBUG
        impLastILoffsStmt?.LastILOffset = compIsForInlining ? BAD_IL_OFFSET : impCurOpcOffs;
        impLastILoffsStmt = null;
#endif

        impLastStmt = null;
        impStmtList = null;
    }

    /// <summary>Extract the last statement from the current stmts list.</summary>
    /// <returns>The extracted statement.</returns>
    /// <remarks>It assumes that the stmt will be reinserted later.</remarks>
    public Statement impExtractLastStmt()
    {
        assert(impLastStmt is not null);

        var stmt = impLastStmt;
        assert(stmt is not null);
        impLastStmt = impLastStmt.PrevStmt;

        if (impLastStmt is null)
        {
            impStmtList = null;
        }
        return stmt;
    }

    /// <summary>add pred edges from finally returns to their continuations</summary>
    /// <remarks>
    ///   <para>These edges were not added during the initial pred list computation, because the initial flow graph does not contain the callfinally block pairs; those blocks are added during importation.</para>
    ///   <para>We rely on handler blocks being lexically contiguous between begin and last.</para>
    /// </remarks>
    public void impFixPredLists()
    {
        var added = false;
        var usingProfileWeights = fgIsUsingProfileWeights;

        for (var XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var HBtab = ref compHndBBtab[XTnum];

            if (HBtab.HasFinallyHandler)
            {
                var finallyBegBlock = HBtab.ebdHndBeg;
                var finallyLastBlock = HBtab.ebdHndLast;
                var predCount = -1;
                var finallyWeight = finallyBegBlock.bbWeight;

                foreach (var finallyBlock in new BasicBlockRangeList(finallyBegBlock, finallyLastBlock))
                {
                    if (finallyBlock.HndIndex != XTnum)
                    {
                        // Must be a nested handler... we could skip to its last
                        continue;
                    }

                    if (finallyBlock.Kind is not BBJ_EHFINALLYRET)
                    {
                        continue;
                    }

                    // Count the number of predecessors. Then we can allocate the bbEhfTargets table and fill it in.
                    // We only need to count once, since it's invariant with the finally block.
                    if (predCount == -1)
                    {
                        predCount = 0;
                        foreach (var predBlock in finallyBegBlock.PredBlocks)
                        {
                            // We only care about preds that are callfinallies.
                            if (predBlock.Kind is not BBJ_CALLFINALLY)
                            {
                                continue;
                            }
                            predCount++;
                        }
                    }

                    BBJumpTable jumpEhf;

                    if (predCount > 0)
                    {
                        var succTab = new FlowEdge[predCount];
                        var predNum = 0;
                        var remainingLikelihood = 1.0;

                        foreach (var predBlock in finallyBegBlock.PredBlocks)
                        {
                            // We only care about preds that are callfinallies.
                            //
                            if (predBlock.Kind is not BBJ_CALLFINALLY)
                            {
                                continue;
                            }

                            var continuation = predBlock.Next;
                            assert(continuation is not null);

                            var newEdge = fgAddRefPred(continuation, finallyBlock);

                            if (usingProfileWeights && (finallyWeight != BB_ZERO_WEIGHT))
                            {
                                // Derive edge likelihood from the entry block's weight relative to other entries.
                                var callFinallyWeight = predBlock.bbWeight;
                                var likelihood = weight_t.Min(callFinallyWeight / finallyWeight, 1.0);
                                newEdge.Likelihood = weight_t.Min(likelihood, remainingLikelihood);
                                remainingLikelihood = weight_t.Max(BB_ZERO_WEIGHT, remainingLikelihood - likelihood);
                            }
                            else
                            {
                                // If we don't have profile data, evenly distribute the likelihoods.
                                //
                                newEdge.Likelihood = 1.0 / predCount;
                            }

                            succTab[predNum++] = newEdge;

                            if (!added)
                            {
                                JITDUMP("\nAdding pred edges from BBJ_EHFINALLYRET blocks\n");
                                added = true;
                            }
                        }

                        assert(predNum == predCount);
                        jumpEhf = new BBJumpTable(succTab);
                    }
                    else
                    {
                        // It's possible for the `finally` to have no CALLFINALLY predecessors if the `try` block
                        // has an unconditional `throw` (the finally will still be invoked in the exceptional
                        // case via the runtime). In that case, jumpEhf->succCount remains the default, zero,
                        // and jumpEhf->succs remains the default, null.
                        jumpEhf = new BBJumpTable();
                    }
                    finallyBlock.EhfTargets = jumpEhf;
                }

                if (usingProfileWeights)
                {
                    // Compute new flow into the finally region's continuation successors.
                    var profileConsistent = true;

                    foreach (var callFinally in finallyBegBlock.PredBlocks)
                    {
                        var callFinallyRet = callFinally.Next;
                        assert(callFinallyRet is not null);

                        callFinallyRet.setBBProfileWeight(callFinallyRet.computeIncomingWeight());
                        profileConsistent &= fgProfileWeightsConsistentOrSmall(callFinally.bbWeight, callFinallyRet.bbWeight);
                    }

                    if (!profileConsistent)
                    {
                        JITDUMP($"Flow into finally handler EH{XTnum} does not match outgoing flow. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                        fgPgoConsistent = false;
                    }
                }
            }
        }
    }

    /// <summary>Get the address of a value.</summary>
    /// <param name="val">The value in question</param>
    /// <param name="curLevel">Stack level for spilling</param>
    /// <param name="allowedMustPreserveIndirFlags">If 'val' is an indirection and it has any must-preserve indir flags (like volatile), then those flags must be included in this mask to be allowed through without creating a temp.</param>
    /// <param name="indirFlags">Flags that indirs created based on this address can and should set.</param>
    /// <returns>In case "val" represents a location (is an indirection/local), will return its address. Otherwise, address of a temporary assigned the value of "val" will be returned.</returns>
    /// <remarks>Returned flags are included in the GTF_IND_COPYABLE_FLAGS mask.</remarks>
    public GenTree impGetNodeAddr(GenTree val, int curLevel, GenTreeFlags allowedMustPreserveIndirFlags, out GenTreeFlags indirFlags)
    {
        indirFlags = GTF_EMPTY;

        switch (val.Oper)
        {
            case GT_BLK:
            case GT_IND:
            case GT_STOREIND:
            case GT_STORE_BLK:
            {
                if ((val.Flags & GTF_IND_MUST_PRESERVE_FLAGS & ~allowedMustPreserveIndirFlags) is not 0)
                {
                    break;
                }

                indirFlags = val.Flags & GTF_IND_COPYABLE_FLAGS;
                return val.AsIndir().Addr;
            }

            case GT_LCL_VAR:
            case GT_STORE_LCL_VAR:
            {
                val.Flags |= GTF_VAR_MOREUSES;
                return gtNewLclVarAddrNode(TYP_BYREF, val.AsLclVar().LclNum);
            }

            case GT_LCL_FLD:
            case GT_STORE_LCL_FLD:
            {
                var lclFld = val.AsLclFld();
                val.Flags |= GTF_VAR_MOREUSES;
                return gtNewLclAddrNode(TYP_BYREF, lclFld.LclNum, lclFld.LclOffs);
            }

            case GT_COMMA:
            {
                var op = val.AsOp();
                _ = impAppendTree(op.Op1, curLevel, impCurStmtDI);
                return impGetNodeAddr(op.Op2, curLevel, allowedMustPreserveIndirFlags, out indirFlags);
            }

            default:
            {
                break;
            }
        }

        assert(!val.Oper.IsStore);
        var lclNum = lvaGrabTemp(shortLifetime: true, "location for address-of(RValue)");
        impStoreToTemp(lclNum, val, curLevel);

        // The 'return value' is now address of the temp itself.
        return gtNewLclVarAddrNode(TYP_BYREF, lclNum);
    }

    /// <summary>Get the first non-prefix opcode.</summary>
    /// <param name="codeAddr"></param>
    /// <param name="codeEndp"></param>
    /// <returns></returns>
    /// <remarks>Used for verification of valid combinations of prefixes and actual opcodes.</remarks>
    private static unsafe OPCODE impGetNonPrefixOpcode(byte* codeAddr, byte* codeEndp)
    {
        while (codeAddr < codeEndp)
        {
            var opcode = (OPCODE)(codeAddr[0]);
            codeAddr += sizeof(byte);

            if (opcode == CEE_PREFIX1)
            {
                if (codeAddr >= codeEndp)
                {
                    break;
                }

                opcode = (OPCODE)(codeAddr[0] + 0x0100);
                codeAddr += sizeof(byte);
            }

            switch (opcode)
            {
                case CEE_UNALIGNED:
                case CEE_VOLATILE:
                case CEE_TAILCALL:
                case CEE_CONSTRAINED:
                case CEE_READONLY:
                {
                    break;
                }

                default:
                {
                    return opcode;
                }
            }

            codeAddr += opcode.Size;
        }
        return CEE_ILLEGAL;
    }

    /// <summary>Return the byte for "b" (allocating/extending impPendingBlockMembers if necessary.)</summary>
    /// <param name="blk"></param>
    /// <returns></returns>
    /// <remarks>Operates on the map in the top-level ancestor.</remarks>
    public byte impGetPendingBlockMember(BasicBlock blk)
        => impInlineRoot.impPendingBlockMembers[blk.bbInd];

    /// <summary>Set the byte for "b" to "val" (allocating/extending impPendingBlockMembers if necessary.)</summary>
    /// <param name="blk"></param>
    /// <param name="val"></param>
    /// <remarks>Operates on the map in the top-level ancestor.</remarks>
    public void impSetPendingBlockMember(BasicBlock blk, byte val)
        => impInlineRoot.impPendingBlockMembers[blk.bbInd] = val;

    /// <summary>Look for special cases where a call to an intrinsic returns an exact type</summary>
    /// <param name="call">handle for the special intrinsic method</param>
    /// <returns>Exact class handle returned by the intrinsic call, if known; otherwise <c>null</c> if not known, or not likely to lead to beneficial optimization.</returns>
    /// <remarks>This computes the return type for generic factory methods, where the type returned is determined by a generic method or class parameter.</remarks>
    public unsafe CORINFO_CLASS_HANDLE impGetSpecialIntrinsicExactReturnType(GenTreeCall call)
    {
        var methodHnd = call._callMethHnd;
        JITDUMP($"Special intrinsic: looking for exact type returned by {eeGetMethodFullName(methodHnd)}\n");

        CORINFO_CLASS_HANDLE result = null;

        // See what intrinsic we have...
        var ni = lookupNamedIntrinsic(methodHnd);

        switch (ni)
        {
            case NI_System_Collections_Generic_Comparer_get_Default:
            case NI_System_Collections_Generic_EqualityComparer_get_Default:
            case NI_System_Array_T_GetEnumerator:
            {
                // Expect one class generic parameter; figure out which it is.
                CORINFO_SIG_INFO sig;
                info.compCompHnd->getMethodSig(methodHnd, &sig);
                assert(sig.sigInst.classInstCount == 1);

                var typeHnd = sig.sigInst.classInst[0];
                assert(typeHnd is not null);

                var instParam = call.Args.FindWellKnownArg(WellKnownArg.InstParam);

                if (instParam is not null)
                {
                    assert(instParam.Next is null);

                    var hClass = gtGetHelperArgClassHandle(instParam.Node);

                    if (hClass != NO_CLASS_HANDLE)
                    {
                        typeHnd = getTypeInstantiationArgument(hClass, 0);
                    }
                }

                if (ni == NI_System_Collections_Generic_EqualityComparer_get_Default)
                {
                    result = info.compCompHnd->getDefaultEqualityComparerClass(typeHnd);
                }
                else if (ni == NI_System_Collections_Generic_Comparer_get_Default)
                {
                    result = info.compCompHnd->getDefaultComparerClass(typeHnd);
                }
                else
                {
                    assert(ni == NI_System_Array_T_GetEnumerator);
                    result = info.compCompHnd->getSZArrayHelperEnumeratorClass(typeHnd);
                }

                if (result != NO_CLASS_HANDLE)
                {
                    JITDUMP($"Special intrinsic for type {eeGetClassName(typeHnd)}: return type is {((result is not null) ? eeGetClassName(result) : "unknown")}\n");
                }
                else
                {
                    JITDUMP($"Special intrinsic for type {eeGetClassName(typeHnd)}: return type undetermined, so deferring opt\n");
                }
                break;
            }

            case NI_System_SZArrayHelper_GetEnumerator:
            {
                // Expect one method generic parameter; figure out which it is.
                CORINFO_SIG_INFO sig;
                info.compCompHnd->getMethodSig(methodHnd, &sig);

                assert(sig.sigInst.methInstCount == 1);
                assert(sig.sigInst.classInstCount == 0);

                var typeHnd = sig.sigInst.methInst[0];
                assert(typeHnd is not null);

                var instParam = call.Args.FindWellKnownArg(WellKnownArg.InstParam);
                if (instParam is not null)
                {
                    assert(instParam.Next is null);

                    var hMethod = gtGetHelperArgMethodHandle(instParam.Node);

                    if (hMethod != NO_METHOD_HANDLE)
                    {
                        typeHnd = getMethodInstantiationArgument(hMethod, 0);
                    }
                }

                result = info.compCompHnd->getSZArrayHelperEnumeratorClass(typeHnd);

                if (result != NO_CLASS_HANDLE)
                {
                    JITDUMP($"Special intrinsic for type {eeGetClassName(typeHnd)}: return type is {((result is not null) ? eeGetClassName(result) : "unknown")}\n");
                }
                else
                {
                    JITDUMP($"Special intrinsic for type {eeGetClassName(typeHnd)}: return type undetermined, so deferring opt\n");
                }
                break;
            }

            default:
            {
                JITDUMP("This special intrinsic not handled, sorry...\n");
                break;
            }
        }

        return result;
    }

    // Assumes that "block" is a basic block that completes with a non-empty stack. We will assign the values
    // on the stack to local variables (the "spill temp" variables). The successor blocks will assume that
    // its incoming stack contents are in those locals. This requires "block" and its successors to agree on
    // the variables that will be used -- and for all the predecessors of those successors, and the
    // successors of those predecessors, etc. Call such a set of blocks closed under alternating
    // successor/predecessor edges a "spill clique." A block is a "predecessor" or "successor" member of the
    // clique (or, conceivably, both). Each block has a specified sequence of incoming and outgoing spill
    // temps. If "block" already has its outgoing spill temps assigned (they are always a contiguous series
    // of local variable numbers, so we represent them with the base local variable number), returns that.
    // Otherwise, picks a set of spill temps, and propagates this choice to all blocks in the spill clique of
    // which "block" is a member (asserting, in debug mode, that no block in this clique had its spill temps
    // chosen already. More precisely, that the incoming or outgoing spill temps are not chosen, depending
    // on which kind of member of the clique the block is).
    public int impGetSpillTmpBase(BasicBlock block)
    {
        if (block.bbStkTempsOut != NO_BASE_TMP)
        {
            return block.bbStkTempsOut;
        }

#if DEBUG
        if (verbose)
        {
            jitprintf($"\n*************** In impGetSpillTmpBase({FMT_BB(block.bbNum)})\n");
        }
#endif

        // Otherwise, choose one, and propagate to all members of the spill clique.
        // Grab enough temps for the whole stack.
        var baseTmp = lvaGrabTemps(stackState.esStackDepth, "IL Stack Entries");

        // We do *NOT* need to reset the SpillClique*Members because a block can only be the predecessor
        // to one spill clique, and similarly can only be the successor to one spill clique
        impWalkSpillCliqueFromPred(block, SetSpillTempsBase);

        return baseTmp;

        void SetSpillTempsBase(SpillCliqueDir predOrSucc, BasicBlock blk)
        {
            if (predOrSucc == SpillCliqueSucc)
            {
                assert(blk.bbStkTempsIn == NO_BASE_TMP); // Should not already be a member of a clique as a successor.
                blk.bbStkTempsIn = baseTmp;
            }
            else
            {
                assert(predOrSucc == SpillCliquePred);
                assert(blk.bbStkTempsOut == NO_BASE_TMP); // Should not already be a member of a clique as a predecessor.
                blk.bbStkTempsOut = baseTmp;
            }
        }
    }

    /// <summary>helper function that will tell us if the IL instruction at the addr passed by param consumes an address at the top of the stack.</summary>
    /// <param name="codeAddr"></param>
    /// <param name="codeEndp"></param>
    /// <returns></returns>
    /// <remarks>We use it to save us lvAddrTaken</remarks>
    public unsafe bool impILConsumesAddr(byte* codeAddr, byte* codeEndp)
    {
        var opcode = impGetNonPrefixOpcode(codeAddr, codeEndp);
        return opcode is CEE_LDFLD;
    }

    /// <summary>convert IL into jit IR</summary>
    /// <remarks>
    ///   <para>The basic flowgraph has already been constructed. Blocks are filled in by the importer as they are discovered to be reachable.</para>
    ///   <para>Blocks may be added to provide the right structure for various EH constructs (notably LEAVEs from catches and finallies).</para>
    /// </remarks>
    public void impImport()
    {
        var inlineRoot = impInlineRoot;

        if (info.compMaxStack <= SMALL_STACK_SIZE)
        {
            impStkSize = SMALL_STACK_SIZE;
        }
        else
        {
            impStkSize = info.compMaxStack;
        }

        if (this == inlineRoot)
        {
            // Allocate the stack contents
            stackState.esStack = new StackEntry[impStkSize];
        }
        else
        {
            // This is the inlinee compiler, steal the stack from the inliner compiler
            // (after ensuring that it is large enough).
            if (inlineRoot.impStkSize < impStkSize)
            {
                inlineRoot.impStkSize = impStkSize;
                inlineRoot.stackState.esStack = new StackEntry[impStkSize];
            }

            stackState.esStack = inlineRoot.stackState.esStack;
        }

        // initialize the entry state at start of method
        initCurrentState();

        // Initialize stuff related to figuring "spill cliques" (see spec comment for impGetSpillTmpBase).
        if (this == inlineRoot) // These are only used on the root of the inlining tree.
        {
            // We have initialized these previously, but to size 0.  Make them larger.
            impPendingBlockMembers.Capacity = fgBBNumMax * 2;
            impSpillCliquePredMembers.Capacity = fgBBNumMax * 2;
            impSpillCliqueSuccMembers.Capacity = fgBBNumMax * 2;
        }

        inlineRoot.impPendingBlockMembers.Clear();
        inlineRoot.impSpillCliquePredMembers.Clear();
        inlineRoot.impSpillCliqueSuccMembers.Clear();

        inlineRoot.impPendingBlockMembers.Capacity = fgBBNumMax * 2;
        inlineRoot.impSpillCliquePredMembers.Capacity = fgBBNumMax * 2;
        inlineRoot.impSpillCliqueSuccMembers.Capacity = fgBBNumMax * 2;

        impBlockListNodeFreeList = null;

#if DEBUG
        impLastILoffsStmt = null;
        impNestedStackSpill = false;
#endif
        impBoxTemp = BAD_VAR_NUM;

        impPendingFree = null;
        impPendingList = null;

        // Skip leading internal blocks.
        // These can arise from needing a leading scratch BB, from EH normalization, and from OSR entry redirects.

        assert(fgFirstBB is not null);
        var entryBlock = fgFirstBB;

        while (entryBlock.HasFlag(BBF_INTERNAL))
        {
            JITDUMP($"Marking leading BBF_INTERNAL block {FMT_BB(entryBlock.bbNum)} as BBF_IMPORTED\n");
            entryBlock.SetFlags(BBF_IMPORTED);

            assert(entryBlock.Kind is BBJ_ALWAYS);
            entryBlock = entryBlock.Target;
        }

        // Note for OSR we'd like to be able to verify this block must be
        // stack empty, but won't know that until we've imported...so instead
        // we'll BADCODE out if we mess up.
        //
        // (the concern here is that the runtime asks us to OSR a
        // different IL version than the one that matched the method that
        // triggered OSR).  This should not happen but I might have the
        // IL versioning stuff wrong.
        //
        // TODO: we also currently expect this block to be a join point,
        // which we should verify over when we find jump targets.
        impImportBlockPending(entryBlock);

        if (opts.IsOSR)
        {
            // We now import all the IR and keep it around so we can
            // analyze address exposure more robustly.

            assert(fgEntryBB is not null);
            JITDUMP($"OSR: protecting original method entry {FMT_BB(fgEntryBB.bbNum)}\n");
            impImportBlockPending(fgEntryBB);
            fgEntryBB.bbRefs++;
            fgEntryBBExtraRefs++;
        }

        // Import blocks in the worker-list until there are no more

        while (impPendingList is not null)
        {
            // Remove the entry at the front of the list

            var dsc = impPendingList;
            assert(dsc.pdBB is not null);

            impPendingList = impPendingList.pdNext;
            impSetPendingBlockMember(dsc.pdBB, val: 0);

            // Restore the stack state
            impRestoreStackState(dsc.pdSavedStack);

            // Add the entry to the free list for reuse
            dsc.pdNext = impPendingFree;
            impPendingFree = dsc;

            // Now import the block
            impImportBlock(dsc.pdBB);

            if (compDonotInline)
            {
                return;
            }
        }

        // If the method had EH, we may be missing some pred edges
        // (notably those from BBJ_EHFINALLYRET blocks). Add them.
        //
        if (info.compXcptnsCount > 0)
        {
            impFixPredLists();
            JITDUMP("\nAfter impImport() added blocks for try,catch,finally");

            if (verbose)
            {
                fgDispBasicBlocks();
            }
        }
    }

    /// <summary>Import the instructions for the given basic block.</summary>
    /// <param name="block"></param>
    /// <remarks>
    ///   <para>Perform verification, throwing an exception on failure.</para>
    ///   <para>Push any successor blocks that are enabled for the first time, or whose verification pre-state is changed.</para>
    /// </remarks>
    public void impImportBlock(BasicBlock block)
    {
        // BBF_INTERNAL blocks only exist during importation due to EH canonicalization. We need to
        // handle them specially. In particular, there is no IL to import for them, but we do need
        // to mark them as imported and put their successors on the pending import list.
        if (block.HasFlag(BBF_INTERNAL))
        {
            JITDUMP($"Marking BBF_INTERNAL block {FMT_BB(block.bbNum)} as BBF_IMPORTED\n");
            block.SetFlags(BBF_IMPORTED);

            foreach (var succBlock in block.Succs)
            {
                impImportBlockPending(succBlock);
            }
            return;
        }

        bool markImport;

        // Make the block globally available
        compCurBB = block;

#if DEBUG
        // Initialize the debug variables
        impCurOpcName = "unknown";
        impCurOpcOffs = block.bbCodeOffs;
#endif

        // Set the current stack state to the merged result
        resetCurrentState(block, ref stackState);

        if (block.hasTryIndex)
        {
            impVerifyEHBlock(block);
        }

        // Now walk the code and import the IL into GenTrees.
        impImportBlockCode(block);

        if (compDonotInline)
        {
            return;
        }
        markImport = false;

        // If the stack is non-empty, we might have to spill its contents
        var spillStack = stackState.esStackDepth is not 0;
        var reimportSpillClique = false;

        while (spillStack)
        {
            // assume the main loop below will handle everything
            spillStack = false;

            // input temps assigned to successor blocks
            var baseTmp = NO_BASE_TMP;

            var tgtBlock = null as BasicBlock;
            var addStmt = null as Statement;

            // if a box temp is used in a block that leaves something on the stack, its lifetime is hard to determine, simply don't reuse such temps.
            impBoxTemp = BAD_VAR_NUM;

            // Do the successors of 'block' have any other predecessors ?
            // We do not want to do some of the optimizations related to multiRef if we can reimport blocks

            var multRef = impCanReimport ? -1 : 0;

            switch (block.Kind)
            {
                case BBJ_COND:
                {
                    addStmt = impExtractLastStmt();
                    assert(addStmt.RootNode.Oper is GT_JTRUE);

                    tgtBlock = block.FalseTarget;

                    // Note if the next block has more than one ancestor
                    multRef |= tgtBlock.bbRefs;

                    // Does the next block have temps assigned?
                    baseTmp = tgtBlock.bbStkTempsIn;

                    if (baseTmp != NO_BASE_TMP)
                    {
                        break;
                    }

                    // Try the target of the jump then
                    tgtBlock = block.TrueTarget;

                    multRef |= tgtBlock.bbRefs;
                    baseTmp = tgtBlock.bbStkTempsIn;
                    break;
                }

                case BBJ_ALWAYS:
                {
                    tgtBlock = block.Target;
                    multRef |= tgtBlock.bbRefs;
                    baseTmp = tgtBlock.bbStkTempsIn;
                    break;
                }

                case BBJ_SWITCH:
                {
                    addStmt = impExtractLastStmt();
                    assert(addStmt.RootNode.Oper is GT_SWITCH);

                    foreach (var switchSucc in block.SwitchSuccs)
                    {
                        tgtBlock = switchSucc;
                        multRef |= tgtBlock.bbRefs;

                        // Thanks to spill cliques, we should have assigned all or none
                        assert((baseTmp == NO_BASE_TMP) || (baseTmp == tgtBlock.bbStkTempsIn));
                        baseTmp = tgtBlock.bbStkTempsIn;

                        if (multRef > 1)
                        {
                            break;
                        }
                    }
                    break;
                }

                case BBJ_CALLFINALLY:
                case BBJ_EHCATCHRET:
                case BBJ_RETURN:
                case BBJ_EHFINALLYRET:
                case BBJ_EHFAULTRET:
                case BBJ_EHFILTERRET:
                case BBJ_THROW:
                {
                    BADCODE("can't have 'unreached' end of BB with non-empty stack");
                    break;
                }

                default:
                {
                    NO_WAY("Unexpected bbKind");
                    break;
                }
            }

            assert(multRef >= 1);

            // Do we have a base temp number?
            var newTemps = baseTmp is NO_BASE_TMP;

            if (newTemps)
            {
                // Grab enough temps for the whole stack
                baseTmp = impGetSpillTmpBase(block);
            }

            // Spill all stack entries into temps
            JITDUMP("\nSpilling stack entries into temps\n");

            var stack = stackState.esStack.AsSpan(0, stackState.esStackDepth);

            for (int level = 0, tempNum = baseTmp; level < stack.Length; level++, tempNum++)
            {
                ref var stackEntry = ref stack[level];
                var tree = stackEntry.val;

                var treeType = tree.Type;
                var treeActualType = treeType.ActualType;

                // VC generates code where it pushes a byref from one branch, and an int (ldc.i4 0) from
                // the other. This should merge to a byref in unverifiable code.
                // However, if the branch which leaves the TYP_I_IMPL on the stack is imported first, the
                // successor would be imported assuming there was a TYP_I_IMPL on
                // the stack. Thus the value would not get GC-tracked. Hence,
                // change the temp to TYP_BYREF and reimport the clique.
                ref var tempDsc = ref lvaGetDesc(tempNum);

                if ((tree.Type is TYP_BYREF) && (tempDsc.Type is TYP_I_IMPL))
                {
                    tempDsc.Type = TYP_BYREF;
                    reimportSpillClique = true;
                }

#if TARGET_64BIT
                if ((treeActualType is TYP_I_IMPL) && (tempDsc.Type is TYP_INT))
                {
                    // Some other block in the spill clique set this to "int", but now we have "native int".
                    // Change the type and go back to re-import any blocks that used the wrong type.
                    tempDsc.Type = TYP_I_IMPL;
                    reimportSpillClique = true;
                }
                else if ((treeActualType is TYP_INT) && (tempDsc.Type is TYP_I_IMPL))
                {
                    // Spill clique has decided this should be "native int", but this block only pushes an "int".
                    // Insert a sign-extension to "native int" so we match the clique.
                    stackEntry.val = gtNewCastNode(TYP_I_IMPL, tree, fromUnsigned: false, TYP_I_IMPL);
                }

                // Consider the case where one branch left a 'byref' on the stack and the other leaves
                // an 'int'. On 32-bit, this is allowed (in non-verifiable code) since they are the same
                // size. JIT64 managed to make this work on 64-bit. For compatibility, we support JIT64
                // behavior instead of asserting and then generating bad code (where we save/restore the
                // low 32 bits of a byref pointer to an 'int' sized local). If the 'int' side has been
                // imported already, we need to change the type of the local and reimport the spill clique.
                // If the 'byref' side has imported, we insert a cast from int to 'native int' to match
                // the 'byref' size.
                if ((treeActualType is TYP_BYREF) && (tempDsc.Type is TYP_INT))
                {
                    // Some other block in the spill clique set this to "int", but now we have "byref".
                    // Change the type and go back to re-import any blocks that used the wrong type.
                    tempDsc.Type = TYP_BYREF;
                    reimportSpillClique = true;
                }
                else if ((treeActualType is TYP_INT) && (tempDsc.Type is TYP_BYREF))
                {
                    // Spill clique has decided this should be "byref", but this block only pushes an "int".
                    // Insert a sign-extension to "native int" so we match the clique size.
                    stackEntry.val = gtNewCastNode(TYP_I_IMPL, tree, fromUnsigned: false, TYP_I_IMPL);
                }

#endif

                if ((treeType is TYP_DOUBLE) && (tempDsc.Type is TYP_FLOAT))
                {
                    // Some other block in the spill clique set this to "float", but now we have "double".
                    // Change the type and go back to re-import any blocks that used the wrong type.
                    tempDsc.Type = TYP_DOUBLE;
                    reimportSpillClique = true;
                }
                else if ((treeType is TYP_FLOAT) && (tempDsc.Type is TYP_DOUBLE))
                {
                    // Spill clique has decided this should be "double", but this block only pushes a "float".
                    // Insert a cast to "double" so we match the clique.
                    stackEntry.val = gtNewCastNode(TYP_DOUBLE, tree, false, TYP_DOUBLE);
                }

                // If addStmt has a reference to tempNum (can only happen if we are spilling to the temps already used by a previous block), we need to spill addStmt

                if ((addStmt is not null) && !newTemps && gtHasRef(addStmt.RootNode, tempNum))
                {
                    var addTree = addStmt.RootNode;

                    if (addTree.Oper is GT_JTRUE)
                    {
                        var compare = addTree.AsOp().Op1.AsOp();
                        assert(compare.Oper.IsCompare);

                        ref var compareOp1Ref = ref compare.Op1Ref;
                        var type = compareOp1Ref.Type.ActualType;

                        if (gtHasRef(compareOp1Ref, tempNum))
                        {
                            var lclNum = lvaGrabTemp(shortLifetime: true, "spill addStmt JTRUE ref Op1");
                            impStoreToTemp(lclNum, compareOp1Ref, level);
                            type = lvaTable[lclNum].Type.ActualType;
                            compareOp1Ref = gtNewLclvNode(type, lclNum);
                        }

                        ref var compareOp2Ref = ref compare.Op2Ref;

                        if (gtHasRef(compareOp2Ref, tempNum))
                        {
                            var lclNum = lvaGrabTemp(shortLifetime: true, "spill addStmt JTRUE ref Op2");
                            impStoreToTemp(lclNum, compareOp2Ref, level);
                            type = lvaTable[lclNum].Type.ActualType;
                            compareOp2Ref = gtNewLclvNode(type, lclNum);
                        }
                    }
                    else
                    {
                        assert(addTree.Oper is GT_SWITCH);

                        var valueRef = addTree.AsOp().Op1;
                        assert(genActualTypeIsIntOrI(valueRef.Type));

                        var lclNum = lvaGrabTemp(shortLifetime: true, "spill addStmt SWITCH");
                        impStoreToTemp(lclNum, valueRef, level);
                        valueRef = gtNewLclvNode(valueRef.Type.ActualType, lclNum);
                    }
                }

                // Spill the stack entry, and replace with the temp
                if (!impSpillStackEntry(level, tempNum, assertOnRecursion: true, "Spill Stack Entry"))
                {
                    if (markImport)
                    {
                        BADCODE("bad stack state");
                    }

                    // We failed to spill, so we need to restart the outer loop
                    spillStack = stackState.esStackDepth is not 0;
                    addStmt = null;
                    break;
                }
            }

            if (addStmt is not null)
            {
                // Put back the 'jtrue'/'switch' if we removed it earlier
                impAppendStmt(addStmt, CHECK_SPILL_NONE);
            }
        }

        // Some of the append/spill logic works on compCurBB
        assert(compCurBB == block);

        /* Save the tree list in the block */
        impEndTreeList(block);

        // impEndTreeList sets BBF_IMPORTED on the block
        // We do *NOT* want to set it later than this because
        // impReimportSpillClique might clear it if this block is both a
        // predecessor and successor in the current spill clique
        assert(block.HasFlag(BBF_IMPORTED));

        // If we had a int/native int, or float/double collision, we need to re-import
        if (reimportSpillClique)
        {
            // This will re-import all the successors of block (as well as each of their predecessors)
            impReimportSpillClique(block);

            // We don't expect to see BBJ_EHFILTERRET here.
            assert(block.Kind is not BBJ_EHFILTERRET);

            foreach (var succ in block.Succs)
            {
                if (!succ.HasFlag(BBF_IMPORTED))
                {
                    impImportBlockPending(succ);
                }
            }
        }
        else
        {
            // the normal case: otherwise just import the successors of block

            // Does this block jump to any other blocks?
            // Filter successor from BBJ_EHFILTERRET have already been handled above in the call
            // to impVerifyEHBlock().
            if (block.Kind is not BBJ_EHFILTERRET)
            {
                foreach (var succ in block.Succs)
                {
                    impImportBlockPending(succ);
                }
            }
        }
    }

    public void impImportBlockCode(BasicBlock block)
    {
        // TODO: Port Compiler.impImportBlockCode
    }

    /// <summary>ensure that block will be imported</summary>
    /// <param name="block">block that should be imported.</param>
    public void impImportBlockPending(BasicBlock block)
    {
        // Notes:
        //   Ensures that "block" is a member of the list of BBs waiting to be imported, pushing it on the list if
        //   necessary (and ensures that it is a member of the set of BB's on the list, by setting its byte in
        //   impPendingBlockMembers).  Does *NOT* change the existing "pre-state" of the block.
        //   
        //   Merges the current verification state into the verification state of "block" (its "pre-state")./

        JITDUMP($"\nimpImportBlockPending for {FMT_BB(block.bbNum)}\n");

        // We will add a block to the pending set if it has not already been imported (or needs to be re-imported),
        // or if it has, but merging in a predecessor's post-state changes the block's pre-state.
        // (When we're doing verification, we always attempt the merge to detect verification errors.)

        // If the block has not been imported, add to pending set.
        var addToPending = !block.HasFlag(BBF_IMPORTED);

        // Initialize bbEntryState the first time we try to add this block to the pending list.
        // A null bbEntryState means that the block does not yet have a recorded pre-state,
        // which corresponds to having no established (i.e. empty) stack depth on entry.
        if ((block.EntryState.esStackDepth is 0) && !block.HasFlag(BBF_IMPORTED) && (impGetPendingBlockMember(block) is 0))
        {
            initBBEntryState(block, stackState);
            assert(addToPending);
            assert(impGetPendingBlockMember(block) is 0);
        }
        else
        {
            // The stack should have the same height on entry to the block from all its predecessors.
            if (block.bbStackDepthOnEntry != stackState.esStackDepth)
            {
#if DEBUG
                NO_WAY($"Block at offset {block.bbCodeOffs:X} to {block.bbCodeOffsEnd:X} in {info.compFullName} entered with different stack depths.\nPrevious depth was {block.bbStackDepthOnEntry}, current depth is {stackState.esStackDepth}");
#else
                NO_WAY("Block entered with different stack depths");
#endif
            }

            if (!addToPending)
            {
                return;
            }

            if (block.bbStackDepthOnEntry > 0)
            {
                // We need to fix the types of any spill temps that might have changed:
                //   int->native int, float->double, int->byref, etc.
                impRetypeEntryStateTemps(block);
            }

            // OK, we must add to the pending list, if it's not already in it.
            if (impGetPendingBlockMember(block) is not 0)
            {
                return;
            }
        }

        // Get an entry to add to the pending list

        var dsc = null as PendingDsc;

        if (impPendingFree is not null)
        {
            // We can reuse one of the freed up dscs.
            dsc = impPendingFree;
            impPendingFree = dsc.pdNext;
            dsc.pdBB = block;
        }
        else
        {
            // We have to create a new dsc
            dsc = new PendingDsc(block);
        }

        // Save the stack trees for later
        impSaveStackState(out dsc.pdSavedStack, false);

        // Add the entry to the pending list

        dsc.pdNext = impPendingList;
        impPendingList = dsc;
        impSetPendingBlockMember(block, 1); // And indicate that it's now a member of the set.

        // Various assertions require us to now to consider the block as not imported (at least for
        // the final time...)
        block.RemoveFlags(BBF_IMPORTED);

#if DEBUG
        if (false && verbose)
        {
            jitprintf($"Added PendingDsc - {dsc.GetHashCode():X8} for {FMT_BB(block.bbNum)}\n");
        }
#endif
    }

    /// <summary>Insert the given "stmt" before "stmtBefore".</summary>
    /// <param name="stmt">a statement to insert</param>
    /// <param name="stmtBefore">an insertion point to insert "stmt" before</param>
    public void impInsertStmtBefore(Statement stmt, Statement stmtBefore)
    {
        assert(stmt is not null);
        assert(stmtBefore is not null);

        if (stmtBefore == impStmtList)
        {
            impStmtList = stmt;
        }
        else
        {
            var stmtPrev = stmtBefore.PrevStmt;
            assert(stmtPrev is not null);

            stmt.PrevStmt = stmtPrev;
            stmtPrev.NextStmt = stmt;
        }

        stmt.NextStmt = stmtBefore;
        stmtBefore.PrevStmt = stmt;
    }

    public bool impIsAddressInLocal(GenTree tree)
        => impIsAddressInLocal(tree, out Unsafe.NullRef<GenTreeLclFld>());

    /// <summary>Check to see if the tree is the address of a local or the address of a field in a local.</summary>
    /// <param name="tree">The tree</param>
    /// <param name="lclVarTree">the local that this points into</param>
    /// <returns>true if it points into a local</returns>
    public bool impIsAddressInLocal(GenTree tree, out GenTreeLclFld lclVarTree)
    {
        Unsafe.SkipInit(out lclVarTree);

        while (tree.Oper is GT_FIELD_ADDR)
        {
            var fieldAddr = tree.AsFieldAddr();

            if (!fieldAddr.IsInstance)
            {
                break;
            }
            tree = fieldAddr.FldObj;
        }

        if (tree.Oper is GT_LCL_ADDR)
        {
            if (!Unsafe.IsNullRef(in lclVarTree))
            {
                lclVarTree = tree.AsLclFld();
            }
            return true;
        }
        return false;
    }

    /// <summary>check if a tree (created during import) is invariant.</summary>
    /// <param name="tree">The tree</param>
    /// <returns>true if it is invariant</returns>
    /// <remarks>This is a variant of GenTree.IsInvariant that is more suitable for use during import. Unlike that function, this one handles GT_FIELD_ADDR nodes.</remarks>
    public bool impIsInvariant(GenTree tree)
    {
        var oper = tree.Oper;
        return oper.IsConst || impIsAddressInLocal(tree) || (oper is GT_FTN_ADDR);
    }

    /// <summary>Check if a return buffer is of a legal shape.</summary>
    /// <param name="retBuf">The return buffer</param>
    /// <param name="call">The call that is passed the return buffer</param>
    /// <returns>True if it is legal according to ABI and IR invariants.</returns>
    /// <remarks>ABI requires all return buffers to point to stack. Also, we have an IR   invariant for async calls that return buffers must be the address of a local.</remarks>
    public unsafe bool impIsLegalRetBuf(GenTree retBuf, GenTreeCall call)
    {
        if (call.IsAsync)
        {
            // Async calls require LCL_ADDR shape for the retbuf to know where to
            // save the value on resumption.
            if (retBuf.Oper is not GT_LCL_ADDR)
            {
                return false;
            }

            // LCL_ADDR on an implicit byref will turn into LCL_VAR in morph.
            if (lvaIsImplicitByRefLocal(retBuf.AsLclVarCommon().LclNum))
            {
                return false;
            }

            return true;
        }

        // The ABI requires the retbuffer to point to stack.
        return !fgAddrCouldBeHeap(retBuf) || eeIsByrefLike(call.RetClsHnd);
    }

    public unsafe bool impIsTailCallILPattern(bool tailPrefixed, OPCODE curOpcode, byte* codeAddrOfNextOpcode, byte* codeEnd, bool isRecursive)
    {
        // Bail out if the current opcode is not a call.
        if (!impOpcodeIsCallOpcode(curOpcode))
        {
            return false;
        }

#if !FEATURE_TAILCALL_OPT_SHARED_RETURN
        // If shared ret tail opt is not enabled, we will enable it for recursive methods.

        if (isRecursive)
#endif
        {
            // we can actually handle if the ret is in a fallthrough block, as long as that is the only part of the
            // sequence. Make sure we don't go past the end of the IL however.
            codeEnd = unchecked((byte*)(nint.Min((nint)(codeEnd + 1), (nint)(info.compCode + info.compILCodeSize))));
        }

        // Bail out if there is no next opcode after call
        if (codeAddrOfNextOpcode >= codeEnd)
        {
            return false;
        }
        return (OPCODE)(codeAddrOfNextOpcode[0]) == CEE_RET;
    }

    /// <summary>Check for the special case where the object is the methods original 'this' pointer.</summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    /// <remarks>Note that, the original 'this' pointer is always local var 0 for non-static method, even if we might have created the copy of 'this' pointer in lvaArg0Var.</remarks>
    public bool impIsThis(GenTree obj)
    {
        if (compIsForInlining)
        {
            return impInlineInfo.InlinerCompiler.impIsThis(obj);
        }
        else
        {
            return ((obj is not null) && (obj.Oper is GT_LCL_VAR) &&
                    lvaIsOriginalThisArg(obj.AsLclVarCommon().LclNum));
        }
    }

#if FEATURE_SIMD
    /// <summary>Try to identify if there are contiguous stores from simd field to memory. If there are, then mark the related lclvar so that it won't be promoted.</summary>
    /// <param name="stmt">Input statement node.</param>
    public void impMarkContiguousSimdFieldStores(Statement stmt)
    {
        if (opts.OptimizationDisabled)
        {
            return;
        }

        var expr = stmt.RootNode;

        if (expr.Oper.IsStore && (expr.Type is TYP_FLOAT))
        {
            var curValue = expr.Data;
            var simdBaseType = curValue.Type;
            var srcSimdLclAddr = getSimdStructFromField(curValue, out var index, out var simdSize, true);

            if ((srcSimdLclAddr is null) || (simdBaseType is not TYP_FLOAT))
            {
                fgPreviousCandidateSimdFieldStoreStmt = null;
            }
            else if (index == 0)
            {
                fgPreviousCandidateSimdFieldStoreStmt = stmt;
            }
            else if (fgPreviousCandidateSimdFieldStoreStmt is not null)
            {
                assert(index > 0);

                var curStore = expr;
                var prevStore = fgPreviousCandidateSimdFieldStoreStmt.RootNode;
                var prevValue = prevStore.Data;

                if (!areArgumentsContiguous(prevStore, curStore) || !areArgumentsContiguous(prevValue, curValue))
                {
                    fgPreviousCandidateSimdFieldStoreStmt = null;
                }
                else
                {
                    if (index == (simdSize / simdBaseType.Size - 1))
                    {
                        // Successfully found the pattern, mark the lclvar as UsedInSimdIntrinsic
                        setLclRelatedToSimdIntrinsic(srcSimdLclAddr);

                        if (curStore.Oper is GT_STOREIND)
                        {
                            var indirAddr = curStore.AsIndir().Addr;

                            if (indirAddr.Oper is GT_FIELD_ADDR)
                            {
                                var fieldAddr = indirAddr.AsFieldAddr();

                                if (fieldAddr.IsInstance)
                                {
                                    var fldObj = fieldAddr.FldObj;

                                    if (fldObj.IsLclVarAddr && varTypeIsStruct(lvaGetDesc(fldObj.AsLclFld().LclNum).Type))
                                    {
                                        setLclRelatedToSimdIntrinsic(fldObj.AsLclVarCommon());
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        fgPreviousCandidateSimdFieldStoreStmt = stmt;
                    }
                }
            }
        }
        else
        {
            fgPreviousCandidateSimdFieldStoreStmt = null;
        }
    }
#endif

    private static bool impOpcodeIsCallOpcode(OPCODE opcode)
        => opcode is CEE_CALL or CEE_CALLI or CEE_CALLVIRT;

    /// <summary>Push catch arg onto the stack.</summary>
    /// <param name="hndBlk">first block of the catch handler</param>
    /// <param name="clsHnd">type being caught</param>
    /// <param name="isSingleBlockFilter">true if catch has single block filtger</param>
    /// <returns>the basic block of the actual handler.</returns>
    /// <remarks>If there are jumps to the beginning of the handler, insert basic block and spill catch arg to a temp. Update the handler block if necessary.</remarks>
    public unsafe BasicBlock impPushCatchArgOnStack(BasicBlock hndBlk, CORINFO_CLASS_HANDLE clsHnd, bool isSingleBlockFilter)
    {
        // Do not inject the basic block twice on reimport. This should be
        // hit only under JIT stress. See if the block is the one we injected.
        // Note that EH canonicalization can inject internal blocks here. We might
        // be able to re-use such a block (but we don't, right now).
        if (hndBlk.HasAllFlags(BBF_IMPORTED | BBF_INTERNAL | BBF_DONT_REMOVE))
        {
            var stmt = hndBlk.FirstStmt;

            if (stmt is not null)
            {
                var tree = stmt.RootNode;

                if ((tree.Oper is GT_STORE_LCL_VAR) && (tree.AsLclVar().Data.Oper is GT_CATCH_ARG))
                {
                    tree = gtNewLclvNode(TYP_REF, tree.AsLclVar().LclNum);
                    impPushOnStack(tree, new typeInfo(clsHnd));

                    assert(hndBlk.Next is not null);
                    return hndBlk.Next;
                }
            }

            // If we get here, it must have been some other kind of internal block. It's possible that
            // someone prepended something to our injected block, but that's unlikely.
        }

        // Push the exception address value on the stack
        // and mark the node as having a side-effect - i.e. cannot be moved around since it is tied to a fixed location (EAX)
        var arg = new GenTree(GT_CATCH_ARG, TYP_REF) {
            HasOrderingSideEffect = true
        };

#if JIT32_GCENCODER
        var forceInsertNewBlock = isSingleBlockFilter || compStressCompile(STRESS_CATCH_ARG, 5);
#else
        var forceInsertNewBlock = compStressCompile(STRESS_CATCH_ARG, 5);
#endif

        // Spill GT_CATCH_ARG to a temp if there are jumps to the beginning of the handler.
        //
        // For typical normal handlers we expect ref count to be 2 here (one artificial, one for
        // the edge from the xxx...)

        if ((hndBlk.bbRefs > 2) || forceInsertNewBlock)
        {
            // Create extra basic block for the spill
            var newBlk = fgNewBBbefore(BBJ_ALWAYS, hndBlk, extendRegion: true);

            newBlk.SetFlags(BBF_IMPORTED | BBF_DONT_REMOVE);
            newBlk.inheritWeight(hndBlk);
            newBlk.bbCodeOffs = hndBlk.bbCodeOffs;

            var newEdge = fgAddRefPred(hndBlk, newBlk);
            newBlk.TargetEdge = newEdge;

            // Spill into a temp.
            var tempNum = lvaGrabTemp(shortLifetime: false, "SpillCatchArg");
            lvaTable[tempNum].Type = TYP_REF;

            var argStore = gtNewTempStore(tempNum, arg);
            arg = gtNewLclvNode(TYP_REF, tempNum);

            hndBlk.bbStkTempsIn = tempNum;

            Statement argStmt;

            if ((info.compStmtOffsetsImplicit & ICorDebugInfo.CALL_SITE_BOUNDARIES) is not 0)
            {
                // Report the debug info. impImportBlockCode won't treat the actual handler as exception block and thus won't do it for us.
                // TODO-Bug: Should be reported with ICorDebugInfo.CALL_SITE?
                impCurStmtDI = new DebugInfo(compInlineContext, new ILLocation(newBlk.bbCodeOffs, ICorDebugInfo.SOURCE_TYPE_INVALID));
                argStmt = gtNewStmt(argStore, impCurStmtDI);
            }
            else
            {
                argStmt = gtNewStmt(argStore);
            }

            fgInsertStmtAtEnd(newBlk, argStmt);
        }

        impPushOnStack(arg, new typeInfo(clsHnd));
        return hndBlk;
    }

    public void impPushOnStack(GenTree tree, typeInfo ti)
    {
        assert(compCurBB is not null);

        // Check for overflow. If inlining, we may be using a bigger stack
        var stackDepth = stackState.esStackDepth++;

        if ((stackDepth >= info.compMaxStack) && (stackDepth >= impStkSize || !compCurBB.HasFlag(BBF_IMPORTED)))
        {
            BADCODE("stack overflow");
        }

        ref var stackEntry = ref stackState.esStack[stackDepth];

        stackEntry.seTypeInfo = ti;
        stackEntry.val = tree;

        var type = tree.Type;

        if (type is TYP_LONG)
        {
            compLongUsed = true;
        }
        else if (varTypeIsFloating(type))
        {
            compFloatingPointUsed = true;
        }
    }

    // Similar to impImportBlockPending, but assumes that block has already been imported once and is being
    // reimported for some reason.  It specifically does *not* look at stackState to set the EntryState
    // for the block, but instead, just re-uses the block's existing EntryState.
    public void impReimportBlockPending(BasicBlock block)
    {
        JITDUMP($"\nimpReimportBlockPending for {FMT_BB(block.bbNum)}");
        assert(block.HasFlag(BBF_IMPORTED));

        // OK, we must add to the pending list, if it's not already in it.
        if (impGetPendingBlockMember(block) is not 0)
        {
            return;
        }

        // Get an entry to add to the pending list

        var dsc = null as PendingDsc;

        if (impPendingFree is not null)
        {
            // We can reuse one of the freed up dscs.
            dsc = impPendingFree;
            dsc.pdBB = block;
            impPendingFree = dsc.pdNext;
        }
        else
        {
            // We have to create a new dsc
            dsc = new PendingDsc(block);
        }

        ref var entryState = ref block.EntryState;

        dsc.pdSavedStack.ssDepth = entryState.esStackDepth;
        dsc.pdSavedStack.ssTrees = entryState.esStack;

        // Add the entry to the pending list

        dsc.pdNext = impPendingList;
        impPendingList = dsc;
        impSetPendingBlockMember(block, 1); // And indicate that it's now a member of the set.

        // Various assertions require us to now to consider the block as not imported (at least for
        // the final time...)
        block.RemoveFlags(BBF_IMPORTED);

#if DEBUG
        if (false && verbose)
        {
            jitprintf($"Added PendingDsc - {dsc.GetHashCode():X8} for {FMT_BB(block.bbNum)}\n");
        }
#endif
    }

    /// <summary>Mark the block as unimported.</summary>
    /// <param name="block"></param>
    /// <remarks>Note that the caller is responsible for calling impImportBlockPending(), with the appropriate stack-state</remarks>
    public void impReimportMarkBlock(BasicBlock block)
    {
#if DEBUG
        if (verbose && block.HasFlag(BBF_IMPORTED))
        {
            jitprintf($"\n{FMT_BB(block.bbNum)} will be reimported\n");
        }
#endif

        // We shouldn't be re-importing one of these special blocks.
        assert(block.Kind is not BBJ_CALLFINALLYRET);

        if (block.isBBCallFinallyPair)
        {
            // If we're going to re-import a BBJ_CALLFINALLY that has a paired BBJ_CALLFINALLYRET,
            // remove the BBJ_CALLFINALLYRET.
            var leaveBlock = block.Next;
            assert(leaveBlock is not null);

            fgPrepareCallFinallyRetForRemoval(leaveBlock);
            fgRemoveBlock(leaveBlock, unreachable: true);

            // The above code marked the BBJ_CALLFINALLY as retless. Remove that.
            block.RemoveFlags(BBF_RETLESS_CALL);
        }

        block.RemoveFlags(BBF_IMPORTED);
    }

    // Assumes that "block" is a basic block that completes with a non-empty stack. We have previously
    // assigned the values on the stack to local variables (the "spill temp" variables). The successor blocks
    // will assume that its incoming stack contents are in those locals. This requires "block" and its
    // successors to agree on the variables and their types that will be used.  The CLI spec allows implicit
    // conversions between 'int' and 'native int' or 'float' and 'double' stack types. So one predecessor can
    // push an int and another can push a native int.  For 64-bit we have chosen to implement this by typing
    // the "spill temp" as native int, and then importing (or re-importing as needed) so that all the
    // predecessors in the "spill clique" push a native int (sign-extending if needed), and all the
    // successors receive a native int. Similarly float and double are unified to double.
    // This routine is called after a type-mismatch is detected, and it will walk the spill clique to mark
    // blocks for re-importation as appropriate (both successors, so they get the right incoming type, and
    // predecessors, so they insert an upcast if needed).
    public void impReimportSpillClique(BasicBlock block)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf($"\n*************** In impReimportSpillClique({FMT_BB(block.bbNum)})\n");
        }
#endif

        // If we get here, it is because this block is already part of a spill clique
        // and one predecessor had an outgoing live stack slot of type int, and this
        // block has an outgoing live stack slot of type native int.
        // We need to reset these before traversal because they have already been set
        // by the previous walk to determine all the members of the spill clique.
        var inlineRoot = impInlineRoot;

        inlineRoot.impSpillCliquePredMembers.Clear();
        inlineRoot.impSpillCliqueSuccMembers.Clear();

        impWalkSpillCliqueFromPred(block, ReimportSpillClique);
    }

    public unsafe void impResolveToken(byte* addr, out CORINFO_RESOLVED_TOKEN resolvedToken, CorInfoTokenKind kind)
    {
        resolvedToken = new CORINFO_RESOLVED_TOKEN {
            tokenContext = impTokenLookupContextHandle,
            tokenScope = info.compScopeHnd,
            token = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(addr, sizeof(int))),
            tokenType = kind,
        };

        fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
        {
            info.compCompHnd->resolveToken(pResolvedToken);
        }
    }

    public void impRestoreStackState(SavedStack savePtr)
    {
        stackState.esStackDepth = savePtr.ssDepth;
        stackState.esStack = (savePtr.ssDepth is not 0) ? [.. savePtr.ssTrees] : [];
    }

    /// <summary>Re-type the incoming lclVar nodes to match the varDsc.</summary>
    /// <param name="blk"></param>
    public void impRetypeEntryStateTemps(BasicBlock blk)
    {
        ref var entryState = ref blk.EntryState;
        var stack = entryState.esStack.AsSpan(0, entryState.esStackDepth);

        for (var level = 0; level < stack.Length; level++)
        {
            var tree = stack[level].val;

            if (tree.Oper is GT_LCL_VAR or GT_LCL_FLD)
            {
                tree.Type = lvaGetDesc(tree.AsLclVarCommon().LclNum).Type;
            }
        }
    }

    public void impSaveStackState(out SavedStack savePtr, bool copy)
    {
        var depth = stackState.esStackDepth;
        var stack = stackState.esStack.AsSpan(0, depth);
        var trees = (depth is not 0) ? stack.ToArray() : [];

        savePtr.ssDepth = depth;
        savePtr.ssTrees = trees;

        if (copy)
        {
            // Make a fresh copy of all the stack entries

            for (var level = 0; level < stack.Length; level++)
            {
                ref var srcEntry = ref stack[level];
                ref var dstEntry = ref trees[level];

                dstEntry.seTypeInfo = srcEntry.seTypeInfo;
                var tree = srcEntry.val;

                if (impValidSpilledStackEntry(tree))
                {
                    dstEntry.val = gtCloneExpr(tree);
                }
                else
                {
                    NO_WAY("Bad oper - Not covered by impValidSpilledStackEntry()");
                }
            }
        }
    }

    /// <summary>Spill all trees referencing the given local.</summary>
    /// <param name="lclNum">The local's number</param>
    /// <param name="chkLevel">Height (exclusive) of the portion of the stack to check</param>
    public void impSpillLclRefs(int lclNum, int chkLevel)
    {
        // Before we make any appends to the tree list we must spill the
        // "special" side effects (GTF_ORDER_SIDEEFF) - GT_CATCH_ARG.
        impSpillSpecialSideEff();

        if (chkLevel == CHECK_SPILL_ALL)
        {
            chkLevel = stackState.esStackDepth;
        }

        assert(chkLevel <= stackState.esStackDepth);
        assert(compCurBB is not null);

        var stack = stackState.esStack.AsSpan(0, chkLevel);

        for (var level = 0; level < stack.Length; level++)
        {
            var tree = stack[level].val;

            // If the tree may throw an exception, and the block has a handler,
            // then we need to spill stores to the local if the local is on entry
            // to the handler. Just spill 'em all without considering the liveness

            var xcptnCaught = ehBlockHasExnFlowDsc(compCurBB) && ((tree.Flags & (GTF_CALL | GTF_EXCEPT)) is not 0);

            //Skip the tree if it doesn't have an affected reference, unless xcptnCaught

            if (xcptnCaught || gtHasRef(tree, lclNum))
            {
                _ = impSpillStackEntry(level, BAD_VAR_NUM, assertOnRecursion: false, "impSpillLclRefs");
            }
        }
    }

    /// <summary>If the stack entry is a tree with side effects in it, assign that tree to a temp and replace it on the stack with refs to its temp.</summary>
    /// <param name="spillGlobEffects"></param>
    /// <param name="i">the stack entry which will be checked and spilled.</param>
    /// <param name="reason"></param>
    public void impSpillSideEffect(bool spillGlobEffects, int i, string reason)
    {
        assert(i <= stackState.esStackDepth);

        var spillFlags = spillGlobEffects ? GTF_GLOB_EFFECT : GTF_SIDE_EFFECT;
        var tree = stackState.esStack[i].val;

        if (((tree.Flags & spillFlags) is not 0) || (spillGlobEffects && !impIsAddressInLocal(tree) && gtHasLocalsWithAddrOp(tree)))
        {
            // When spillGlobEffects is true
            //   No need to spill the LCL_ADDR nodes.
            //   Spill if we still see GT_LCL_VAR that contains lvHasLdAddrOp or lvAddrTaken flag.
            _ = impSpillStackEntry(i, BAD_VAR_NUM, assertOnRecursion: false, reason);
        }
    }

    /// <summary>If the stack contains any trees with side effects in them, assign those trees to temps and replace them on the stack with refs to their temps.</summary>
    /// <param name="spillGlobEffects"></param>
    /// <param name="chkLevel">[0..chkLevel) is the portion of the stack which will be checked and spilled.</param>
    /// <param name="reason"></param>
    public void impSpillSideEffects(bool spillGlobEffects, int chkLevel, string reason)
    {
        assert(chkLevel != CHECK_SPILL_NONE);

        // Before we make any appends to the tree list we must spill the "special" side effects (GTF_ORDER_SIDEEFF on a GT_CATCH_ARG)
        impSpillSpecialSideEff();

        if (chkLevel == CHECK_SPILL_ALL)
        {
            chkLevel = stackState.esStackDepth;
        }

        assert(chkLevel <= stackState.esStackDepth);

        for (var i = 0; i < chkLevel; i++)
        {
            impSpillSideEffect(spillGlobEffects, i, reason);
        }
    }

    /// <summary>If the stack contains any trees with special side effects in them, assign those trees to temps and replace them on the stack with refs to their temps.</summary>
    public void impSpillSpecialSideEff()
    {
        // Only exception objects need to be carefully handled
        assert(compCurBB is not null);

        if (compCurBB.CatchType is BBCT_NONE)
        {
            return;
        }

        var stack = stackState.esStack.AsSpan(0, stackState.esStackDepth);

        for (var level = 0; level < stack.Length; level++)
        {
            var tree = stack[level].val;

            // Make sure if we have an exception object in the sub tree we spill ourselves.
            if (gtHasCatchArg(tree))
            {
                _ = impSpillStackEntry(level, BAD_VAR_NUM, assertOnRecursion: false, "impSpillSpecialSideEff");
            }
        }
    }

    public unsafe bool impSpillStackEntry(int level, int tnum, bool assertOnRecursion, string reason)
    {
#if DEBUG
        using var guard = new RecursiveGuard(ref impNestedStackSpill, assertOnRecursion);
#endif

        ref var stackEntry = ref stackState.esStack[level];
        var tree = stackEntry.val;

        // Allocate a temp if we haven't been asked to use a particular one

        if ((tnum != BAD_VAR_NUM) && (tnum >= lvaCount))
        {
            return false;
        }

        var isNewTemp = false;

        if (tnum == BAD_VAR_NUM)
        {
            tnum = lvaGrabTemp(shortLifetime: true, reason);
            isNewTemp = true;
        }
        ref var lvaDsc = ref lvaGetDesc(tnum);

        // Assign the spilled entry to the temp
        impStoreToTemp(tnum, tree, level);

        if (isNewTemp)
        {
            assert(!lvaDsc.lvSingleDef);
            lvaDsc.lvSingleDef = true;
            JITDUMP($"Marked V{tnum:D2} as a single def temp\n");

            // If temp is newly introduced and a ref type, grab what type info we can.
            if (lvaDsc.Type == TYP_REF)
            {
                var stkHnd = stackEntry.seTypeInfo.GetClassHandleForObjRef();
                lvaSetClass(tnum, tree, stkHnd);
            }

            // If we're assigning a GT_RET_EXPR, note the temp over on the call,
            // so the inliner can use it in case it needs a return spill temp.
            if (tree.Oper is GT_RET_EXPR)
            {
                JITDUMP($"\n*** see V{tnum:D2} = GT_RET_EXPR, noting temp\n");
                var call = tree.AsRetExpr().InlineCandidate;

                if (call.IsGuardedDevirtualizationCandidate)
                {
                    for (byte i = 0; i < call.InlineCandidatesCount; i++)
                    {
                        call.GetGDVCandidateInfo(i).preexistingSpillTemp = tnum;
                    }
                }
                else
                {
                    call.SingleInlineCandidateInfo.preexistingSpillTemp = tnum;
                }
            }
        }

        // The tree type may be modified by impStoreToTemp, so use the type of the lclVar.
        var type = lvaDsc.Type.ActualType;
        var temp = gtNewLclvNode(type, tnum);
        stackEntry.val = temp;

        return true;
    }

    public GenTree impStoreStruct(GenTree store, int curLevel = CHECK_SPILL_NONE, in DebugInfo di = default, BasicBlock? block = null)
        => impStoreStruct(store, ref Unsafe.NullRef<Statement>(), curLevel, di, block);

    /// <summary>Import a struct store.</summary>
    /// <param name="store">the store</param>
    /// <param name="afterStmt">statement to insert any additional statements after</param>
    /// <param name="curLevel">stack level for which a spill may be being done</param>
    /// <param name="di">debug info for new statements</param>
    /// <param name="block">block to insert any additional statements in</param>
    /// <returns>The tree that should be appended to the statement list that represents the store.</returns>
    /// <remarks>Temp stores may be appended to impStmtList if spilling is necessary.</remarks>
    public unsafe GenTree impStoreStruct(GenTree store, ref Statement afterStmt, int curLevel = CHECK_SPILL_NONE, in DebugInfo di = default, BasicBlock? block = null)
    {
        var storeOper = store.Oper;
        assert(varTypeIsStruct(store.Type) && storeOper.IsStore);

        ref var dataRef = ref store.DataRef;
        assert(store.Type == dataRef.Type);

        if (store.Type is TYP_STRUCT)
        {
            assert(ClassLayout.AreCompatible(store.GetLayout(this), dataRef.GetLayout(this)));
        }

        var usedDI = di;

        if (!usedDI.IsValid)
        {
            usedDI = impCurStmtDI;
        }

        var dataOper = dataRef.Oper;

        if (dataOper.IsCall)
        {
            var call = dataRef.AsCall();

            if (call.ShouldHaveRetBufArg)
            {
                // Case of call returning a struct via hidden retbuf arg.
                var destAddr = impGetNodeAddr(store, CHECK_SPILL_ALL, GTF_IND_MUST_PRESERVE_FLAGS, out var indirFlags);

                // Return buffers cannot have volatile, unaligned, or initclass flags
                if (((indirFlags & GTF_IND_MUST_PRESERVE_FLAGS) != 0) || !impIsLegalRetBuf(destAddr, call))
                {
                    var lclNum = lvaGrabTemp(shortLifetime: false, "stack copy for value returned via return buffer");
                    lvaSetStruct(lclNum, call.RetClsHnd, unsafeValueClsCheck: false);

                    var spilledCall = impStoreStruct(gtNewStoreLclVarNode(lclNum, call), ref afterStmt, curLevel, di, block);
                    dataRef = gtNewCommaNode(store.Type, spilledCall, gtNewLclvNode(lvaGetDesc(lclNum).Type, lclNum));

                    return impStoreStruct(store, ref afterStmt, curLevel, di, block);
                }

                var newArg = NewCallArg.CreateForPrimitive(destAddr).WithWellKnownArg(WellKnownArg.RetBuffer);

                if (destAddr.Oper is GT_LCL_ADDR)
                {
                    lvaSetVarDoNotEnregister(destAddr.AsLclVarCommon().LclNum, DoNotEnregisterReason.HiddenBufferStructArg);
                }

#if !TARGET_ARM
                var args = call.Args;

                // Unmanaged instance methods on Windows or Unix X86 need the retbuf arg after the first (this) parameter
                if ((TargetOS.IsWindows || compUnixX86Abi()) && call.IsUnmanaged)
                {
                    if (callConvIsInstanceMethodCallConv(call.UnmanagedCallConv))
                    {
                        var head = args.Head;

                        // The argument list has already been reversed. Insert the
                        // return buffer as the second-to-last node  so it will be
                        // pushed on to the stack after the user args but before
                        // the native this arg as required by the native ABI.
                        if (head is null)
                        {
                            // Empty arg list
                            _ = args.PushFront(newArg);
                        }
#if TARGET_X86
                        else if (call.UnmanagedCallConv == CorInfoCallConvExtension.Thiscall)
                        {
                            // For thiscall, the "this" parameter is not included in the argument list reversal,
                            // so we need to put the return buffer as the last parameter.
                            _ = args.PushBack(newArg);
                        }
                        else if (head.Next is null)
                        {
                            // Only 1 arg, so insert at beginning
                            _ = args.PushFront(newArg);
                        }
                        else
                        {
                            // Find second last arg
                            var  secondLastArg = null as CallArg;

                            foreach (var arg in args.Args)
                            {
                                assert(arg.Next is not null);

                                if (arg.Next.Next is null)
                                {
                                    secondLastArg = arg;
                                    break;
                                }
                            }

                            assert(secondLastArg is not null, "Expected to find second last arg");
                            _ = args.InsertAfter(secondLastArg, newArg);
                        }
#else
                        else
                        {
                            _ = args.InsertAfter(head, newArg);
                        }
#endif
                    }
                    else
                    {
#if TARGET_X86
                        // The argument list has already been reversed.
                        // Insert the return buffer as the last node so it will be pushed on to the stack last
                        // as required by the native ABI.
                        _ = args.PushBack(newArg);
#else
                        // insert the return value buffer into the argument list as first byref parameter
                        _ = args.PushFront(newArg);
#endif
                    }
                }
                else
#endif
                {
                    // insert the return value buffer into the argument list as first byref parameter after 'this'
                    _ = args.InsertAfterThisOrFirst(newArg);
                }

                // now returns void, not a struct
                dataRef.Type = TYP_VOID;

                // return the morphed call node
                return dataRef;
            }

#if UNIX_AMD64_ABI
            if (store.Oper is GT_STORE_LCL_VAR)
            {
                // TODO-Cleanup: delete this quirk.
                lvaGetDesc(store.AsLclVar().LclNum).lvIsMultiRegRet = true;
            }
#endif
        }
        else if (dataOper is GT_RET_EXPR)
        {
            var retExpr = dataRef.AsRetExpr();
            var call = retExpr.InlineCandidate;

            if (call.ShouldHaveRetBufArg)
            {
                var args = call.Args;

                // insert the return value buffer into the argument list as first byref parameter after 'this'
                var destAddr = impGetNodeAddr(store, CHECK_SPILL_ALL, GTF_IND_MUST_PRESERVE_FLAGS, out var indirFlags);

                // Return buffers cannot have volatile, unaligned, or initclass flags
                if (((indirFlags & GTF_IND_MUST_PRESERVE_FLAGS) != 0) || !impIsLegalRetBuf(destAddr, call))
                {
                    var lclNum = lvaGrabTemp(shortLifetime: false, "stack copy for value returned via return buffer");
                    lvaSetStruct(lclNum, call.RetClsHnd, unsafeValueClsCheck: false);
                    destAddr = gtNewLclVarAddrNode(TYP_I_IMPL, lclNum);

                    // Insert address of temp into existing call
                    var retBufArg = NewCallArg.CreateForPrimitive(destAddr).WithWellKnownArg(WellKnownArg.RetBuffer);
                    _ = args.InsertAfterThisOrFirst(retBufArg);

                    // Now the store needs to copy from the new temp instead.
                    call.Type = TYP_VOID;
                    retExpr.Type = TYP_VOID;
                    var tmpType = lvaGetDesc(lclNum).Type;
                    dataRef = gtNewCommaNode(tmpType, retExpr, gtNewLclvNode(tmpType, lclNum));
                    return impStoreStruct(store, ref afterStmt, CHECK_SPILL_ALL, di, block);
                }

                _ = args.InsertAfterThisOrFirst(NewCallArg.CreateForPrimitive(destAddr).WithWellKnownArg(WellKnownArg.RetBuffer));

                // now returns void, not a struct
                dataRef.Type = TYP_VOID;
                call.Type = TYP_VOID;

                // We already have appended the write to 'dest' GT_CALL's args
                // So now we just return an empty node (pruning the GT_RET_EXPR)
                return dataRef;
            }
        }
        else if (dataOper is GT_COMMA)
        {
            var comma = dataRef.AsOp();
            var sideEffectAddressStore = null as GenTree;

            if (store.Oper is GT_STORE_BLK or GT_STOREIND)
            {
                var storeIndir = store.AsIndir();
                var addr = storeIndir.Addr;

                if ((addr.Flags & GTF_ALL_EFFECT) is not 0)
                {
                    var addrTmp = fgMakeTemp(addr);
                    sideEffectAddressStore = addrTmp.Store;
                    storeIndir.Addr = addrTmp.Load;
                }
            }

            if (!Unsafe.IsNullRef(in afterStmt))
            {
                assert(block is not null);

                // Insert op1 after '*afterStmt'
                if (sideEffectAddressStore is not null)
                {
                    var addrStmt = gtNewStmt(sideEffectAddressStore, usedDI);
                    fgInsertStmtAfter(block, afterStmt, addrStmt);
                    afterStmt = addrStmt;
                }

                var newStmt = gtNewStmt(comma.Op1, usedDI);
                fgInsertStmtAfter(block, afterStmt, newStmt);
                afterStmt = newStmt;
            }
            else if (impLastStmt is not null)
            {
                // Do the side-effect as a separate statement.
                if (sideEffectAddressStore is not null)
                {
                    impAppendTree(sideEffectAddressStore, curLevel, usedDI);
                }
                impAppendTree(comma.Op1, curLevel, usedDI);
            }
            else
            {
                // In this case we have neither been given a statement to insert after, nor are we
                // in the importer where we can append the side effect.
                // Instead, we're going to sink the store below the COMMA.
                dataRef = comma.Op2;
                comma.Op2 = impStoreStruct(store, ref afterStmt, curLevel, usedDI, block);
                gtUpdateNodeSideEffects(store);
                comma.SetAllEffectsFlags(comma.Op1, comma.Op2);

                if (sideEffectAddressStore is not null)
                {
                    comma = gtNewCommaNode(comma.Type, sideEffectAddressStore, comma);
                }
                return comma;
            }

            // Evaluate the second thing using recursion.
            dataRef = comma.Op2;
            gtUpdateNodeSideEffects(store);
            return impStoreStruct(store, ref afterStmt, curLevel, usedDI, block);
        }

        if ((storeOper is GT_STORE_LCL_VAR) && dataRef.IsMultiRegNode)
        {
            lvaGetDesc(store.AsLclVar().LclNum).IsMultiRegDest = true;
        }
        return store;
    }

    public void impStoreToTemp(int lclNum, GenTree val, int curLevel, in DebugInfo di = default, BasicBlock? block = null)
        => impStoreToTemp(lclNum, val, ref Unsafe.NullRef<Statement>(), curLevel, di, block);

    /// <summary>Append a store of the given value to a temp to the current tree list.</summary>
    /// <param name="lclNum"></param>
    /// <param name="val"></param>
    /// <param name="curLevel">The stack level for which the spill to the temp is being done.</param>
    /// <param name="afterStmt"></param>
    /// <param name="di"></param>
    /// <param name="block"></param>
    public void impStoreToTemp(int lclNum, GenTree val, ref Statement afterStmt, int curLevel, in DebugInfo di = default, BasicBlock? block = null)
    {
        var store = gtNewTempStore(lclNum, val, ref afterStmt, curLevel, di, block);

        if (!store.IsNothingNode)
        {
            if (!Unsafe.IsNullRef(in afterStmt))
            {
                assert(block is not null);
                var storeStmt = gtNewStmt(store, di);

                fgInsertStmtAfter(block, afterStmt, storeStmt);
                afterStmt = storeStmt;
            }
            else
            {
                _ = impAppendTree(store, curLevel, impCurStmtDI);
            }
        }
    }

    private static unsafe void impValidateMemoryAccessOpcode(byte* codeAddr, byte* codeEndp, bool volatilePrefix)
    {
        var opcode = impGetNonPrefixOpcode(codeAddr, codeEndp);

        if (opcode is (>= CEE_LDIND_I1 and <= CEE_STIND_R8) or CEE_STIND_I or CEE_LDFLD or CEE_STFLD or CEE_LDOBJ or CEE_STOBJ or CEE_INITBLK or CEE_CPBLK)
        {
            // Opcode of all ldind and stdind happen to be in continuous, except stind.i.
            return;
        }

        if (volatilePrefix && (opcode is CEE_LDSFLD or CEE_STSFLD))
        {
            // volatile. prefix is allowed with the ldsfld and stsfld
            return;
        }

        BADCODE("Invalid opcode for unaligned. or volatile. prefix");
    }

    public unsafe void impVerifyEHBlock(BasicBlock block)
    {
        assert(block.hasTryIndex);
        assert(!compIsForInlining || opts.compInlineMethodsWithEH);

        var tryIndex = block.TryIndex;
        ref var HBtab = ref ehGetDsc(tryIndex);

        if (bbIsTryBeg(block) && (block.bbStackDepthOnEntry is not 0))
        {
            BADCODE("Evaluation stack must be empty on entry into a try block");
        }

        // Save the stack contents, we'll need to restore it later
        impSaveStackState(out var blockState, copy: false);

        while (!Unsafe.IsNullRef(in HBtab))
        {
            // Recursively process the handler block, if we haven't already done so.
            var hndBegBB = HBtab.ebdHndBeg;

            if (!hndBegBB.HasFlag(BBF_IMPORTED) && (impGetPendingBlockMember(hndBegBB) is 0))
            {
                // Construct the proper verification stack state either empty or one that contains just the Exception Object that we are dealing with
                stackState.esStackDepth = 0;

                if (handlerGetsXcptnObj(hndBegBB.CatchType))
                {
                    CORINFO_CLASS_HANDLE clsHnd;

                    if (HBtab.HasFilter)
                    {
                        clsHnd = impObjectClass;
                    }
                    else
                    {
                        CORINFO_RESOLVED_TOKEN resolvedToken;

                        resolvedToken.tokenContext = impTokenLookupContextHandle;
                        resolvedToken.tokenScope = info.compScopeHnd;
                        resolvedToken.token = (int)(HBtab.ebdTyp);
                        resolvedToken.tokenType = CORINFO_TOKENKIND_Class;
                        info.compCompHnd->resolveToken(&resolvedToken);

                        clsHnd = resolvedToken.hClass;
                    }

                    // push catch arg the stack, spill to a temp if necessary
                    // Note: can update HBtab->ebdHndBeg!
                    hndBegBB = impPushCatchArgOnStack(hndBegBB, clsHnd, false);
                }

                // Queue up the handler for importing
                //
                impImportBlockPending(hndBegBB);
            }

            // Process the filter block, if we haven't already done so.
            if (HBtab.HasFilter)
            {
                var filterBB = HBtab.ebdFilter;

                if (!filterBB.HasFlag(BBF_IMPORTED) && (impGetPendingBlockMember(filterBB) is 0))
                {
                    stackState.esStackDepth = 0;

                    // push catch arg the stack, spill to a temp if necessary
                    // Note: can update HBtab->ebdFilter!
                    var isSingleBlockFilter = (filterBB.Next == hndBegBB);
                    filterBB = impPushCatchArgOnStack(filterBB, impObjectClass, isSingleBlockFilter);

                    impImportBlockPending(filterBB);
                }
            }

            // Now process our enclosing try index (if any)
            tryIndex = HBtab.ebdEnclosingTryIndex;

            if (tryIndex == EHblkDsc.NO_ENCLOSING_INDEX)
            {
                HBtab = ref Unsafe.NullRef<EHblkDsc>();
            }
            else
            {
                HBtab = ref ehGetDsc(tryIndex);
            }
        }

        // Restore the stack contents
        impRestoreStackState(blockState);
    }

    public void impWalkSpillCliqueFromPred(BasicBlock block, Action<SpillCliqueDir, BasicBlock> callback)
    {
        var toDo = true;

        var succCliqueToDo = null as BlockListNode;
        var predCliqueToDo = new BlockListNode(block);

        while (toDo)
        {
            toDo = false;

            // Look at the successors of every member of the predecessor to-do list.
            while (predCliqueToDo is not null)
            {
                var node = predCliqueToDo;
                predCliqueToDo = node.Next;

                var blk = node.Blk;
                FreeBlockListNode(node);

                foreach (var succ in blk.Succs)
                {
                    // If it's not already in the clique, add it, and also add it
                    // as a member of the successor "toDo" set.
                    if (impSpillCliqueGetMember(SpillCliqueSucc, succ) is 0)
                    {
                        callback(SpillCliqueSucc, succ);
                        impSpillCliqueSetMember(SpillCliqueSucc, succ, 1);
                        succCliqueToDo = new BlockListNode(succ, succCliqueToDo);
                        toDo = true;
                    }
                }
            }

            // Look at the predecessors of every member of the successor to-do list.
            while (succCliqueToDo is not null)
            {
                var node = succCliqueToDo;
                succCliqueToDo = node.Next;

                var blk = node.Blk;
                FreeBlockListNode(node);

                foreach (var predBlock in blk.PredBlocks)
                {
                    // If it's not already in the clique, add it, and also add it
                    // as a member of the predecessor "toDo" set.
                    if (impSpillCliqueGetMember(SpillCliquePred, predBlock) is 0)
                    {
                        callback(SpillCliquePred, predBlock);
                        impSpillCliqueSetMember(SpillCliquePred, predBlock, 1);
                        predCliqueToDo = new BlockListNode(predBlock, predCliqueToDo);
                        toDo = true;
                    }
                }
            }
        }

        // If this fails, it means we didn't walk the spill clique properly and somehow managed
        // miss walking back to include the predecessor we started from.
        // This most likely cause: missing or out of date bbPreds
        assert(impSpillCliqueGetMember(SpillCliquePred, block) is not 0);
    }

    public byte impSpillCliqueGetMember(SpillCliqueDir predOrSucc, BasicBlock blk)
    {
        if (predOrSucc == SpillCliquePred)
        {
            return impInlineRoot.impSpillCliquePredMembers[blk.bbInd];
        }
        else
        {
            assert(predOrSucc == SpillCliqueSucc);
            return impInlineRoot.impSpillCliqueSuccMembers[blk.bbInd];
        }
    }

    public void impSpillCliqueSetMember(SpillCliqueDir predOrSucc, BasicBlock blk, byte val)
    {
        if (predOrSucc == SpillCliquePred)
        {
            impInlineRoot.impSpillCliquePredMembers[blk.bbInd] = val;
        }
        else
        {
            assert(predOrSucc == SpillCliqueSucc);
            impInlineRoot.impSpillCliqueSuccMembers[blk.bbInd] = val;
        }
    }

    /// <summary>make observations that help determine the profitability of a discretionary inline</summary>
    /// <param name="inlineInfo">InlineInfo for the inline, or null for the prejit root</param>
    /// <param name="inlineResult">InlineResult accumulating information about this inline</param>
    /// <remarks>
    ///   <para>If inlining or prejitting the root, this method also makes various observations about the method that factor into inline decisions.</para>
    ///   <para>It sets `compNativeSizeEstimate` as a side effect.</para>
    /// </remarks>
    public unsafe void impMakeDiscretionaryInlineObservations(InlineInfo? inlineInfo, InlineResult inlineResult)
    {
        assert((inlineInfo is not null) == compIsForInlining);

        // If we're really inlining, we should just have one result in play.
        assert((inlineInfo is null) || (inlineResult == inlineInfo.inlineResult));

        // If this is a "forceinline" method, the JIT probably shouldn't have gone
        // to the trouble of estimating the native code size. Even if it did, it
        // shouldn't be relying on the result of this method.
        assert(inlineResult.Observation is InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);

        // Note if the caller contains NEWOBJ or NEWARR.
        var rootCompiler = impInlineRoot;

        if ((rootCompiler.optMethodFlags & OMF_HAS_NEWARRAY) != 0)
        {
            inlineResult.Note(InlineObservation.CALLER_HAS_NEWARRAY);
        }

        if ((rootCompiler.optMethodFlags & OMF_HAS_NEWOBJ) != 0)
        {
            inlineResult.Note(InlineObservation.CALLER_HAS_NEWOBJ);
        }

        var calleeIsStatic = (info.compFlags & CORINFO_FLG_STATIC) != 0;
        var isSpecialMethod = (info.compFlags & CORINFO_FLG_CONSTRUCTOR) != 0;

        if (isSpecialMethod)
        {
            if (calleeIsStatic)
            {
                inlineResult.Note(InlineObservation.CALLEE_IS_CLASS_CTOR);
            }
            else
            {
                inlineResult.Note(InlineObservation.CALLEE_IS_INSTANCE_CTOR);
            }
        }
        else if (!calleeIsStatic)
        {
            // Callee is an instance method.
            // Check if the callee has the same 'this' as the root.

            if (inlineInfo is not null)
            {
                var iciCall = inlineInfo.iciCall;
                assert(iciCall is not null);

                var thisArg = iciCall.AsCall().Args.ThisArg;
                assert(thisArg is not null);

                var isSameThis = impIsThis(thisArg.Node);
                inlineResult.NoteBool(InlineObservation.CALLSITE_IS_SAME_THIS, isSameThis);
            }
        }

        ref var rootSigInst = ref rootCompiler.info.compMethodInfo->args.sigInst;
        ref var sigInst = ref info.compMethodInfo->args.sigInst;

        var callsiteIsGeneric = (rootSigInst.methInstCount != 0) || (rootSigInst.classInstCount != 0);
        var calleeIsGeneric = (sigInst.methInstCount != 0) || (sigInst.classInstCount != 0);

        if (!callsiteIsGeneric && calleeIsGeneric)
        {
            inlineResult.Note(InlineObservation.CALLSITE_NONGENERIC_CALLS_GENERIC);
        }

        // Inspect callee's arguments (and the actual values at the callsite for them)
        var sig = info.compMethodInfo->args;
        var sigArg = sig.args;

        CallArg? argUse = null;

        if (inlineInfo is not null)
        {
            var iciCall = inlineInfo.iciCall;
            assert(iciCall is not null);
            argUse = iciCall.AsCall().Args.Args.FirstOrDefault();
        }

        for (var i = 0; i < info.compMethodInfo->args.numArgs; i++)
        {
            if ((argUse is not null) && (argUse.WellKnownArg == WellKnownArg.ThisPointer))
            {
                argUse = argUse.Next;
            }

            CORINFO_CLASS_HANDLE sigClass;
            var corType = strip(info.compCompHnd->getArgType(&sig, sigArg, &sigClass));
            var argNode = argUse?.EarlyNode;

            if (corType == CORINFO_TYPE_CLASS)
            {
                sigClass = info.compCompHnd->getArgClass(&sig, sigArg);
            }
            else if (corType == CORINFO_TYPE_VALUECLASS)
            {
                inlineResult.Note(InlineObservation.CALLEE_ARG_STRUCT);
            }
            else if (corType == CORINFO_TYPE_BYREF)
            {
                sigClass = info.compCompHnd->getArgClass(&sig, sigArg);
                corType = info.compCompHnd->getChildType(sigClass, &sigClass);
            }

            if (argNode is not null)
            {
                var argCls = gtGetClassHandle(argNode, out var isExact, out var isNonNull);

                if (argCls is not null)
                {
                    var isArgValueType = eeIsValueClass(argCls);

                    // Exact class of the arg is known
                    if (isExact && !isArgValueType)
                    {
                        inlineResult.Note(InlineObservation.CALLSITE_ARG_EXACT_CLS);

                        if ((argCls != sigClass) && (sigClass is not null))
                        {
                            // .. but the signature accepts a less concrete type.
                            inlineResult.Note(InlineObservation.CALLSITE_ARG_EXACT_CLS_SIG_IS_NOT);
                        }
                    }
                    // Arg is a reference type in the signature and a boxed value type was passed.
                    else if (isArgValueType && (corType == CORINFO_TYPE_CLASS))
                    {
                        inlineResult.Note(InlineObservation.CALLSITE_ARG_BOXED);
                    }
                }

                if (argNode.Oper.IsConst)
                {
                    inlineResult.Note(InlineObservation.CALLSITE_ARG_CONST);
                }

                assert(argUse is not null);
                argUse = argUse.Next;
            }
            sigArg = info.compCompHnd->getArgNext(sigArg);
        }

        // Note if the callee's return type is a value type
        if (info.compMethodInfo->args.retType == CORINFO_TYPE_VALUECLASS)
        {
            inlineResult.Note(InlineObservation.CALLEE_RETURNS_STRUCT);
        }

        // Note if the callee's class is a promotable struct
        if ((info.compClassAttr & CORINFO_FLG_VALUECLASS) != 0)
        {
            assert(structPromotionHelper is not null);
            if (structPromotionHelper.CanPromoteStructType(info.compClassHnd))
            {
                inlineResult.Note(InlineObservation.CALLEE_CLASS_PROMOTABLE);
            }
            inlineResult.Note(InlineObservation.CALLEE_CLASS_VALUETYPE);
        }

#if FEATURE_SIMD

        // Note if this method is has simd args or return value
        if ((inlineInfo is not null) && inlineInfo.hasSimdTypeArgLocalOrReturn)
        {
            inlineResult.Note(InlineObservation.CALLEE_HAS_Simd);
        }

#endif

        // Roughly classify callsite frequency.
        var frequency = InlineCallsiteFrequency.UNUSED;

        // If this is a prejit root, or a maximally hot block...
        if (inlineInfo is null)
        {
            frequency = InlineCallsiteFrequency.HOT;
        }
        else
        {
            var iciBlock = inlineInfo.iciBlock;
            assert(iciBlock is not null);

            // No training data.  Look for loop-like things.
            // We consider a recursive call loop-like.  Do not give the inlining boost to the method itself.
            // However, give it to things nearby.
            if (iciBlock.isMaxBBWeight)
            {
                frequency = InlineCallsiteFrequency.HOT;
            }
            else if (iciBlock.HasFlag(BBF_BACKWARD_JUMP) &&
                     (inlineInfo.fncHandle != inlineInfo.inlineCandidateInfo.ilCallerHandle))
            {
                frequency = InlineCallsiteFrequency.LOOP;
            }
            else if (iciBlock.hasProfileWeight && (iciBlock.bbWeight > BB_ZERO_WEIGHT))
            {
                frequency = InlineCallsiteFrequency.WARM;
            }
            // Now modify the multiplier based on where we're called from.
            else if (iciBlock.isRunRarely || ((info.compFlags & FLG_CCTOR) == FLG_CCTOR))
            {
                frequency = InlineCallsiteFrequency.RARE;
            }
            else
            {
                frequency = InlineCallsiteFrequency.BORING;
            }
        }

        // Also capture the block weight of the call site.
        //
        // In the prejit root case, assume at runtime there might be a hot call site
        // for this method, so we won't prematurely conclude this method should never
        // be inlined.
        //
        weight_t weight = 0;

        if (inlineInfo is not null)
        {
            assert(inlineInfo.iciBlock is not null);
            weight = inlineInfo.iciBlock.bbWeight;
        }
        else
        {
            const weight_t prejitHotCallerWeight = 1000000.0;
            weight = prejitHotCallerWeight;
        }

        inlineResult.NoteInt(InlineObservation.CALLSITE_FREQUENCY, (int)(frequency));
        inlineResult.NoteInt(InlineObservation.CALLSITE_WEIGHT, (int)(weight));

        var hasProfile = false;
        var profileFreq = 0.0;

        // If the call site has profile data, report the relative frequency of the site.
        if ((inlineInfo is not null) && rootCompiler.fgHaveSufficientProfileWeights)
        {
            assert(inlineInfo.iciBlock is not null);
            var callSiteWeight = inlineInfo.iciBlock.bbWeight;
            var entryWeight = rootCompiler.fgCalledCount;
            profileFreq = fgProfileWeightsEqual(entryWeight, 0.0) ? 0.0 : callSiteWeight / entryWeight;
            hasProfile = true;

            assert(callSiteWeight >= 0);
            assert(entryWeight >= 0);
        }
        else if (inlineInfo is null)
        {
            // Simulate a hot callsite for PrejitRoot mode.
            hasProfile = true;
            profileFreq = 1.0;
        }

        inlineResult.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, hasProfile);
        inlineResult.NoteDouble(InlineObservation.CALLSITE_PROFILE_FREQUENCY, profileFreq);
    }

    public unsafe var_types impNormStructType(CORINFO_CLASS_HANDLE structHnd)
        => impNormStructType(structHnd, out Unsafe.NullRef<var_types>());

    /// <summary>Normalize the type of a (known to be) struct class handle.</summary>
    /// <param name="structHnd">The class handle for the struct type of interest.</param>
    /// <param name="pSimdBaseJitType">if the struct is a simd type, set to the simd base JIT type</param>
    /// <returns>The JIT type for the struct (e.g. TYP_STRUCT, or TYP_Simd*).</returns>
    /// <remarks>
    ///   <para>This may also modify the compFloatingPointUsed flag if the type is a simd type.</para>
    ///   <para>Normalizing the type involves examining the struct type to determine if it should be modified to one that is handled specially by the JIT, possibly being a candidate for full enregistration, e.g. TYP_Simd16.</para>
    ///   <para>If the size of the struct is already known call <see cref="structSizeMightRepresentSimdType" /> to determine if this api needs to be called.</para>
    /// </remarks>
    public unsafe var_types impNormStructType(CORINFO_CLASS_HANDLE structHnd, out var_types pSimdBaseJitType)
    {
        Unsafe.SkipInit(out pSimdBaseJitType);

        assert(structHnd != NO_CLASS_HANDLE);
        var structType = TYP_STRUCT;

#if FEATURE_SIMD
        var structFlags = info.compCompHnd->getClassAttribs(structHnd);

        // Don't bother if the struct contains GC references of byrefs, it can't be a simd type.
        if ((structFlags & (CORINFO_FLG_CONTAINS_GC_PTR | CORINFO_FLG_BYREF_LIKE)) == 0)
        {
            var originalSize = info.compCompHnd->getClassSize(structHnd);

            if (structSizeMightRepresentSimdType(originalSize))
            {
                var simdBaseType = getBaseTypeAndSizeOfSimdType(structHnd, out var sizeBytes);

                if (simdBaseType != TYP_UNDEF)
                {
                    assert((sizeBytes == originalSize) || (sizeBytes is SIZE_UNKNOWN));
                    structType = GetSimdTypeForSize(sizeBytes);

                    if (!Unsafe.IsNullRef(in pSimdBaseJitType))
                    {
                        pSimdBaseJitType = simdBaseType;
                    }

                    // Also indicate that we use floating point registers.
                    compFloatingPointUsed = true;
                }
            }
        }
#endif

        return structType;
    }

    public static bool impValidSpilledStackEntry(GenTree tree)
    {
        var oper = tree.Oper;
        return (oper is GT_LCL_VAR) || oper.IsConst;
    }

    /// <summary>Set the pre-state of "block" (which should not have a pre-state allocated) to a copy of "srcState", cloning tree pointers as required.</summary>
    /// <param name="block"></param>
    /// <param name="srcState"></param>
    public void initBBEntryState(BasicBlock block, EntryState srcState)
    {
        ref var dstState = ref block.EntryState;

        var depth = srcState.esStackDepth;
        dstState.esStackDepth = depth;

        if (depth is not 0)
        {
            var srcStack = srcState.esStack.AsSpan(0, depth);
            dstState.esStack = [.. srcState.esStack];
            var dstStack = dstState.esStack.AsSpan(0, depth);

            for (var level = 0; level < srcStack.Length; level++)
            {
                var tree = srcStack[level].val;
                dstStack[level].val = gtCloneExpr(tree);
            }
        }
        else
        {
            dstState.esStack = [];
        }
    }

    public void initCurrentState()
    {
        // initialize stack info
        stackState.esStackDepth = 0;

        assert(fgFirstBB is not null);

        // copy current state to entry state of first BB
        initBBEntryState(fgFirstBB, stackState);
    }

    public bool IsIntrinsicImplementedByUserCall(NamedIntrinsic intrinsicName)
    {
        // Currently, if a math intrinsic is not implemented by target-specific
        // instructions, it will be implemented by a System.Math call. In the
        // future, if we turn to implementing some of them with helper calls,
        // this predicate needs to be revisited.
        return !IsTargetIntrinsic(intrinsicName);
    }

    public bool IsMathIntrinsic(NamedIntrinsic intrinsicName)
    {
        switch (intrinsicName)
        {
            case NI_System_Math_Abs:
            case NI_System_Math_Acos:
            case NI_System_Math_Acosh:
            case NI_System_Math_Asin:
            case NI_System_Math_Asinh:
            case NI_System_Math_Atan:
            case NI_System_Math_Atanh:
            case NI_System_Math_Atan2:
            case NI_System_Math_Cbrt:
            case NI_System_Math_Ceiling:
            case NI_System_Math_Cos:
            case NI_System_Math_Cosh:
            case NI_System_Math_Exp:
            case NI_System_Math_Floor:
            case NI_System_Math_FusedMultiplyAdd:
            case NI_System_Math_ILogB:
            case NI_System_Math_Log:
            case NI_System_Math_Log2:
            case NI_System_Math_Log10:
            case NI_System_Math_Max:
            case NI_System_Math_MaxMagnitude:
            case NI_System_Math_MaxMagnitudeNumber:
            case NI_System_Math_MaxNative:
            case NI_System_Math_MaxNumber:
            case NI_System_Math_MaxUnsigned:
            case NI_System_Math_Min:
            case NI_System_Math_MinMagnitude:
            case NI_System_Math_MinMagnitudeNumber:
            case NI_System_Math_MinNative:
            case NI_System_Math_MinNumber:
            case NI_System_Math_MinUnsigned:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_Pow:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            case NI_System_Math_Round:
            case NI_System_Math_Sin:
            case NI_System_Math_Sinh:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Tan:
            case NI_System_Math_Tanh:
            case NI_System_Math_Truncate:
            {
                assert((intrinsicName > NI_SYSTEM_MATH_START) && (intrinsicName < NI_SYSTEM_MATH_END));
                return true;
            }

            default:
            {
                assert((intrinsicName < NI_SYSTEM_MATH_START) || (intrinsicName > NI_SYSTEM_MATH_END));
                return false;
            }
        }
    }

    public bool IsTargetIntrinsic(NamedIntrinsic intrinsicName)
    {
        switch (intrinsicName)
        {
#if TARGET_XARCH
            case NI_System_Math_Abs:
            case NI_System_Math_Ceiling:
            case NI_System_Math_Floor:
            case NI_System_Math_Max:
            case NI_System_Math_MaxMagnitude:
            case NI_System_Math_MaxMagnitudeNumber:
            case NI_System_Math_MaxNative:
            case NI_System_Math_MaxNumber:
            case NI_System_Math_Min:
            case NI_System_Math_MinMagnitude:
            case NI_System_Math_MinMagnitudeNumber:
            case NI_System_Math_MinNative:
            case NI_System_Math_MinNumber:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            case NI_System_Math_Round:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Truncate:
            {
                return true;
            }

            case NI_System_Math_FusedMultiplyAdd:
            {
                return compOpportunisticallyDependsOn(InstructionSet_AVX2);
            }
#elif TARGET_ARM64
            case NI_System_Math_Abs:
            case NI_System_Math_Ceiling:
            case NI_System_Math_Floor:
            case NI_System_Math_FusedMultiplyAdd:
            case NI_System_Math_Max:
            case NI_System_Math_MaxMagnitude:
            case NI_System_Math_MaxMagnitudeNumber:
            case NI_System_Math_MaxNative:
            case NI_System_Math_MaxNumber:
            case NI_System_Math_Min:
            case NI_System_Math_MinMagnitude:
            case NI_System_Math_MinMagnitudeNumber:
            case NI_System_Math_MinNative:
            case NI_System_Math_MinNumber:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            case NI_System_Math_Round:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Truncate:
            {
                return true;
            }
#elif TARGET_ARM
            case NI_System_Math_Abs:
            case NI_System_Math_Sqrt:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            {
                return true;
            }
#elif TARGET_RISCV64
            case NI_System_Math_Abs:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Max:
            case NI_System_Math_MaxNumber:
            case NI_System_Math_MaxNative:
            case NI_System_Math_Min:
            case NI_System_Math_MinNumber:
            case NI_System_Math_MinNative:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            {
                return true;
            }

            case NI_System_Math_MinUnsigned:
            case NI_System_Math_MaxUnsigned:
            case NI_PRIMITIVE_LeadingZeroCount:
            case NI_PRIMITIVE_TrailingZeroCount:
            case NI_PRIMITIVE_PopCount:
            {
                return compOpportunisticallyDependsOn(InstructionSet_Zbb);
            }
#elif TARGET_LOONGARCH64
            case NI_System_Math_Abs:
            case NI_System_Math_Sqrt:
            case NI_System_Math_ReciprocalSqrtEstimate:
            {
                // TODO-LoongArch64: support these standard intrinsics
                return false;
            }

            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            {
                return true;
            }
#elif TARGET_WASM
            case NI_System_Math_Abs:
            case NI_System_Math_Ceiling:
            case NI_System_Math_Floor:
            case NI_System_Math_Max:
            case NI_System_Math_MaxNative:
            case NI_System_Math_Min:
            case NI_System_Math_MinNative:
            case NI_System_Math_Round:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Truncate:
            case NI_PRIMITIVE_LeadingZeroCount:
            case NI_PRIMITIVE_TrailingZeroCount:
            case NI_PRIMITIVE_PopCount:
            {
                return true;
            }
#endif

            default:
                return false;
        }
    }

    public unsafe NamedIntrinsic lookupNamedIntrinsic(CORINFO_METHOD_HANDLE method)
    {
        // TODO: Port Compiler.lookupNamedIntrinsic
        return NI_Illegal;
    }

    public void ReimportSpillClique(SpillCliqueDir predOrSucc, BasicBlock blk)
    {
        // For Preds we could be a little smarter and just find the existing store
        // and re-type it/add a cast, but that is complicated and hopefully very rare, so
        // just re-import the whole block (just like we do for successors)

        if (!blk.HasFlag(BBF_IMPORTED) && (impGetPendingBlockMember(blk) is 0))
        {
            // If we haven't imported this block (EntryState is a null ref) and we're not going to
            // (because it isn't on the pending list) then just ignore it for now.
            assert(blk.EntryState.esStackDepth is 0);
            assert(blk.EntryState.esStack.Length is 0);
            return;
        }

        // For successors we have a valid stackState, so just mark them for reimport
        // the 'normal' way
        // Unlike predecessors, we *DO* need to reimport the current block because the
        // initial import had the wrong entry state types.
        // Similarly, blocks that are currently on the pending list, still need to call
        // impImportBlockPending to fixup their entry state.
        if (predOrSucc == SpillCliqueSucc)
        {
            impReimportMarkBlock(blk);

            // Set the current stack state to that of the blk->bbEntryState
            resetCurrentState(blk, ref stackState);

            impImportBlockPending(blk);
        }
        else if ((blk != compCurBB) && blk.HasFlag(BBF_IMPORTED))
        {
            // As described above, we are only visiting predecessors so they can
            // add the appropriate casts, since we have already done that for the current
            // block, it does not need to be reimported.
            // Nor do we need to reimport blocks that are still pending, but not yet
            // imported.
            //
            // For predecessors, we have no state to seed the EntryState, so we just have
            // to assume the existing one is correct.
            // If the block is also a successor, it will get the EntryState properly
            // updated when it is visited as a successor in the above "if" block.
            assert(predOrSucc == SpillCliquePred);
            impReimportBlockPending(blk);
        }
    }

    /// <summary>Resets the current state to the state at the start of the basic block</summary>
    /// <param name="block"></param>
    /// <param name="currentState"></param>
    public void resetCurrentState(BasicBlock block, ref EntryState currentState)
    {
        ref var entryState = ref block.EntryState;

        currentState.esStackDepth = entryState.esStackDepth;
        currentState.esStack = (entryState.esStack.Length is not 0) ? [.. entryState.esStack] : [];
    }
}
