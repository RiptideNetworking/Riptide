// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;

namespace Riptide.Utils
{
    /// <summary>
    /// Extensions to add and get structs. Using XMLSerializer.
    /// </summary>
    public static class StructConvertor
    {
        ///<inheritdoc cref="AddStruct{T}(Message, T)"/>
        ///<remarks>Just an alternative way of calling AddStruct.</remarks>
        public static Message Add<T>(this Message message, T value) where T : struct => AddStruct(message, value);

        ///<inheritdoc cref="AddStructs{T}(Message, T[], bool)"/>
        ///<remarks>Just an alternative way of calling AddStructs.</remarks>
        public static Message Add<T>(this Message message, T[] value) where T : struct => AddStructs(message, value);

        /// <summary>
        /// Adds a struct to the message.
        /// Only Adds public fields.
        /// </summary>
        /// <typeparam name="T">Type of the struct</typeparam>
        /// <param name="message">Instance of message, handled by extension</param>
        /// <param name="value">struct to add to message</param>
        /// <returns>Message instance.</returns>
        public static Message AddStruct<T>(this Message message, T value) where T : struct
        {
            using(MemoryStream stream = new MemoryStream())
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                serializer.Serialize(stream, value);
                message.AddBytes(stream.ToArray());
                return message;
            }
        }

        /// <summary>
        /// Adds multiple structs of the same type to the message.
        /// Only Adds public fields.
        /// </summary>
        /// <typeparam name="T">Type of the struct</typeparam>
        /// <param name="message">Instance of message, handled by extension</param>
        /// <param name="values">struct to add to message</param>
        /// <returns>Message instance.</returns>
        public static Message AddStructs<T>(this Message message, T[] values, bool includeLength = true) where T : struct
        {
            if (includeLength)
                message.AddInt(values.Length);

            for (int i = 0; i < values.Length; i++)
            {
                AddStruct(message, values[i]);
            }
            return message;
        }

        /// <summary>
        /// Gets a singular struct from the message.
        /// </summary>
        /// <typeparam name="T">Type of the struct</typeparam>
        /// <param name="message">Instance of message, handled by extension</param>
        /// <returns>Struct</returns>
        public static T GetStruct<T>(this Message message) where T : struct
        {
            using (MemoryStream stream = new MemoryStream(message.GetBytes()))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                return (T)serializer.Deserialize(stream);
            }
        }

        /// <summary>
        /// Gets an array of the same struct
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <returns>Structs</returns>
        public static T[] GetStructs<T>(this Message message) where T : struct
        {
            int lenght = message.GetInt();
            T[] values = new T[lenght];

            for (int i = 0; i < lenght; i++)
            {
                values[i] = GetStruct<T>(message);
            }

            return values;
        }
    }
}
