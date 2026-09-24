// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

internal partial struct LocalAddressVisitor
{
    private readonly List<Value> _valueStack = [];
    private readonly GenTreeStack _ancestors = [];
    private Statement? _statement;
    private bool _madeChanges;
    private bool _propagatedAddrs;

    public static bool DoPreOrder => true;

    public static bool DoPostOrder => true;

    public static bool UseExecutionOrder => true;

    internal readonly bool MadeChanges => _madeChanges;

    internal readonly bool PropagatedAnyAddresses => _propagatedAddrs;

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<LocalAddressVisitor>.WalkTree(ref this, ref use, user, _ancestors);

    internal void VisitStmt(Statement stmt)
    {
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("LocalAddressVisitor visiting statement:\n");
            _compiler.gtDispStmt(stmt);
        }
#endif
        _statement = stmt;
        _stmtModified = false;
        _stmtSideEffectsModified = false;

        if (_sequenceLocals)
        {
            _sequencer.Start(stmt);
        }

        _ = WalkTree(ref stmt.RootNodeRef, user: null);
        EscapeValue(ref TopValue(0), user: null);
        PopValue();
        assert(_valueStack.Count is 0);
        _madeChanges |= _stmtModified;

        if (_stmtSideEffectsModified)
        {
            _compiler.gtUpdateStmtSideEffects(stmt);
        }

