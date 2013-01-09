﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using Ella.Internal;
using Ella.Model;
using Ella.Network.Communication;
using log4net;

namespace Ella.Network
{
    internal partial class NetworkController
    {
        private static readonly NetworkController _instance = new NetworkController();

        private Server _server;
        private readonly Dictionary<int, EndPoint> _remoteHosts = new Dictionary<int, EndPoint>();

        private static ILog _log = LogManager.GetLogger(typeof(NetworkController));

        private Dictionary<int, Action<RemoteSubscriptionHandle>> _pendingSubscriptions =
            new Dictionary<int, Action<RemoteSubscriptionHandle>>();

        /// <summary>
        /// Starts the network controller.
        /// </summary>
        internal static void Start()
        {

            _instance._server = new Server(EllaConfiguration.Instance.NetworkPort, IPAddress.Any);
            _instance._server.NewMessage += _instance.NewMessage;
            _instance._server.Start();
            Client.Broadcast();
        }


        /// <summary>
        /// Subscribes to remote host.
        /// </summary>
        /// <typeparam name="T">The type to subscribe to</typeparam>
        internal static void SubscribeToRemoteHost<T>(Action<RemoteSubscriptionHandle> callback)
        {
            _instance.SubscribeTo(typeof(T), callback);
        }

        internal static bool IsRunning { get { return _instance._server != null; } }

        /// <summary>
        /// Subscribes to a remote host.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="callback"></param>
        private void SubscribeTo(Type type, Action<RemoteSubscriptionHandle> callback)
        {
            Message m = new Message { Type = MessageType.Subscribe, Data = Serializer.Serialize(type) };
            //TODO when to remove?
            _pendingSubscriptions.Add(m.Id, callback);
            foreach (IPEndPoint address in _remoteHosts.Values)
            {
                Client.SendAsync(m, address.Address.ToString(), address.Port);
            }
        }


        /// <summary>
        /// Handles a new message from the network
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MessageEventArgs" /> instance containing the event data.</param>
        private void NewMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Sender == EllaConfiguration.Instance.NodeId)
                return;
            _log.DebugFormat("New {1} message from {0}", e.Address, e.Message.Type);
            switch (e.Message.Type)
            {
                case MessageType.Discover:
                    {
                        ProcessDiscover(e);
                        break;
                    }
                case MessageType.DiscoverResponse:
                    {
                        ProcessDiscoverResponse(e);
                        break;
                    }
                case MessageType.Publish:
                    {
                        ProcessPublish(e);

                        break;
                    }
                case MessageType.Subscribe:
                    {
                        ProcessSubscribe(e);
                        break;
                    }
                case MessageType.SubscribeResponse:
                    {
                        ProcessSubscribeResponse(e);
                        break;
                    }
                case MessageType.Unsubscribe:
                    {
                        ProcessUnsubscribe(e);
                        break;
                    }
            }
        }
    }
}
