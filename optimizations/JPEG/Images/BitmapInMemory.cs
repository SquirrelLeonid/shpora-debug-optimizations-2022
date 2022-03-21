using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace JPEG.Images
{
    internal class BitmapInMemory : IDisposable
    {
        private readonly Bitmap bitmap;
        private readonly BitmapData data;
        public readonly unsafe byte* FirstPixelPointer;
        public readonly int Height;
        public readonly int Stride;
        public readonly int Width;

        public unsafe BitmapInMemory(Bitmap bitmap)
        {
            this.bitmap = bitmap;
            Height = bitmap.Height;
            Width = bitmap.Width;
            data = this.bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            Stride = data.Stride;
            FirstPixelPointer = (byte*)data.Scan0;
        }

        public unsafe byte* this[int y, int x]
        {
            get
            {
                if (y < 0 || y >= Height ||
                    x < 0 || x >= Stride)
                    throw new IndexOutOfRangeException("index(es) beyond of bitmap area");

                return FirstPixelPointer + Stride * y + x * 3;
            }
        }

        public unsafe byte* this[int pixelNumber]
        {
            get
            {
                if (pixelNumber < 0 || pixelNumber > Stride * Height)
                    throw new IndexOutOfRangeException("index beyond of bitmap area");

                var rowToSkip = pixelNumber / Width;
                var pixelToSkip = pixelNumber % Width - 1;
                return FirstPixelPointer + rowToSkip * Stride + pixelToSkip * 3;
            }
        }

        public void Dispose()
        {
            bitmap.UnlockBits(data);
        }

        public void Save(string name, ImageFormat format)
        {
            bitmap.Save(name, format);
        }

        public unsafe double GetYCbCrPixelComponents(int y, int x, int shift)
        {
            if (y < 0 || y >= Height ||
                x < 0 || x >= Stride)
                return 0;

            var ptrToPixelComponent = this[y, x];

            var b = *ptrToPixelComponent;
            var g = *(ptrToPixelComponent + 1);
            var r = *(ptrToPixelComponent + 2);

            return shift switch
            {
                0 => 16.0 + (65.738 * r + 129.057 * g + 24.064 * b) / 256.0,
                1 => 128.0 + (-37.945 * r - 74.494 * g + 112.439 * b) / 256.0,
                2 => 128.0 + (112.439 * r - 94.154 * g - 18.285 * b) / 256.0,
                _ => throw new Exception()
            };
        }

        public unsafe void SetYCbCrComponents(
            int y, int x,
            double _y, double cb, double cr)
        {
            if (y < 0 || y >= Height ||
                x < 0 || x >= Stride)
                return;

            var ptrToPixelComponent = this[y, x];

            *ptrToPixelComponent = ToByte((298.082 * _y + 516.412 * cb) / 256.0 - 276.836);
            *(ptrToPixelComponent + 1) = ToByte((298.082 * _y - 100.291 * cb - 208.120 * cr) / 256.0 + 135.576);
            *(ptrToPixelComponent + 2) = ToByte((298.082 * _y + 408.583 * cr) / 256.0 - 222.921);
        }

        public static byte ToByte(double d)
        {
            var val = (int)d;
            if (val > byte.MaxValue)
                return byte.MaxValue;
            if (val < byte.MinValue)
                return byte.MinValue;
            return (byte)val;
        }
    }
}