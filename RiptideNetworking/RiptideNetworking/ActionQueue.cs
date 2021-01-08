using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    public class ActionQueue
    {
        public readonly string logName;
        private readonly List<Action> executionQueue = new List<Action>();
        private readonly List<Action> executionQueueCopy = new List<Action>();
        private bool hasActionToExecute = false;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name of this action queue instance. Used when logging messages.</param>
        public ActionQueue(string logName = "ACTION QUEUE")
        {
            this.logName = logName;
        }

        /// <summary>Adds an action to the queue.</summary>
        /// <param name="action">The action to be added to the queue.</param>
        public void Add(Action action)
        {
            if (action == null)
            {
                RiptideLogger.Log(logName, "No action to execute!");
                return;
            }

            lock (executionQueue)
            {
                executionQueue.Add(action);
                hasActionToExecute = true;
            }
        }

        /// <summary>Executes all actions in the queue on the calling thread.</summary>
        public void ExecuteAll()
        {
            if (hasActionToExecute)
            {
                // If there's an action in the queue
                executionQueueCopy.Clear(); // Clear the old actions from the copied queue
                lock (executionQueue)
                {
                    executionQueueCopy.AddRange(executionQueue); // Copy actions from the queue to the copied queue
                    executionQueue.Clear(); // Clear the actions from the queue
                    hasActionToExecute = false;
                }

                // Execute all actions from the copied queue
                for (int i = 0; i < executionQueueCopy.Count; i++)
                {
                    executionQueueCopy[i]();
                }
            }
        }
    }
}
