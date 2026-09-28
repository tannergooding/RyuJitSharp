// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    public void fgStress64RsltMul()
    {
        if (!compStressCompile(STRESS_64RSLT_MUL, 20))
        {
            return;
        }

        var visitor = new Stress64RsltMulVisitor(this);
        foreach (var block in Blocks)
        {
            foreach (var statement in block.Statements)
            {
                _ = visitor.WalkTree(ref statement.RootNodeRef, null);
            }
        }
    }

    private struct Stress64RsltMulVisitor(Compiler compiler) : IGenTreeVisitor<Stress64RsltMulVisitor>
    {
        private readonly Compiler _compiler = compiler;
        private readonly GenTreeStack _ancestors = [];

        public static bool DoPreOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;
            if ((tree.Oper is not GT_MUL) || (tree.Type is not TYP_INT) || tree.HasOverflowCheck)
            {
                return WALK_CONTINUE;
            }

            JITDUMP("STRESS_64RSLT_MUL before:\n");
            DISPTREE(tree);

            var multiply = tree.AsOp();
            multiply.Op1 = _compiler.gtNewCastNode(TYP_LONG, multiply.Op1, false, TYP_LONG);
            multiply.Op2 = _compiler.gtNewCastNode(TYP_LONG, multiply.Op2, false, TYP_LONG);
            multiply.Type = TYP_LONG;
            use = _compiler.gtNewCastNode(TYP_INT, multiply, false, TYP_INT);

            // Prevent optNarrowTree from folding back to the original multiplication.
            multiply.Op1._debugFlags |= GTF_DEBUG_CAST_DONT_FOLD;
            multiply.Op2._debugFlags |= GTF_DEBUG_CAST_DONT_FOLD;

            JITDUMP("STRESS_64RSLT_MUL after:\n");
            DISPTREE(use);

            return WALK_SKIP_SUBTREES;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<Stress64RsltMulVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    public void lvaStressLclFld()
    {
        if (!compStressCompile(STRESS_LCL_FLDS, 5))
        {
            return;
        }

        // Discover all disqualifying uses before changing any local's storage.
        for (var pass = 0; pass < 2; pass++)
        {
            var visitor = new StressLclFldVisitor(this, pass == 0);
            foreach (var block in Blocks)
            {
                foreach (var statement in block.Statements)
                {
                    _ = visitor.WalkTree(ref statement.RootNodeRef, null);
                }
            }
        }
    }

    private static int lvaStressLclFldPadding(int lclNum)
    {
        // Native stress selects even locals and uses their number modulo seven as padding.
        return ((lclNum % 2) != 0) ? 0 : lclNum % 7;
    }

    private fgWalkResult lvaStressLclFldNode(ref GenTree use, bool firstPass)
    {
        var tree = use;
        if (!tree.Oper.IsAnyLocal)
        {
            return WALK_CONTINUE;
        }

        var local = tree.AsLclVarCommon();
        var lclNum = local.LclNum;
        ref var varDsc = ref lvaGetDesc(lclNum);
        var localType = local.Type;
        var varType = varDsc.Type;

        if (varDsc.lvNoLclFldStress)
        {
            return WALK_CONTINUE;
        }

        if (firstPass)
        {
            if ((local.Oper is GT_LCL_FLD or GT_STORE_LCL_FLD) ||
                ((local.Oper is GT_LCL_ADDR) && (local.LclOffs != 0)))
            {
                varDsc.lvNoLclFldStress = true;
                return WALK_CONTINUE;
            }

            if ((tree.Flags & GTF_VAR_CONTEXT) != 0)
            {
                assert(tree.Oper is GT_LCL_VAR);
                varDsc.lvNoLclFldStress = true;
                return WALK_CONTINUE;
            }

            if (varDsc.lvIsParam || (lclNum >= info.compLocalsCount))
            {
                varDsc.lvNoLclFldStress = true;
                return WALK_CONTINUE;
            }

            // OSR locals retain their Tier0 homes; patchpoints must report unchanged homes.
            if (lvaIsOSRLocal(lclNum) || ((optMethodFlags & OMF_HAS_PATCHPOINT) != 0))
            {
                varDsc.lvNoLclFldStress = true;
                return WALK_CONTINUE;
            }

            // Preserve native exclusion for tailcall-loop explicit local initialization.
            if (((optMethodFlags & OMF_HAS_RECURSIVE_TAILCALL) != 0) || varDsc.lvKeepType)
            {
                varDsc.lvNoLclFldStress = true;
                return WALK_CONTINUE;
            }

            if (varTypeIsStruct(localType) || (varType != localType) || varDsc.lvPinned)
            {
                varDsc.lvNoLclFldStress = true;
                return WALK_CONTINUE;
            }

            // Retyping storage would lose a small local's implicit normalization semantics.
            if ((varType.Size != varType.ActualType.Size) || (lvaStressLclFldPadding(lclNum) == 0))
            {
                varDsc.lvNoLclFldStress = true;
                return WALK_CONTINUE;
            }
        }
        else
        {
            var layout = (varType is TYP_STRUCT) ? varDsc.Layout : null;
            noway_assert((varType == localType) || ((layout is not null) && layout.IsCustomLayout));
            var padding = lvaStressLclFldPadding(lclNum);

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
            padding = roundUp(padding, TYP_DOUBLE.Size);
#endif

            if (varTypeIsGC(varType) || ((layout is not null) && layout.HasGCPtr))
            {
                padding = roundUp(padding, TARGET_POINTER_SIZE);
            }

            if (varType is not TYP_STRUCT)
            {
                var size = roundUp(padding + lvaLclStackHomeSize(lclNum), TARGET_POINTER_SIZE);
                var builder = new ClassLayoutBuilder(this, size);
                builder.SetName($"{varType.Name}_{size}_Stress", $"{varType.Name}_{size}");
                if (varTypeIsGC(varType))
                {
                    builder.SetGCPtrType(padding / TARGET_POINTER_SIZE, varType);
                }

                layout = typGetCustomLayout(builder);
                varDsc.Type = TYP_STRUCT;
                varDsc.Layout = layout;
                lvaSetVarAddrExposed(lclNum, AddressExposedReason.STRESS_LCL_FLD);
                JITDUMP($"Converting V{lclNum:D2} of type {varType.Name} to {layout.Size} sized block with LCL_FLD at offset (padding {padding})\n");
            }

            tree.Flags |= GTF_GLOB_REF;
            if (tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR)
            {
                var oper = (tree.Oper is GT_LCL_VAR) ? GT_LCL_FLD : GT_STORE_LCL_FLD;
                var data = tree.Oper.IsLocalStore ? local.Data : null;
                var field = new GenTreeLclFld(oper, localType, lclNum, (ushort)padding, data,
                    null, tree, NodeThreading.None)
                {
                    Flags = tree.Flags,
                };
                field.CopySsaIdentityFrom(local);
                tree = use = field;
            }
            else
            {
                tree.AsLclFld().LclOffs = (ushort)padding;
            }

            if ((tree.Oper is GT_STORE_LCL_FLD) && tree.AsLclFld().IsPartial(this))
            {
                tree.Flags |= GTF_VAR_USEASG;
            }
        }

        return WALK_CONTINUE;
    }

    private struct StressLclFldVisitor(Compiler compiler, bool firstPass) : IGenTreeVisitor<StressLclFldVisitor>
    {
        private readonly Compiler _compiler = compiler;
        private readonly bool _firstPass = firstPass;
        private readonly GenTreeStack _ancestors = [];

        public static bool DoPreOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
            => _compiler.lvaStressLclFldNode(ref use, _firstPass);

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<StressLclFldVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
#endif
}
