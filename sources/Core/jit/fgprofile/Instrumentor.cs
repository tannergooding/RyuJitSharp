// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgprofile.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public class Instrumentor
{
    protected readonly Compiler Compiler;
    protected int SchemaCountValue;
    protected int InstrCountValue;
    private bool _modifiedFlow;

    protected Instrumentor(Compiler compiler)
    {
        Compiler = compiler;
    }

    public virtual bool ShouldProcess(BasicBlock block) => false;

    public virtual bool ShouldInstrument(BasicBlock block) => ShouldProcess(block);

    public virtual void Prepare(bool preImport)
    {
    }

    public virtual void BuildSchemaElements(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema)
    {
    }

    public virtual unsafe void Instrument(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema, byte* profileMemory)
    {
    }

    public int SchemaCount => SchemaCountValue;
    public int InstrCount => InstrCountValue;
    public bool ModifiedFlow => _modifiedFlow;

    protected void SetModifiedFlow()
    {
        _modifiedFlow = true;
    }

#if DEBUG
    protected static uint ConvertSynthesizedCount32(double weight)
    {
        const double signedLimit = 9223372036854775808.0;

        // MSVC x64 truncates the original double to a signed 64-bit integer before
        // retaining its low 32 bits. An invalid signed conversion yields long.MinValue.
        if (double.IsNaN(weight) || weight < -signedLimit || weight >= signedLimit)
        {
            return 0;
        }

        return unchecked((uint)(long)weight);
    }

    protected static ulong ConvertSynthesizedCount64(double weight)
    {
        const double highBit = 9223372036854775808.0;
        const double beyondUInt64 = 18446744073709551616.0;

        // MSVC x64 truncates through a signed 64-bit conversion, subtracting the high bit
        // for unsigned values above long.MaxValue. Invalid conversions yield long.MinValue.
        if (double.IsNaN(weight) || weight < -highBit || weight >= beyondUInt64)
        {
            return 1UL << 63;
        }

        if (weight >= highBit)
        {
            return unchecked((ulong)(long)(weight - highBit)) | (1UL << 63);
        }

        return unchecked((ulong)(long)weight);
    }
#endif
}

public sealed class NonInstrumentor(Compiler compiler) : Instrumentor(compiler);
