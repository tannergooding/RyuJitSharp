// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public sealed class dataSection
    {
        public const uint MIN_DATA_ALIGN = 4;
        public const uint MAX_DATA_ALIGN = 64;

        public enum sectionType
        {
            data,
            blockAbsoluteAddr,
            blockRelative32,
            asyncResumeInfo,
        }

        public dataSection? dsNext;
        public uint dsAlignment;
        public uint dsOffset;
        public uint dsSize;
        public sectionType dsType;
        public var_types dsDataType;

        private byte[]? _data;
        private BasicBlock?[]? _blocks;
        private emitLocation[]? _locations;

        public byte[] Data
        {
            get
            {
                assert(dsType == sectionType.data);
                assert(_data is not null);
                return _data;
            }

            set
            {
                assert(dsType == sectionType.data);
                _data = value;
            }
        }

        public BasicBlock?[] Blocks
        {
            get
            {
                assert(dsType is sectionType.blockAbsoluteAddr or sectionType.blockRelative32);
                assert(_blocks is not null);
                return _blocks;
            }

            set
            {
                assert(dsType is sectionType.blockAbsoluteAddr or sectionType.blockRelative32);
                _blocks = value;
            }
        }

        public emitLocation[] Locations
        {
            get
            {
                assert(dsType == sectionType.asyncResumeInfo);
                assert(_locations is not null);
                return _locations;
            }

            set
            {
                assert(dsType == sectionType.asyncResumeInfo);
                _locations = value;
            }
        }
    }
}
