// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace RyuJitSharp;

public static partial class BitSetSupport
{
    public const uint BitsInByte = 8;

    public enum Operation
    {
        BSOP_Assign,
        BSOP_AssignAllowUninitRhs,
        BSOP_AssignNocopy,
        BSOP_OldStyleClearD,
        BSOP_ClearD,
        BSOP_MakeSingleton,
        BSOP_MakeEmpty,
        BSOP_MakeFull,
        BSOP_MakeCopy,
        BSOP_IsEmpty,
        BSOP_Count,
        BSOP_RemoveElemD,
        BSOP_RemoveElem,
        BSOP_AddElemD,
        BSOP_AddElem,
        BSOP_UnionD,
        BSOP_UnionDChanged,
        BSOP_Union,
        BSOP_IntersectionD,
        BSOP_Intersection,
        BSOP_IsEmptyIntersection,
        BSOP_DiffD,
        BSOP_Diff,
        BSOP_IsMember,
        BSOP_IsNotMember,
        BSOP_NoBitsAbove,
        BSOP_LeftShiftSingletonByOneD,
        BSOP_IsSubset,
        BSOP_Equal,
        BSOP_NotEqual,
        BSOP_NextBit,
        BSOP_ToString,
        BSOP_NUMOPS,
    }

    public static readonly string[] OpNames = Enum.GetNames<Operation>()[..(int)Operation.BSOP_NUMOPS];

    public sealed class BitSetOpCounter
    {
        private readonly uint[] _opCounts = new uint[(int)Operation.BSOP_NUMOPS];
        private readonly string _fileName;
        private FileStream? _opOutputFile;
        private uint _totalOps;

        public BitSetOpCounter(string fileName)
        {
            _totalOps = 0;
            _fileName = fileName;
            _opOutputFile = null;
        }

        public void RecordOp(Operation op)
        {
            var index = (int)op;
            _opCounts[index] = unchecked(_opCounts[index] + 1);
            _totalOps = unchecked(_totalOps + 1);

            if ((_totalOps % 1_000_000) != 0)
            {
                return;
            }

            WriteReport();
        }

        private void WriteReport()
        {
            _opOutputFile ??= new FileStream(
                _fileName,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);

            var operationOrder = new int[(int)Operation.BSOP_NUMOPS];
            var operationOrdered = new bool[(int)Operation.BSOP_NUMOPS];

            for (var k = 0; k < (int)Operation.BSOP_NUMOPS; k++)
            {
                var candidateSet = false;
                var candidate = 0u;
                var candidateIndex = 0;

                for (var j = 0; j < (int)Operation.BSOP_NUMOPS; j++)
                {
                    if (operationOrdered[j])
                    {
                        continue;
                    }

                    if (!candidateSet || _opCounts[j] > candidate)
                    {
                        candidateIndex = j;
                        candidate = _opCounts[j];
                        candidateSet = true;
                    }
                }

                assert(candidateSet);
                operationOrder[k] = candidateIndex;
                operationOrdered[candidateIndex] = true;
            }

            var output = new StringBuilder();
            _ = output.Append("@ ")
                .Append(unchecked((int)_totalOps).ToString(CultureInfo.InvariantCulture))
                .Append(" total ops.")
                .AppendLine();

            for (var ii = 0; ii < (int)Operation.BSOP_NUMOPS; ii++)
            {
                var i = operationOrder[ii];
                _ = output.Append("   Op ")
                    .Append(OpNames[i].PadLeft(40))
                    .Append(": ")
                    .Append(unchecked((int)_opCounts[i]).ToString(CultureInfo.InvariantCulture).PadLeft(8))
                    .AppendLine();
            }

            var bytes = Encoding.ASCII.GetBytes(output.ToString());
            _opOutputFile.Write(bytes, 0, bytes.Length);
            _opOutputFile.Flush();
        }
    }
}
