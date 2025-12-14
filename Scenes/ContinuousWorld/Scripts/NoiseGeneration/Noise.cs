using UnityEngine;

namespace ContinuousWorld
{
    public static class Noise
    {
        public enum NormalizeMode { Local, Global };

        public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, NoiseSettings settings, Vector2 sampleCenter)
        {
            INoiseSampler sampler = GetSampler(settings.noiseType);

            Vector2[] octaveOffsets = CalculateOctaveOffsets(settings, sampleCenter);

            float[,] noiseMap = new float[mapWidth, mapHeight];

            // We track min/max for Local normalization
            float minLocalNoiseHeight = float.MaxValue;
            float maxLocalNoiseHeight = float.MinValue;

            float halfWidth = mapWidth / 2f;
            float halfHeight = mapHeight / 2f;

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    float amplitude = 1;
                    float frequency = 1;
                    float noiseHeight = 0;

                    for (int i = 0; i < settings.octaves; i++)
                    {
                        float sampleX = (x - halfWidth + octaveOffsets[i].x) / settings.scale * frequency;
                        float sampleY = (y - halfHeight + octaveOffsets[i].y) / settings.scale * frequency;

                        float noiseValue = sampler.Sample(sampleX, sampleY);

                        noiseHeight += noiseValue * amplitude;

                        amplitude *= settings.persistance;
                        frequency *= settings.lacunarity;
                    }

                    if (noiseHeight > maxLocalNoiseHeight) maxLocalNoiseHeight = noiseHeight;
                    if (noiseHeight < minLocalNoiseHeight) minLocalNoiseHeight = noiseHeight;

                    noiseMap[x, y] = noiseHeight;
                }
            }

            if (settings.normalizeMode == NormalizeMode.Global)
            {
                float maxPossibleHeight = CalculateMaxPossibleHeight(settings);
                NormalizeGlobal(noiseMap, mapWidth, mapHeight, maxPossibleHeight);
            }
            else
            {
                NormalizeLocal(noiseMap, mapWidth, mapHeight, minLocalNoiseHeight, maxLocalNoiseHeight);
            }

            return noiseMap;
        }

        private static INoiseSampler GetSampler(NoiseType type)
        {
            return type switch
            {
                NoiseType.UnityPerlin => new PerlinNoiseSampler(),
                NoiseType.Simplex => new SimplexNoiseSampler(),
                _ => new PerlinNoiseSampler(),
            };
        }

        private static Vector2[] CalculateOctaveOffsets(NoiseSettings settings, Vector2 sampleCenter)
        {
            System.Random prng = new System.Random(settings.seed);
            Vector2[] offsets = new Vector2[settings.octaves];

            for (int i = 0; i < settings.octaves; i++)
            {
                float offsetX = prng.Next(-100000, 100000) + settings.offset.x + sampleCenter.x;
                float offsetY = prng.Next(-100000, 100000) - settings.offset.y - sampleCenter.y;
                offsets[i] = new Vector2(offsetX, offsetY);
            }
            return offsets;
        }

        private static float CalculateMaxPossibleHeight(NoiseSettings settings)
        {
            float maxPossibleHeight = 0;
            float amplitude = 1;
            for (int i = 0; i < settings.octaves; i++)
            {
                maxPossibleHeight += amplitude;
                amplitude *= settings.persistance;
            }
            return maxPossibleHeight;
        }

        private static void NormalizeLocal(float[,] noiseMap, int width, int height, float min, float max)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // InverseLerp maps [min, max] to [0, 1]
                    noiseMap[x, y] = Mathf.InverseLerp(min, max, noiseMap[x, y]);
                }
            }
        }

        private static void NormalizeGlobal(float[,] noiseMap, int width, int height, float maxPossibleHeight)
        {
            // We use a slight buffer (0.9f) to prevent clipping if noise slightly exceeds expectation
            // though strictly speaking, maxPossibleHeight should be absolute.
            float normalizationFactor = maxPossibleHeight / 0.9f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float normalizedHeight = (noiseMap[x, y] + 1) / normalizationFactor;
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }
    }
}