// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace RyuJitSharp;

/// <summary>Represent a particular run of the state machine</summary>
/// <remarks>For example, it maintains the array of counts for the terminated states. These counts should be stored in per method based for them to be correct under multithreadeded environment.</remarks>
public sealed partial class CodeSeqSM
{
    private Compiler? _compiler;
    private SM_STATE_ID _curState;
    private int _nativeSize;

    public int NativeSize => _nativeSize;

    public void Start(Compiler comp)
    {
        _compiler = comp;
        _nativeSize = 0;

        Reset();
    }

    public void Reset()
    {
        _curState = SM_STATE_ID_START;
    }

    public void End()
    {
        assert(_compiler is not null);

        if (s_smStates[_curState].term)
        {
            TermStateMatch(_curState, _compiler.verbose);
        }
    }

    public void Run(SM_OPCODE opcode, int level)
    {
        assert(_compiler is not null);

        assert(level <= MAX_CODE_SEQUENCE_LENGTH);
        var handleNext = true;

        while (handleNext)
        {
            var nextState = GetDestState(_curState, opcode);

            if (nextState != 0)
            {
                // This is easy, Just go to the next state.
                _curState = nextState;
                return;
            }

            assert(_curState != SM_STATE_ID_START);

            if (s_smStates[_curState].term)
            {
                TermStateMatch(_curState, _compiler.verbose);
                _curState = SM_STATE_ID_START;
            }
            else
            {
                handleNext = false;
            }
        }

        // This is hard. We need to rollback to the longest matched term state and restart from there.

        var rollbackState = s_smStates[_curState].longestTermState;
        TermStateMatch(rollbackState, _compiler.verbose);

        assert(s_smStates[_curState].length > s_smStates[rollbackState].length);
        Unsafe.SkipInit(out InlineArrayMaxCodeSequenceLength<SM_OPCODE> opcodesToRevisit);

        // So it can fit in the local array opcodesToRevisit[]
        var numOfOpcodesToRevisit = s_smStates[_curState].length - s_smStates[rollbackState].length + 1;
        assert(numOfOpcodesToRevisit is > 1 and <= MAX_CODE_SEQUENCE_LENGTH);

        var index = numOfOpcodesToRevisit - 1;
        opcodesToRevisit[index] = opcode;

        // Fill in the local array:
        for (var i = 0; i < numOfOpcodesToRevisit - 1; i++)
        {
            opcodesToRevisit[--index] = s_smStates[_curState].opc;
            _curState = s_smStates[_curState].prevState;
        }

        assert(_curState == rollbackState);

        // Now revisit these opcodes, starting from SM_STATE_ID_START.
        _curState = SM_STATE_ID_START;

        for (var i = 0; i < numOfOpcodesToRevisit; i++)
        {
            Run(opcodesToRevisit[i], level + 1);
        }
    }

    public SM_STATE_ID GetDestState(SM_STATE_ID srcState, SM_OPCODE opcode)
    {
        assert(opcode < SM_COUNT);

        ref var thisJumpTable = ref Unsafe.AddByteOffset(ref s_smJumpTableCells[0], s_smStates[srcState].jumpTableByteOffset);
        ref var cell = ref Unsafe.Add(ref thisJumpTable, (int)(opcode));

        if (cell.srcState != srcState)
        {
            // Either way means there is not outgoing edge from srcState.
            assert((cell.srcState is 0) || (cell.srcState != srcState));
            return 0;
        }
        else
        {
            return cell.destState;
        }
    }

    // Matched a termination state
    public void TermStateMatch(SM_STATE_ID stateId, bool verbose)
    {
        assert(s_smStates[stateId].term);

#if DEBUG && !SMGEN_COMPILE
        if (verbose)
        {
            jitprintf($"weight={StateWeights[stateId],3} : state {stateId,3} [ {StateDesc(stateId)} ]\n");
        }
#endif

        _nativeSize += StateWeights[stateId];
    }

    // Given an SM opcode retrieve the weight for this single opcode state.
    // For example, ID for single opcode state SM_NOSHOW is 2.
    public short GetWeightForOpcode(SM_OPCODE opcode)
    {
        var stateId = ((SM_STATE_ID)(opcode)) + SM_STATE_ID_START + 1;
        return StateWeights[stateId];
    }

#if DEBUG
    public string StateDesc(SM_STATE_ID stateId)
    {
        if (stateId == 0)
        {
            return "invalid";
        }
        if (stateId == SM_STATE_ID_START)
        {
            return "start";
        }

        var i = 0;
        var b = stateId;

        Unsafe.SkipInit(out InlineArrayMaxCodeSequenceLength<SM_OPCODE> stateDescOpcodes);

        while (s_smStates[b].prevState != 0)
        {
            stateDescOpcodes[i] = s_smStates[b].opc;
            b = s_smStates[b].prevState;
            i++;
        }
        assert((i == s_smStates[stateId].length) && (i > 0));

        var stateDesc = new StringBuilder(500);

        while (--i > 0)
        {
            _ = stateDesc.Append(CultureInfo.InvariantCulture, $"{stateDescOpcodes[i].Name} -> ");
        }
        _ = stateDesc.Append(CultureInfo.InvariantCulture, $"{stateDescOpcodes[0].Name}");

        return stateDesc.ToString();
    }
#endif

    public static SM_OPCODE MapToSMOpcode(OPCODE opcode)
    {
        assert(opcode < CEE_COUNT);
        var smOpcode = s_opcodeMap[(int)(opcode)];

        assert(smOpcode < SM_COUNT);
        return smOpcode;
    }
}
