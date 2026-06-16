// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public readonly struct IndentStack
{
    private static readonly string[] s_emptyIndents = [
        " ",    // ICVertical,
        " ",    // ICBottom,
        " ",    // ICTop,
        " ",    // ICMiddle,
        " ",    // ICDash,
        "",     // ICTerminal,
        "?",    // ICError,
    ];

    private static readonly string[] s_asciiIndents = [
        "|",    // ICVertical,
        "\\",   // ICBottom,
        "/",    // ICTop,
        "+",    // ICMiddle,
        "-",    // ICDash,
        "*",    // ICTerminal,
        "?",    // ICError,
    ];

    private static readonly string[] s_unicodeIndents = [
        "│",    // ICVertical,
        "└",    // ICBottom,
        "┌",    // ICTop,
        "├",    // ICMiddle,
        "─",    // ICDash,
        "▌",    // ICTerminal,
        "?",    // ICError,
    ];

    private readonly List<Compiler.IndentInfo> _stack;
    private readonly string[] _indents;

    public IndentStack(Compiler compiler)
    {
        _stack = [];

        if (compiler.asciiTrees)
        {
            _indents = s_asciiIndents;
        }
        else
        {
            _indents = s_unicodeIndents;
        }
    }

    // Return the depth of the current indentation.
    public int Depth => _stack.Count;

    // Push a new indentation onto the stack, of the given type.
    public void Push(Compiler.IndentInfo info) => _stack.Add(info);

    // Pop the most recent indentation type off the stack.
    public Compiler.IndentInfo Pop()
    {
        var index = _stack.Count - 1;
        var info = _stack[index];

        _stack.RemoveAt(index);
        return info;
    }

    // Print the current indentation and arcs.
    public void Print()
    {
        var stack = CollectionsMarshal.AsSpan(_stack);

        for (var i = 0; i < stack.Length; i++)
        {
            var entry = stack[i];

            switch (entry)
            {
                case Compiler.IINone:
                {
                    jitprintf("   ");
                    break;
                }

                case Compiler.IIArc:
                {
                    if ((i + 1) == stack.Length)
                    {
                        jitprintf($"{_indents[(int)(ICMiddle)]}{_indents[(int)(ICDash)]}{_indents[(int)(ICDash)]}");
                    }
                    else
                    {
                        jitprintf($"{_indents[(int)(ICVertical)]}  ");
                    }
                    break;
                }

                case Compiler.IIArcBottom:
                {
                    jitprintf($"{_indents[(int)(ICBottom)]}{_indents[(int)(ICDash)]}{_indents[(int)(ICDash)]}");
                    break;
                }

                case Compiler.IIArcTop:
                {
                    jitprintf($"{_indents[(int)(ICTop)]}{_indents[(int)(ICDash)]}{_indents[(int)(ICDash)]}");
                    break;
                }

                case Compiler.IIError:
                {
                    jitprintf($"{_indents[(int)(ICError)]}{_indents[(int)(ICDash)]}{_indents[(int)(ICDash)]}");
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }
        jitprintf($"{_indents[(int)(ICTerminal)]}");
    }
}
#endif
