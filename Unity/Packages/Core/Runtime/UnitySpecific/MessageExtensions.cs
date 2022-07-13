// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using UnityEngine;

namespace Riptide
{
    /// <summary>Contains Unity-specific extension methods for the <see cref="Message"/> class.</summary>
    public static class MessageExtensions
    {
        #region Vector2
        /// <inheritdoc cref="AddVector2(Message, Vector2)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddVector2(Message, Vector2)"/>.</remarks>
        public static Message Add(this Message message, Vector2 value) => AddVector2(message, value);

        /// <summary>Adds a <see cref="Vector2"/> to the message.</summary>
        /// <param name="value">The <see cref="Vector2"/> to add.</param>
        /// <returns>The message that the <see cref="Vector2"/> was added to.</returns>
        public static Message AddVector2(this Message message, Vector2 value)
        {
            return message.AddFloat(value.x).AddFloat(value.y);
        }

        /// <summary>Retrieves a <see cref="Vector2"/> from the message.</summary>
        /// <returns>The <see cref="Vector2"/> that was retrieved.</returns>
        public static Vector2 GetVector2(this Message message)
        {
            return new Vector2(message.GetFloat(), message.GetFloat());
        }
        #endregion

        #region Vector3
        /// <inheritdoc cref="AddVector3(Message, Vector3)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddVector3(Message, Vector3)"/>.</remarks>
        public static Message Add(this Message message, Vector3 value) => AddVector3(message, value);

        /// <summary>Adds a <see cref="Vector3"/> to the message.</summary>
        /// <param name="value">The <see cref="Vector3"/> to add.</param>
        /// <returns>The message that the <see cref="Vector3"/> was added to.</returns>
        public static Message AddVector3(this Message message, Vector3 value)
        {
            return message.AddFloat(value.x).AddFloat(value.y).AddFloat(value.z);
        }

        /// <summary>Retrieves a <see cref="Vector3"/> from the message.</summary>
        /// <returns>The <see cref="Vector3"/> that was retrieved.</returns>
        public static Vector3 GetVector3(this Message message)
        {
            return new Vector3(message.GetFloat(), message.GetFloat(), message.GetFloat());
        }
        #endregion

        #region Quaternion
        /// <inheritdoc cref="AddQuaternion(Message, Quaternion)"/>
        /// <remarks>This method is simply an alternative way of calling <see cref="AddQuaternion(Message, Quaternion)"/>.</remarks>
        public static Message Add(this Message message, Quaternion value) => AddQuaternion(message, value);

        /// <summary>Adds a <see cref="Quaternion"/> to the message.</summary>
        /// <param name="value">The <see cref="Quaternion"/> to add.</param>
        /// <returns>The message that the <see cref="Quaternion"/> was added to.</returns>
        public static Message AddQuaternion(this Message message, Quaternion value)
        {
            return message.AddFloat(value.x).AddFloat(value.y).AddFloat(value.z).AddFloat(value.w);
        }

        /// <summary>Retrieves a <see cref="Quaternion"/> from the message.</summary>
        /// <returns>The <see cref="Quaternion"/> that was retrieved.</returns>
        public static Quaternion GetQuaternion(this Message message)
        {
            return new Quaternion(message.GetFloat(), message.GetFloat(), message.GetFloat(), message.GetFloat());
        }
        #endregion
    }
}
