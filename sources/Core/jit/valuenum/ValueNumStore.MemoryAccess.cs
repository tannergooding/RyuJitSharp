// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public unsafe ValueNum VNForFieldSelector(CORINFO_FIELD_HANDLE fieldHnd, out var_types fieldType,
        out ValueSize size)
    {
        var fieldVN = VNForHandle((nint)fieldHnd, GTF_ICON_FIELD_HDL);
        fieldType = _compiler.eeGetFieldType(fieldHnd, out var structHnd);
        var structSize = 0;

        if (fieldType is TYP_STRUCT)
        {
            structSize = _compiler.info.compCompHnd->getClassSize(structHnd);
            fieldType = _compiler.impNormStructType(structHnd);
        }

        size = fieldType is TYP_STRUCT ? new ValueSize(structSize) : ValueSize.FromJitType(fieldType);
        assert(!size.IsNull);

#if DEBUG
        if (_compiler.verbose)
        {
            var fieldName = _compiler.eeGetFieldName(fieldHnd, false);
            JITDUMP($"    VNForHandle({fieldName}) is ${fieldVN:x}, fieldType is {fieldType.Name}");
            if (size.IsExact)
            {
                JITDUMP($", size = {size.ExactSize}");
            }
            JITDUMP("\n");
        }
#endif
        return fieldVN;
    }

    public static bool LoadStoreIsEntire(ValueSize locationSize, nint offset, ValueSize indSize)
        => (offset == 0) && (locationSize == indSize);

    public ValueNum VNForLoad(ValueNumKind vnk, ValueNum locationValue, ValueSize locationSize,
        var_types loadType, nint offset, ValueSize loadSize)
    {
        var loadOffset = unchecked((uint)offset);
        ValueNum loadValue;
        if (LoadStoreIsEntire(locationSize, unchecked((nint)loadOffset), loadSize))
        {
            loadValue = locationValue;
        }
        else if (locationSize.IsExact && loadSize.IsExact)
        {
            assert(!loadSize.IsNull && !locationSize.IsNull);
            var loadExactSize = loadSize.ExactSize;
            if ((offset < 0) || (locationSize.ExactSize < unchecked(loadOffset + loadExactSize)))
            {
#if DEBUG
                JITDUMP("    *** VNForLoad: out-of-bounds load!\n");
#endif
                return VNForExpr(_compiler.compCurBB, loadType);
            }

#if DEBUG
            JITDUMP("  VNForLoad:\n");
#endif
            loadValue = VNForMapPhysicalSelect(vnk, loadType, locationValue, loadOffset, loadExactSize);
        }
        else
        {
#if DEBUG
            JITDUMP("    *** VNForLoad: out-of-bounds load, due to unknown load size\n");
#endif
            return VNForExpr(_compiler.compCurBB, loadType);
        }

        loadValue = VNForLoadStoreBitCast(loadValue, loadType, loadSize);
        assert(TypeOfVN(loadValue).ActualType == loadType.ActualType);
        return loadValue;
    }

    public ValueNumPair VNPairForLoad(ValueNumPair locationValue, ValueSize locationSize,
        var_types loadType, nint offset, ValueSize loadSize)
    {
        var liberal = VNForLoad(VNK_Liberal, locationValue.Liberal, locationSize, loadType, offset, loadSize);
        var conservative = VNForLoad(VNK_Conservative, locationValue.Conservative,
            locationSize, loadType, offset, loadSize);
        return new(liberal, conservative);
    }

    public ValueNum VNForStore(ValueNum locationValue, ValueSize locationSize, nint offset,
        ValueSize storeSize, ValueNum value)
    {
        assert(!LoadStoreIsEntire(locationSize, offset, storeSize));
        if (!locationSize.IsExact || !storeSize.IsExact)
        {
#if DEBUG
            JITDUMP("    *** VNForStore: location or store size is unknown\n");
#endif
            return NoVN;
        }

        assert(storeSize.ExactSize > 0);
        var exactLocationSize = locationSize.ExactSize;
        var exactStoreSize = storeSize.ExactSize;
        var storeOffset = unchecked((uint)offset);
        if ((offset < 0) || (exactLocationSize < unchecked(storeOffset + exactStoreSize)))
        {
#if DEBUG
            JITDUMP($"    *** VNForStore: out-of-bounds store -- location size is {exactLocationSize}, " +
                $"offset is {offset}, store size is {exactStoreSize}\n");
#endif
            return NoVN;
        }

#if DEBUG
        JITDUMP("  VNForStore:\n");
#endif
        return VNForMapPhysicalStore(locationValue, storeOffset, exactStoreSize, value);
    }

    public ValueNumPair VNPairForStore(ValueNumPair locationValue, ValueSize locationSize,
        nint offset, ValueSize storeSize, ValueNumPair value)
    {
        var liberal = VNForStore(locationValue.Liberal, locationSize, offset, storeSize, value.Liberal);
        var conservative = locationValue.BothEqual() && value.BothEqual() ? liberal
            : VNForStore(locationValue.Conservative, locationSize, offset, storeSize, value.Conservative);
        return new(liberal, conservative);
    }

    public ValueNum VNForLoadStoreBitCast(ValueNum value, var_types indType, ValueSize indSize)
    {
        var typeOfValue = TypeOfVN(value);
        if (typeOfValue != indType)
        {
            assert((typeOfValue is TYP_STRUCT) || (indType is TYP_STRUCT) ||
                (indSize.IsExact && (indType.Size == indSize.ExactSize)));
            value = VNForBitCast(value, indType, indSize);
#if DEBUG
            JITDUMP("    VNForLoadStoreBitcast returns ");
            if (_compiler.verbose)
            {
                _compiler.vnPrint(value, 1);
            }
            JITDUMP("\n");
#endif
        }

        assert(TypeOfVN(value).ActualType == indType.ActualType);
        return value;
    }

    public ValueNumPair VNPairForLoadStoreBitCast(ValueNumPair value, var_types indType, ValueSize indSize)
    {
        var liberal = VNForLoadStoreBitCast(value.Liberal, indType, indSize);
        var conservative = value.BothEqual() ? liberal : VNForLoadStoreBitCast(value.Conservative, indType, indSize);
        return new(liberal, conservative);
    }
}
