using GridWorld;
using Unity.MLAgents.SideChannels;
using UnityEngine;

namespace SideChannels
{
    public class Grid2DChannelRegistrar : SideChannelRegistrar
    {
        public override void RegisterChannels()
        {
            if (GodViewSideChannel.Instance == null)
            {
                new GodViewSideChannel();
            }

            RegisterSafe(GodViewSideChannel.Instance);

            Debug.Log("GridWorld 2D Side Channels Registered.");
        }

        private void OnDestroy()
        {
            if (GodViewSideChannel.Instance != null)
            {
                SideChannelManager.UnregisterSideChannel(GodViewSideChannel.Instance);
            }
        }
    }
}