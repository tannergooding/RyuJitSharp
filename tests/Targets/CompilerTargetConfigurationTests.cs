using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompilerTargetConfigurationTests
{
    [Test]
    public static void LongConstantFactoryUsesTheTargetWidthRepresentation()
    {
        WithCompiler(compiler =>
        {
            const long value = 0x1234567887654321L;
            var constant = compiler.gtNewLconNode(value);

            Assert.That(constant.Type, Is.EqualTo(TYP_LONG));
            Assert.That(constant.LngValue, Is.EqualTo(value));

            if (Configuration.TARGET_64BIT)
            {
                Assert.That(constant, Is.TypeOf<GenTreeIntCon>());
            }
            else
            {
                Assert.That(constant, Is.TypeOf<GenTreeLngCon>());
            }
        });
    }

    [Test]
    public static void NullCheckTypeUsesTheTargetArchitecture()
    {
        WithCompiler(compiler =>
        {
            var type = compiler.gtTypeForNullCheck(compiler.gtNewLconNode(0));

            Assert.That(type, Is.EqualTo(Configuration.TARGET_XARCH ? TYP_INT : TYP_LONG));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif

        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
