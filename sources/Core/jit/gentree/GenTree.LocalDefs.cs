// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public static VisitResult VisitPromotedRangeLocalDefs<TVisitor>(Compiler compiler, GenTreeLclVarCommon def,
        in LclVarDsc varDsc, nint offset, ValueSize storeSize, ref TVisitor visitor)
        where TVisitor : struct, ILocalDefVisitor
    {
        var fieldLclNum = compiler.lvaGetFieldLocal(varDsc, unchecked((uint)offset));

        if (fieldLclNum != BAD_VAR_NUM)
        {
            ref var fieldVarDsc = ref compiler.lvaGetDesc(fieldLclNum);

            if (fieldVarDsc.lvValueSize == storeSize)
            {
                var index = fieldLclNum - varDsc.lvFieldLclStart;

                return visitor.Visit(new PromotedRangeLocalDef(def, fieldLclNum, index, true, 0, storeSize, 0, storeSize));
            }
        }

        for (var index = 0; index < varDsc.lvFieldCnt; index++)
        {
            fieldLclNum = varDsc.lvFieldLclStart + index;
            ref var fieldVarDsc = ref compiler.lvaGetDesc(fieldLclNum);

            if (!compiler.gtStoreMayDefineField(fieldVarDsc, offset, storeSize, out var fieldStoreOffset, out var fieldStoreSize))
            {
                continue;
            }

            var isEntire = (fieldStoreOffset == 0) && (fieldStoreSize == fieldVarDsc.lvValueSize);
            var valueOffset = nint.Max(fieldVarDsc.lvFldOffset, offset) - offset;

            if (visitor.Visit(new PromotedRangeLocalDef(def, fieldLclNum, index, isEntire,
                fieldStoreOffset, fieldStoreSize, valueOffset, storeSize)) == VisitResult.Abort)
            {
                return VisitResult.Abort;
            }
        }

        return VisitResult.Continue;
    }

    public unsafe bool IsEntireLocalDef(Compiler compiler, GenTreeLclVarCommon def)
    {
        if (Oper is GT_STORE_LCL_VAR)
        {
            return true;
        }

        if (Oper is GT_STORE_LCL_FLD)
        {
            return !def.AsLclFld().IsPartial(compiler);
        }

        assert(Oper is GT_CALL);
        var call = AsCall();

        if (def == compiler.gtCallGetDefinedAsyncResumedLclAddr(call))
        {
            return compiler.lvaLclExactSize(def.LclNum) == TARGET_POINTER_SIZE;
        }

        assert(def == compiler.gtCallGetDefinedRetBufLclAddr(call));
        var storeSize = new ValueSize(compiler.typGetObjLayout(call.RetClsHnd).Size);

        return compiler.IsEntireAccess(def.LclNum, def.LclOffs, storeSize);
    }

    public VisitResult VisitLocalDef<TVisitor>(Compiler compiler, GenTreeLclVarCommon def, ref TVisitor visitor)
        where TVisitor : struct, ILocalDefVisitor
    {
        assert(Oper is GT_STORE_LCL_VAR);
        ref var varDsc = ref compiler.lvaGetDesc(def.LclNum);

        if (!varDsc.lvPromoted)
        {
            return visitor.Visit(new StoreLclVarDef(def));
        }

        for (var index = 0; index < varDsc.lvFieldCnt; index++)
        {
            var fieldLclNum = varDsc.lvFieldLclStart + index;

            if (visitor.Visit(new PromotedStoreLclVarDef(def, fieldLclNum, index)) == VisitResult.Abort)
            {
                return VisitResult.Abort;
            }
        }

        return VisitResult.Continue;
    }

    public VisitResult VisitLocalDef<TVisitor>(Compiler compiler, GenTreeLclVarCommon def, bool isEntire,
        nint offset, ValueSize size, ref TVisitor visitor) where TVisitor : struct, ILocalDefVisitor
    {
        assert(Oper is GT_CALL);
        ref var varDsc = ref compiler.lvaGetDesc(def.LclNum);

        if (!varDsc.lvPromoted)
        {
            return visitor.Visit(new CallLocalDef(def, isEntire, offset, size));
        }

        return VisitPromotedRangeLocalDefs(compiler, def, varDsc, offset, size, ref visitor);
    }

    /// <summary>Visit logical definitions, including promoted fields and locals defined via call arguments.</summary>
    /// <remarks>Must recognize a superset of stores transformed by LocalAddressVisitor.</remarks>
    public unsafe VisitResult VisitLogicalLocalDefs<TVisitor>(Compiler compiler, ref TVisitor visitor)
        where TVisitor : struct, ILocalDefVisitor
    {
        if (Oper is GT_STORE_LCL_VAR)
        {
            return VisitLocalDef(compiler, AsLclVarCommon(), ref visitor);
        }

        if (Oper is GT_STORE_LCL_FLD)
        {
            var fld = AsLclFld();
            ref var varDsc = ref compiler.lvaGetDesc(fld.LclNum);

            if (!varDsc.lvPromoted)
            {
                return visitor.Visit(new StoreLclFldDef(fld));
            }

            return VisitPromotedRangeLocalDefs(compiler, fld, varDsc, fld.LclOffs, fld.ValueSize, ref visitor);
        }

        if (Oper is GT_CALL)
        {
            var call = AsCall();
            var asyncResumedLclAddr = compiler.gtCallGetDefinedAsyncResumedLclAddr(call);

            if (asyncResumedLclAddr is not null)
            {
                var isEntire = compiler.lvaLclExactSize(asyncResumedLclAddr.LclNum) == TARGET_POINTER_SIZE;

                if (VisitLocalDef(compiler, asyncResumedLclAddr, isEntire, asyncResumedLclAddr.LclOffs,
                    new ValueSize(TARGET_POINTER_SIZE), ref visitor) == VisitResult.Abort)
                {
                    return VisitResult.Abort;
                }
            }

            var retBufLclAddr = compiler.gtCallGetDefinedRetBufLclAddr(call);

            if (retBufLclAddr is not null)
            {
                var storeSize = new ValueSize(compiler.typGetObjLayout(call.RetClsHnd).Size);
                var isEntire = compiler.IsEntireAccess(retBufLclAddr.LclNum, retBufLclAddr.LclOffs, storeSize);

                return VisitLocalDef(compiler, retBufLclAddr, isEntire, retBufLclAddr.LclOffs, storeSize, ref visitor);
            }
        }

        return VisitResult.Continue;
    }

    public bool HasAnyLocalDefs(Compiler compiler)
        => VisitPhysicalLocalDefNodes(compiler, static _ => VisitResult.Abort) == VisitResult.Abort;
}
