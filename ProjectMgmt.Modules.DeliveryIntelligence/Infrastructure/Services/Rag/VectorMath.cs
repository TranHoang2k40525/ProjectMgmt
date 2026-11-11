using System.Numerics;
using System.Runtime.InteropServices;

namespace DeliveryIntelligence.Infrastructure.Services.Rag;

/// <summary>Vector lưu dạng float32 đã chuẩn hóa: tích vô hướng = độ tương đồng cosine.</summary>
public static class VectorMath
{
    public static float[] Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(Dot(vector, vector));
        if (norm == 0 || float.IsNaN(norm)) throw new ArgumentException("Vector rỗng hoặc toàn số 0.", nameof(vector));
        var result = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++) result[i] = vector[i] / norm;
        return result;
    }

    public static byte[] ToBytes(float[] vector) => MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();

    public static ReadOnlySpan<float> FromBytes(byte[] bytes) => MemoryMarshal.Cast<byte, float>(bytes);

    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Hai vector khác số chiều.");
        var sum = Vector<float>.Zero;
        var i = 0;
        for (; i <= a.Length - Vector<float>.Count; i += Vector<float>.Count)
        {
            sum += new Vector<float>(a[i..]) * new Vector<float>(b[i..]);
        }
        var result = Vector.Sum(sum);
        for (; i < a.Length; i++) result += a[i] * b[i];
        return result;
    }
}
