namespace JPEG.Images
{
    public class PixelFormat
    {
        private string format;

        private PixelFormat(string format)
        {
            this.format = format;
        }

        public static PixelFormat RGB => new(nameof(RGB));
        public static PixelFormat YCbCr => new(nameof(YCbCr));

        protected bool Equals(PixelFormat other)
        {
            return string.Equals(format, other.format);
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != GetType()) return false;
            return Equals((PixelFormat)obj);
        }

        public override int GetHashCode()
        {
            return format != null ? format.GetHashCode() : 0;
        }

        public static bool operator ==(PixelFormat a, PixelFormat b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(PixelFormat a, PixelFormat b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return format;
        }

        ~PixelFormat()
        {
            format = null;
        }
    }
}