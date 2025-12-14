using UnityEngine;

namespace GridWorld.Visuals
{
    [RequireComponent(typeof(Camera))]
    public class CameraCapture : MonoBehaviour
    {
        private const int defaultImageDepth = 24;

        [SerializeField] private int imageQuality = 75;
        private Camera _cam;
        private Texture2D _cacheTexture;
        private RenderTexture _privateRT;

        public RenderTexture OutputTexture => _privateRT;

        public void Initialize(int width, int height)
        {
            _cam = GetComponent<Camera>();


            _privateRT = new RenderTexture(width, height, defaultImageDepth);
            _privateRT.name = $"RT_{gameObject.name}_{GetInstanceID()}";

            _cam.targetTexture = _privateRT;

            _cacheTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        }

        public byte[] CaptureFrame()
        {
            if (_cam == null || _cam.targetTexture == null) return new byte[0];

            var prevActive = RenderTexture.active;
            RenderTexture.active = _cam.targetTexture;

            _cam.Render();

            _cacheTexture.ReadPixels(new Rect(0, 0, _cam.targetTexture.width, _cam.targetTexture.height), 0, 0);
            _cacheTexture.Apply();

            RenderTexture.active = prevActive;

            return _cacheTexture.EncodeToJPG(imageQuality);
        }

        private void OnDestroy()
        {
            if (_cacheTexture != null) Destroy(_cacheTexture);

            if (_privateRT != null)
            {
                _cam.targetTexture = null;
                _privateRT.Release(); // Release GPU memory
                Destroy(_privateRT);  // Destroy object
            }
        }
    }
}