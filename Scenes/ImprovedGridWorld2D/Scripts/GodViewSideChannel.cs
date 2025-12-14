using UnityEngine;
using Unity.MLAgents.SideChannels;
using System;
using NUnit.Framework.Constraints;

namespace GridWorld
{
    public class GodViewSideChannel : SideChannel
    {
        private const int frameRequestCode = 1;
        public const string ChannelIdStr = "621f0a70-4f87-11ea-a6bf-784f4387d1f7";

        public static GodViewSideChannel Instance { get; private set; }

        public static event Action OnRequestFrame;

        public GodViewSideChannel()
        {
            ChannelId = new Guid(ChannelIdStr);
            Instance = this;
        }

        protected override void OnMessageReceived(IncomingMessage msg)
        {
            var code = msg.ReadInt32();
            if (code == frameRequestCode)
            {
                OnRequestFrame?.Invoke();
            }
        }

        public void SendImage(byte[] imageData)
        {
            using (var msg = new OutgoingMessage())
            {
                msg.SetRawBytes(imageData);
                QueueMessageToSend(msg);
            }
        }
    }
}