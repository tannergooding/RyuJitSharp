// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public static unsafe fgWalkPreFn gtMarkColonCond;

    public static unsafe fgWalkPreFn gtClearColonCond;

#if DEBUG
    public void gtDispRange(LIR.ReadOnlyRange range)
    {
        // TODO: Port Compiler.gtDispRange
    }

    public void gtDispStmt(Statement stmt, string? msg = null)
    {
        // TODO: Port Compiler.gtDispStmt
    }

    public void gtDispTree(GenTree tree, IndentStack? indentStack = null, string? msg = null, bool topOnly = false, bool isLIR = false)
    {
        // TODO: Port Compiler.gtDispTree
    }

    public void gtDispTreeRange(LIR.Range containingRange, GenTree tree)
    {
        // TODO: Port Compiler.gtDispTreeRange
    }
#endif

    public GenTreeLclVar gtNewTempStore(uint tmp, GenTree val)
        => gtNewTempStore(tmp, val, out _, CHECK_SPILL_NONE, default, null);

    public GenTreeLclVar gtNewTempStore(uint tmp, GenTree val, out Statement pAfterStmt, uint curLevel = CHECK_SPILL_NONE, in DebugInfo di = default, BasicBlock? block = null)
    {
        // TODO: Port Compiler.gtNewTempStore
        Unsafe.SkipInit(out pAfterStmt);
        return null!;
    }

    /// <inheritdoc cref="gtPeelOffsets(ref GenTree, out long, out FieldSeq)" />
    public void gtPeelOffsets(ref GenTree addr, out target_ssize_t offset) => gtPeelOffsets(ref addr, out offset, out Unsafe.NullRef<FieldSeq?>());

    /// <summary>Peel all ADD(addr, CNS_INT(x)) nodes off the specified address node and return the base node and sum of offsets peeled.</summary>
    /// <param name="addr">The address node.</param>
    /// <param name="offset">The sum of offset peeled such that ADD(addr, offset) is equivalent to the original addr.</param>
    /// <param name="fldSeq">The combined field sequence for all the peeled offsets.</param>
    public void gtPeelOffsets(ref GenTree addr, out target_ssize_t offset, out FieldSeq? fldSeq)
    {
        assert(addr.Type is TYP_I_IMPL or TYP_BYREF or TYP_REF);

        Unsafe.SkipInit(out fldSeq);
        offset = 0;

        if (!Unsafe.IsNullRef(in fldSeq))
        {
            fldSeq = null;
        }

        while (true)
        {
            if ((addr.Oper is GT_ADD) && !addr.HasOverflowCheck)
            {
                var addrOp = addr.AsOp();

                var op1 = addrOp.Op1;
                var op2 = addrOp.Op2;

                assert(op1 is not null);
                assert(op2 is not null);

                if (op2.Oper.IsCnsIntOrI && (op2.Type is TYP_I_IMPL) && !op2.IsIconHandle)
                {
                    var intCon = op2.AsIntCon();
                    offset += intCon.IconValue;

                    if (!Unsafe.IsNullRef(in fldSeq))
                    {
                        assert(m_fieldSeqStore is not null);
                        fldSeq = m_fieldSeqStore.Append(fldSeq, intCon.FieldSeq);
                    }
                    addr = op1;
                }
                else if (op1.Oper.IsCnsIntOrI && (op1.Type is TYP_I_IMPL) && !op1.IsIconHandle)
                {
                    var intCon = op1.AsIntCon();
                    offset += intCon.IconValue;

                    if (!Unsafe.IsNullRef(in fldSeq))
                    {
                        assert(m_fieldSeqStore is not null);
                        fldSeq = m_fieldSeqStore.Append(intCon.FieldSeq, fldSeq);
                    }
                    addr = op2;
                }
                else
                {
                    break;
                }
            }
            else if (addr.Oper is GT_LEA)
            {
                var addrMode = addr.AsAddrMode();

                if (addrMode.HasIndex)
                {
                    break;
                }
                offset += addrMode.Offset;

                assert(addrMode.Base is not null);
                addr = addrMode.Base;
            }
            else
            {
                break;
            }
        }
    }
}
