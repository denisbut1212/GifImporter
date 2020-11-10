using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static partial class UniGif
{
    /// <summary>
    /// Decode to textures from GIF data
    /// </summary>
    /// <param name="gifData">GIF data</param>
    /// <param name="callback">Callback method(param is GIF texture list)</param>
    /// <param name="filterMode">Textures filter mode</param>
    /// <param name="wrapMode">Textures wrap mode</param>
    /// <returns>IEnumerator</returns>
    private static IEnumerator DecodeTextureCoroutine(GifData gifData, Action<List<GifTexture>> callback,
        FilterMode filterMode, TextureWrapMode wrapMode)
    {
        if (gifData.m_imageBlockList == null || gifData.m_imageBlockList.Count < 1)
            yield break;

        var gifTexList = new List<GifTexture>(gifData.m_imageBlockList.Count);
        var disposalMethodList = new List<ushort>(gifData.m_imageBlockList.Count);

        var imgIndex = 0;

        for (var i = 0; i < gifData.m_imageBlockList.Count; i++)
        {
            var decodedData = GetDecodedData(gifData.m_imageBlockList[i]);

            var graphicCtrlEx = GetGraphicCtrlExt(gifData, imgIndex);

            var transparentIndex = GetTransparentIndex(graphicCtrlEx);

            disposalMethodList.Add(GetDisposalMethod(graphicCtrlEx));

            Color32 bgColor;
            var colorTable =
                GetColorTableAndSetBgColor(gifData, gifData.m_imageBlockList[i], transparentIndex, out bgColor);

            yield return 0;

            bool filledTexture;
            var tex = CreateTexture2D(gifData, gifTexList, imgIndex, disposalMethodList, bgColor, filterMode,
                wrapMode, out filledTexture);

            yield return 0;

            // Set pixel data
            var dataIndex = 0;
            // Reverse set pixels. because GIF data starts from the top left.
            for (var y = tex.height - 1; y >= 0; y--)
                SetTexturePixelRow(tex, y, gifData.m_imageBlockList[i], decodedData, ref dataIndex, colorTable, bgColor,
                    transparentIndex, filledTexture);

            tex.Apply();

            yield return 0;

            var delaySec = GetDelaySec(graphicCtrlEx);

            // Add to GIF texture list
            gifTexList.Add(new GifTexture(tex, delaySec));

            imgIndex++;
        }

        callback?.Invoke(gifTexList);
    }

    #region Call from DecodeTexture methods

    /// <summary>
    /// Get decoded image data from ImageBlock
    /// </summary>
    private static byte[] GetDecodedData(ImageBlock imgBlock)
    {
        // Combine LZW compressed data
        var lzwData = new List<byte>();
        for (var i = 0; i < imgBlock.m_imageDataList.Count; i++)
        for (var k = 0; k < imgBlock.m_imageDataList[i].m_imageData.Length; k++)
            lzwData.Add(imgBlock.m_imageDataList[i].m_imageData[k]);

        // LZW decode
        var needDataSize = imgBlock.m_imageHeight * imgBlock.m_imageWidth;
        var decodedData = DecodeGifLzw(lzwData, imgBlock.m_lzwMinimumCodeSize, needDataSize);

        // Sort interlace GIF
        if (imgBlock.m_interlaceFlag)
            decodedData = SortInterlaceGifData(decodedData, imgBlock.m_imageWidth);

        return decodedData;
    }

    /// <summary>
    /// Get color table and set background color (local or global)
    /// </summary>
    private static List<byte[]> GetColorTableAndSetBgColor(GifData gifData, ImageBlock imgBlock, int transparentIndex,
        out Color32 bgColor)
    {
        List<byte[]> colorTable;
        if (imgBlock.m_localColorTableFlag)
            colorTable = imgBlock.m_localColorTable;
        else if (gifData.m_globalColorTableFlag)
            colorTable = gifData.m_globalColorTable;
        else
            colorTable = null;

        if (colorTable != null)
        {
            // Set background color from color table
            var bgRgb = colorTable[gifData.m_bgColorIndex];
            bgColor = new Color32(bgRgb[0], bgRgb[1], bgRgb[2],
                (byte) (transparentIndex == gifData.m_bgColorIndex ? 0 : 255));
        }
        else
        {
            bgColor = Color.black;
        }

        return colorTable;
    }

    /// <summary>
    /// Get GraphicControlExtension from GifData
    /// </summary>
    private static GraphicControlExtension? GetGraphicCtrlExt(GifData gifData, int imgBlockIndex)
    {
        if (gifData.m_graphicCtrlExList != null && gifData.m_graphicCtrlExList.Count > imgBlockIndex)
            return gifData.m_graphicCtrlExList[imgBlockIndex];

        return null;
    }

    /// <summary>
    /// Get transparent color index from GraphicControlExtension
    /// </summary>
    private static int GetTransparentIndex(GraphicControlExtension? graphicCtrlEx)
    {
        var transparentIndex = -1;
        if (graphicCtrlEx != null && graphicCtrlEx.Value.m_transparentColorFlag)
            transparentIndex = graphicCtrlEx.Value.m_transparentColorIndex;

        return transparentIndex;
    }

    /// <summary>
    /// Get delay seconds from GraphicControlExtension
    /// </summary>
    private static float GetDelaySec(GraphicControlExtension? graphicCtrlEx)
    {
        // Get delay sec from GraphicControlExtension
        var delaySec = graphicCtrlEx != null ? graphicCtrlEx.Value.m_delayTime / 100f : 1f / 60f;
        if (delaySec <= 0f)
            delaySec = 0.1f;

        return delaySec;
    }

    /// <summary>
    /// Get disposal method from GraphicControlExtension
    /// </summary>
    private static ushort GetDisposalMethod(GraphicControlExtension? graphicCtrlEx)
    {
        return graphicCtrlEx?.m_disposalMethod ?? (ushort) 2;
    }

    /// <summary>
    /// Create Texture2D object and initial settings
    /// </summary>
    private static Texture2D CreateTexture2D(GifData gifData, List<GifTexture> gifTexList, int imgIndex,
        List<ushort> disposalMethodList, Color32 bgColor, FilterMode filterMode, TextureWrapMode wrapMode,
        out bool filledTexture)
    {
        filledTexture = false;

        // Create texture
        var tex = new Texture2D(gifData.m_logicalScreenWidth, gifData.m_logicalScreenHeight, TextureFormat.ARGB32,
            false);
        tex.filterMode = filterMode;
        tex.wrapMode = wrapMode;

        // Check dispose
        var disposalMethod = imgIndex > 0 ? disposalMethodList[imgIndex - 1] : (ushort) 2;
        var useBeforeIndex = -1;
        if (disposalMethod == 1)
            // 1 (Do not dispose)
        {
            useBeforeIndex = imgIndex - 1;
        }
        else if (disposalMethod == 2)
        {
            // 2 (Restore to background color)
            filledTexture = true;
            var pix = new Color32[tex.width * tex.height];
            for (var i = 0; i < pix.Length; i++) pix[i] = bgColor;

            tex.SetPixels32(pix);
            tex.Apply();
        }
        else if (disposalMethod == 3)
        {
            // 3 (Restore to previous)
            for (var i = imgIndex - 1; i >= 0; i--)
                if (disposalMethodList[i] == 0 || disposalMethodList[i] == 1)
                {
                    useBeforeIndex = i;
                    break;
                }
        }

        if (useBeforeIndex >= 0)
        {
            filledTexture = true;
            var pix = gifTexList[useBeforeIndex].m_texture2d.GetPixels32();
            tex.SetPixels32(pix);
            tex.Apply();
        }

        return tex;
    }

    /// <summary>
    /// Set texture pixel row
    /// </summary>
    private static void SetTexturePixelRow(Texture2D tex, int y, ImageBlock imgBlock, byte[] decodedData,
        ref int dataIndex, List<byte[]> colorTable, Color32 bgColor, int transparentIndex, bool filledTexture)
    {
        // Row no (0~)
        var row = tex.height - 1 - y;

        for (var x = 0; x < tex.width; x++)
        {
            // Line no (0~)
            var line = x;

            // Out of image blocks
            if (row < imgBlock.m_imageTopPosition ||
                row >= imgBlock.m_imageTopPosition + imgBlock.m_imageHeight ||
                line < imgBlock.m_imageLeftPosition ||
                line >= imgBlock.m_imageLeftPosition + imgBlock.m_imageWidth)
            {
                // Get pixel color from bg color
                if (filledTexture == false) 
                    tex.SetPixel(x, y, bgColor);

                continue;
            }

            // Out of decoded data
            if (dataIndex >= decodedData.Length)
            {
                if (filledTexture == false)
                {
                    tex.SetPixel(x, y, bgColor);
                    if (dataIndex == decodedData.Length)
                        Debug.LogError("dataIndex exceeded the size of decodedData. dataIndex:" + dataIndex +
                                       " decodedData.Length:" + decodedData.Length + " y:" + y + " x:" + x);
                }

                dataIndex++;
                continue;
            }

            // Get pixel color from color table
            {
                var colorIndex = decodedData[dataIndex];
                if (colorTable == null || colorTable.Count <= colorIndex)
                {
                    if (filledTexture == false)
                    {
                        tex.SetPixel(x, y, bgColor);
                        if (colorTable == null)
                            Debug.LogError(
                                "colorIndex exceeded the size of colorTable. colorTable is null. colorIndex:" +
                                colorIndex);
                        else
                            Debug.LogError("colorIndex exceeded the size of colorTable. colorTable.Count:" +
                                           colorTable.Count + " colorIndex:" + colorIndex);
                    }

                    dataIndex++;
                    continue;
                }

                var rgb = colorTable[colorIndex];

                // Set alpha
                var alpha = transparentIndex >= 0 && transparentIndex == colorIndex ? (byte) 0 : (byte) 255;

                if (filledTexture == false || alpha != 0)
                {
                    // Set color
                    var col = new Color32(rgb[0], rgb[1], rgb[2], alpha);
                    tex.SetPixel(x, y, col);
                }
            }

            dataIndex++;
        }
    }

    #endregion

    #region Decode LZW & Sort interrace methods

    /// <summary>
    /// GIF LZW decode
    /// </summary>
    /// <param name="compData">LZW compressed data</param>
    /// <param name="lzwMinimumCodeSize">LZW minimum code size</param>
    /// <param name="needDataSize">Need decoded data size</param>
    /// <returns>Decoded data array</returns>
    private static byte[] DecodeGifLzw(List<byte> compData, int lzwMinimumCodeSize, int needDataSize)
    {
        var clearCode = 0;
        var finishCode = 0;

        // Initialize dictionary
        var dic = new Dictionary<int, string>();
        InitDictionary(dic, lzwMinimumCodeSize, out var lzwCodeSize, out clearCode, out finishCode);

        // Convert to bit array
        var compDataArr = compData.ToArray();
        var bitData = new BitArray(compDataArr);

        var output = new byte[needDataSize];
        var outputAddIndex = 0;

        string prevEntry = null;

        var dicInitFlag = false;

        var bitDataIndex = 0;

        // LZW decode loop
        while (bitDataIndex < bitData.Length)
        {
            if (dicInitFlag)
            {
                InitDictionary(dic, lzwMinimumCodeSize, out lzwCodeSize, out clearCode, out finishCode);
                dicInitFlag = false;
            }

            var key = bitData.GetNumeral(bitDataIndex, lzwCodeSize);

            string entry;

            if (key == clearCode)
            {
                // Clear (Initialize dictionary)
                dicInitFlag = true;
                bitDataIndex += lzwCodeSize;
                prevEntry = null;
                continue;
            }
            if (key == finishCode)
            {
                // Exit
                Debug.LogWarning("early stop code. bitDataIndex:" + bitDataIndex + " lzwCodeSize:" + lzwCodeSize +
                                 " key:" + key + " dic.Count:" + dic.Count);
                break;
            }
            if (dic.ContainsKey(key))
            {
                // Output from dictionary
                entry = dic[key];
            }
            else if (key >= dic.Count)
            {
                if (prevEntry != null)
                    // Output from estimation
                {
                    entry = prevEntry + prevEntry[0];
                }
                else
                {
                    Debug.LogWarning("It is strange that come here. bitDataIndex:" + bitDataIndex + " lzwCodeSize:" +
                                     lzwCodeSize + " key:" + key + " dic.Count:" + dic.Count);
                    bitDataIndex += lzwCodeSize;
                    continue;
                }
            }
            else
            {
                Debug.LogWarning("It is strange that come here. bitDataIndex:" + bitDataIndex + " lzwCodeSize:" +
                                 lzwCodeSize + " key:" + key + " dic.Count:" + dic.Count);
                bitDataIndex += lzwCodeSize;
                continue;
            }

            // Output
            // Take out 8 bits from the string.
            var temp = Encoding.Unicode.GetBytes(entry);
            for (var i = 0; i < temp.Length; i++)
                if (i % 2 == 0)
                {
                    output[outputAddIndex] = temp[i];
                    outputAddIndex++;
                }

            if (outputAddIndex >= needDataSize)
                // Exit
                break;

            if (prevEntry != null)
                // Add to dictionary
                dic.Add(dic.Count, prevEntry + entry[0]);

            prevEntry = entry;

            bitDataIndex += lzwCodeSize;

            switch (lzwCodeSize)
            {
                case 3 when dic.Count >= 8:
                    lzwCodeSize = 4;
                    break;
                case 4 when dic.Count >= 16:
                    lzwCodeSize = 5;
                    break;
                case 5 when dic.Count >= 32:
                    lzwCodeSize = 6;
                    break;
                case 6 when dic.Count >= 64:
                    lzwCodeSize = 7;
                    break;
                case 7 when dic.Count >= 128:
                    lzwCodeSize = 8;
                    break;
                case 8 when dic.Count >= 256:
                    lzwCodeSize = 9;
                    break;
                case 9 when dic.Count >= 512:
                    lzwCodeSize = 10;
                    break;
                case 10 when dic.Count >= 1024:
                    lzwCodeSize = 11;
                    break;
                case 11 when dic.Count >= 2048:
                    lzwCodeSize = 12;
                    break;
                case 12 when dic.Count >= 4096:
                {
                    var nextKey = bitData.GetNumeral(bitDataIndex, lzwCodeSize);
                    if (nextKey != clearCode) 
                        dicInitFlag = true;

                    break;
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Initialize dictionary
    /// </summary>
    /// <param name="dic">Dictionary</param>
    /// <param name="lzwMinimumCodeSize">LZW minimum code size</param>
    /// <param name="lzwCodeSize">out LZW code size</param>
    /// <param name="clearCode">out Clear code</param>
    /// <param name="finishCode">out Finish code</param>
    private static void InitDictionary(Dictionary<int, string> dic, int lzwMinimumCodeSize, out int lzwCodeSize,
        out int clearCode, out int finishCode)
    {
        var dicLength = (int) Math.Pow(2, lzwMinimumCodeSize);

        clearCode = dicLength;
        finishCode = clearCode + 1;

        dic.Clear();

        for (var i = 0; i < dicLength + 2; i++) dic.Add(i, ((char) i).ToString());

        lzwCodeSize = lzwMinimumCodeSize + 1;
    }

    /// <summary>
    /// Sort interlace GIF data
    /// </summary>
    /// <param name="decodedData">Decoded GIF data</param>
    /// <param name="xNum">Pixel number of horizontal row</param>
    /// <returns>Sorted data</returns>
    private static byte[] SortInterlaceGifData(byte[] decodedData, int xNum)
    {
        var rowNo = 0;
        var dataIndex = 0;
        var newArr = new byte[decodedData.Length];
        // Every 8th. row, starting with row 0.
        for (var i = 0; i < newArr.Length; i++)
        {
            if (rowNo % 8 == 0)
            {
                newArr[i] = decodedData[dataIndex];
                dataIndex++;
            }

            if (i != 0 && i % xNum == 0) 
                rowNo++;
        }

        rowNo = 0;
        // Every 8th. row, starting with row 4.
        for (var i = 0; i < newArr.Length; i++)
        {
            if (rowNo % 8 == 4)
            {
                newArr[i] = decodedData[dataIndex];
                dataIndex++;
            }

            if (i != 0 && i % xNum == 0) rowNo++;
        }

        rowNo = 0;
        // Every 4th. row, starting with row 2.
        for (var i = 0; i < newArr.Length; i++)
        {
            if (rowNo % 4 == 2)
            {
                newArr[i] = decodedData[dataIndex];
                dataIndex++;
            }

            if (i != 0 && i % xNum == 0) rowNo++;
        }

        rowNo = 0;
        // Every 2nd. row, starting with row 1.
        for (var i = 0; i < newArr.Length; i++)
        {
            if (rowNo % 8 != 0 && rowNo % 8 != 4 && rowNo % 4 != 2)
            {
                newArr[i] = decodedData[dataIndex];
                dataIndex++;
            }

            if (i != 0 && i % xNum == 0) 
                rowNo++;
        }

        return newArr;
    }

    #endregion
}