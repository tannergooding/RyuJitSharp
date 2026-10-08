// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace RyuJitSharp;

public static partial class API_ICorJitInfo_NamesExtensions
{
    extension(API_ICorJitInfo_Names api)
    {
        public string Name
        {
            get
            {
                assert(s_name.Length == (int)API_COUNT);

                return s_name[(int)api];
            }
        }
    }
}
