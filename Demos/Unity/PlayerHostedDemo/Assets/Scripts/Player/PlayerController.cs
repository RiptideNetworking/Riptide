
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2022 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking;
using UnityEngine;

namespace RiptideDemos.RudpTransport.Unity.PlayerHosted
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Player player;
        [SerializeField] private CharacterController controller;
        [SerializeField] private float gravity;
        [SerializeField] private float moveSpeed;
        [SerializeField] private float jumpSpeed;

        private bool[] inputs;
        private float yVelocity;

        private void OnValidate()
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();

            if (player == null)
                player = GetComponent<Player>();
        }

        private void Start()
        {
            gravity *= Time.fixedDeltaTime * Time.fixedDeltaTime;
            moveSpeed *= Time.fixedDeltaTime;
            jumpSpeed *= Time.fixedDeltaTime;

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
            Vector2 inputDirection = Vector2.zero;
            if (inputs[0])
                inputDirection.y += 1;

            if (inputs[1])
                inputDirection.y -= 1;

            if (inputs[2])
                inputDirection.x -= 1;

            if (inputs[3])
                inputDirection.x += 1;

            Move(inputDirection);

            for (int i = 0; i < inputs.Length; i++)
                inputs[i] = false;
        }

        private void Move(Vector2 inputDirection)
        {
            Vector3 moveDirection = transform.right * inputDirection.x + transform.forward * inputDirection.y;
            moveDirection *= moveSpeed;

            if (controller.isGrounded)
            {
                yVelocity = 0f;
                if (inputs[4])
                    yVelocity = jumpSpeed;
            }
            yVelocity += gravity;

            moveDirection.y = yVelocity;
            controller.Move(moveDirection);

            SendMovement();
        }

        #region Messages
        private void SendMovement()
        {
            Message message = Message.Create(MessageSendMode.unreliable, MessageId.playerMovement, shouldAutoRelay: true);
            message.AddUShort(player.Id);
            message.AddVector3(transform.position);
            message.AddVector3(transform.forward);
            NetworkManager.Singleton.Client.Send(message);
        }
        #endregion
    }
}