        if (_sequenceLocals)
        {
            if (_stmtModified)
            {
                _sequencer.Sequence(stmt);
            }
            else
            {
                _sequencer.Finish(stmt);
            }
        }

#if DEBUG
        if (_compiler.verbose)
        {
            if (_stmtModified)
            {
                jitprintf("LocalAddressVisitor modified statement:\n");
                _compiler.gtDispStmt(stmt);
            }

            jitprintf("\n");
        }
#endif
    }

    internal void VisitBlock(BasicBlock block)
    {
        _compiler.compCurBB = block;
        _lclAddrAssertions?.StartBlock(block);

        foreach (var stmt in block.Statements)
        {
            VisitStmt(stmt);
        }

        // JMP implicitly uses every argument, but is rare enough to check once
        // per block rather than on the hot visitor path.
        if (block.EndsWithJmpMethod(_compiler))
        {
            for (var lclNum = 0; lclNum < _compiler.info.compArgsCount; lclNum++)
            {
                UpdateEarlyRefCount(lclNum, node: null, user: null);
            }
        }

        _lclAddrAssertions?.EndBlock(block);
    }

    public Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        var localNode = false;

        switch (use.Oper)
        {
            case GT_IND:
            case GT_BLK:
            case GT_STOREIND:
            case GT_STORE_BLK:
            {
                localNode = MorphStructField(ref use, user);
                break;
            }

            case GT_FIELD_ADDR:
            {
                localNode = MorphStructFieldAddress(ref use, new ValueSize(0)) != BAD_VAR_NUM;
                break;
            }

            case GT_LCL_FLD:
            case GT_STORE_LCL_FLD:
            {
                MorphLocalField(ref use, user);
                localNode = true;
                break;
            }

            case GT_LCL_VAR:
            case GT_LCL_ADDR:
            case GT_STORE_LCL_VAR:
            {
                localNode = true;
                break;
            }

            case GT_QMARK:
            {
                return HandleQMarkSubTree(ref use, user);
            }
        }

        if (localNode)
        {
            var node = use;
            var lclNum = node.AsLclVarCommon().LclNum;
            ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
            UpdateEarlyRefCount(lclNum, node, user);

            if (varDsc.lvIsStructField)
            {
                assert(!_compiler.lvaIsImplicitByRefLocal(lclNum));
                UpdateEarlyRefCount(varDsc.lvParentLcl, node, user);
            }

            if (varDsc.lvPromoted)
            {
                for (var child = varDsc.lvFieldLclStart; child < varDsc.lvFieldLclStart + varDsc.lvFieldCnt; child++)
                {
                    UpdateEarlyRefCount(child, node, user);
                }
            }
        }

        PushValue(ref use, user);
        return Compiler.WALK_CONTINUE;
    }

    public Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
    {
        var node = use;

        switch (node.Oper)
        {
            case GT_STORE_LCL_FLD:
            {
                if (node.AsLclFld().IsPartial(_compiler))
                {
                    node.Flags |= GTF_VAR_USEASG;
                }
                goto case GT_STORE_LCL_VAR;
            }

            case GT_STORE_LCL_VAR:
            {
                assert(TopValue(0).Node == node.AsLclVarCommon().Data);

                if (_lclAddrAssertions is not null)
                {
                    HandleLocalStoreAssertions(node.AsLclVarCommon(), in TopValue(0));
                }

                EscapeValue(ref TopValue(0), node);
                PopValue();
                SequenceLocal(node.AsLclVarCommon());
                break;
            }

            case GT_LCL_VAR:
            {
                if (_lclAddrAssertions is not null)
                {
                    HandleLocalAssertions(node.AsLclVarCommon(), ref TopValue(0));
                }

                SequenceLocal(node.AsLclVarCommon());
                break;
            }

            case GT_LCL_FLD:
            {
                SequenceLocal(node.AsLclVarCommon());
                break;
            }

            case GT_LCL_ADDR:
            {
                assert(TopValue(0).Node == node);
                TopValue(0).Address(node.AsLclFld());
                SequenceLocal(node.AsLclVarCommon());
                break;
            }

            case GT_ADD:
            {
                var op = node.AsOp();
                assert(TopValue(2).Node == node);
                assert(TopValue(1).Node == op.Op1);
                assert(TopValue(0).Node == op.Op2);

                if (op.Op2.Oper.IsCnsIntOrI)
                {
                    var offset = op.Op2.AsIntCon().IconValue;

                    if ((offset >= 0) && ((ulong)offset <= uint.MaxValue) &&
                        TopValue(2).AddOffset(ref TopValue(1), (uint)offset))
                    {
                        TopValue(0).Consume();
                        PopValue();
                        PopValue();
                        break;
                    }
                }

                EscapeValue(ref TopValue(0), node);
                PopValue();
                EscapeValue(ref TopValue(0), node);
                PopValue();
                break;
            }

            case GT_SUB:
            {
                ref var rhs = ref TopValue(0);
                ref var lhs = ref TopValue(1);

                if (_compiler.opts.OptimizationEnabled && lhs.IsAddress && rhs.IsAddress)
                {
                    ref var lhsDsc = ref _compiler.lvaGetDesc(lhs.LclNum);
                    ref var rhsDsc = ref _compiler.lvaGetDesc(rhs.LclNum);
                    var lhsLclNum = lhs.LclNum;
                    var rhsLclNum = rhs.LclNum;
                    var lhsOffset = lhs.Offset;
                    var rhsOffset = rhs.Offset;

                    if (lhsDsc.lvIsStructField)
                    {
                        lhsLclNum = lhsDsc.lvParentLcl;
                        lhsOffset = unchecked(lhsOffset + lhsDsc.lvFldOffset);
                    }

                    if (rhsDsc.lvIsStructField)
                    {
                        rhsLclNum = rhsDsc.lvParentLcl;
                        rhsOffset = unchecked(rhsOffset + rhsDsc.lvFldOffset);
                    }

                    if ((lhsLclNum == rhsLclNum) && (rhsOffset <= lhsOffset) &&
                        ((lhsOffset - rhsOffset) <= int.MaxValue))
                    {
                        // Native tolerates an incorrectly typed BYREF SUB left by inlining.
                        assert(node.Type is TYP_I_IMPL or TYP_BYREF);
                        var constant = new GenTreeIntCon(TYP_I_IMPL, (nint)(lhsOffset - rhsOffset),
                            fields: null, node, ReplacementThreading);
                        constant._vnPair.SetBoth(ValueNumStore.NoVN);
                        ReplaceNode(ref use, constant);
                        lhs.Consume();
                        rhs.Consume();
                        PopValue();
                        PopValue();
                        _stmtModified = true;
                        break;
                    }
                }

                EscapeValue(ref TopValue(0), node);
                PopValue();
                EscapeValue(ref TopValue(0), node);
                PopValue();
                break;
            }

            case GT_FIELD_ADDR:
            {
                var address = node.AsFieldAddr();

                if (address.IsInstance)
                {
                    assert(TopValue(1).Node == node);
                    assert(TopValue(0).Node == address.FldObj);

                    if (!TopValue(1).AddOffset(ref TopValue(0), unchecked((uint)address.FldOffset)))
                    {
                        EscapeValue(ref TopValue(0), node);
                    }

                    PopValue();
                }
                else
                {
                    assert(TopValue(0).Node == node);
                }
                break;
            }

            case GT_STOREIND:
            case GT_STORE_BLK:
            {
                var indir = node.AsIndir();
                assert(TopValue(2).Node == node);
                assert(TopValue(1).Node == indir.Addr);
                assert(TopValue(0).Node == indir.Data);
                EscapeValue(ref TopValue(0), node);

                if (indir.IsVolatile || !TopValue(1).IsAddress)
                {
                    EscapeValue(ref TopValue(1), node);
                }
                else
                {
                    ProcessIndirection(ref use, ref TopValue(1), user);

                    if ((_lclAddrAssertions is not null) && use.Oper.IsLocalStore)
                    {
                        HandleLocalStoreAssertions(use.AsLclVarCommon(), in TopValue(0));
                    }
                }

                PopValue();
                PopValue();
                break;
            }

            case GT_BLK:
            case GT_IND:
            {
                var indir = node.AsIndir();
                assert(TopValue(1).Node == node);
                assert(TopValue(0).Node == indir.Addr);

                if (indir.IsVolatile || !TopValue(0).IsAddress)
                {
                    EscapeValue(ref TopValue(0), node);
                }
                else
                {
                    ProcessIndirection(ref use, ref TopValue(0), user);

                    if ((_lclAddrAssertions is not null) && (use.Oper is GT_LCL_VAR))
                    {
                        HandleLocalAssertions(use.AsLclVarCommon(), ref TopValue(1));
                    }
                }

                PopValue();
                break;
            }

            case GT_CAST:
            {
                var cast = node.AsCast();
                assert(TopValue(1).Node == node);
                assert(TopValue(0).Node == cast.Op1);
                var pointerCast = cast.CastType is TYP_I_IMPL or TYP_U_IMPL or TYP_BYREF;

                if (!pointerCast || node.HasOverflowCheck || !TopValue(0).IsAddress ||
                    !TopValue(1).AddOffset(ref TopValue(0), 0))
                {
                    EscapeValue(ref TopValue(0), node);
                }

                PopValue();
                break;
            }

            case GT_CALL:
            {
                while (TopValue(0).Node != node)
                {
                    EscapeValue(ref TopValue(0), node);
                    PopValue();
                }

                SequenceCall(node.AsCall());
                break;
            }

            case GT_EQ:
            case GT_NE:
            {
                assert(TopValue(2).Node == node);
                assert(TopValue(1).Node == node.AsOp().Op1);
                assert(TopValue(0).Node == node.AsOp().Op2);
                ref var lhs = ref TopValue(1);
                ref var rhs = ref TopValue(0);

                if ((lhs.IsAddress && rhs.Node.IsIntegralConst(0)) || (rhs.IsAddress && lhs.Node.IsIntegralConst(0)))
                {
#if DEBUG
                    JITDUMP($"Rewriting known address vs null comparison [{node.TreeId:D6}]\n");
#endif
                    ReplaceNode(ref lhs.Use, _compiler.gtNewIconNode(TYP_INT, 0));
                    ReplaceNode(ref rhs.Use, _compiler.gtNewIconNode(TYP_INT, 1));
                    _stmtModified = true;
                    rhs.Consume();
                    lhs.Consume();
                    PopValue();
                    PopValue();
                }
                else if (lhs.IsAddress && rhs.IsAddress)
                {
#if DEBUG
                    JITDUMP($"Rewriting known address vs address comparison [{node.TreeId:D6}]\n");
#endif
                    var sameAddress = lhs.IsSameAddress(in rhs);
                    ReplaceNode(ref lhs.Use, _compiler.gtNewIconNode(TYP_INT, 0));
                    ReplaceNode(ref rhs.Use, _compiler.gtNewIconNode(TYP_INT, sameAddress ? 0 : 1));
                    _stmtModified = true;
                    rhs.Consume();
                    lhs.Consume();
                    PopValue();
                    PopValue();
                }
                else
                {
                    EscapeValue(ref TopValue(0), node);
                    PopValue();
                    EscapeValue(ref TopValue(0), node);
                    PopValue();
                }
                break;
            }

            case GT_NULLCHECK:
            {
                assert(TopValue(1).Node == node);
                assert(TopValue(0).Node == node.AsUnOp().Op1);

                if (TopValue(0).IsAddress)
                {
#if DEBUG
                    JITDUMP($"Bashing nullcheck of local [{node.TreeId:D6}] to NOP\n");
#endif
                    node.BashToNOP();
                    TopValue(0).Consume();
                    PopValue();
                    _stmtModified = true;
                }
                else
                {
                    EscapeValue(ref TopValue(0), node);
                    PopValue();
                }
                break;
            }

            default:
            {
                while (TopValue(0).Node != node)
                {
                    EscapeValue(ref TopValue(0), node);
                    PopValue();
                }
                break;
            }
        }

        assert(TopValue(0).Node == use);
        return Compiler.WALK_CONTINUE;
    }

    private Compiler.fgWalkResult HandleQMarkSubTree(ref GenTree use, GenTree? user)
    {
        var qmark = use.AsQmark();
        var colon = qmark.Op2.AsOp();
        assert(!qmark.IsReverseOp);

        if (WalkTree(ref qmark.Op1Ref, qmark) is Compiler.WALK_ABORT)
        {
            return Compiler.WALK_ABORT;
        }

        if (_lclAddrAssertions is not null)
        {
            // Each branch starts with the condition's facts. AlwaysAssertions
            // only shrinks and therefore does not need saving and restoring.
            var original = _lclAddrAssertions.CurrentAssertions;

            if (WalkTree(ref colon.Op1Ref, colon) is Compiler.WALK_ABORT)
            {
                return Compiler.WALK_ABORT;
            }

            var first = _lclAddrAssertions.CurrentAssertions;
            _lclAddrAssertions.CurrentAssertions = original;

            if (WalkTree(ref colon.Op2Ref, colon) is Compiler.WALK_ABORT)
            {
                return Compiler.WALK_ABORT;
            }

            _lclAddrAssertions.CurrentAssertions &= first;
        }
        else if ((WalkTree(ref colon.Op1Ref, colon) is Compiler.WALK_ABORT) ||
                 (WalkTree(ref colon.Op2Ref, colon) is Compiler.WALK_ABORT))
        {
            return Compiler.WALK_ABORT;
        }

        assert(TopValue(0).Node == colon.Op2);
        assert(TopValue(1).Node == colon.Op1);
        assert(TopValue(2).Node == qmark.Op1);
        EscapeValue(ref TopValue(0), colon);
        PopValue();
        EscapeValue(ref TopValue(0), colon);
        PopValue();
        EscapeValue(ref TopValue(0), qmark);
        PopValue();
        PushValue(ref use, user);
        return Compiler.WALK_SKIP_SUBTREES;
    }

    private readonly void PushValue(ref GenTree use, GenTree? user)
    {
        assert(_statement is not null);
        _valueStack.Add(new Value(_statement, ref use, user));
    }

    private readonly ref Value TopValue(int index) => ref CollectionsMarshal.AsSpan(_valueStack)[_valueStack.Count - 1 - index];

    private readonly void PopValue()
    {
#if DEBUG
        assert(TopValue(0).IsConsumed);
#endif
        _valueStack.RemoveAt(_valueStack.Count - 1);
    }

    private readonly void HandleLocalStoreAssertions(GenTreeLclVarCommon store, in Value data)
    {
        assert(_lclAddrAssertions is not null);
        _lclAddrAssertions.Clear(store.LclNum);

        if (data.IsAddress && (store.Oper is GT_STORE_LCL_VAR))
        {
            ref var varDsc = ref _compiler.lvaGetDesc(store.LclNum);

            // Promoted locals are not propagated here; nearly all have ldaddr
            // uses anyway, matching the native optimization restriction.
            if (!varDsc.lvPromoted && !varDsc.lvIsStructField && !varDsc.lvHasLdAddrOp)
            {
                _lclAddrAssertions.Record(store.LclNum, data.LclNum, data.Offset);
            }
        }
    }

    private void HandleLocalAssertions(GenTreeLclVarCommon local, ref Value value)
    {
        assert((local.Oper is GT_LCL_VAR) && (_lclAddrAssertions is not null));

        if (local.Type is not TYP_I_IMPL and not TYP_BYREF)
        {
            return;
        }

        if (_lclAddrAssertions.GetCurrentAssertion(local.LclNum) is LocalEqualsLocalAddrAssertion assertion)
        {
            JITDUMP("Using assertion ");
#if DEBUG
            if (_compiler.verbose)
            {
                assertion.Print();
            }
#endif
            JITDUMP("\n");
            value.Address(assertion.AddressLclNum, assertion.AddressOffset);
            _propagatedAddrs = true;
        }
    }

    private void SequenceLocal(GenTreeLclVarCommon local)
    {
        if (_sequenceLocals)
        {
            _sequencer.SequenceLocal(local);
        }
    }

    private void SequenceCall(GenTreeCall call)
    {
        if (_sequenceLocals)
        {
            _sequencer.SequenceCall(call);
        }
    }
}
