namespace JPEG.Utilities
{
    internal static class MatrixExtensions
    {
        /// <summary>
        ///     Used subsampling 4:2:0
        /// </summary>
        /// <param name="matrix"></param>
        /// <returns></returns>
        public static T[,] ApplySubsampling<T>(this T[,] matrix)
        {
            var height = matrix.GetLength(0);
            var width = matrix.GetLength(1);
            var result = new T[height / 2, width / 2];

            for (var y = 0; y < height; y += 2)
            for (var x = 0; x < width; x += 2)
                result[y / 2, x / 2] = matrix[y, x];

            return result;
        }
    }
}