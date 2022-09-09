using UnityEngine;

namespace Riptide.Demos.DedicatedClient
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform camTransform;
        private bool[] inputs;

        private void Start()
        {
            inputs = new bool[5];
        }

        private void Update()
        {
            // Sample inputs every frame and store them until they're sent. This ensures no inputs are missed because they happened between FixedUpdate calls
            if (Input.GetKey(KeyCode.W))
                inputs[0] = true;

            if (Input.GetKey(KeyCode.S))
                inputs[1] = true;

            if (Input.GetKey(KeyCode.A))
                inputs[2] = true;

            if (Input.GetKey(KeyCode.D))
                inputs[3] = true;

            if (Input.GetKey(KeyCode.Space))
                inputs[4] = true;
        }

        private void FixedUpdate()
        {
            SendInput();

            // Reset input booleans
            for (int i = 0; i < inputs.Length; i++)
                inputs[i] = false;
        }

        #region Messages
        private void SendInput()
        {
            Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.PlayerInput);
            message.AddBools(inputs, false);
            message.AddVector3(camTransform.forward);
            NetworkManager.Singleton.Client.Send(message);
        }
        #endregion
    }
}
