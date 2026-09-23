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
        private readonly int _lclNum;
        private readonly int _lclOffs;

        /// <summary>Computes the alias info for a given node. Note that this does not include the set of lclVar accesses for a node unless the node is itself a lclVar access (e.g. a GT_LCL_VAR, GT_STORE_LCL_VAR, etc.).</summary>
        /// <param name="compiler">The compiler context.</param>
        /// <param name="node">The node in question.</param>
        public NodeInfo(Compiler compiler, GenTree node)
        {
            _compiler = compiler;
            _node = node;

            if (node.Oper.IsCall)
            {
                var call = node.AsCall();

                // For calls having return buffer, update the local number that is written after this call.
                var retBufArgNode = compiler.gtCallGetDefinedRetBufLclAddr(call);

                if (retBufArgNode is not null)
                {
                    var lclVar = retBufArgNode.AsLclVarCommon();

                    _flags |= ALIAS_WRITES_LCL_VAR;
                    _lclNum = lclVar.LclNum;
                    _lclOffs = lclVar.LclOffs;

                    if (compiler.lvaGetDesc(_lclNum).IsAddressExposed)
                    {
                        _flags |= ALIAS_WRITES_ADDRESSABLE_LOCATION;
                    }
                }

                // Calls are treated as reads and writes of addressable locations unless they are known to be pure.
                if (call.IsPure(compiler))
                {
                    _flags = ALIAS_NONE;
                }
                else
                {
                    _flags = ALIAS_READS_ADDRESSABLE_LOCATION | ALIAS_WRITES_ADDRESSABLE_LOCATION;
                }
            }
            else if (node.Oper.IsAtomic)
            {
                // Atomic operations both read and write addressable locations.
                _flags = ALIAS_READS_ADDRESSABLE_LOCATION | ALIAS_WRITES_ADDRESSABLE_LOCATION;
            }
            else
            {
                // Is the operation a write? If so, set `node` to the location that is being written to.
                var isWrite = false;

                if (node.Oper.IsStore || (node.Oper is GT_MEMORYBARRIER))
                {
                    isWrite = true;
                }
#if FEATURE_HW_INTRINSICS
                else if (node.Oper.IsHWIntrinsic && node.AsHWIntrinsic().IsMemoryStoreOrBarrier)
                {
                    isWrite = true;
                }
#endif

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
                    _flags = ALIAS_NONE;
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
                        _lclNum = lclNum;
                        _lclOffs = lclOffs;
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
                        _lclNum = lclNum;
                        _lclOffs = lclOffs;
                    }
                }
            }
        }

        public Compiler Compiler => _compiler;

        public bool IsLclVarRead => (_flags & ALIAS_READS_LCL_VAR) is not 0;

        public bool IsLclVarWrite => (_flags & ALIAS_WRITES_LCL_VAR) is not 0;

        public int LclNum
        {
            get
            {
                assert(Debugger.IsAttached || IsLclVarRead || IsLclVarWrite);
                return _lclNum;
            }
        }

        public int LclOffs

        {
            get
            {
                assert(Debugger.IsAttached || IsLclVarRead || IsLclVarWrite);
                return _lclOffs;
            }
        }

        public GenTree Node => _node;

        public bool ReadsAddressableLocation => (_flags & ALIAS_READS_ADDRESSABLE_LOCATION) is not 0;

        public bool WritesAddressableLocation => (_flags & ALIAS_WRITES_ADDRESSABLE_LOCATION) is not 0;

        public bool WritesAnyLocation
        {
            get
            {
                if ((_flags & ALIAS_WRITES_ADDRESSABLE_LOCATION) is not 0)
                {
                    return true;
                }

                if ((_flags & ALIAS_WRITES_LCL_VAR) is not 0)
                {
                    // Stores to locals live into handlers cannot be reordered with
                    // exception-throwing nodes so we conservatively consider them
                    // globally visible.

                    ref var varDsc = ref _compiler.lvaGetDesc(_lclNum);

                    if (varDsc.lvTracked)
                    {
                        return varDsc.IsLiveInOutOfHandler;
                    }
                    return _compiler.compHndBBtabCount > 0;
                }
                return false;
            }
        }
    }
}
