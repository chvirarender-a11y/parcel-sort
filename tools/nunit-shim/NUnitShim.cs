// Just enough of NUnit to compile and run the EditMode tests outside Unity (see tools/run-tests.sh).
// Inside Unity the real NUnit from the Test Framework package is used instead.
using System;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public class TestAttribute : Attribute { }

    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!Equals(expected, actual))
                throw new AssertionException(string.Format("Expected {0} but was {1}. {2}", expected, actual, message));
        }
        public static void IsTrue(bool condition, string message = null)
        {
            if (!condition) throw new AssertionException("Expected true. " + message);
        }
        public static void IsFalse(bool condition, string message = null)
        {
            if (condition) throw new AssertionException("Expected false. " + message);
        }
        public static void IsNull(object value, string message = null)
        {
            if (value != null) throw new AssertionException("Expected null. " + message);
        }
        public static void IsNotNull(object value, string message = null)
        {
            if (value == null) throw new AssertionException("Expected not null. " + message);
        }
    }
}
