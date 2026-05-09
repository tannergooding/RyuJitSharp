// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public sealed class AddCodeDsc
    {
        /// <summary>The block to which we jump to raise the exception.</summary>
        public BasicBlock? acdDstBlk;

        public ushort acdTryIndex;

        public ushort acdHndIndex;

        // Which EH region forms the key?
        public AcdKeyDesignator acdKeyDsg;

        /// <summary>what kind of a special block is this?</summary>
        public SpecialCodeKind acdKind;

        /// <summary>do we need to keep this helper block?</summary>
        public bool acdUsed;

#if !FEATURE_FIXED_OUT_ARGS
        /// <summary>has acdStkLvl value been already set?</summary>
        public bool acdStkLvlInit;

        /// <summary>stack level in stack slots.</summary>
        public int acdStkLvl;
#endif

#if DEBUG
        public int acdNum;
#endif

        /// <summary>determine new key designator after modifying the region indices.</summary>
        /// <param name="compiler">current compiler instance</param>
        /// <returns>true if the key designator changes</returns>
        public bool UpdateKeyDesignator(Compiler compiler)
        {
            // This ACD may now have a new enclosing region.
            // Figure out the new parent key designator.
            //
            // For example, suppose there is a try that has an array
            // bounds check and an empty finally, all within a
            // finally. When we remove the try, the ACD for the bounds
            // check changes from being enclosed in a try to being
            // enclosed in a finally.
            //
            // Filter ACDs should always remain in filter regions.

            var inHnd = acdHndIndex > 0;
            var inTry = acdTryIndex > 0;

            AcdKeyDesignator newDsg;

            if (!inTry && !inHnd)
            {
                // Moved outside of all EH regions.
                assert(acdKeyDsg != AcdKeyDesignator.KD_FLT);
                newDsg = AcdKeyDesignator.KD_NONE;
            }
            else if (inTry && (!inHnd || (acdTryIndex < acdHndIndex)))
            {
                // Moved into a parent try region.
                assert(acdKeyDsg != AcdKeyDesignator.KD_FLT);
                newDsg = AcdKeyDesignator.KD_TRY;
            }
            else
            {
                // Moved into a parent or renumbered handler or filter region.
                if (acdKeyDsg == AcdKeyDesignator.KD_FLT)
                {
                    newDsg = AcdKeyDesignator.KD_FLT;
                }
                else
                {
                    newDsg = AcdKeyDesignator.KD_HND;
                }
            }

            var result = (newDsg != acdKeyDsg);
            acdKeyDsg = newDsg;
            return result;
        }

#if DEBUG
        public void Dump()
        {
            jitprintf($"ACD{acdNum} {acdKind} ");

            switch (acdKeyDsg)
            {
                case AcdKeyDesignator.KD_NONE:
                {
                    jitprintf("in method region");
                    break;
                }

                case AcdKeyDesignator.KD_TRY:
                {
                    jitprintf($"in try region of EH#{acdTryIndex - 1}");
                    break;
                }

                case AcdKeyDesignator.KD_HND:
                {
                    jitprintf($"in handler region of EH#{acdHndIndex - 1}");
                    break;
                }

                case AcdKeyDesignator.KD_FLT:
                {
                    jitprintf($"in filter region of EH#{acdHndIndex - 1}");
                    break;
                }

                default:
                {
                    jitprintf("(unexpected region)");
                    break;
                }
            }

            jitprintf($" map key 0x{new AddCodeDscKey(this).Data:X}\n");
        }
#endif
    }
}
