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
        public readonly int Width;
        public readonly int Stride;

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

        public static unsafe Bitmap RestoreFromCompressed(,int width, int height)
        {
            var bmp = new Bitmap(width, height);
        }

        public unsafe byte* this[int y, int x]
        {
            get
            {
                if (y < 0 || y > Height ||
                    x < 0 || x > Stride)
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
    }
}