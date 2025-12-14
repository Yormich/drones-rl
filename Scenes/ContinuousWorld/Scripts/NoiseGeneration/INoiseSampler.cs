namespace ContinuousWorld
{
    public interface INoiseSampler
    {
        // Should return value ideally between -1 and 1
        float Sample(float x, float y);
    }
}