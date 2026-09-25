// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
    private readonly SpillDsc? rsGetSpillInfo(GenTree tree, regNumber reg, out SpillDsc? previous)
    {
        previous = null;
        var descriptor = _rsSpillDesc[(int)reg];

        while (descriptor is not null)
        {
            if (ReferenceEquals(descriptor.spillTree, tree))
            {
                break;
            }

            previous = descriptor;
            descriptor = descriptor.spillNext;
        }

        return descriptor;
    }

    public void rsSpillTree(regNumber reg, GenTree tree, byte regIndex = 0)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Tree spills outside Windows AMD64 are not implemented.");
#else
        var isMultiRegTree = false;
        var treeType = tree.Type;

        if (tree.IsMultiRegLclVar)
        {
            treeType = Compiler.lvaGetDesc(tree.AsLclVar().LclNum).Type;
            isMultiRegTree = true;
        }
        else if (tree.IsMultiRegNode)
        {
            treeType = tree.GetRegTypeByIndex(regIndex);
            isMultiRegTree = true;
        }

        var tempType = tmpNormalizeType(treeType);
        var floatSpill = varTypeUsesFloatReg(treeType);
        _rsNeededSpillReg = true;

        assert((tree.Flags & GTF_SPILL) != 0);
        var regFlags = GTF_EMPTY;

        if (isMultiRegTree)
        {
            regFlags = tree.GetRegSpillFlagByIdx(regIndex);
            assert((regFlags & GTF_SPILL) != 0);
            regFlags &= ~GTF_SPILL;
        }
        else
        {
            assert(!varTypeIsMultiReg(tree.Type));
            tree.Flags &= ~GTF_SPILL;
        }

        assert(tree.GetRegByIndex(regIndex) == reg);

        var spill = _rsSpillFree;
        if (spill is not null)
        {
            _rsSpillFree = spill.spillNext;
        }
        else
        {
            spill = new SpillDsc();
        }

        var temp = tmpGetTemp(tempType);
        spill.spillTemp = temp;
        tempType = temp.tdTempType;
        spill.spillTree = tree;

#if DEBUG
        if (Compiler.verbose)
        {
            jitprintf($"\t\t\t\t\t\t\tThe register {Compiler.compRegVarName(reg)} spilled with ");
            RyuJitSharp.Compiler.printTreeId(tree);
            if (isMultiRegTree)
            {
                jitprintf($"[{regIndex}]");
            }
        }
#endif

        spill.spillNext = _rsSpillDesc[(int)reg];
        _rsSpillDesc[(int)reg] = spill;

#if DEBUG
        if (Compiler.verbose)
        {
            jitprintf("\n");
        }
#endif

        var storeType = floatSpill ? treeType : tempType;
        _codeGen.spillReg(storeType, temp, reg);
        rsMarkSpill(tree, reg);

        if (isMultiRegTree)
        {
            regFlags |= GTF_SPILLED;
            tree.SetRegSpillFlagByIdx(regFlags, regIndex);
        }
#endif
    }

    private TempDsc rsGetSpillTempWord(regNumber reg, SpillDsc descriptor, SpillDsc? previous)
    {
        assert((previous is null) || ReferenceEquals(previous.spillNext, descriptor));

        if (previous is not null)
        {
            previous.spillNext = descriptor.spillNext;
        }
        else
        {
            _rsSpillDesc[(int)reg] = descriptor.spillNext;
        }

        var temp = descriptor.spillTemp;
        assert(temp is not null);
        descriptor.spillNext = _rsSpillFree;
        _rsSpillFree = descriptor;

        return temp;
    }

    public TempDsc rsUnspillInPlace(GenTree tree, regNumber oldReg, byte regIndex = 0)
    {
        var spill = rsGetSpillInfo(tree, oldReg, out var previous);
        assert(spill is not null);
        var temp = rsGetSpillTempWord(oldReg, spill, previous);

        if (tree.IsMultiRegNode)
        {
            var flags = tree.GetRegSpillFlagByIdx(regIndex);
            flags &= ~GTF_SPILLED;
            tree.SetRegSpillFlagByIdx(flags, regIndex);
        }
        else
        {
            tree.Flags &= ~GTF_SPILLED;
        }

#if DEBUG
        if (Compiler.verbose)
        {
            jitprintf("\t\t\t\t\t\t\tTree-Node marked unspilled from ");
            RyuJitSharp.Compiler.printTreeId(tree);
            if (tree.IsMultiRegNode)
            {
                jitprintf($"[{regIndex}]");
            }
            jitprintf("\n");
        }
#endif

        return temp;
    }

    public readonly void rsMarkSpill(GenTree tree, regNumber reg)
    {
        tree.Flags |= GTF_SPILLED;
    }
}
