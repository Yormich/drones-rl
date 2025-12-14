using Unity.MLAgents.SideChannels;
using UnityEngine;

namespace SideChannels
{
    public class Grid3DChannelRegistrar : SideChannelRegistrar
    {
        public override void RegisterChannels()
        {
            if (Grid3DSideChannel.Instance == null)
            {
                new Grid3DSideChannel();
            }

            RegisterSafe(Grid3DSideChannel.Instance);
            Debug.Log("GridWorld 3D Side Channels Registered.");
        }

        private static void OnDestroy()
        {
            if (Grid3DSideChannel.Instance != null)
            {
                SideChannelManager.UnregisterSideChannel(Grid3DSideChannel.Instance);
            }
        }
    }

}