using UnityEngine;
using Unity.MLAgents;
using GridWorld;


namespace GridWorld
{
    public class GodViewRecorder : MonoBehaviour
    {
        private int imageQuality;
        private Camera _cam;
        private Texture2D _cpuTexture;

        public void Initialize(int imageQuality = 75)
        {
            this.imageQuality = imageQuality;
            _cam = GetComponent<Camera>();

            if (_cam.targetTexture == null)
            {
                Debug.LogError($"[GodViewRecorder] Camera on {gameObject.name} has no Target Texture! " +
                               "Please assign the UI RenderTexture to the Camera prefab.");
                return;
            }

            _cpuTexture = new Texture2D(_cam.targetTexture.width, _cam.targetTexture.height, TextureFormat.RGB24, false);
        }

        private void OnEnable()
        {
            GodViewSideChannel.OnRequestFrame += TrySendFrame;
        }

        private void OnDisable()
        {
            GodViewSideChannel.OnRequestFrame -= TrySendFrame;
        }

        private void TrySendFrame()
        {
            // Only the active camera (the one the user is looking at in UI) responds
            if (GodViewSideChannel.Instance == null)
            {
                Debug.LogError("[GodViewRecorder] No GodViewSideChannel instance found!");
                return;
            }

            if (!gameObject.activeInHierarchy || _cam == null || _cam.targetTexture == null) return;

            SendFrame();
        }

        private void SendFrame()
        {
            var prevActive = RenderTexture.active;
            RenderTexture.active = _cam.targetTexture;

            _cpuTexture.ReadPixels(new Rect(0, 0, _cam.targetTexture.width, _cam.targetTexture.height), 0, 0);
            _cpuTexture.Apply();

            RenderTexture.active = prevActive;

            byte[] bytes = _cpuTexture.EncodeToJPG(this.imageQuality);

            GodViewSideChannel.Instance.SendImage(bytes);
        }

        private void OnDestroy()
        {
            if (_cpuTexture != null) Destroy(_cpuTexture);
        }
    }
}