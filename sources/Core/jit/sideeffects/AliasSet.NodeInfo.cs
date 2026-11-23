// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public partial struct AliasSet
{
    /// <summary>Represents basic alias information for a single IR node.</summary>
    public readonly struct NodeInfo
    {
        private const int ALIAS_NONE = 0x0;
        private const int ALIAS_READS_ADDRESSABLE_LOCATION = 0x1;
        private const int ALIAS_WRITES_ADDRESSABLE_LOCATION = 0x2;
        private const int ALIAS_READS_LCL_VAR = 0x4;
        private const int ALIAS_WRITES_LCL_VAR = 0x8;

        private readonly Compiler _compiler;
        private readonly GenTree _node;
        private readonly int _flags;
        private readonly int _readLclNum;
        private readonly int _readLclOffs;

        /// <summary>Computes the alias info for a given node, including its logical local definitions but not local reads in its operands.</summary>
        /// <param name="compiler">The compiler context.</param>
        /// <param name="node">The node in question.</param>
        public NodeInfo(Compiler compiler, GenTree node)
        {
            _compiler = compiler;
            _node = node;
            _flags = ALIAS_NONE;
            _readLclNum = 0;
            _readLclOffs = 0;

            if (((node.Flags & GTF_ASG) != 0) || node.Oper.IsCall)
            {
                var visitor = new LocalWriteFlagVisitor(compiler);
                _ = node.VisitLogicalLocalDefs(compiler, ref visitor);

                if (visitor.HasLocalDefs)
                {
                    _flags |= ALIAS_WRITES_LCL_VAR;
                    if (visitor.WritesAddressableLocation)
                    {
                        _flags |= ALIAS_WRITES_ADDRESSABLE_LOCATION;
                    }
                }
            }

            if (node.Oper.IsCall)
            {
                // Calls are treated as reads and writes of addressable locations unless they are known to be pure.
                if (!node.AsCall().IsPure(compiler))
                {
                    _flags |= ALIAS_READS_ADDRESSABLE_LOCATION | ALIAS_WRITES_ADDRESSABLE_LOCATION;
                }
                return;
            }

            if (node.Oper.IsAtomic)
            {
                // Atomic operations both read and write addressable locations.
                _flags |= ALIAS_READS_ADDRESSABLE_LOCATION | ALIAS_WRITES_ADDRESSABLE_LOCATION;
                return;
            }

            if (IsLclVarWrite)
            {
                return;
            }

            // Is the operation a write? If so, set `node` to the location that is being written to.
            var isWrite = false;

            if (node.Oper.IsStore || (node.Oper is GT_MEMORYBARRIER))
            {
                isWrite = true;
            }
            else if (Configuration.FEATURE_HW_INTRINSICS && node.Oper.IsHWIntrinsic &&
                node.AsHWIntrinsic().IsMemoryStoreOrBarrier)
            {
                isWrite = true;
            }

            assert(isWrite || !node.RequiresAsgFlag);

            // `node` is the location being accessed. Determine whether or not it is a memory or local variable access, and if
            // it is the latter, get the number of the lclVar.
            var isMemoryAccess = false;
            var isLclVarAccess = false;
            var lclNum = 0;
            var lclOffs = 0;

            if (node.Oper.IsIndir)
            {
                // If the indirection targets a lclVar, we can be more precise with regards to aliasing by treating the
                // indirection as a lclVar access.
                var address = node.AsIndir().Addr;

                if (address.Oper is GT_LCL_ADDR)
                {
                    var lclVar = address.AsLclVarCommon();
                    isLclVarAccess = true;

                    lclNum = lclVar.LclNum;
                    lclOffs = lclVar.LclOffs;
                }
                else
                {
                    isMemoryAccess = true;
                }
            }
            else if (node.IsImplicitIndir)
            {
                isMemoryAccess = true;
            }
            else if (node.Oper.IsLocal)
            {
                var lclVar = node.AsLclVarCommon();
                isLclVarAccess = true;

                lclNum = lclVar.LclNum;
                lclOffs = lclVar.LclOffs;
            }
            else
            {
                // This is neither a memory nor a local var access.
                return;
            }

            assert(isMemoryAccess || isLclVarAccess);

            // Now that we've determined whether or not this access is a read or a write and whether the accessed location is
            // memory or a lclVar, determine whether or not the location is addressable and update the alias set.
            var isAddressableLocation = isMemoryAccess || compiler.lvaGetDesc(lclNum).IsAddressExposed;

            if (!isWrite)
            {
                if (isAddressableLocation)
                {
                    _flags |= ALIAS_READS_ADDRESSABLE_LOCATION;
                }

                if (isLclVarAccess)
                {
                    _flags |= ALIAS_READS_LCL_VAR;
                    _readLclNum = lclNum;
                    _readLclOffs = lclOffs;
                }
            }
            else
            {
                if (isAddressableLocation)
                {
                    _flags |= ALIAS_WRITES_ADDRESSABLE_LOCATION;
                }

                if (isLclVarAccess)
                {
                    _flags |= ALIAS_WRITES_LCL_VAR;
                }
            }
        }

        public Compiler Compiler => _compiler;

        public bool IsLclVarRead => (_flags & ALIAS_READS_LCL_VAR) is not 0;

        public bool IsLclVarWrite => (_flags & ALIAS_WRITES_LCL_VAR) is not 0;

        public int ReadLclNum
        {
            get
            {
                assert(Debugger.IsAttached || IsLclVarRead);
                return _readLclNum;
            }
        }

        public int ReadLclOffs
        {
            get
            {
                assert(Debugger.IsAttached || IsLclVarRead);
                return _readLclOffs;
            }
        }

        public GenTree Node => _node;

        public bool ReadsAddressableLocation => (_flags & ALIAS_READS_ADDRESSABLE_LOCATION) is not 0;

        public bool WritesAddressableLocation => (_flags & ALIAS_WRITES_ADDRESSABLE_LOCATION) is not 0;

        public bool WritesAnyLocation
        {
            get
            {
                if (WritesAddressableLocation)
                {
                    return true;
                }

                if (IsLclVarWrite)
                {
                    var visitor = new WritesAnyLocationVisitor(_compiler);
                    _ = VisitLocalWrites(ref visitor);
                    return visitor.WritesAnyLocation;
                }

                return false;
            }
        }

        public GenTree.VisitResult VisitLocalWrites<TVisitor>(ref TVisitor visitor)
            where TVisitor : struct, ILocalDefVisitor
        {
            if (_node.Oper.IsCall || (_node.Oper is GT_STORE_LCL_VAR or GT_STORE_LCL_FLD))
            {
                var trackingVisitor = new LocalWriteTrackingVisitor<TVisitor>(visitor);
                var result = _node.VisitLogicalLocalDefs(_compiler, ref trackingVisitor);
                visitor = trackingVisitor.Visitor;

                if (trackingVisitor.HasLocalDefs)
                {
                    return result;
                }
            }

            var location = _node.Oper.IsIndir ? _node.AsIndir().Addr : _node;
            assert(location.Oper.IsAnyLocal);
            return visitor.Visit(new IndirectLocalDef(location.AsLclVarCommon(), ValueSize.FromJitType(_node.Type)));
        }

        private struct LocalWriteTrackingVisitor<TVisitor>(TVisitor visitor) : ILocalDefVisitor
            where TVisitor : struct, ILocalDefVisitor
        {
            public bool HasLocalDefs;
            public TVisitor Visitor = visitor;

            public GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
            {
                HasLocalDefs = true;
                return Visitor.Visit(def);
            }
        }

        private struct LocalWriteFlagVisitor(Compiler compiler) : ILocalDefVisitor
        {
            public bool HasLocalDefs;
            public bool WritesAddressableLocation;

            public GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
            {
                HasLocalDefs = true;
                WritesAddressableLocation |= compiler.lvaGetDesc(def.LclNum).IsAddressExposed;
                return GenTree.VisitResult.Continue;
            }
        }

        private struct WritesAnyLocationVisitor(Compiler compiler) : ILocalDefVisitor
        {
            public bool WritesAnyLocation;

            public GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
            {
                ref var varDsc = ref compiler.lvaGetDesc(def.LclNum);
                WritesAnyLocation = varDsc.lvTracked
                    ? varDsc.IsLiveInOutOfHandler
                    : compiler.compHndBBtabCount > 0;
                return WritesAnyLocation ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue;
            }
        }

        private readonly struct IndirectLocalDef(GenTreeLclVarCommon def, ValueSize size) : ILocalDef
        {
            public GenTreeLclVarCommon DefNode => def;

            public int LclNum => def.LclNum;

            public int MultiDefIndex => BAD_VAR_NUM;

            public bool IsEntire(Compiler compiler) =>
                compiler.IsEntireAccess(LclNum, unchecked((uint)def.LclOffs), size);

            public nint GetOffset(Compiler compiler) => def.LclOffs;

            public ValueSize GetSize(Compiler compiler) => size;

            public nint GetValueOffset(Compiler compiler) => 0;

            public ValueSize GetStoreSize(Compiler compiler) => size;
        }
    }
}
