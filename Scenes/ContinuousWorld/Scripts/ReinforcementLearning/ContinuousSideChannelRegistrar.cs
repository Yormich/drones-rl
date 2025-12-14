using UnityEngine;
using SideChannels;
using Unity.MLAgents.SideChannels;

namespace ContinuousWorld
{
    public class ContinuousSideChannelRegistrar : SideChannelRegistrar
    {
        public override void RegisterChannels()
        {
            if (ContinuousSideChannel.Instance == null)
            {
                new ContinuousSideChannel();
            }

            RegisterSafe(ContinuousSideChannel.Instance);
            Debug.Log("Continuous World Side Channels Registered.");
        }

        private static void OnDestroy()
        {
            if (ContinuousSideChannel.Instance != null)
            {
                SideChannelManager.UnregisterSideChannel(ContinuousSideChannel.Instance);
            }
        }
    }
}