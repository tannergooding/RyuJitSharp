// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class IntegerArgumentReverseMappingTests
{
    private static readonly CorInfoCallConvExtension[] s_callConventions = [
        CorInfoCallConvExtension.Managed,
        CorInfoCallConvExtension.C,
        CorInfoCallConvExtension.Stdcall,
        CorInfoCallConvExtension.Thiscall,
        CorInfoCallConvExtension.Fastcall,
        CorInfoCallConvExtension.CMemberFunction,
        CorInfoCallConvExtension.StdcallMemberFunction,
        CorInfoCallConvExtension.FastcallMemberFunction,
        CorInfoCallConvExtension.Swift,
    ];

    [SetUp]
    public static void RequireFixedRegisterSet()
    {
        Assume.That(Configuration.HAS_FIXED_REGISTER_SET, "This fixture requires fixed argument registers.");
    }

    [Test]
    public static void IntegerArgumentRegistersMapBackToTheirTableIndices()
    {
        Assume.That(Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64);

        WithoutNativeAssertions(() => {
            foreach (var callConv in s_callConventions)
            {
                for (var index = 0; index < IntArgRegs.Length; index++)
                {
                    Assert.That(genMapIntRegNumToRegArgNum(IntArgRegs[index], callConv), Is.EqualTo(index));
                }
            }
        });
    }

    [TestCase(CorInfoCallConvExtension.Managed, false)]
    [TestCase(CorInfoCallConvExtension.C, false)]
    [TestCase(CorInfoCallConvExtension.Stdcall, false)]
    [TestCase(CorInfoCallConvExtension.Thiscall, true)]
    [TestCase(CorInfoCallConvExtension.Fastcall, false)]
    [TestCase(CorInfoCallConvExtension.CMemberFunction, true)]
    [TestCase(CorInfoCallConvExtension.StdcallMemberFunction, true)]
    [TestCase(CorInfoCallConvExtension.FastcallMemberFunction, true)]
    [TestCase(CorInfoCallConvExtension.Swift, false)]
    public static void FixedBufferRegisterMapsToItsNativeArgumentIndex(
        CorInfoCallConvExtension callConv, bool instanceConvention)
    {
        Assume.That(Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64);

        bool expectedFixed;
        uint expectedIndex = 0;
        var expectedRegister = REG_NA;
        if (Configuration.TARGET_ARM64)
        {
            expectedFixed = !TargetOS.IsWindows || !instanceConvention;
            expectedIndex = 8;
            expectedRegister = REG_ARM64_R8;
        }
        else if (Configuration.SWIFT_SUPPORT)
        {
            expectedFixed = callConv == CorInfoCallConvExtension.Swift;
            expectedIndex = Configuration.UNIX_AMD64_ABI ? 6u : 4u;
            expectedRegister = REG_X64_RAX;
        }
        else
        {
            expectedFixed = false;
        }

        WithoutNativeAssertions(() => {
            Assert.That(hasFixedRetBuffReg(callConv), Is.EqualTo(expectedFixed));
            if (expectedFixed)
            {
                Assert.That(unchecked((uint)theFixedRetBuffArgNum(callConv)), Is.EqualTo(expectedIndex));
                Assert.That(theFixedRetBuffReg(callConv), Is.EqualTo(expectedRegister));
                Assert.That(
                    genMapIntRegNumToRegArgNum(expectedRegister, callConv),
                    Is.EqualTo(unchecked((int)expectedIndex)));
            }

            Assert.That(genMapIntRegNumToRegArgNum(IntArgRegs[0], callConv), Is.Zero);
        });
    }

    [Test]
    public static void InvalidIntegerRegisterReportsTheExistingAssertions()
    {
        Assume.That(TestConfiguration.IsDebug);
        Assume.That(Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64);

        WithAssertionRecorder(() => _ = genMapIntRegNumToRegArgNum(REG_FP_FIRST, CorInfoCallConvExtension.Managed));

        string[] expected = [
            "(regMaskTP.CreateFromRegNum(regNum, regNum.SingleTypeMask) & fullIntArgRegMask(callConv)) != RBM_NONE",
            "invalid register arg register",
        ];
        Assert.That(s_assertions, Is.EqualTo(expected));
    }

    private static void WithoutNativeAssertions(Action action)
    {
        if (TestConfiguration.IsDebug)
        {
            WithAssertionRecorder(action);
            Assert.That(s_assertions, Is.Empty);
        }
        else
        {
            action();
        }
    }

    private static readonly List<string?> s_assertions = [];

    private static void WithAssertionRecorder(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();
        action();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));

        return 0;
    }
}
