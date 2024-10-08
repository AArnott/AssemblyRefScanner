// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft. All rights reserved.
#nullable disable

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
#pragma warning disable CS0169,CS0067 // unused fields and events
#pragma warning disable SA1136 // Enum values should be on separate lines
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1202 // Elements should be ordered by access
#pragma warning disable SA1203 // Constants should appear before fields
#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1314 // Type parameter names should begin with T
#pragma warning disable SA1400 // Access modifier should be declared
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1502 // Element should not be on a single line
#pragma warning disable SA1649 // File name should match first type name

using System.Drawing;

// This file contains a variety of API shapes to test the DocID creation code.
// Changes to this file require updates to the expected DocIDs in the tests.
namespace DocIdSamples;

enum ColorA { Red, Blue, Green }

public interface IProcess
{
}

public struct ValueType
{
    private int total;

    public void M(int i)
    {
        Widget p = new();
        p.AnEvent += this.P_AnEvent;
    }

    private void P_AnEvent(int i) => throw new NotImplementedException();
}

public class Widget : IProcess
{
    // ctors
    static Widget() { }

    public Widget() { }

    public Widget(string s) { }

    ~Widget() { }

    // operators
    public static Widget operator +(Widget x1, Widget x2) => null;

    public static explicit operator int(Widget x) => 0;

    public static implicit operator long(Widget x) => 0;

    // events
    public event Del AnEvent;

    // fields
    private string message;
    private static ColorA defaultColor;
    private const double PI = 3.14159;
    protected readonly double monthlyAverage;
    private long[] array1;
    private Widget[,] array2;
    private unsafe int* pCount;
    private unsafe float** ppValues;

    // methods
    public static void M0() { }

    public void M1(char c, out float f, ref ValueType v, in int i) => f = 0;

    public void M2(short[] x1, int[,] x2, long[][] x3) { }

    public void M3(long[][] x3, Widget[][,,] x4) { }

    public unsafe void M4(char* pc, Color** pf) { }

    public unsafe void M5(void* pv, double*[][,] pd) { }

    public void M6(int i, params object[] args) { }

    public void M7(ReadOnlySpan<char> x) { }

    // properties and indexes
    public int Width { get => 0; set { } }

    public int this[int i] { get => 0; set { } }

    public int this[string s, int i] { get => 0; set { } }

    // nested types
    public class NestedClass
    {
        public void M(int i) { }
    }

    public interface IMenuItem { }

    public delegate void Del(int i);

    public enum Direction { North, South, East, West }
}

public class MyList<T>
{
    public void Test(T t) { }

    class Helper<U, V> { }
}

public class UseList
{
    public void Process(MyList<int> list) { }

    public MyList<T> GetValues<T>(T value) => null;
}
