// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private ClassLayout GetNonGCLayout(ClassLayout layout)
    {
        assert(layout.HasGCPtr);
        var compiler = CompilerInstance;
        var builder = new ClassLayoutBuilder(compiler, layout.Size);
        builder.CopyPaddingFrom(0, layout);
#if DEBUG
        builder.CopyNameFrom(layout, "[nongc] ");
#endif
        return compiler.typGetCustomLayout(builder);
    }

    private ClassLayout GetByrefLayout(ClassLayout layout)
    {
        assert(layout.HasGCPtr);
        var compiler = CompilerInstance;
        var builder = new ClassLayoutBuilder(compiler, layout.Size);
        builder.CopyPaddingFrom(0, layout);

        if (layout.GCPtrCount > 0)
        {
            for (var slot = 0; slot < layout.SlotCount; slot++)
            {
                var type = layout.GetGCPtrType(slot);
                if (type is TYP_REF)
                {
                    type = TYP_BYREF;
                }
                builder.SetGCPtrType(slot, type);
            }
        }

#if DEBUG
        builder.CopyNameFrom(layout, "[byref] ");
#endif
        return compiler.typGetCustomLayout(builder);
    }

    private void UpdateAncestorTypes(GenTree tree, GenTreeStack ancestors,
        var_types newType, ClassLayout? newLayout, bool retypeFields)
    {
        assert(newType is TYP_BYREF or TYP_I_IMPL);
        var compiler = CompilerInstance;
        var parentIndex = 1;
        var keepChecking = true;
        var sawIndir = false;

        while (keepChecking && (ancestors.Count > parentIndex))
        {
            var parent = StackNode(ancestors, parentIndex);
            keepChecking = false;

            switch (parent.Oper)
            {
                case GT_STORE_LCL_VAR:
                {
                    if (parent.Type != newType)
                    {
                        var descriptor = compiler.lvaGetDesc(parent.AsLclVarCommon().LclNum);
                        if (parent.Type is TYP_REF || descriptor.Type == newType)
                        {
                            parent.ChangeType(newType);
                        }
                    }
                    break;
                }

                case GT_EQ:
                case GT_NE:
                case GT_LT:
                case GT_GT:
                case GT_LE:
                case GT_GE:
                {
                    var op = parent.AsOp();
                    if (op.Op1 == tree)
                    {
                        if (op.Op2.IsIntegralConst(0))
                        {
                            op.Op2.ChangeType(newType);
                        }
                    }
                    else if (op.Op2 == tree && op.Op1.IsIntegralConst(0))
                    {
                        op.Op1.ChangeType(newType);
                    }
                    break;
                }

                case GT_NULLCHECK:
                case GT_ARR_LENGTH:
                {
                    break;
                }

                case GT_COMMA:
                {
                    if (parent.AsOp().Op1 == StackNode(ancestors, parentIndex - 1))
                    {
                        break;
                    }
                    goto case GT_QMARK;
                }

                case GT_QMARK:
                case GT_ADD:
                case GT_FIELD_ADDR:
                case GT_BOX:
                {
                    if (parent.Type != newType)
                    {
                        parent.ChangeType(newType);
                    }
                    parentIndex++;
                    keepChecking = true;
                    break;
                }

                case GT_INDEX_ADDR:
                {
                    if (parent.Type != newType)
                    {
                        parent.ChangeType(newType);
                    }
                    break;
                }

                case GT_SUB:
                {
                    var parentType = parent.Type;
                    assert(parentType is not TYP_REF);
                    if (parentType != newType)
                    {
                        assert(newType is TYP_I_IMPL && parentType is TYP_BYREF);
                        parent.ChangeType(newType);
                        parentIndex++;
                        keepChecking = true;
                    }
                    break;
                }

                case GT_COLON:
                {
                    var op = parent.AsOp();
                    if (op.Op1 == tree)
                    {
                        assert(op.Op2.IsIntegralConst(0));
                        op.Op2.ChangeType(newType);
                    }
                    else
                    {
                        assert(op.Op2 == tree);
                        assert(op.Op1.IsIntegralConst(0));
                        op.Op1.ChangeType(newType);
                    }
                    parent.ChangeType(newType);
                    parentIndex++;
                    keepChecking = true;
                    break;
                }

                case GT_STOREIND:
                case GT_STORE_BLK:
                {
                    var indir = parent.AsIndir();
                    if (tree == indir.Addr)
                    {
                        parent.Flags &= ~GTF_IND_TGT_HEAP;
                        if (newType is not TYP_BYREF)
                        {
                            parent.Flags |= GTF_IND_TGT_NOT_HEAP;
                        }
                    }
                    else
                    {
                        assert(tree == indir.Data);
                        if (varTypeIsGC(parent.Type))
                        {
                            var wasRetyped = false;
                            if (_storeAddressToIndexMap.TryGetValue(parent, out var info) &&
                                (info.Index != BAD_VAR_NUM) && !DoesIndexPointToStack(info.Index))
                            {
                                parent.ChangeType(TYP_BYREF);
                                wasRetyped = true;
                            }
                            if (!wasRetyped)
                            {
                                parent.ChangeType(newType);
                            }
                        }
                        else if (retypeFields && parent.Oper is GT_STORE_BLK)
                        {
                            UpdateBlockLayout(parent.AsBlk(), newLayout);
                        }
                    }
                    break;
                }

                case GT_IND:
                case GT_BLK:
                {
                    if (retypeFields && !sawIndir)
                    {
                        var didRetype = false;
                        if (varTypeIsGC(parent.Type))
                        {
                            parent.ChangeType(newType);
                            didRetype = true;
                        }
                        else if (parent.Oper is GT_BLK)
                        {
                            didRetype = UpdateBlockLayout(parent.AsBlk(), newLayout);
                        }
                        if (didRetype)
                        {
                            parentIndex++;
                            keepChecking = true;
                            sawIndir = true;
                        }
                    }
                    break;
                }

                case GT_CALL:
                {
                    break;
                }

                default:
                {
#if DEBUG
                    JITDUMP($"UpdateAncestorTypes: unexpected op {parent.Oper} in [{parent.TreeId:D6}]\n");
#endif
                    throw new FatalJitException("Unexpected parent while retyping a stack-object use.");
                }
            }

            if (keepChecking)
            {
                tree = StackNode(ancestors, parentIndex - 1);
            }
        }
    }

    private bool UpdateBlockLayout(GenTreeBlk block, ClassLayout? newLayout)
    {
        var oldLayout = block.Layout;
        if (!oldLayout.HasGCPtr)
        {
            return false;
        }

        var layout = newLayout
            ?? throw new FatalJitException("Retyping struct fields requires their new layout.");
        if (layout.Size == oldLayout.Size)
        {
            block.Layout = layout;
        }
        else
        {
            assert(layout.Size > oldLayout.Size);
            block.Layout = layout.HasGCPtr ? GetByrefLayout(oldLayout) : GetNonGCLayout(oldLayout);
        }
        return true;
    }

    private unsafe struct RewriteUsesVisitor : IGenTreeVisitor<RewriteUsesVisitor>
    {
        public static bool DoPreOrder => true;
        public static bool DoPostOrder => true;
        public static bool ComputeStack => true;

        private readonly ObjectAllocator _allocator;
        private readonly GenTreeStack _ancestors;

        public RewriteUsesVisitor(ObjectAllocator allocator)
        {
            _allocator = allocator;
            _ancestors = [];
        }

        public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;
            if (!tree.Oper.IsAnyLocal)
            {
                return Compiler.WALK_CONTINUE;
            }

            var compiler = _allocator.CompilerInstance;
            var local = tree.AsLclVarCommon().LclNum;
            ref var descriptor = ref compiler.lvaGetDesc(local);
            if (!descriptor.lvTracked)
            {
                return Compiler.WALK_CONTINUE;
            }

            var newType = descriptor.Type;
            ClassLayout? newLayout = null;
            var retypeFields = false;
            if (_allocator._heapLocalToStackObjLocalMap.TryGetValue(local, out var stackLocal))
            {
                assert(tree.Oper is GT_LCL_VAR);
                newType = TYP_I_IMPL;
                tree = compiler.gtNewLclVarAddrNode(TYP_I_IMPL, stackLocal);
                use = tree;
#if DEBUG
                JITDUMP($"Update V{local:D2} to V{stackLocal:D2} in use [{tree.TreeId:D6}]\n");
                if (compiler.verbose)
                {
                    compiler.gtDispTree(tree);
                }
#endif
            }
            else if (newType is TYP_STRUCT)
            {
                if (tree.Oper is GT_STORE_LCL_VAR or GT_STORE_LCL_FLD)
                {
                    return Compiler.WALK_CONTINUE;
                }

                newLayout = descriptor.Layout
                    ?? throw new FatalJitException("Retyped struct local requires its layout.");
                newType = newLayout.HasGCPtr ? TYP_BYREF : TYP_I_IMPL;
                retypeFields = true;
            }
            else
            {
                tree.ChangeType(newType);
            }

            _allocator.UpdateAncestorTypes(tree, _ancestors, newType, newLayout, retypeFields);
            return Compiler.WALK_CONTINUE;
        }

        public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;
            var compiler = _allocator.CompilerInstance;
            if (tree.Oper is GT_BOX)
            {
                var boxLocal = tree.AsBox().BoxOp;
                assert(boxLocal.Oper is GT_LCL_VAR or GT_LCL_ADDR);
                if (boxLocal.Oper is GT_LCL_ADDR)
                {
#if DEBUG
                    JITDUMP($"Removing BOX wrapper [{tree.TreeId:D6}]\n");
#endif
                    use = boxLocal;
                }
            }
            else if (tree.Oper is GT_CALL)
            {
                var call = tree.AsCall();
                if (call.IsHelperCall(CORINFO_HELP_UNBOX))
                {
#if DEBUG
                    JITDUMP($"Found unbox helper call [{call.TreeId:D6}]\n");
#endif
                    var argument = call.Args.GetArgByIndex(1)
                        ?? throw new FatalJitException("UNBOX requires its object argument.");
                    var objectNode = argument.Node;
                    if ((objectNode.Oper.IsLocal || objectNode.Oper is GT_LCL_ADDR) &&
                        objectNode.Type is not TYP_REF)
                    {
                        var forEffect = user is null || call.Type is TYP_VOID;
                        var objectLocal = objectNode.AsLclVarCommon();
                        JITDUMP($"Rewriting to invoke box type test helper{(forEffect ? " for side effect" : "")}\n");
                        call._callMethHnd = Compiler.eeFindHelper(CORINFO_HELP_UNBOX_TYPETEST);
                        call.ChangeType(TYP_VOID);
                        call._returnType = TYP_VOID;
                        var methodTable = compiler.gtNewMethodTableLookup(objectLocal, onStack: true);
                        call.Args.Remove(argument);
                        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(methodTable));

                        if (!forEffect)
                        {
                            var copy = compiler.gtCloneExpr(objectLocal)
                                ?? throw new FatalJitException("UNBOX requires a cloneable local.");
                            var payload = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, copy,
                                compiler.gtNewIconNode(TYP_I_IMPL, TARGET_POINTER_SIZE));
                            use = compiler.gtNewCommaNode(TYP_BYREF, call, payload);
                        }
                    }
                }
                else if (call.IsDelegateInvoke)
                {
                    var argument = call.Args.ThisArg
                        ?? throw new FatalJitException("Delegate invoke requires a this argument.");
                    var receiver = argument.Node;
                    if (receiver.Oper is GT_LCL_VAR or GT_LCL_ADDR)
                    {
                        var local = receiver.AsLclVarCommon();
                        if (receiver.Oper is GT_LCL_ADDR || _allocator.DoesLclVarPointToStack(local.LclNum))
                        {
#if DEBUG
                            JITDUMP($"Expanding delegate invoke [{call.TreeId:D6}]\n");
#endif
                            var copy = compiler.gtClone(local, complexOK: true)
                                ?? throw new FatalJitException("Delegate invoke requires a cloneable receiver.");
                            var instanceOffset = compiler.eeGetEEInfo().offsetOfDelegateInstance;
                            var instanceAddr = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, copy,
                                compiler.gtNewIconNode(TYP_I_IMPL, instanceOffset));
                            argument.EarlyNodeRef = compiler.gtNewIndir(TYP_REF, instanceAddr);

                            var targetOffset = compiler.eeGetEEInfo().offsetOfDelegateFirstTarget;
                            var targetAddr = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, local,
                                compiler.gtNewIconNode(TYP_I_IMPL, targetOffset));
                            call._callType = CT_INDIRECT;
                            call.ControlExpr = compiler.gtNewIndir(TYP_I_IMPL, targetAddr);
                            call._callMethHnd = NO_METHOD_HANDLE;
                            call._callMoreFlags &= ~GTF_CALL_M_DELEGATE_INV;
                        }
                    }
                }
            }
            else if (tree.Oper.IsIndir)
            {
                var indir = tree.AsIndir();
                var addr = indir.Addr;
                if (addr.Oper is GT_COMMA)
                {
                    var lastEffect = addr.AsOp().Op1;
                    if (lastEffect.Oper is GT_CALL &&
                        lastEffect.AsCall().IsHelperCall(CORINFO_HELP_UNBOX_TYPETEST))
                    {
                        var actualAddr = addr.EffectiveVal;
                        GenTree? effects = null;
                        compiler.gtExtractSideEffList(indir, ref effects, GTF_SIDE_EFFECT, ignoreRoot: true);
                        indir.Addr = actualAddr;
                        indir.Flags &= ~GTF_SIDE_EFFECT;
                        indir.Flags |= GTF_IND_NONFAULTING;
                        use = compiler.gtNewCommaNode(indir.Type,
                            effects ?? throw new FatalJitException("UNBOX type test must retain its side-effect call."),
                            indir);
                    }
                }
            }

            return Compiler.WALK_CONTINUE;
        }

        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<RewriteUsesVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    internal void RewriteUses()
    {
        var compiler = CompilerInstance;
        for (var local = 0; local < compiler.lvaCount; local++)
        {
            ref var descriptor = ref compiler.lvaGetDesc(local);
            if (!descriptor.lvTracked)
            {
                JITDUMP($"V{local:D2} not tracked\n");
                continue;
            }

            if (!MayLclVarPointToStack(local))
            {
                JITDUMP($"V{local:D2} not possibly stack pointing\n");
                descriptor.lvTracked = false;
                continue;
            }

            var newType = _heapLocalToStackObjLocalMap.ContainsKey(local) ||
                _heapLocalToStackArrLocalMap.ContainsKey(local) || DoesLclVarPointToStack(local)
                ? TYP_I_IMPL : TYP_BYREF;
            if (descriptor.Type is TYP_STRUCT)
            {
                assert(_trackFields);
                var layout = descriptor.Layout
                    ?? throw new FatalJitException("Tracked struct requires its layout.");
                if (!layout.HasGCPtr)
                {
                    assert(newType is TYP_I_IMPL);
                    JITDUMP($"V{local:D2} not GC\n");
                    descriptor.lvTracked = false;
                    continue;
                }

                if (newType is TYP_I_IMPL)
                {
                    JITDUMP($"Changing layout of struct V{local:D2} to block\n");
                    descriptor.ChangeLayout(GetNonGCLayout(layout));
                }
                else
                {
                    JITDUMP($"Changing layout of struct V{local:D2} to byref\n");
                    descriptor.ChangeLayout(GetByrefLayout(layout));
                }
            }
            else
            {
                if (!varTypeIsGC(descriptor.Type))
                {
                    JITDUMP($"V{local:D2} not GC\n");
                    descriptor.lvTracked = false;
                    continue;
                }

                if (descriptor.Type != newType)
                {
                    assert(!descriptor.lvIsParam || newType is not TYP_I_IMPL);
                    JITDUMP($"Changing the type of V{local:D2} from {StackPointerTypeName(descriptor.Type)} to {StackPointerTypeName(newType)}\n");
                    descriptor.Type = newType;
                }
                else
                {
                    JITDUMP($"V{local:D2} already properly typed\n");
                    descriptor.lvTracked = false;
                }
            }
        }

        foreach (var block in compiler.Blocks)
        {
            foreach (var statement in block.Statements)
            {
                var visitor = new RewriteUsesVisitor(this);
                _ = visitor.WalkTree(ref statement.RootNodeRef, null);
            }
        }
    }

    private static string StackPointerTypeName(var_types type)
    {
        return type switch
        {
            TYP_REF => "ref",
            TYP_BYREF => "byref",
            TYP_I_IMPL => "long",
            _ => throw new FatalJitException("Unexpected type for stack-pointer retyping.")
        };
    }
}
