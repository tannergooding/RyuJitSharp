// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class LIR
{
    /// <summary>
    ///   <para>Represents a use &lt;-&gt; def edge between two nodes in a range of LIR.</para>
    ///   <para>Provides utilities to point the use to a different def.</para>
    ///   <para>Note that because this type deals in edges between nodes, it represents the single use of the def.</para>
    /// </summary>
    public ref struct Use
    {
        private Range _range;
        private ref GenTree _edge;
        private GenTree _user;

        /// <summary>Constructs a use &lt;-&gt; def edge given the range that contains the use and the def, the use -&gt; def edge, and the user.</summary>
        /// <param name="range">The range that contains the use and the def.</param>
        /// <param name="edge">The use -&gt; def edge.</param>
        /// <param name="user">The node that uses the def.</param>
        public Use(Range range, ref GenTree edge, GenTree user)
        {
            _range = range;
            _edge = ref edge;
            _user = user;

            AssertIsValid();
        }

        /// <summary>Make a use into a dummy use.</summary>
        /// <param name="range">The range that contains the node</param>
        /// <param name="node">The node for which to create a dummy use.</param>
        /// <param name="dummyUse">The resulting dummy use</param>
        /// <remarks>
        ///   <para>This method is provided as a convenience to allow transforms to work uniformly over Use values.</para>
        ///   <para>It allows the creation of a Use given a node that is not used.</para>
        /// </remarks>
        public static void MakeDummyUse(Range range, GenTree node, [UnscopedRef] out Use dummyUse)
        {
            assert(node is not null);

            dummyUse._range = range;
            dummyUse._user = node;
            dummyUse._edge = ref dummyUse._user;

            assert(dummyUse.IsInitialized());
        }

        /// <summary>Returns the node that produces the def for this use.</summary>
        /// <returns></returns>
        public readonly GenTree Def()
        {
            assert(IsInitialized());
            return _edge;
        }

        /// <summary>Returns the node that uses the def for this use.</summary>
        /// <returns></returns>
        public readonly GenTree User()
        {
            assert(IsInitialized());
            assert(!IsDummyUse());
            return _user;
        }

        /// <summary>Returns true if the use is minimally valid; false otherwise.</summary>
        /// <returns></returns>
        public readonly bool IsInitialized() => (_range is not null) && (_user is not null) && !Unsafe.IsNullRef(ref _edge);

        /// <summary>DEBUG function to assert on many validity conditions.</summary>
        /// <returns></returns>
        [Conditional("DEBUG")]
        public readonly void AssertIsValid()
        {
#if DEBUG
            assert(IsInitialized());
            assert(_range.Contains(_user));
            assert(Def() is not null);
            assert(Unsafe.AreSame(in _user.GetUseRefOrNullRef(Def()), in _edge));
#endif
        }

        /// <summary>Indicates whether or not a use is a dummy use.</summary>
        /// <returns>true if this use is a dummy use; false otherwise.</returns>
        /// <remarks>This method must be called before attempting to call the User(): for dummy uses, the user is the same node as the def.</remarks>
        public readonly bool IsDummyUse() => Unsafe.AreSame(in _edge, in _user);

        /// <summary>Changes the use to point to a new value.</summary>
        /// <param name="replacement">The replacement node.</param>
        public void ReplaceWith(GenTree replacement)
        {
            // For example, given the following LIR:
            //
            //    t15 =    lclVar    int    arg1
            //    t16 =    lclVar    int    arg1
            //
            //          /--*  t15 int
            //          +--*  t16 int
            //    t17 = *  ==        int
            //
            //          /--*  t17 int
            //          *  jmpTrue   void
            //
            // If we wanted to replace the use of t17 with a use of the constant "1", we
            // might do the following (where `opEq` is a `Use` value that represents the
            // use of t17):
            //
            //    GenTree* constantOne = compiler->gtNewIconNode(1);
            //    range.InsertAfter(opEq.Def(), constantOne);
            //    opEq.ReplaceWith(constantOne);
            //
            // Which would produce something like the following LIR:
            //
            //    t15 =    lclVar    int    arg1
            //    t16 =    lclVar    int    arg1
            //
            //          /--*  t15 int
            //          +--*  t16 int
            //    t17 = *  ==        int
            //
            //    t18 =    const     int    1
            //
            //          /--*  t18 int
            //          *  jmpTrue   void
            //
            // Eliminating the now-dead compare and its operands using `LIR::Range::Remove`
            // would then give us:
            //
            //    t18 =    const     int    1
            //
            //          /--*  t18 int
            //          *  jmpTrue   void

#if DEBUG
            assert(IsInitialized());
            assert(replacement is not null);
            assert(IsDummyUse() || _range.Contains(_user));
            assert(_range.Contains(replacement));
#endif

            if (!IsDummyUse())
            {
                _user.ReplaceOperand(ref _edge, replacement);
            }
            else
            {
                _edge = replacement;
            }
        }

        /// <inheritdoc cref="ReplaceWithLclVar(Compiler, uint, out GenTree)" />
        public uint ReplaceWithLclVar(Compiler compiler, uint lclNum = BAD_VAR_NUM) => ReplaceWithLclVar(compiler, lclNum, out _);

        /// <inheritdoc cref="ReplaceWithLclVar(Compiler, uint, out GenTree)" />
        public uint ReplaceWithLclVar(Compiler compiler, out GenTree pStore) => ReplaceWithLclVar(compiler, BAD_VAR_NUM, out pStore);

        /// <summary>
        ///   <para>Assigns the def for this use to a local var and points the use to a use of that local var.</para>
        ///   <para>If no local number is provided, creates a new local var.</para>
        /// </summary>
        /// <param name="compiler">The Compiler context.</param>
        /// <param name="lclNum">The local to use for temporary storage. If BAD_VAR_NUM (the default) is provided, this method will create and use a new local var.</param>
        /// <param name="pStore">On return, contains the created store node</param>
        /// <returns>The number of the local var used for temporary storage.</returns>
        public uint ReplaceWithLclVar(Compiler compiler, uint lclNum, out GenTree pStore)
        {
            // For example, given the following IR:
            //
            //    t15 =    lclVar    int    arg1
            //    t16 =    lclVar    int    arg1
            //
            //          /--*  t15 int
            //          +--*  t16 int
            //    t17 = *  ==        int
            //
            //          /--*  t17 int
            //          *  jmpTrue   void
            //
            // If we wanted to replace the use of t17 with a use of a new local var
            // that holds the value represented by t17, we might do the following
            // (where `opEq` is a `Use` value that represents the use of t17):
            //
            //    opEq.ReplaceUseWithLclVar(compiler, block->getBBWeight(compiler));
            //
            // This would produce the following LIR:
            //
            //    t15 =    lclVar    int    arg1
            //    t16 =    lclVar    int    arg1
            //
            //          /--*  t15 int
            //          +--*  t16 int
            //    t17 = *  ==        int
            //
            //          /--*  t17 int
            //          *  st.lclVar int    tmp0
            //
            //    t18 =    lclVar    int    tmp0
            //
            //          /--*  t18 int
            //          *  jmpTrue   void

#if DEBUG
            assert(IsInitialized());
            assert(compiler is not null);
            assert(_range.Contains(_user));
            assert(_range.Contains(_edge));
#endif

            var node = _edge;

            if (lclNum == BAD_VAR_NUM)
            {
                lclNum = compiler.lvaGrabTemp(true, "ReplaceWithLclVar is creating a new local variable");
            }

            var store = compiler.gtNewTempStore(lclNum, node).AsLclVar();

            assert(store is not null);
            assert(store.Op1 == node);

            var load = new GenTreeLclVar(GT_LCL_VAR, store.Type, store.AsLclVarCommon().LclNum);

            _range.InsertAfter(node, store, load);

            ReplaceWith(load);

            JITDUMP("ReplaceWithLclVar created store :\n");
            DISPNODE(store);

            pStore = store;
            return lclNum;
        }
    }
}
