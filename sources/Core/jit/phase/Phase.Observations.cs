// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Phase
{
    /// <summary>Observations made before a phase runs that should still be true afterwards,if the phase status is MODIFIED_NOTHING.</summary>
    private struct Observations
    {
        private Compiler _compiler;
        private int _fgBBcount;
        private int _fgBBNumMax;
        private int _compHndBBtabCount;
        private int _lvaCount;
        private int _compGenTreeID;
        private int _compStatementID;
        private int _compBasicBlockID;

        /// <summary>snapshot key compiler variables before running a phase</summary>
        /// <param name="compiler">current compiler instance</param>
        public Observations(Compiler compiler)
        {
            _compiler = null!;
            _fgBBcount = 0;
            _fgBBNumMax = 0;
            _compHndBBtabCount = 0;
            _lvaCount = 0;
            _compGenTreeID = 0;
            _compStatementID = 0;
            _compBasicBlockID = 0;

            if (Configuration.DEBUG)
            {
                compiler = compiler.impInlineRoot;

                _compiler = compiler;
                _fgBBcount = compiler.fgBBcount;
                _fgBBNumMax = compiler.fgBBNumMax;
                _compHndBBtabCount = compiler.compHndBBtabCount;
                _lvaCount = compiler.lvaCount;
                _compGenTreeID = compiler.compGenTreeID;
                _compStatementID = compiler.compStatementID;
                _compBasicBlockID = compiler.compBasicBlockID;
            }
        }

        /// <summary>verify key compiler variables are unchanged if phase claims it made no modifications</summary>
        /// <param name="status">status from the just-completed phase</param>
        public readonly void Check(PhaseStatus status)
        {
            if (Configuration.DEBUG && status == PhaseStatus.MODIFIED_NOTHING)
            {
                var compiler = _compiler;

                assert(_fgBBcount == compiler.fgBBcount);
                assert(_fgBBNumMax == compiler.fgBBNumMax);
                assert(_compHndBBtabCount == compiler.compHndBBtabCount);
                assert(_lvaCount == compiler.lvaCount);
                assert(_compGenTreeID == compiler.compGenTreeID);
                assert(_compStatementID == compiler.compStatementID);
                assert(_compBasicBlockID == compiler.compBasicBlockID);
            }
        }
    }
}
