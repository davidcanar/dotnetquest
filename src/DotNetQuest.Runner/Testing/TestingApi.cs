// xUnit-style testing API for learner-written tests.
// Mirrors the familiar [Fact]/[Theory]/[InlineData]/Assert surface so the
// Testing world exercises read exactly like real xUnit code — without adding
// an xunit package dependency to the runner assembly.

namespace DotNetQuest.Runner.Testing;

/// <summary>Marks a parameterless method as a test.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FactAttribute : Attribute
{
}

/// <summary>Marks a parameterized method as a test executed once per data row.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TheoryAttribute : Attribute
{
}

/// <summary>Supplies one data row for a [Theory] test.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class InlineDataAttribute : Attribute
{
    public object?[] DataValues { get; }

    public InlineDataAttribute(params object?[] dataValues) => DataValues = dataValues;
}

/// <summary>Assertions mirroring the xUnit surface.</summary>
public static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected!, actual!))
            throw new TestFailureException($"Assert.Equal() Failure: Expected: {Format(expected)}. Actual: {Format(actual)}.");
    }

    public static void NotEqual<T>(T expected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(expected!, actual!))
            throw new TestFailureException($"Assert.NotEqual() Failure: Both values are {Format(actual)}.");
    }

    public static void True(bool condition)
    {
        if (!condition)
            throw new TestFailureException("Assert.True() Failure: condition was false.");
    }

    public static void False(bool condition)
    {
        if (condition)
            throw new TestFailureException("Assert.False() Failure: condition was true.");
    }

    private static string Format(object? value) => value?.ToString() ?? "null";
}

/// <summary>Thrown by failed assertions; the runner reports it as a test failure.</summary>
public sealed class TestFailureException : Exception
{
    public TestFailureException(string message) : base(message) { }
}
