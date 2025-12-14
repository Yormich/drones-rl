using System;
using Unity.MLAgents.SideChannels;
using UnityEngine;

namespace SideChannels
{
    public enum ContinuousViewType
    {
        Trailing = 0, // Behind the drone
    }

    public class ContinuousSideChannel : SideChannel
    {
        private const int RequestSnapshotCode = 1;
        // specific GUID for Continuous World
        public const string ChannelIdStr = "f1e2d3c4-b5a6-9780-1234-56789abcdef1";
        public static ContinuousSideChannel Instance { get; private set; }

        public static event Action OnRequestFullSnapshot;

        public ContinuousSideChannel()
        {
            ChannelId = new Guid(ChannelIdStr);
            Instance = this;
        }

        protected override void OnMessageReceived(IncomingMessage msg)
        {
            int code = msg.ReadInt32();
            if (code == RequestSnapshotCode)
            {
                OnRequestFullSnapshot?.Invoke();
            }
        }

        public void SendView(ContinuousViewType viewType, byte[] jpgBytes)
        {
            using (var msg = new OutgoingMessage())
            {
                byte[] combinedBytes = new byte[4 + jpgBytes.Length];

                byte[] viewIdBytes = BitConverter.GetBytes((int)viewType);

                System.Array.Copy(viewIdBytes, 0, combinedBytes, 0, 4);

                System.Array.Copy(jpgBytes, 0, combinedBytes, 4, jpgBytes.Length);

                msg.SetRawBytes(combinedBytes);

                QueueMessageToSend(msg);
            }
        }
    }
}