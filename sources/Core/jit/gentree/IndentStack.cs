// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Collections.Generic;

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

    private readonly Stack<Compiler.IndentInfo> stack;
    private readonly string[] indents;

    public IndentStack(Compiler compiler)
    {
        stack = [];

        if (compiler.asciiTrees)
        {
            indents = s_asciiIndents;
        }
        else
        {
            indents = s_unicodeIndents;
        }
    }

    // Return the depth of the current indentation.
    public int Depth => stack.Count;

    // Push a new indentation onto the stack, of the given type.
    public void Push(Compiler.IndentInfo info) => stack.Push(info);

    // Pop the most recent indentation type off the stack.
    public Compiler.IndentInfo Pop() => stack.Pop();

    // Print the current indentation and arcs.
    public void Print()
    {
        var index = 0;

        foreach (var entry in stack)
        {
            switch (entry)
            {
                case Compiler.IINone:
                {
                    jitprintf("   ");
                    break;
                }

                case Compiler.IIArc:
                {
                    if (index is 0)
                    {
                        jitprintf($"{indents[(int)(ICMiddle)]}{indents[(int)(ICDash)]}{indents[(int)(ICDash)]}");
                    }
                    else
                    {
                        jitprintf($"{indents[(int)(ICVertical)]}  ");
                    }
                    break;
                }

                case Compiler.IIArcBottom:
                {
                    jitprintf($"{indents[(int)(ICBottom)]}{indents[(int)(ICDash)]}{indents[(int)(ICDash)]}");
                    break;
                }

                case Compiler.IIArcTop:
                {
                    jitprintf($"{indents[(int)(ICTop)]}{indents[(int)(ICDash)]}{indents[(int)(ICDash)]}");
                    break;
                }

                case Compiler.IIError:
                {
                    jitprintf($"{indents[(int)(ICError)]}{indents[(int)(ICDash)]}{indents[(int)(ICDash)]}");
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            index++;
        }
        jitprintf($"{indents[(int)(ICTerminal)]}");
    }
}
#endif
