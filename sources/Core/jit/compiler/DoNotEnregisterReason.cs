// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Reasons why we can't enregister a local.</summary>
public enum DoNotEnregisterReason
{
    None,

    /// <summary>the address of this local is exposed.</summary>
    AddrExposed,     

    /// <summary>struct enregistration is disabled.</summary>
    DontEnregStructs,

    /// <summary>the struct size does not much any register size, usually the struct size is too big.</summary>
    NotRegSizeStruct,

    /// <summary>the local is accessed with LCL_FLD, note we can do it not only for struct locals.</summary>
    LocalField,
    
    VMNeedsStackAddr,

    /// <summary>the local is alive in and out of exception handler and not single def.</summary>
    LiveInOutOfHandler,

    /// <summary>Is read or written via a block operation.</summary>
    BlockOp,

    /// <summary>Is a struct passed as an argument in a way that requires a stack location.</summary>
    IsStructArg,

    /// <summary>It is a field of a dependently promoted struct</summary>
    DepField,

    /// <summary>opts.compFlags &amp; CLFLG_REGVAR is not set</summary>
    NoRegVars,

#if !TARGET_64BIT
    /// <summary>It is a decomposed field of a long parameter.</summary>
    LongParamField
#endif

    PinningRef,

    /// <summary>the local is accessed with LCL_ADDR_VAR/FLD.</summary>
    LclAddrNode,

    CastTakesAddr,

    /// <summary>the local is used as STORE_BLK source.</summary>
    StoreBlkSrc,          

    /// <summary>the local is passed using LCL_FLD as another type.</summary>
    SwizzleArg,           

    /// <summary>the struct is returned and it promoted or there is a cast.</summary>
    BlockOpRet,           

    /// <summary>the local is used to do SP check on return from function</summary>
    ReturnSpCheck,        

    /// <summary>the local is used to do SP check on every call</summary>
    CallSpCheck,          

    /// <summary>a promoted struct was used by a simd/HWI node; it must be dependently promoted</summary>
    simdUserForcesDep,

    /// <summary>the argument is a hidden return buffer passed to a method.</summary>
    HiddenBufferStructArg,

    WasmGCVisibility,
}
