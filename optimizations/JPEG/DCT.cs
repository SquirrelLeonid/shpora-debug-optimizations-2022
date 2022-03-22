using System;
using System.Threading.Tasks;

namespace JPEG
{
    public class DCT
    {
        private static double beta;
        private static readonly double DivideOneToSqrt = 1 / Math.Sqrt(2);
        private static double piByHeight;
        private static double piByWidth;

        public static double[,] DCT2D(double[,] subMatrix)
        {
            var height = subMatrix.GetLength(0);
            var width = subMatrix.GetLength(1);
            var coeffs = new double[width, height];

            Parallel.For(0, width, u =>
                Parallel.For(0, height, v =>
                {
                    var sum = 0.0;

                    for (var x = 0; x < width; x++)
                    for (var y = 0; y < height; y++)
                        sum += BasisFunction(subMatrix[x, y], u, v, x, y);

                    coeffs[u, v] = sum * beta * Alpha(u) * Alpha(v);
                }));

            return coeffs;
        }

        public static void IDCT2D(double[,] coeffs, double[,] output)
        {
            var height = coeffs.GetLength(0);
            var width = coeffs.GetLength(1);

            Parallel.For(0, width, x =>
                Parallel.For(0, height, y =>
                {
                    var sum = 0.0;
                    for (var u = 0; u < width; u++)
                    for (var v = 0; v < height; v++)
                        sum += BasisFunction(coeffs[u, v], u, v, x, y) * Alpha(u) * Alpha(v);

                    output[x, y] = sum * beta;
                }));
        }

        public static double BasisFunction(double a, double u, double v, double x, double y)
        {
            var b = Math.Cos((2d * x + 1d) * u * piByWidth);
            var c = Math.Cos((2d * y + 1d) * v * piByHeight);

            return a * b * c;
        }

        private static double Alpha(int u)
        {
            return u == 0 ? DivideOneToSqrt : 1;
        }

        public static void SetConstants(int width, int height)
        {
            beta = 1d / width + 1d / height;
            piByHeight = Math.PI / (2 * height);
            piByWidth = Math.PI / (2 * width);
        }
    }
}