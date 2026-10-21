using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

static class TestRunner
{
    static int Main()
    {
        int passed = 0, failed = 0;
        var tests = Assembly.GetExecutingAssembly().GetTypes()
            .SelectMany(t => t.GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0));
        foreach (var method in tests)
        {
            string name = method.DeclaringType.Name + "." + method.Name;
            try
            {
                method.Invoke(Activator.CreateInstance(method.DeclaringType), null);
                passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (TargetInvocationException e)
            {
                failed++;
                Console.WriteLine("FAIL " + name + ": " + e.InnerException.Message);
            }
        }
        Console.WriteLine(string.Format("{0} passed, {1} failed", passed, failed));
        return failed == 0 ? 0 : 1;
    }
}
