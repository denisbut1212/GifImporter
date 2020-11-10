using System;
using System.Collections;

/// <summary>
/// Extension methods class
/// </summary>
public static class UniGifExtension
{
    /// <summary>
    /// Convert BitArray to int (Specifies the start index and bit length)
    /// </summary>
    /// <param name="array"></param>
    /// <param name="startIndex">Start index</param>
    /// <param name="bitLength">Bit length</param>
    /// <returns>Converted int</returns>
    public static int GetNumeral(this BitArray array, int startIndex, int bitLength)
    {
        var newArray = new BitArray(bitLength);

        for (int i = 0; i < bitLength; i++) 
            newArray[i] = array.Length > startIndex + i && array.Get(startIndex + i);

        return newArray.ToNumeral();
    }

    /// <summary>
    /// Convert BitArray to int
    /// </summary>
    /// <returns>Converted int</returns>
    public static int ToNumeral(this BitArray array)
    {
        if (array == null)
            throw new ArgumentNullException();

        if (array.Length > 32)
            throw new ArgumentOutOfRangeException(nameof(array));

        var result = new int[1];
        array.CopyTo(result, 0);
        return result[0];
    }
}