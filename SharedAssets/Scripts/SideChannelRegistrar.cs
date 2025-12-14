using UnityEngine;
using Unity.MLAgents.SideChannels;

namespace SideChannels
{
    public abstract class SideChannelRegistrar : MonoBehaviour
    {
        public abstract void RegisterChannels();

        protected static void RegisterSafe(SideChannel channel)
        {
            SideChannelManager.UnregisterSideChannel(channel);
            SideChannelManager.RegisterSideChannel(channel);
        }
    }
}