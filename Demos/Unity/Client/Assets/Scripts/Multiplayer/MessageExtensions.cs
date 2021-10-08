using RiptideNetworking;
using UnityEngine;

public static class MessageExtensions
{
    #region Add
    /// <summary>Adds a Vector2 to the message.</summary>
    /// <param name="value">The Vector2 to add.</param>
    public static void Add(this Message message, Vector2 value)
    {
        message.Add(value.x);
        message.Add(value.y);
    }
    /// <summary>Adds a Vector3 to the message.</summary>
    /// <param name="value">The Vector3 to add.</param>
    public static void Add(this Message message, Vector3 value)
    {
        message.Add(value.x);
        message.Add(value.y);
        message.Add(value.z);
    }
    /// <summary>Adds a Quaternion to the message.</summary>
    /// <param name="value">The Quaternion to add.</param>
    public static void Add(this Message message, Quaternion value)
    {
        message.Add(value.x);
        message.Add(value.y);
        message.Add(value.z);
        message.Add(value.w);
    }
    #endregion

    #region Get
    /// <summary>Reads a Vector2 from the message.</summary>
    public static Vector2 GetVector2(this Message message)
    {
        return new Vector2(message.GetFloat(), message.GetFloat());
    }

    /// <summary>Reads a Vector3 from the message.</summary>
    public static Vector3 GetVector3(this Message message)
    {
        return new Vector3(message.GetFloat(), message.GetFloat(), message.GetFloat());
    }

    /// <summary>Reads a Quaternion from the message.</summary>
    public static Quaternion GetQuaternion(this Message message)
    {
        return new Quaternion(message.GetFloat(), message.GetFloat(), message.GetFloat(), message.GetFloat());
    }
    #endregion
}