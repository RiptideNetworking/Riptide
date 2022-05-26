using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    public class PartialMessageProgress
    {
        public uint SplitMessageID { get; private set; }
        public int SplitMessageCount { get; private set; }
        public int SplitMessagesRecieved { get; private set; }
        public float PercentDone
        {
            get
            {
                return (float)((float)SplitMessagesRecieved / SplitMessageCount) * 100;
            }
        }

        public PartialMessageProgress(uint splitMessageID, int splitMessageCount, int splitMessagesRecieved)
        {
            SplitMessageID = splitMessageID;
            SplitMessageCount = splitMessageCount;
            SplitMessagesRecieved = splitMessagesRecieved;
        }
    }
}
