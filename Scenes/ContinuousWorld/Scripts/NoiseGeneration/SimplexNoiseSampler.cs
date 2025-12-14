using ContinuousWorld;
using GenerationUtilities;

namespace ContinuousWorld
{
    public class SimplexNoiseSampler : INoiseSampler
    {
        public float Sample(float x, float y)
        {
            return SimplexNoise.Noise2D(x, y);
        }
    }
}