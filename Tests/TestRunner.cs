using System;
using System.Linq;
using System.Reflection;

namespace ProjectPluto.Tests
{
    public sealed class AssertionFailed : Exception
    {
        public AssertionFailed(string message) : base(message) { }
    }

    public static class Assert
    {
        public static void Equal(double expected, double actual, double tol, string name)
        {
            if (double.IsNaN(actual) || Math.Abs(expected - actual) > tol)
                throw new AssertionFailed($"{name}: expected {expected} ± {tol}, got {actual}");
        }

        public static void True(bool condition, string name)
        {
            if (!condition)
                throw new AssertionFailed($"{name}: expected true");
        }
    }

    public static class TestRunner
    {
        public static int Main()
        {
            int passed = 0, failed = 0;
            var tests = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.Name.EndsWith("Tests"))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0)
                .OrderBy(m => m.DeclaringType.Name).ThenBy(m => m.Name);
            foreach (MethodInfo test in tests)
            {
                try
                {
                    test.Invoke(null, null);
                    passed++;
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine($"FAIL {test.DeclaringType.Name}.{test.Name}: {e.InnerException.Message}");
                }
            }
            Console.WriteLine($"{passed} passed, {failed} failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
