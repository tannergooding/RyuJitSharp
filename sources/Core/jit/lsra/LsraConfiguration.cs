namespace RyuJitSharp;

internal static class LsraConfiguration
{
    internal static bool VarTypeNeedsPartialCalleeSave(var_types type)
    {
        if (!Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE)
        {
            return false;
        }

        assert(type is not TYP_STRUCT);
        if (Configuration.TARGET_AMD64)
        {
            return type is TYP_SIMD32 or TYP_SIMD64;
        }
        if (Configuration.TARGET_ARM64)
        {
            return type is TYP_SIMD16 or TYP_SIMD12;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Partial SIMD callee saves are not supported on this target.");
    }
}
