﻿//=============================================================================
// Project  : Ella Middleware
// File    : ApplicationMessage.cs
// Authors contact  : Bernhard Dieber (Bernhard.Dieber@aau.at)
// Copyright 2013 by Bernhard Dieber, Jennifer Simonjan
// This code is published under the Microsoft Public License (Ms-PL).  A copy
// of the license should be distributed with the code.  It can also be found
// at the project website: http://ella.CodePlex.com.   This notice, the
// author's name, and all copyright notices must remain intact in all
// applications, documentation, and source files.
//=============================================================================

using System;
using Ella.Internal;

namespace Ella.Control
{
    /// <summary>
    /// This class encapsulates an application-defined message
    /// </summary>
    [Serializable]
    public class ApplicationMessage
    {
        /// <summary>
        /// Gets or sets the message payload
        /// </summary>
        /// <value>
        /// The data.
        /// </value>
        public byte[] Data { get; set; }

        /// <summary>
        /// Gets or sets the type of the message.
        /// </summary>
        /// <value>
        /// The type of the message.
        /// </value>
        public short MessageType { get; set; }

        /// <summary>
        /// Gets or sets the message id.
        /// </summary>
        /// <value>
        /// The message id.
        /// </value>
        public int MessageId { get; set; }

        /// <summary>
        /// Gets the message sender, this is automatically set by Ella upon sending a message.
        /// </summary>
        /// <value>
        /// The sender.
        /// </value>
        internal int Sender { get; set; }

        /// <summary>
        /// The subscriptionhandle
        /// </summary>
        public SubscriptionHandle Handle { get; internal set; }

        /// <summary>
        /// Serializes an object and stores it in the <see cref="Data"/> property.<br />
        /// This uses a standard binary serializer
        /// </summary>
        /// <param name="payloadObject">The payload object.</param>
        public void SerializeData(object payloadObject)
        {
            Data = Serializer.Serialize(payloadObject);
        }

        /// <summary>
        /// Deserializes the <see cref="Data"/> property and returns an object of the given type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T DeserializeData<T>()
        {
            return (T)Serializer.Deserialize<T>(Data);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("ApplicationMessage {2} type {0} from {1}", MessageType, Sender, MessageId);
        }
    }
}
