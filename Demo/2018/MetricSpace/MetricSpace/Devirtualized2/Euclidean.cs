﻿using System;
using System.Runtime.CompilerServices;

namespace MetricSpace.Devirtualized2
{
    interface IArithmetic<T>
    {
        T Zero { get; }
        T Add(T a, T b);
        T Subtract(T a, T b);
        T Multiply(T a, T b);
    }

    struct FloatArithmetic : IArithmetic<float>
    {
        public float Zero => 0;
        public float Add(float a, float b) => a + b;
        public float Multiply(float a, float b) => a - b;
        public float Subtract(float a, float b) => a * b;
    }

    // 配列自体用。これは大して意味は持ってない。誤用防止程度
    public interface IFixedArray<T> { }

    // 値型ジェネリック トリック用
    public interface IFixedArrayAccessor<T, TArray>
        where TArray : struct, IFixedArray<T>
    {
        TArray New();
        ref T At(ref TArray array, int i);
        int Length { get; }
    }

    public struct Fixed1<T> : IFixedArrayAccessor<T, Fixed1<T>.Array>
    {
        public struct Array : IFixedArray<T>
        {
            public T Item1;
            public Array(T item1) => Item1 = item1;
            public static implicit operator Array(T value) => new Array(value);
        }

        public Array New() => default;
        public int Length => 1;
        public unsafe Span<T> AsSpan(ref Array array) => new Span<T>(Unsafe.AsPointer(ref array.Item1), 1);
        public ref T At(ref Array array, int i) => ref AsSpan(ref array)[i];
    }

    public struct Fixed2<T> : IFixedArrayAccessor<T, Fixed2<T>.Array>
    {
        public struct Array : IFixedArray<T>
        {
            public T Item1; public T Item2;
            public Array(T item1, T item2) => (Item1, Item2) = (item1, item2);
            public static implicit operator Array((T, T) value) => new Array(value.Item1, value.Item2);
        }

        public Array New() => default;
        public int Length => 2;
        public unsafe Span<T> AsSpan(ref Array array) => new Span<T>(Unsafe.AsPointer(ref array.Item1), 2);
        public ref T At(ref Array array, int i) => ref AsSpan(ref array)[i];
        // 範囲チェックをさぼる(危険でいい)なら以下の書き方でも OK
        //public ref T At(ref Array array, int i) => ref Unsafe.Add(ref array.Item1, i);
    }

    public struct Fixed3<T> : IFixedArrayAccessor<T, Fixed3<T>.Array>
    {
        public struct Array : IFixedArray<T>
        {
            public T Item1; public T Item2; public T Item3;
            public Array(T item1, T item2, T item3) => (Item1, Item2, Item3) = (item1, item2, item3);
            public static implicit operator Array((T, T, T) value) => new Array(value.Item1, value.Item2, value.Item3);
        }

        public Array New() => default;
        public int Length => 3;
        public unsafe Span<T> AsSpan(ref Array array) => new Span<T>(Unsafe.AsPointer(ref array.Item1), 3);
        public ref T At(ref Array array, int i) => ref AsSpan(ref array)[i];
    }

    public struct Fixed4<T> : IFixedArrayAccessor<T, Fixed4<T>.Array>
    {
        public struct Array : IFixedArray<T>
        {
            public T Item1; public T Item2; public T Item3; public T Item4;
            public Array(T item1, T item2, T item3, T item4) => (Item1, Item2, Item3, Item4) = (item1, item2, item3, item4);
            public static implicit operator Array((T, T, T, T) value) => new Array(value.Item1, value.Item2, value.Item3, value.Item4);
        }

        public Array New() => default;
        public int Length => 4;
        public unsafe Span<T> AsSpan(ref Array array) => new Span<T>(Unsafe.AsPointer(ref array.Item1), 4);
        public ref T At(ref Array array, int i) => ref AsSpan(ref array)[i];
    }

    // 以下、必要なだけ FixedN を用意

    class Euclidean<T, TArithmetic, TArray, TArrayAccessor>
        where TArithmetic : struct, IArithmetic<T>
        where TArray : struct, IFixedArray<T>
        where TArrayAccessor : struct, IFixedArrayAccessor<T, TArray>
    {
        public static T DistanceSquared(TArray a, TArray b)
        {
            var arith = default(TArithmetic);
            var accessor = default(TArrayAccessor);
            var d = arith.Zero;
            for (int i = 0; i < accessor.Length; i++)
            {
                var dif = arith.Subtract(accessor.At(ref b, i), accessor.At(ref a, i));
                var sq = arith.Multiply(dif, dif);
                d = arith.Add(d, sq);
            }
            return d;
        }
    }

    class Program
    {
        static void Main()
        {
            // これも、Fixed2<float> を使う時点で残りの型引数確定なんだけど、残念ながら型推論はされない
            // 常にこの4つの型引数が必要
            Euclidean<float, FloatArithmetic, Fixed2<float>.Array, Fixed2<float>>.DistanceSquared((1, 2), (3, 4));
        }
    }
}
