﻿//=============================================================================
// Project  : Ella Middleware
// File    : NetworkController.Static.cs
// Authors contact  : Bernhard Dieber (Bernhard.Dieber@aau.at)
// Copyright 2013 by Bernhard Dieber, Jennifer Simonjan
// This code is published under the Microsoft Public License (Ms-PL).  A copy
// of the license should be distributed with the code.  It can also be found
// at the project website: http://ella.CodePlex.com.   This notice, the
// author's name, and all copyright notices must remain intact in all
// applications, documentation, and source files.
//=============================================================================

using System;
using System.Net;
using Ella.Control;
using Ella.Internal;
using Ella.Network.Communication;
using log4net;

namespace Ella.Network
{
    internal partial class NetworkController
    {
        private static readonly NetworkController _instance = new NetworkController();
        private static ILog _log = LogManager.GetLogger(typeof(NetworkController));


        internal static bool IsRunning { get { return _instance._server != null; } }

        /// <summary>
        /// Starts the network controller.
        /// </summary>
        internal static void Start()
        {
            _instance._udpServer = new UdpServer(EllaConfiguration.Instance.NetworkPort);
            _instance._udpServer.NewMessage += _instance.NewMessage;

            _instance._udpServer.Start();
            _instance._server = new Server(EllaConfiguration.Instance.NetworkPort, IPAddress.Any);
            _instance._server.NewMessage += _instance.NewMessage;
            _instance._server.Start();
            Sender.Broadcast();
        }
        /// <summary>
        /// Subscribes to remote host.
        /// </summary>
        /// <typeparam name="T">The type to subscribe to</typeparam>
        internal static void SubscribeToRemoteHost<T>(Action<RemoteSubscriptionHandle> callback)
        {
            _instance.SubscribeTo(typeof(T), callback);
        }

        /// <summary>
        /// Sends the application message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="remoteSubscriptionHandle">The remote subscription handle.</param>
        /// <param name="isReply">if set to <c>true</c>, this is a reply to another message.</param>
        /// <returns></returns>
        internal static bool SendApplicationMessage(ApplicationMessage message, RemoteSubscriptionHandle remoteSubscriptionHandle, bool isReply = false)
        {
            return _instance.SendMessage(message, remoteSubscriptionHandle,isReply);
        }

        /// <summary>
        /// Unsubscribes the specified node from the subscription defined by the subscription reference.
        /// </summary>
        /// <param name="subscriptionReference">The subscription reference.</param>
        /// <param name="nodeId">The node id.</param>
        internal static void Unsubscribe(int subscriptionReference, int nodeId)
        {
            _instance.UnsubscribeFrom(subscriptionReference, nodeId);
        }

        /// <summary>
        /// Broadcasts the shutdown.
        /// </summary>
        internal static void BroadcastShutdown()
        {
            _instance.SendShutdownMessage();
        }

        /// <summary>
        /// Connects to multicast group.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <param name="port">The port.</param>
        internal static void ConnectToMulticast(string group, int port)
        {
            _instance._udpServer.ConnectToMulticastGroup(group,port);
    }
    }
}
