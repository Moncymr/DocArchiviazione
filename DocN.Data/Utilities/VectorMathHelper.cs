namespace DocN.Data.Utilities;

/// <summary>
/// Centralized utility class for vector mathematics operations.
/// Eliminates code duplication across multiple services.
/// </summary>
public static class VectorMathHelper
{
    /// <summary>
    /// Calculates the cosine similarity between two vectors.
    /// Returns a value between -1 and 1, where 1 means identical direction.
    /// </summary>
    /// <param name="vector1">First vector</param>
    /// <param name="vector2">Second vector</param>
    /// <returns>Cosine similarity score between -1 and 1</returns>
    /// <exception cref="ArgumentNullException">Thrown when either vector is null</exception>
    /// <exception cref="ArgumentException">Thrown when vectors have different lengths or are empty</exception>
    public static double CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1 == null)
            throw new ArgumentNullException(nameof(vector1));
        if (vector2 == null)
            throw new ArgumentNullException(nameof(vector2));
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same length");
        if (vector1.Length == 0)
            throw new ArgumentException("Vectors cannot be empty");

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (Math.Sqrt(magnitude1) * Math.Sqrt(magnitude2));
    }

    /// <summary>
    /// Calculates the Euclidean distance between two vectors.
    /// Smaller values indicate more similar vectors.
    /// </summary>
    /// <param name="vector1">First vector</param>
    /// <param name="vector2">Second vector</param>
    /// <returns>Euclidean distance</returns>
    /// <exception cref="ArgumentNullException">Thrown when either vector is null</exception>
    /// <exception cref="ArgumentException">Thrown when vectors have different lengths</exception>
    public static double EuclideanDistance(float[] vector1, float[] vector2)
    {
        if (vector1 == null)
            throw new ArgumentNullException(nameof(vector1));
        if (vector2 == null)
            throw new ArgumentNullException(nameof(vector2));
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same length");

        double sum = 0;
        for (int i = 0; i < vector1.Length; i++)
        {
            double diff = vector1[i] - vector2[i];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }

    /// <summary>
    /// Calculates the dot product of two vectors.
    /// </summary>
    /// <param name="vector1">First vector</param>
    /// <param name="vector2">Second vector</param>
    /// <returns>Dot product</returns>
    /// <exception cref="ArgumentNullException">Thrown when either vector is null</exception>
    /// <exception cref="ArgumentException">Thrown when vectors have different lengths</exception>
    public static double DotProduct(float[] vector1, float[] vector2)
    {
        if (vector1 == null)
            throw new ArgumentNullException(nameof(vector1));
        if (vector2 == null)
            throw new ArgumentNullException(nameof(vector2));
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same length");

        double dotProduct = 0;
        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
        }

        return dotProduct;
    }

    /// <summary>
    /// Normalizes a vector to unit length.
    /// </summary>
    /// <param name="vector">Vector to normalize</param>
    /// <returns>Normalized vector</returns>
    /// <exception cref="ArgumentNullException">Thrown when vector is null</exception>
    public static float[] Normalize(float[] vector)
    {
        if (vector == null)
            throw new ArgumentNullException(nameof(vector));

        double magnitude = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }

        magnitude = Math.Sqrt(magnitude);
        
        if (magnitude == 0)
            return vector;

        var normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = (float)(vector[i] / magnitude);
        }

        return normalized;
    }
}
