// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

public class Test
{
    public const int HaystackSize = 4096,
        NeedleLocation = 1024;

    public static int ExitCode = 0;

    public static int Main(string[] args)
    {
        Console.WriteLine("Starting...");
        IndexOfAny_LastIndexOfAny_AlgComplexity_Chars();
        Console.WriteLine($"Exiting with code {ExitCode}");
        return ExitCode;
    }

    public static void IndexOfAny_LastIndexOfAny_AlgComplexity_Bytes()
        => RunIndexOfAnyLastIndexOfAnyAlgComplexityTest<byte>();

    public static void IndexOfAny_LastIndexOfAny_AlgComplexity_Chars()
        => RunIndexOfAnyLastIndexOfAnyAlgComplexityTest<char>();

    public static void IndexOfAny_LastIndexOfAny_AlgComplexity_Ints()
        => RunIndexOfAnyLastIndexOfAnyAlgComplexityTest<int>();

    private static void RunIndexOfAnyLastIndexOfAnyAlgComplexityTest<T>() where T : unmanaged, IEquatable<T>
    {
        T[] needles = GetIndexOfAnyNeedlesForAlgComplexityTest<T>().ToArray();
        RunIndexOfAnyAlgComplexityTest<T>(needles);
        RunLastIndexOfAnyAlgComplexityTest<T>(needles);
    }

    private static void AssertEqual (int expected, int actual, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) {
        if (expected != actual) {
            Console.WriteLine($"Expected value {expected} but got {actual}. At {filePath}:{lineNumber}");
            ExitCode = 1;
        }
    }

    private static void RunIndexOfAnyAlgComplexityTest<T>(T[] needle) where T : unmanaged, IEquatable<T>
    {
        // For the following paragraphs, let:
        //   n := length of haystack
        //   i := index of first occurrence of any needle within haystack
        //   l := length of needle array
        //
        // This test ensures that the complexity of IndexOfAny is O(i * l) rather than O(n * l),
        // or just O(n * l) if no needle is found. The reason for this is that it's common for
        // callers to invoke IndexOfAny immediately before slicing, and when this is called in
        // a loop, we want the entire loop to be bounded by O(n * l) rather than O(n^2 * l).
        //
        // We test this by utilizing the BoundedMemory infrastructure to allocate a poison page
        // after the scratch buffer, then we intentionally use MemoryMarshal to manipulate the
        // scratch buffer so that it extends into the poison page. If the runtime skips past the
        // first occurrence of the needle and attempts to read all the way to the end of the span,
        // this will manifest as an AV within this unit test.

        var boundedMem = new Memory<T>(new T[HaystackSize]);
        Span<T> span = boundedMem.Span;
        span.Clear();

        // span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length + PoisonRegionSize);

        for (int i = 0; i < needle.Length; i++)
        {
            span[NeedleLocation] = needle[i];
            AssertEqual(NeedleLocation, MemoryExtensions.IndexOfAny(span, needle));
            AssertEqual(NeedleLocation, MemoryExtensions.IndexOfAny((ReadOnlySpan<T>)span, needle));
        }
    }

    private static void RunLastIndexOfAnyAlgComplexityTest<T>(T[] needle) where T : unmanaged, IEquatable<T>
    {
        // Similar to RunIndexOfAnyAlgComplexityTest (see comments there), but we run backward
        // since we're testing LastIndexOfAny.

        var boundedMem = new Memory<T>(new T[HaystackSize]);
        Span<T> span = boundedMem.Span;
        span.Clear();

        // span = MemoryMarshal.CreateSpan(ref Unsafe.Subtract(ref MemoryMarshal.GetReference(span), PoisonRegionSize), span.Length + PoisonRegionSize);

        for (int i = 0; i < needle.Length; i++)
        {
            span[^NeedleLocation] = needle[i];
            AssertEqual(span.Length - NeedleLocation, MemoryExtensions.LastIndexOfAny(span, needle));
            AssertEqual(span.Length - NeedleLocation, MemoryExtensions.LastIndexOfAny((ReadOnlySpan<T>)span, needle));
        }
    }

    // returns [ 'a', 'b', 'c', ... ], or the equivalent in bytes, ints, etc.
    private static IEnumerable<T> GetIndexOfAnyNeedlesForAlgComplexityTest<T>() where T : unmanaged
    {
        for (int i = 0; i < 26; i++)
        {
            yield return (T)Convert.ChangeType('a' + i, typeof(T), CultureInfo.InvariantCulture);
        }
    }
}
