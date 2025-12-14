using UnityEngine;

namespace GenerationUtilities
{
    public static class SimplexNoise
    {
        private static readonly int[] Perm = new int[512];
        private static readonly int[] Grad3 = {
            1,1,0, -1,1,0, 1,-1,0, -1,-1,0,
            1,0,1, -1,0,1, 1,0,-1, -1,0,-1,
            0,1,1, 0,-1,1, 0,1,-1, 0,-1,-1
        };

        static SimplexNoise()
        {
            // 1. Initialize logic
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;

            // 2. Shuffle (Fisher-Yates)
            var rng = new System.Random(42); // Fixed seed for consistency
            for (int i = 255; i > 0; i--)
            {
                int swapIndex = rng.Next(i + 1);
                (p[i], p[swapIndex]) = (p[swapIndex], p[i]);
            }

            // 3. Duplicate for wrapping
            for (int i = 0; i < 512; i++) Perm[i] = p[i & 255];
        }

        /// <summary>
        /// 2D Simplex Noise. Returns value between -1 and 1.
        /// </summary>
        public static float Noise2D(float x, float y)
        {
            const float F2 = 0.366025403f; // 0.5 * (sqrt(3.0) - 1.0)
            const float G2 = 0.211324865f; // (3.0 - sqrt(3.0)) / 6.0

            float s = (x + y) * F2;
            int i = Mathf.FloorToInt(x + s);
            int j = Mathf.FloorToInt(y + s);

            float t = (i + j) * G2;
            float X0 = i - t;
            float Y0 = j - t;
            float x0 = x - X0;
            float y0 = y - Y0;

            // Determine which simplex we are in
            int i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; } // Lower triangle, XY order: (0,0)->(1,0)->(1,1)
            else { i1 = 0; j1 = 1; } // Upper triangle, YX order: (0,0)->(0,1)->(1,1)

            // Middle corner
            float x1 = x0 - i1 + G2;
            float y1 = y0 - j1 + G2;
            // Last corner
            float x2 = x0 - 1.0f + 2.0f * G2;
            float y2 = y0 - 1.0f + 2.0f * G2;

            // Hashed gradient indices
            int ii = i & 255;
            int jj = j & 255;

            // Calculate contribution
            float n0 = GetContribution2D(ii, jj, x0, y0);
            float n1 = GetContribution2D(ii + i1, jj + j1, x1, y1);
            float n2 = GetContribution2D(ii + 1, jj + 1, x2, y2);

            // Scale to [-1, 1] (approximate)
            return 70.0f * (n0 + n1 + n2);
        }

        /// <summary>
        /// 3D Simplex Noise. Returns value between -1 and 1.
        /// </summary>
        public static float Noise3D(float x, float y, float z)
        {
            const float F3 = 1.0f / 3.0f;
            const float G3 = 1.0f / 6.0f;

            float s = (x + y + z) * F3;
            int i = Mathf.FloorToInt(x + s);
            int j = Mathf.FloorToInt(y + s);
            int k = Mathf.FloorToInt(z + s);

            float t = (i + j + k) * G3;
            float x0 = x - (i - t);
            float y0 = y - (j - t);
            float z0 = z - (k - t);

            int i1, j1, k1, i2, j2, k2;

            if (x0 >= y0)
            {
                if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; } // X Y Z
                else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; } // X Z Y
                else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; } // Z X Y
            }
            else
            {
                if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; } // Z Y X
                else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; } // Y Z X
                else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; } // Y X Z
            }

            float x1 = x0 - i1 + G3;
            float y1 = y0 - j1 + G3;
            float z1 = z0 - k1 + G3;

            float x2 = x0 - i2 + 2.0f * G3;
            float y2 = y0 - j2 + 2.0f * G3;
            float z2 = z0 - k2 + 2.0f * G3;

            float x3 = x0 - 1.0f + 3.0f * G3;
            float y3 = y0 - 1.0f + 3.0f * G3;
            float z3 = z0 - 1.0f + 3.0f * G3;

            int ii = i & 255;
            int jj = j & 255;
            int kk = k & 255;

            float n0 = GetContribution3D(ii, jj, kk, x0, y0, z0);
            float n1 = GetContribution3D(ii + i1, jj + j1, kk + k1, x1, y1, z1);
            float n2 = GetContribution3D(ii + i2, jj + j2, kk + k2, x2, y2, z2);
            float n3 = GetContribution3D(ii + 1, jj + 1, kk + 1, x3, y3, z3);

            return 70.0f * (n0 + n1 + n2 + n3);
        }

        // Helper to reduce code duplication in Noise2D
        private static float GetContribution2D(int i, int j, float x, float y)
        {
            float t = 0.5f - x * x - y * y;
            if (t < 0) return 0;

            int gi = Perm[i + Perm[j]] % 12;
            // 2D gradients from the 3D set (dropping Z)
            return t * t * t * t * (Grad3[gi * 3] * x + Grad3[gi * 3 + 1] * y);
        }

        // Helper to reduce code duplication in Noise3D
        private static float GetContribution3D(int i, int j, int k, float x, float y, float z)
        {
            float t = 0.5f - x * x - y * y - z * z;
            if (t < 0) return 0;

            int gi = Perm[i + Perm[j + Perm[k]]] % 12;
            t *= t;
            return t * t * (Grad3[gi * 3] * x + Grad3[gi * 3 + 1] * y + Grad3[gi * 3 + 2] * z);
        }
    }
}