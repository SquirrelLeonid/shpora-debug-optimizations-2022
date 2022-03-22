using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using JPEG.Images;
using JPEG.Utilities;

namespace JPEG
{
    internal class Program
    {
        private const int CompressionQuality = 70;

        private const int DCTSize = 8;
        private const int DoubledDCTSize = DCTSize * 2;

        private static void Main(string[] args)
        {
            try
            {
                Console.WriteLine(IntPtr.Size == 8 ? "64-bit version" : "32-bit version");
                var sw = Stopwatch.StartNew();
                var fileName = @"marbles.bmp";
                var compressedFileName = fileName + ".compressed." + CompressionQuality;
                var uncompressedFileName = fileName + ".uncompressed." + CompressionQuality + ".bmp";

                using (var fileStream = File.OpenRead(fileName))
                using (var bmp = (Bitmap)Image.FromStream(fileStream, false, false))
                {
                    var bitmapInMemory = new BitmapInMemory(bmp);

                    sw.Stop();
                    Console.WriteLine($"{bmp.Width}x{bmp.Height} - {fileStream.Length / (1024.0 * 1024):F2} MB");
                    sw.Start();

                    var compressionResult = Compress(bitmapInMemory, CompressionQuality);
                    compressionResult.Save(compressedFileName);
                }

                sw.Stop();
                Console.WriteLine("Compression: " + sw.Elapsed);
                sw.Restart();
                var compressedImage = CompressedImage.Load(compressedFileName);
                var uncompressedImage = Uncompress(compressedImage);
                uncompressedImage.Save(uncompressedFileName, ImageFormat.Bmp);
                Console.WriteLine("Decompression: " + sw.Elapsed);
                Console.WriteLine($"Peak commit size: {MemoryMeter.PeakPrivateBytes() / (1024.0 * 1024):F2} MB");
                Console.WriteLine($"Peak working set: {MemoryMeter.PeakWorkingSet() / (1024.0 * 1024):F2} MB");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static CompressedImage Compress(BitmapInMemory bitmapInMemory, int quality = 50)
        {
            var allQuantizedBytes = new List<byte>();

            for (var y = 0; y < bitmapInMemory.Height; y += DoubledDCTSize)
            for (var x = 0; x < bitmapInMemory.Width; x += DoubledDCTSize)
            {
                var yChannel = GetEnlargeYChannel(bitmapInMemory, y, x);
                var cbChannel = GetSubMatrix(
                    bitmapInMemory,
                    y, DoubledDCTSize,
                    x, DoubledDCTSize,
                    1).ApplySubsampling();

                var crChannel = GetSubMatrix(
                    bitmapInMemory,
                    y, DoubledDCTSize,
                    x, DoubledDCTSize,
                    2).ApplySubsampling();

                foreach (var channel in new[]
                             { yChannel[0], yChannel[1], yChannel[2], yChannel[3], cbChannel, crChannel })
                {
                    ShiftMatrixValues(channel, -128);
                    var channelFreqs = DCT.DCT2D(channel);
                    var quantizedFreqs = Quantize(channelFreqs, quality);
                    var quantizedBytes = ZigZagScan(quantizedFreqs);
                    allQuantizedBytes.AddRange(quantizedBytes);
                }
            }

            long bitsCount;
            Dictionary<BitsWithLength, byte> decodeTable;
            var compressedBytes = HuffmanCodec.Encode(allQuantizedBytes, out decodeTable, out bitsCount);

            return new CompressedImage
            {
                Quality = quality,
                CompressedBytes = compressedBytes,
                BitsCount = bitsCount,
                DecodeTable = decodeTable,
                Height = bitmapInMemory.Height,
                Width = bitmapInMemory.Width
            };
        }

        private static double[][,] GetEnlargeYChannel(
            BitmapInMemory bmp,
            int yOffset,
            int xOffset)
        {
            var result = new[]
            {
                GetSubMatrix(bmp, yOffset, DCTSize, xOffset, DCTSize, 0),
                GetSubMatrix(bmp, yOffset, DCTSize, xOffset + DCTSize, DCTSize, 0),
                GetSubMatrix(bmp, yOffset + DCTSize, DCTSize, xOffset, DCTSize, 0),
                GetSubMatrix(bmp, yOffset + DCTSize, DCTSize, xOffset + DCTSize, DCTSize, 0)
            };

            return result;
        }

        private static BitmapInMemory Uncompress(CompressedImage image)
        {
            var result = new BitmapInMemory(
                new Bitmap(
                    image.Width,
                    image.Height,
                    PixelFormat.Format24bppRgb));

            var YXoffsets = GetMatrixOffsets(DCTSize);

            using var allQuantizedBytes = new MemoryStream(
                HuffmanCodec.Decode(
                    image.CompressedBytes,
                    image.DecodeTable,
                    image.BitsCount));

            for (var y = 0; y < image.Height; y += DCTSize * 2)
            for (var x = 0; x < image.Width; x += DCTSize * 2)
            {
                var yChannel = new[]
                {
                    new double[DCTSize, DCTSize],
                    new double[DCTSize, DCTSize],
                    new double[DCTSize, DCTSize],
                    new double[DCTSize, DCTSize]
                };
                var cbChannel = new double[DCTSize, DCTSize];
                var crChannel = new double[DCTSize, DCTSize];

                foreach (var channel in new[]
                             { yChannel[0], yChannel[1], yChannel[2], yChannel[3], cbChannel, crChannel })
                {
                    var quantizedBytes = new byte[DCTSize * DCTSize];
                    allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
                    var quantizedFreqs = ZigZagUnScan(quantizedBytes);
                    var channelFreqs = DeQuantize(quantizedFreqs, image.Quality);
                    DCT.IDCT2D(channelFreqs, channel);
                    ShiftMatrixValues(channel, 128);
                }

                var restoredCbChannel = RestoreChannel(cbChannel);
                var restoredCrChannel = RestoreChannel(crChannel);

                for (var i = 0; i < 4; i++)
                {
                    var yOffset = YXoffsets[i].Item1;
                    var xOffset = YXoffsets[i].Item2;
                    SetPixels(result,
                        yChannel[i], restoredCbChannel[i], restoredCrChannel[i],
                        y + yOffset,
                        x + xOffset);
                }
            }

            return result;
        }

        private static Tuple<int, int>[] GetMatrixOffsets(int offsetValue)
        {
            return new[]
            {
                Tuple.Create(0, 0),
                Tuple.Create(0, offsetValue),
                Tuple.Create(offsetValue, 0),
                Tuple.Create(offsetValue, offsetValue)
            };
        }

        private static void ShiftMatrixValues(double[,] subMatrix, int shiftValue)
        {
            var height = subMatrix.GetLength(0);
            var width = subMatrix.GetLength(1);


            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                subMatrix[y, x] += shiftValue;
        }

        private static double[][,] RestoreChannel(double[,] subsampled)
        {
            var height = subsampled.GetLength(0);
            var width = subsampled.GetLength(1);
            var result = new double[4][,];

            var YXoffsets = GetMatrixOffsets(4);

            for (var i = 0; i < 4; i++)
            {
                var yOffset = YXoffsets[i].Item1;
                var xOffset = YXoffsets[i].Item2;
                var channel = new double[height, width];

                for (var y = 0; y < DCTSize / 2; y++)
                for (var x = 0; x < DCTSize / 2; x++)
                {
                    channel[y * 2, x * 2] = subsampled[yOffset + y, xOffset + x];
                    channel[y * 2, x * 2 + 1] = subsampled[yOffset + y, xOffset + x];
                    channel[y * 2 + 1, x * 2] = subsampled[yOffset + y, xOffset + x];
                    channel[y * 2 + 1, x * 2 + 1] = subsampled[yOffset + y, xOffset + x];
                }

                result[i] = channel;
            }

            return result;
        }

        private static void SetPixels(
            BitmapInMemory bitmapInMemory,
            double[,] yChannel, double[,] cbChannel, double[,] crChannel,
            int yOffset,
            int xOffset)
        {
            var height = yChannel.GetLength(0);
            var width = yChannel.GetLength(1);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmapInMemory.SetYCbCrComponents(
                    yOffset + y,
                    xOffset + x,
                    yChannel[y, x],
                    cbChannel[y, x],
                    crChannel[y, x]);
        }

        /// <summary>
        /// </summary>
        /// <param name="bitmapInMemory"></param>
        /// <param name="yOffset"></param>
        /// <param name="yLength"></param>
        /// <param name="xOffset"></param>
        /// <param name="xLength"></param>
        /// <param name="shift">pixel component number</param>
        /// <returns></returns>
        private static double[,] GetSubMatrix(
            BitmapInMemory bitmapInMemory,
            int yOffset, int yLength,
            int xOffset, int xLength,
            int shift)
        {
            var result = new double[yLength, xLength];

            for (var y = 0; y < yLength; y++)
            for (var x = 0; x < xLength; x++)
                result[y, x] = bitmapInMemory.GetYCbCrPixelComponents(
                    yOffset + y,
                    xOffset + x,
                    shift);

            return result;
        }

        private static byte[] ZigZagScan(byte[,] channelFreqs)
        {
            return new[]
            {
                channelFreqs[0, 0], channelFreqs[0, 1], channelFreqs[1, 0], channelFreqs[2, 0], channelFreqs[1, 1],
                channelFreqs[0, 2], channelFreqs[0, 3], channelFreqs[1, 2],
                channelFreqs[2, 1], channelFreqs[3, 0], channelFreqs[4, 0], channelFreqs[3, 1], channelFreqs[2, 2],
                channelFreqs[1, 3], channelFreqs[0, 4], channelFreqs[0, 5],
                channelFreqs[1, 4], channelFreqs[2, 3], channelFreqs[3, 2], channelFreqs[4, 1], channelFreqs[5, 0],
                channelFreqs[6, 0], channelFreqs[5, 1], channelFreqs[4, 2],
                channelFreqs[3, 3], channelFreqs[2, 4], channelFreqs[1, 5], channelFreqs[0, 6], channelFreqs[0, 7],
                channelFreqs[1, 6], channelFreqs[2, 5], channelFreqs[3, 4],
                channelFreqs[4, 3], channelFreqs[5, 2], channelFreqs[6, 1], channelFreqs[7, 0], channelFreqs[7, 1],
                channelFreqs[6, 2], channelFreqs[5, 3], channelFreqs[4, 4],
                channelFreqs[3, 5], channelFreqs[2, 6], channelFreqs[1, 7], channelFreqs[2, 7], channelFreqs[3, 6],
                channelFreqs[4, 5], channelFreqs[5, 4], channelFreqs[6, 3],
                channelFreqs[7, 2], channelFreqs[7, 3], channelFreqs[6, 4], channelFreqs[5, 5], channelFreqs[4, 6],
                channelFreqs[3, 7], channelFreqs[4, 7], channelFreqs[5, 6],
                channelFreqs[6, 5], channelFreqs[7, 4], channelFreqs[7, 5], channelFreqs[6, 6], channelFreqs[5, 7],
                channelFreqs[6, 7], channelFreqs[7, 6], channelFreqs[7, 7]
            };
        }

        private static byte[,] ZigZagUnScan(IReadOnlyList<byte> quantizedBytes)
        {
            return new[,]
            {
                {
                    quantizedBytes[0], quantizedBytes[1], quantizedBytes[5], quantizedBytes[6], quantizedBytes[14],
                    quantizedBytes[15], quantizedBytes[27], quantizedBytes[28]
                },
                {
                    quantizedBytes[2], quantizedBytes[4], quantizedBytes[7], quantizedBytes[13], quantizedBytes[16],
                    quantizedBytes[26], quantizedBytes[29], quantizedBytes[42]
                },
                {
                    quantizedBytes[3], quantizedBytes[8], quantizedBytes[12], quantizedBytes[17], quantizedBytes[25],
                    quantizedBytes[30], quantizedBytes[41], quantizedBytes[43]
                },
                {
                    quantizedBytes[9], quantizedBytes[11], quantizedBytes[18], quantizedBytes[24], quantizedBytes[31],
                    quantizedBytes[40], quantizedBytes[44], quantizedBytes[53]
                },
                {
                    quantizedBytes[10], quantizedBytes[19], quantizedBytes[23], quantizedBytes[32], quantizedBytes[39],
                    quantizedBytes[45], quantizedBytes[52], quantizedBytes[54]
                },
                {
                    quantizedBytes[20], quantizedBytes[22], quantizedBytes[33], quantizedBytes[38], quantizedBytes[46],
                    quantizedBytes[51], quantizedBytes[55], quantizedBytes[60]
                },
                {
                    quantizedBytes[21], quantizedBytes[34], quantizedBytes[37], quantizedBytes[47], quantizedBytes[50],
                    quantizedBytes[56], quantizedBytes[59], quantizedBytes[61]
                },
                {
                    quantizedBytes[35], quantizedBytes[36], quantizedBytes[48], quantizedBytes[49], quantizedBytes[57],
                    quantizedBytes[58], quantizedBytes[62], quantizedBytes[63]
                }
            };
        }

        private static byte[,] Quantize(double[,] channelFreqs, int quality)
        {
            var height = channelFreqs.GetLength(0);
            var width = channelFreqs.GetLength(1);

            var result = new byte[height, width];

            var quantizationMatrix = GetQuantizationMatrix(quality);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                result[y, x] = (byte)(channelFreqs[y, x] / quantizationMatrix[y, x]);

            return result;
        }

        private static double[,] DeQuantize(byte[,] quantizedBytes, int quality)
        {
            var height = quantizedBytes.GetLength(0);
            var width = quantizedBytes.GetLength(1);

            var result = new double[height, width];

            var quantizationMatrix = GetQuantizationMatrix(quality);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                //NOTE cast to sbyte not to loose negative numbers
                result[y, x] = (sbyte)quantizedBytes[y, x] * quantizationMatrix[y, x];

            return result;
        }

        private static int[,] GetQuantizationMatrix(int quality)
        {
            if (quality < 1 || quality > 99)
                throw new ArgumentException("quality must be in [1,99] interval");

            var multiplier = quality < 50 ? 5000 / quality : 200 - 2 * quality;

            var result = new[,]
            {
                { 16, 11, 10, 16, 24, 40, 51, 61 },
                { 12, 12, 14, 19, 26, 58, 60, 55 },
                { 14, 13, 16, 24, 40, 57, 69, 56 },
                { 14, 17, 22, 29, 51, 87, 80, 62 },
                { 18, 22, 37, 56, 68, 109, 103, 77 },
                { 24, 35, 55, 64, 81, 104, 113, 92 },
                { 49, 64, 78, 87, 103, 121, 120, 101 },
                { 72, 92, 95, 98, 112, 100, 103, 99 }
            };

            var height = result.GetLength(0);
            var width = result.GetLength(1);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                result[y, x] = (multiplier * result[y, x] + 50) / 100;

            return result;
        }
    }
}