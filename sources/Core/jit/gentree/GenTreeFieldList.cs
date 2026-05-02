// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Represents a list of fields constituting a struct, when it is passed as an argument.</summary>
public sealed partial class GenTreeFieldList : GenTree
{
    private UseList _uses;

    public GenTreeFieldList()
        : base(GT_FIELD_LIST, TYP_STRUCT)
    {
        IsContained = true;
    }

    /// <summary>If this FIELD_LIST has only one field, then return it; otherwise return the field list.</summary>
    public GenTree SoleFieldOrThis
    {
        get
        {
            var head = _uses.Head;
            return ((head is not null) && head.Next is null) ? head.Node : this;
        }
    }

    public UseList Uses => _uses;

    /// <summary>Check if 2 FIELD_LIST nodes are equal.</summary>
    /// <param name="list1">The first FIELD_LIST node</param>
    /// <param name="list2">The second FIELD_LIST node</param>
    /// <returns>true if the 2 FIELD_LIST nodes have the same type, number of uses, and the uses are equal.</returns>
    public static bool Equals(GenTreeFieldList list1, GenTreeFieldList list2)
    {
        assert(list1.Type is TYP_STRUCT);
        assert(list2.Type is TYP_STRUCT);

        var result = true;

        var fieldListUse1 = list1._uses.Head;
        var fieldListUse2 = list2._uses.Head;

        while ((fieldListUse1 is not null) && (fieldListUse2 is not null))
        {
            if ((fieldListUse1.Type != fieldListUse2.Type))
            {
                result = false;
                break;
            }
            else if (fieldListUse1.Offset != fieldListUse2.Offset)
            {
                result = false;
                break;
            }
            else if (!Compare(fieldListUse1.Node, fieldListUse2.Node))
            {
                result = false;
                break;
            }

            fieldListUse1 = fieldListUse1.Next;
            fieldListUse2 = fieldListUse2.Next;
        }

        result &= (fieldListUse1 is null) && (fieldListUse2 is null);
        return result;
    }

    /// <summary>Add a new field use to the end of the use list and update side effect flags.</summary>
    /// <param name="compiler"></param>
    /// <param name="node"></param>
    /// <param name="offset"></param>
    /// <param name="type"></param>
    public void AddField(Compiler compiler, GenTree node, ushort offset, var_types type)
    {
        var use = new Use(node, offset, type);
        _uses.AddUse(use);
        Flags |= node.Flags & GTF_ALL_EFFECT;
    }

    /// <summary>Add a new field use to the end of the use list without updating side effect flags.</summary>
    /// <param name="compiler"></param>
    /// <param name="node"></param>
    /// <param name="offset"></param>
    /// <param name="type"></param>
    public void AddFieldLIR(Compiler compiler, GenTree node, ushort offset, var_types type)
    {
        var use = new Use(node, offset, type);
        _uses.AddUse(use);
    }

    /// <summary>Insert a new field use after the specified use and update side effect flags.</summary>
    /// <param name="compiler"></param>
    /// <param name="insertAfter"></param>
    /// <param name="node"></param>
    /// <param name="offset"></param>
    /// <param name="type"></param>
    public void InsertField(Compiler compiler, Use insertAfter, GenTree node, ushort offset, var_types type)
    {
        var use = new Use(node, offset, type);
        _uses.InsertUse(insertAfter, use);
        Flags |= node.Flags & GTF_ALL_EFFECT;
    }

    /// <summary>Insert a new field use after the specified use without updating side effect flags.</summary>
    /// <param name="compiler"></param>
    /// <param name="insertAfter"></param>
    /// <param name="node"></param>
    /// <param name="offset"></param>
    /// <param name="type"></param>
    public void InsertFieldLIR(Compiler compiler, Use insertAfter, GenTree node, ushort offset, var_types type)
    {
        var use = new Use(node, offset, type);
        _uses.InsertUse(insertAfter, use);
    }
}
