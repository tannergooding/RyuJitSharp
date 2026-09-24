// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void MapParameterRegisterLocals()
    {
        var compiler = CompilerInstance;
        compiler._paramRegLocalMappings ??= [];

        for (var localNumber = 0; localNumber < compiler.info.compArgsCount; localNumber++)
        {
            ref var descriptor = ref compiler.lvaGetDesc(localNumber);
            ref readonly var abiInfo = ref compiler.lvaGetParameterAbiInfo(localNumber);

            if (compiler.lvaGetPromotionType(descriptor) != Compiler.PROMOTION_TYPE_INDEPENDENT)
            {
                continue;
            }

            if (!abiInfo.HasAnyRegisterSegment)
            {
                continue;
            }

            assert(!abiInfo.IsSplitAcrossRegistersAndStack);

            for (var index = 0; index < descriptor.lvFieldCnt; index++)
            {
                var fieldLocalNumber = descriptor.lvFieldLclStart + index;
                ref var field = ref compiler.lvaGetDesc(fieldLocalNumber);

                foreach (ref readonly var segment in abiInfo.Segments)
                {
                    if (segment.Offset + segment.Size <= field.lvFldOffset)
                    {
                        continue;
                    }

                    if (field.lvFldOffset + field.lvExactSize <= segment.Offset)
                    {
                        continue;
                    }

                    var offset = unchecked((uint)(segment.Offset - field.lvFldOffset));
                    compiler._paramRegLocalMappings.Add(new(segment, fieldLocalNumber, offset));
                }

                assert(!field.lvIsParamRegTarget);
                field.lvIsParamRegTarget = true;
            }
        }

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"{compiler._paramRegLocalMappings.Count} parameter register to local mappings\n");
            foreach (var mapping in compiler._paramRegLocalMappings)
            {
                jitprintf($"  {mapping.RegisterSegment.Register.Name} -> V{mapping.LclNum:D2}+{mapping.Offset}\n");
            }
        }
#endif
    }
}
