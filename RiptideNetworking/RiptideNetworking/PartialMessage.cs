using RiptideNetworking.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RiptideNetworking
{
    public static class PartialMessageHandler
    {
        public static uint id { get; set; }
        public static Client Client { get; set; }
        public static Server Server { get; set; }
        public static Dictionary<uint, PartialMessage> PartialMessages = new Dictionary<uint, PartialMessage>();

        public enum MessageDirection
        {
            SendToClient,
            SendToServer
        }

        public static void SendPartialMessage(ushort splitMessageInboundId, ushort splitMessageId, ushort id, byte[] bytes, ushort? toClientID, MessageDirection messageDirection)
        {
            Message splitMessageInbound = Message.Create(MessageSendMode.reliable, splitMessageInboundId);
            splitMessageInbound.AddUShort(id);
            splitMessageInbound.AddUInt(PartialMessageHandler.id);

            int unWrittenLength = 1237;
            int splitCount = bytes.Length / unWrittenLength; //Todo get Unwritten length from a message prop

            if (splitCount <= 0)
                splitCount = 1;

            if (bytes.Length % unWrittenLength != 0)
                splitCount++;

            splitMessageInbound.AddInt(splitCount); //How many splits will be in this file

            if (messageDirection == MessageDirection.SendToServer)
                PartialMessageHandler.Client.Send(splitMessageInbound);
            else if (messageDirection == MessageDirection.SendToClient)
                if (toClientID.HasValue)
                    PartialMessageHandler.Server.Send(splitMessageInbound, toClientID.Value);
                else
                    PartialMessageHandler.Server.SendToAll(splitMessageInbound);

            uint splitMessageOrdinal = 0;
            foreach (byte[] copySlice in bytes.Slices(unWrittenLength))
            {
                splitMessageOrdinal++;

                Message sliceMessage = Message.Create(MessageSendMode.reliable, splitMessageId);
                sliceMessage.AddUInt(PartialMessageHandler.id);
                sliceMessage.AddUInt(splitMessageOrdinal);
                sliceMessage.AddBytes(copySlice);

                if (messageDirection == MessageDirection.SendToClient)
                    if (toClientID.HasValue)
                        PartialMessageHandler.Server.Send(sliceMessage, toClientID.Value);
                    else
                        PartialMessageHandler.Server.SendToAll(sliceMessage);
                else if (messageDirection == MessageDirection.SendToServer)
                    PartialMessageHandler.Client.Send(sliceMessage);
            }

            PartialMessageHandler.id++;
        }
    }

    public class PartialMessage
    {
        public ushort ServerToClientID { get; set; }
        public uint PartialMessageID { get; set; }
        public int PartialMessageCount { get; set; }
        public List<byte[]> MessageData { get; set; } = new List<byte[]>();
        public bool IsDone => MessageData.Count >= PartialMessageCount;

        public void AddPartialMessage(uint ordinal, byte[] bytes)
        {
            if (this.MessageData.Any() == false)
                this.MessageData.Add(bytes);
            else
                if (this.MessageData.Count < ordinal)
                    this.MessageData.Add(bytes);
                else if (this.MessageData.Count >= ordinal)
                    this.MessageData.Insert((int)ordinal - 1, bytes);

            PartialMessageProgress progressMessage = new PartialMessageProgress(this.PartialMessageID, this.PartialMessageCount, this.MessageData.Count);
            PartialMessageHandler.Client.messageProgressHandlers[this.ServerToClientID].Invoke(progressMessage);

            if (this.IsDone)
            {
                Message message = new Message(this.MessageData.SelectMany(byteArr => byteArr).ToArray());
                PartialMessageHandler.Client.messageHandlers[this.ServerToClientID].Invoke(message);
            }
        }
    }
}
