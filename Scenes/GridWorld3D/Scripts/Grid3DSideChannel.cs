using System;
using Unity.MLAgents.SideChannels;
using UnityEngine;

namespace SideChannels
{
    public enum Grid3DViewType
    {
        GodView = 0,
        Front = 1,
        Back = 2,
        Left = 3,
        Right = 4,
        Up = 5,
        Down = 6
    }
    public class Grid3DSideChannel : SideChannel
    {
        public const string ChannelIdStr = "a1b2c3d4-e5f6-7890-1234-56789abcdef0";
        public static Grid3DSideChannel Instance { get; private set; }

        // Event that tells the environment: "Send me everything right now"
        public static event Action OnRequestFullSnapshot;

        public Grid3DSideChannel()
        {
            ChannelId = new Guid(ChannelIdStr);
            Instance = this;
        }

        protected override void OnMessageReceived(IncomingMessage msg)
        {
            // Python sends '1' to request a snapshot
            int code = msg.ReadInt32();
            if (code == 1)
            {
                OnRequestFullSnapshot?.Invoke();
            }
        }

        public void SendView(Grid3DViewType viewType, byte[] pngBytes)
        {
            using (var msg = new OutgoingMessage())
            {
                byte[] combined = new byte[4 + pngBytes.Length];
                Array.Copy(BitConverter.GetBytes((int)viewType), 0, combined, 0, 4);
                Array.Copy(pngBytes, 0, combined, 4, pngBytes.Length);
                msg.SetRawBytes(combined);
                QueueMessageToSend(msg);
            }
        }
    }

}