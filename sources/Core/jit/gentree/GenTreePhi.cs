// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>a variable sized list of GT_PHI_ARG nodes.</summary>
/// <remarks>
///   <para>All PHI_ARG nodes must represent uses of the same local variable and the PHI node's type must be the same as the local variable's type.</para>
///   <para>The PHI node does not represent a definition by itself, it is always the value operand of a STORE_LCL_VAR node.</para>
///   <para>The local store node itself is the definition for the same local variable referenced by all the used PHI_ARG nodes:</para>
///   <list type="bullet">
///     <item><c>STORE_LCL_VAR&lt;V01&gt;(PHI(PHI_ARG(V01), PHI_ARG(V01), PHI_ARG(V01)))</c></item>
///   </list>
///   <para>The order of the PHI_ARG uses is not currently relevant and it may be the same or not as the order of the predecessor blocks.</para>
/// </remarks>
public sealed partial class GenTreePhi : GenTree
{
    private Use? _firstUse;

    public GenTreePhi(var_types type)
        : base(GT_PHI, type)
    {
    }

    public Use? FirstUse
    {
        get
        {
            return _firstUse;
        }

        set
        {
            _firstUse = value;
        }
    }

    public UseList Uses => new UseList(_firstUse);

    /// <summary>Checks if 2 PHI nodes are equal.</summary>
    /// <param name="phi1">The first PHI node</param>
    /// <param name="phi2">The second PHI node</param>
    /// <returns>true if the 2 PHI nodes have the same type, number of uses, and the uses are equal.</returns>
    /// <remarks>The order of uses must be the same for equality, even if the order is not usually relevant and is not guaranteed to reflect a particular order of the predecessor blocks.</remarks>
    public static bool Equals(GenTreePhi phi1, GenTreePhi phi2)
    {
        var result = true;

        if (phi1.Type != phi2.Type)
        {
            result = false;
        }
        else
        {
            var phi1Use = phi1._firstUse;
            var phi2Use = phi2._firstUse;

            while ((phi1Use is not null) && (phi2Use is not null))
            {
                if (!Compare(phi1Use.Node, phi2Use.Node))
                {
                    result = false;
                    break;
                }

                phi1Use = phi1Use.Next;
                phi2Use = phi2Use.Next;
            }

            result &= (phi1Use is null) && (phi2Use is null);
        }
        return result;
    }
}
