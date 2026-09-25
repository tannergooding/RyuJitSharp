// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    internal enum OperandKind
    {
        ClsVar,
        Local,
        Indir,
        Imm,
        Reg,
    }

    internal readonly unsafe struct OperandDesc
    {
        private readonly OperandKind _kind;
        private readonly CORINFO_FIELD_HANDLE _fieldHnd;
        private readonly int _varNum;
        private readonly ushort _offset;
        private readonly GenTree? _addr;
        private readonly GenTreeIndir? _indir;
        private readonly var_types _indirType;
        private readonly nint _immediate;
        private readonly bool _immediateNeedsReloc;
        private readonly regNumber _reg;

        public OperandDesc(CORINFO_FIELD_HANDLE fieldHnd)
        {
            _kind = OperandKind.ClsVar;
            _fieldHnd = fieldHnd;
        }

        public OperandDesc(int varNum, ushort offset)
        {
            _kind = OperandKind.Local;
            _varNum = varNum;
            _offset = offset;
        }

        public OperandDesc(GenTreeIndir indir)
        {
            _kind = OperandKind.Indir;
            _addr = indir.Addr;
            _indir = indir;
            _indirType = indir.Type;
        }

        public OperandDesc(var_types indirType, GenTree addr)
        {
            _kind = OperandKind.Indir;
            _addr = addr;
            _indirType = indirType;
        }

        public OperandDesc(nint immediate, bool immediateNeedsReloc)
        {
            _kind = OperandKind.Imm;
            _immediate = immediate;
            _immediateNeedsReloc = immediateNeedsReloc;
        }

        public OperandDesc(regNumber reg)
        {
            _kind = OperandKind.Reg;
            _reg = reg;
        }

        public OperandKind GetKind() => _kind;

        public CORINFO_FIELD_HANDLE GetFieldHnd()
        {
            assert(_kind == OperandKind.ClsVar);
            return _fieldHnd;
        }

        public int GetVarNum()
        {
            assert(_kind == OperandKind.Local);
            return _varNum;
        }

        public int GetLclOffset()
        {
            assert(_kind == OperandKind.Local);
            return _offset;
        }

        public GenTreeIndir GetIndirForm()
        {
            if (_indir is not null)
            {
                return _indir;
            }

            assert(_addr is not null);
            return indirForm(_indirType, _addr);
        }

        public nint GetImmediate()
        {
            assert(_kind == OperandKind.Imm);
            return _immediate;
        }

        public emitAttr GetEmitAttrForImmediate(emitAttr baseAttr)
        {
            assert(_kind == OperandKind.Imm);
            return _immediateNeedsReloc ? baseAttr | EA_CNS_RELOC_FLG : baseAttr;
        }

        public regNumber GetReg() => _reg;

        public bool IsContained() => _kind != OperandKind.Reg;
    }

    public static GenTreeIndir indirForm(var_types type, GenTree address)
    {
        var indir = new GenTreeIndir(GT_IND, type, address)
        {
            RegNum = REG_NA,
            IsContained = true,
        };

        return indir;
    }

    public static GenTreeStoreInd storeIndirForm(var_types type, GenTree address, GenTree data)
    {
        var store = new GenTreeStoreInd(type, address, data)
        {
            RegNum = REG_NA,
        };

        return store;
    }
}
