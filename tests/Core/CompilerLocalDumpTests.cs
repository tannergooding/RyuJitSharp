// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if DEBUG
[NonParallelizable]
internal static unsafe class CompilerLocalDumpTests
{
    private static int s_classNameRequests;

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ClassMetadataDiagnosticsQueryNamesOnlyWhenVerbose(bool update, bool verbose)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.runWithSPMIErrorTrap = &UnavailableClassName;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var classHandle = (CORINFO_CLASS_STRUCT_*)0x1000;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_REF,
                lvClassHnd = update ? classHandle : null,
                lvSingleDef = true,
            };
            compiler.verbose = verbose;
            s_classNameRequests = 0;

            var output = CodeGenLifeTransitionTests.Capture(() =>
            {
                if (update)
                {
                    compiler.lvaUpdateClass(0, classHandle, isExact: true);
                }
                else
                {
                    compiler.lvaSetClass(0, classHandle, isExact: true);
                }
            });

            Assert.That(s_classNameRequests, Is.EqualTo(verbose ? (update ? 2 : 1) : 0));
            Assert.That((nint)compiler.lvaTable[0].lvClassHnd, Is.EqualTo((nint)classHandle));
            Assert.That(compiler.lvaTable[0].lvClassIsExact, Is.True);
            var pointer = FMT_DSP_PTR(classHandle);
            var expected = update
                ? $"{Environment.NewLine}lvaUpdateClass: Updating class for V00 from ({pointer}) <unknown class> to ({pointer}) <unknown class> [exact]{Environment.NewLine}"
                : $"{Environment.NewLine}lvaSetClass: setting class for V00 to ({pointer}) <unknown class>  [exact]{Environment.NewLine}";
            Assert.That(output, Is.EqualTo(verbose ? expected : ""));
        });
    }

    [TestCase(false, TYP_STRUCT, "<unknown class>")]
    [TestCase(true, TYP_STRUCT, "<unknown class>")]
    [TestCase(false, TYP_INT, "int")]
    [TestCase(true, TYP_INT, "int")]
    public static void ArrayAddressElementNamesUseNativeSpacing(bool wrapped, var_types elementType, string name)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.runWithSPMIErrorTrap = &UnavailableClassName;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var elementClass = elementType is TYP_STRUCT ? (CORINFO_CLASS_STRUCT_*)0x1000 : null;
            var address = new GenTreeIndexAddr(compiler.gtNewNull(), compiler.gtNewIconNode(TYP_INT, 0),
                elementType, elementClass, 4, 8, 16, false);
            GenTree tree = wrapped ? new GenTreeArrAddr(address, elementType, elementClass, 16) : address;

            var output = CodeGenLifeTransitionTests.Capture(() => compiler.gtDispTree(tree, topOnly: true));

            Assert.That(output, Does.Contain($"byref {name}[]"));
            Assert.That(output, Does.Not.Contain($"byref  {name}[]"));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte UnavailableClassName(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        s_classNameRequests++;
        return 0;
    }

    [TestCase(0, "struct ( 0)")]
    [TestCase(16, "struct (16)")]
    public static void StructStackHomeSizeUsesSpacePaddedNativeWidth(int size, string expected)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            compiler.lvaTable[1] = new LclVarDsc
            {
                Type = TYP_STRUCT,
                Layout = new ClassLayout(size),
                lvOnFrame = true,
                RegNum = REG_STK,
            };
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;

            var output = CodeGenLifeTransitionTests.Capture(
                () => compiler.lvaDumpEntry(1, Compiler.FINAL_FRAME_LAYOUT, 0));

            Assert.That(output, Does.Contain(expected));
        });
    }
}
#endif
