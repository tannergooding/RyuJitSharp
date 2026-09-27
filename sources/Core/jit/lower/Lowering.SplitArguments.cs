// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_ARG_SPLIT
namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe void SplitArgumentBetweenRegistersAndStack(GenTreeCall call, CallArg callArg)
    {
        ref var argSlot = ref callArg.NodeRef;
        var arg = argSlot;
        assert((arg.Oper is GT_BLK or GT_FIELD_LIST) || arg.Oper.IsLocalRead);
        assert(!call.IsFastTailCall);
        var abiInfo = callArg.AbiInfo;
        assert(abiInfo.IsSplitAcrossRegistersAndStack);
#if DEBUG
        for (var i = 0; i < abiInfo.NumSegments; i++)
        {
            assert((i < abiInfo.NumSegments - 1) == abiInfo.Segments[i].IsPassedInRegister);
        }
#endif
        var compiler = CompilerInstance;
        var numRegisters = abiInfo.NumSegments - 1;
        var stackSegment = abiInfo.Segments[numRegisters];
#if DEBUG
        JITDUMP($"Dividing split arg [{arg.TreeId:D6}] with {numRegisters} registers, {stackSegment.Size} stack space into two arguments\n");
#endif
        var layout = callArg.SignatureLayout;
        assert(layout is not null);
        var registersLayout = layout.SliceLayout(compiler, 0, stackSegment.Offset);
        var stackLayout = layout.SliceLayout(compiler, stackSegment.Offset, (int)layout.Size - stackSegment.Offset);

        GenTree stackNode;
        GenTree registersNode;
        if (arg.Oper is GT_FIELD_LIST)
        {
            JITDUMP("Argument is a FIELD_LIST\n");
            var fields = arg.AsFieldList();
            GenTreeFieldList.Use? splitPoint = null;
            foreach (var use in fields.Uses)
            {
                if (use.Offset >= stackSegment.Offset)
                {
                    splitPoint = use;
                    JITDUMP($"Found split point at offset {use.Offset}\n");
                    break;
                }

                if (use.Offset + use.Type.Size > stackSegment.Offset)
                {
                    break;
                }
            }

            if (splitPoint is null)
            {
                JITDUMP("No clean split point found, spilling FIELD_LIST\n");
                var local = StoreFieldListToNewLocal(compiler.typGetObjLayout(callArg.SignatureClassHandle), fields);
                stackNode = compiler.gtNewLclFldNode(TYP_STRUCT, local, checked((ushort)stackSegment.Offset), stackLayout);
                registersNode = compiler.gtNewLclFldNode(TYP_STRUCT, local, 0, registersLayout);
                BlockRange().InsertBefore(arg, stackNode);
                BlockRange().InsertBefore(arg, registersNode);
            }
            else
            {
                var stackFields = new GenTreeFieldList();
                var registerFields = new GenTreeFieldList();
                stackNode = stackFields;
                registersNode = registerFields;
                BlockRange().InsertBefore(arg, stackNode);
                BlockRange().InsertBefore(arg, registersNode);

                foreach (var use in fields.Uses)
                {
                    if (use == splitPoint)
                    {
                        break;
                    }
                    registerFields.AddFieldLIR(compiler, use.Node, use.Offset, use.Type);
                }

                for (var use = splitPoint; use is not null; use = use.Next)
                {
                    stackFields.AddFieldLIR(compiler, use.Node, checked((ushort)(use.Offset - stackSegment.Offset)), use.Type);
                }
            }
            BlockRange().Remove(arg);
        }
        else if (arg.Oper is GT_BLK)
        {
            JITDUMP("Argument is a BLK\n");
            var blockAddress = arg.AsBlk().Addr;
            compiler.gtPeelOffsets(ref blockAddress, out var offset);
            var gotUse = BlockRange().TryGetUse(blockAddress, out var addressUse);
            assert(gotUse);

            int addressLocal;
            if (addressUse.Def().Oper.IsScalarLocal &&
                !compiler.lvaGetDesc(addressUse.Def().AsLclVarCommon().LclNum).IsAddressExposed &&
                IsInvariantInRange(addressUse.Def(), arg))
            {
                JITDUMP("Reusing LCL_VAR\n");
                addressLocal = addressUse.Def().AsLclVarCommon().LclNum;
            }
            else
            {
                JITDUMP("Spilling address\n");
                addressLocal = addressUse.ReplaceWithLclVar(compiler);
            }

            GenTree CreateAddress(int fieldOffset)
            {
                GenTree address = compiler.gtNewLclVarNode(TYP_UNDEF, addressLocal);
                var combinedOffset = unchecked((uint)fieldOffset + (uint)offset);
                if (combinedOffset != 0)
                {
                    var offsetNode = compiler.gtNewIconNode(TYP_I_IMPL, (nint)combinedOffset);
                    address = compiler.gtNewBinaryNode(GT_ADD, varTypeIsGC(address.Type) ? TYP_BYREF : TYP_I_IMPL,
                        address, offsetNode);
                }

                return address;
            }

            var address = CreateAddress(stackSegment.Offset);
            stackNode = compiler.gtNewBlkIndir(address, stackLayout, arg.Flags & GTF_IND_COPYABLE_FLAGS);
            BlockRange().InsertBefore(arg, LIR.SeqTree(compiler, stackNode));
            LowerRange(address, stackNode);

            var registerFields = new GenTreeFieldList();
            registersNode = registerFields;
            BlockRange().InsertBefore(arg, registersNode);
            for (var i = 0; i < numRegisters; i++)
            {
                var segment = abiInfo.Segments[i];
                address = CreateAddress(segment.Offset);
                var indirection = compiler.gtNewIndir(segment.GetRegisterType(layout), address,
                    arg.Flags & GTF_IND_COPYABLE_FLAGS);
                registerFields.AddFieldLIR(compiler, indirection, checked((ushort)segment.Offset), indirection.Type);
                BlockRange().InsertBefore(registersNode, LIR.SeqTree(compiler, indirection));
                LowerRange(address, indirection);
            }
            BlockRange().Remove(arg, markOperandsUnused: true);
        }
        else
        {
            assert(arg.Oper.IsLocalRead);
            JITDUMP("Argument is a local\n");
            var local = arg.AsLclVarCommon();
            stackNode = compiler.gtNewLclFldNode(TYP_STRUCT, local.LclNum,
                checked((ushort)(local.LclOffs + stackSegment.Offset)), stackLayout);
            BlockRange().InsertBefore(arg, stackNode);
            compiler.lvaSetVarDoNotEnregister(local.LclNum, DoNotEnregisterReason.LocalField);

            var registerFields = new GenTreeFieldList();
            registersNode = registerFields;
            BlockRange().InsertBefore(arg, registersNode);
            for (var i = 0; i < numRegisters; i++)
            {
                var segment = abiInfo.Segments[i];
                var field = compiler.gtNewLclFldNode(segment.GetRegisterType(layout), local.LclNum,
                    checked((ushort)(local.LclOffs + segment.Offset)));
                registerFields.AddFieldLIR(compiler, field, checked((ushort)segment.Offset), field.Type);
                BlockRange().InsertBefore(registersNode, field);
            }
            BlockRange().Remove(arg);
        }

        JITDUMP("New stack node is:\n");
        DISPTREERANGE(BlockRange(), stackNode);
        JITDUMP("New registers node is:\n");
        DISPTREERANGE(BlockRange(), registersNode);

        var newStackSegment = AbiPassingSegment.OnStack(stackSegment.StackOffset, 0, stackSegment.Size);
        var newStackAbi = AbiPassingInformation.FromSegment(compiler, false, in newStackSegment);
        var newRegistersAbi = new AbiPassingInformation(numRegisters);
        for (var i = 0; i < numRegisters; i++)
        {
            newRegistersAbi.Segments[i] = abiInfo.Segments[i];
        }

        callArg.AbiInfo = newStackAbi;
        argSlot = stackNode;
        var registerArg = call.Args.InsertAfter(callArg, NewCallArg.CreateForStruct(registersNode, TYP_STRUCT, registersLayout));
        registerArg.AbiInfo = newRegistersAbi;
        if (callArg.LateNode is not null)
        {
            registerArg.LateNext = callArg.LateNext;
            callArg.LateNext = registerArg;
            registerArg.LateNode = registersNode;
            registerArg.EarlyNode = null;
        }

        JITDUMP("Added a new call arg. New call is:\n");
        DISPTREERANGE(BlockRange(), call);
    }
}
#endif
