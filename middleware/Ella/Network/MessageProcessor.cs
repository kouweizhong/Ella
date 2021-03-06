﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Ella.Attributes;
using Ella.Control;
using Ella.Controller;
using Ella.Exceptions;
using Ella.Internal;
using Ella.Model;
using Ella.Network.Communication;
using log4net;

namespace Ella.Network
{
    internal class MessageProcessor
    {
        private ILog _log = LogManager.GetLogger(typeof(MessageProcessor));
        private readonly Dictionary<int, EndPoint> _remoteHosts = new Dictionary<int, EndPoint>();
        internal Dictionary<int, Type> SubscriptionCache { get; set; }

        internal Dictionary<int, Action<RemoteSubscriptionHandle>> PendingSubscriptions { get; private set; }

        public Dictionary<int, EndPoint> RemoteHosts
        {
            get { return _remoteHosts; }
        }

        internal MessageProcessor()
        {
            PendingSubscriptions = new Dictionary<int, Action<RemoteSubscriptionHandle>>();
            SubscriptionCache = new Dictionary<int, Type>();
        }

        /// <summary>
        /// Handles a new message from the network
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MessageEventArgs" /> instance containing the event data.</param>
        internal void NewMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Sender == EllaConfiguration.Instance.NodeId)
            {
                return;
            }
            _log.DebugFormat("New {1} message from {0} with id {2}", e.Address, e.Message.Type, e.Message.Id);
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
                case MessageType.ApplicationMessage:
                    {
                        ApplicationMessage msg = Serializer.Deserialize<ApplicationMessage>(e.Message.Data);
                        Send.DeliverApplicationMessage(msg, ((RemoteSubscriptionHandle)msg.Handle).SubscriberNodeID == EllaConfiguration.Instance.NodeId);
                        break;
                    }
                case MessageType.ApplicationMessageResponse:
                    {
                        ProcessApplicationMessageResponse(e);
                        break;
                    }
                case MessageType.EventCorrelation:
                    ProcessEventCorrelation(e);
                    break;
                case MessageType.NodeShutdown:
                    ProcessNodeShutdown(e);
                    break;
                case MessageType.NewPublisher:
                    ProcessNewPublisher(e);
                    break;

            }
        }


        #region Discovery


        /// <summary>
        /// Processes the discovery.
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        private void ProcessDiscover(MessageEventArgs e)
        {
            /*
             * Respond to a discovery message
             */
            int port = BitConverter.ToInt32(e.Message.Data, 4);
            IPEndPoint ep = (IPEndPoint)e.Address;
            ep.Port = port;
            if (!RemoteHosts.ContainsKey(e.Message.Sender))
            {
                _log.InfoFormat("Discovered host {0} on {1}", e.Message.Sender, ep);
                try
                {
                    if (!AddRemoteHost(e, ep))
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _log.ErrorFormat("Could not add sender {0} to remotehosts list due to {1}", e.Message.Sender,
                        ex.GetType().Name);
                }
            }
            else
            {
                //TODO is there an exception here???

                if ((RemoteHosts[e.Message.Sender] as IPEndPoint).Address.ToString() == ep.Address.ToString())
                    _log.WarnFormat("Duplicate discovery message from node {0}", e.Message.Sender);
                else
                {
                    _log.WarnFormat("Duplicate discovery message from node {0} from different address. Stored {1} New {2}", e.Message.Sender, (RemoteHosts[e.Message.Sender] as IPEndPoint).Address.ToString(), ep.Address.ToString());
                }
                //return;
            }
            byte[] portBytes = BitConverter.GetBytes(EllaConfiguration.Instance.NetworkPort);

            Message m = new Message { Type = MessageType.DiscoverResponse, Data = portBytes };
            SenderBase.SendMessage(m, ep);

            /*
             * EnqueueMessage a message to the new host and inquire about possible new publishers
             */

            Thread.Sleep(2000);
            RetryPendingSubscriptions(ep);
        }

        private void RetryPendingSubscriptions(IPEndPoint ep, Type type = null)
        {
            _log.DebugFormat("Retrying to find publishers for pending subscriptions {0}", type == null ? string.Empty : string.Format("of type {0}", type.Name));
            try
            {
                var cache = type == null ? SubscriptionCache.ToArray() : SubscriptionCache.Where(c => c.Value == type).ToArray();
                foreach (var sub in cache)
                {
                    if (type != null && sub.Value != type)
                        return;

                    Message subscription = new Message(sub.Key)
                    {
                        Type = MessageType.Subscribe,
                        Data = Serializer.Serialize(sub.Value)
                    };

                    _log.DebugFormat("Resending subscription request message {0}", subscription.Id);
                    SenderBase.SendAsync(subscription, ep);
                }
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Could not retry pending subscriptions: {0}\n{1}", ex.Message, ex.StackTrace);
                return;
            }

        }


        private bool AddRemoteHost(MessageEventArgs e, IPEndPoint ep)
        {
            bool added = false;
            lock (_remoteHosts)
            {
                if (!_remoteHosts.ContainsKey(e.Message.Sender))
                {
                    _log.DebugFormat("Adding new remote Host {0}", ep);
                    RemoteHosts.Add(e.Message.Sender, ep);
                    added = true;
                }
                else
                {
                    _log.DebugFormat("Not adding host {0} since its already in the list", e.Message.Sender);
                }
            }
            string hostlist =
                     (from r in RemoteHosts.Values select r.ToString()).Aggregate((s1, s2) => s1 + "\n" + s2);
            _log.DebugFormat("New Host list, length {1}, values {0}", hostlist, _remoteHosts.Count);
            return added;
        }

        /// <summary>
        /// Processes the discovery response.
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        private void ProcessDiscoverResponse(MessageEventArgs e)
        {
            if (!RemoteHosts.ContainsKey(e.Message.Sender))
            {
                int port = BitConverter.ToInt32(e.Message.Data, 0);
                IPEndPoint ep = (IPEndPoint)e.Address;
                ep.Port = port;
                _log.InfoFormat("Host {0} on {1} responded to discovery", e.Message.Sender, ep);
                AddRemoteHost(e, ep);
                RetryPendingSubscriptions(ep);
            }
        }
        #endregion
        #region Subscribe

        /// <summary>
        /// Processes the subscribe response.
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        private void ProcessSubscribeResponse(MessageEventArgs e)
        {
            if (e.Message.Data.Length > 0)
            {
                int inResponseTo = BitConverter.ToInt32(e.Message.Data, 0);
                ICollection<RemoteSubscriptionHandle> handles =
                    Serializer.Deserialize<ICollection<RemoteSubscriptionHandle>>(e.Message.Data, 4);
                //var stubs = from s in EllaModel.Instance.Subscriptions
                //            where s.Event.Publisher is Stub && (s.Event.Publisher as Stub).DataType == type
                //            select s;

                if (PendingSubscriptions.ContainsKey(inResponseTo))
                {
                    Action<RemoteSubscriptionHandle> action = PendingSubscriptions[inResponseTo];
                    foreach (var handle in handles)
                    {
                        handle.PublisherNodeID = e.Message.Sender;
                        action(handle);
                    }
                }
                else
                {
                    _log.WarnFormat("Detected invalid subscription reference {0}, may be from previous application run. Unsubscribing...",
                        inResponseTo);
                    //TODO this is not a clean solution, might clash with other msgIDs
                    Networking.Unsubscribe(inResponseTo, e.Message.Sender);
                }
            }
            else
            {
                _log.Debug("Received SubscriptionResponse message with no payload");
            }
        }

        /// <summary>
        /// Processes the subscription.
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        private void ProcessSubscribe(MessageEventArgs e)
        {
            Type type = null;
            try
            {
                type = Serializer.Deserialize<Type>(e.Message.Data);
                _log.DebugFormat("Processing remote subscription request for {0}", type.Name);
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Could not process remote subscription request. {0}", ex.Message);
                return;
            }
            //get the subscriptions that this node is already subscribed for, to avoid double subscriptions
            var currentHandles = (from s1 in (EllaModel.Instance.FilterSubscriptions(s => s.Event.EventDetail.DataType == type).Select(s => s.Handle)).OfType<RemoteSubscriptionHandle>()
                                  where s1.SubscriberNodeID == e.Message.Sender
                                  select s1).ToList().GroupBy(s => s.SubscriptionReference);

            IEnumerable<RemoteSubscriptionHandle> handles = SubscriptionController.SubscribeRemoteSubscriber(type, e.Message.Sender,
                (IPEndPoint)
                    RemoteHosts[e.Message.Sender],
                e.Message.Id);

            /*
             * Sending reply
             */
            IPEndPoint ep = (IPEndPoint)RemoteHosts[e.Message.Sender];
            if (ep == null)
            {
                _log.ErrorFormat("No suitable endpoint to reply to subscription found");
                return;
            }
            if (handles != null)
            {
                //EnqueueMessage reply
                byte[] handledata = Serializer.Serialize(handles);
                byte[] reply = new byte[handledata.Length + 4];
                byte[] idbytes = BitConverter.GetBytes(e.Message.Id);
                Array.Copy(idbytes, reply, idbytes.Length);
                Array.Copy(handledata, 0, reply, idbytes.Length, handledata.Length);
                Message m = new Message { Type = MessageType.SubscribeResponse, Data = reply };
                _log.DebugFormat("Replying to subscription request at {0}", ep);

                SenderBase.SendAsync(m, ep);
            }
            /* 
                 * Notify about previous subscriptions on the same type by the same node
                 */
            foreach (var currentHandle in currentHandles)
            {
                //EnqueueMessage reply
                byte[] handledata = Serializer.Serialize(currentHandle.ToList());
                byte[] reply = new byte[handledata.Length + 4];
                byte[] idbytes = BitConverter.GetBytes(currentHandle.Key);
                Array.Copy(idbytes, reply, idbytes.Length);
                Array.Copy(handledata, 0, reply, idbytes.Length, handledata.Length);
                Message m = new Message { Type = MessageType.SubscribeResponse, Data = reply };

                _log.DebugFormat("Sending previous subscription information to {0}", ep);
                SenderBase.SendAsync(m, ep);
            }
            _log.Debug("Checking for relevant event correlations");
            Thread.Sleep(1000);
            if (handles != null)
            {
                /*
                 * Process event correlations based on the subscription handles from the subscribe process
                 */
                foreach (var handle in handles)
                {
                    /*
                     * 1. search for correlations of this handle
                     * 2. send a message for each correlation#
                     */
                    var correlations = EllaModel.Instance.GetEventCorrelations(handle.EventHandle).ToArray();
                    foreach (var correlation in correlations)
                    {
                        KeyValuePair<EventHandle, EventHandle> pair =
                            new KeyValuePair<EventHandle, EventHandle>(handle.EventHandle, correlation);
                        Message m = new Message()
                        {
                            Type = MessageType.EventCorrelation,
                            Data = Serializer.Serialize(pair)
                        };
                        SenderBase.SendAsync(m, ep);
                    }
                }
            }
        }

        /// <summary>
        /// Processes the unsubscribe.
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        internal void ProcessUnsubscribe(MessageEventArgs e)
        {
            int ID = e.Message.Id;

            List<SubscriptionBase> remoteSubs =
                EllaModel.Instance.FilterSubscriptions(s => s.Handle is RemoteSubscriptionHandle).ToList();

            remoteSubs = remoteSubs.FindAll(s => (s.Handle as RemoteSubscriptionHandle).SubscriptionReference == ID);

            foreach (var sub in remoteSubs)
            {
                Ella.Unsubscribe.From(sub.Subscriber as Proxy, sub.Handle);
            }

        }
        #endregion
        #region Publish
        /// <summary>
        /// Processes the publish.
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        private void ProcessPublish(MessageEventArgs e)
        {
            /*
                * Remote publisher is identified by
                * Remote node ID (contained in the message object)
                * Remote publisher ID
                * Remote publisher-event ID
                * The message reference (message ID used for the subscribe message)
                * SubscriptionRequest node ID
                * Assume shorts for all
                *   
            */
            short publisherID = BitConverter.ToInt16(e.Message.Data, 0);
            short eventID = BitConverter.ToInt16(e.Message.Data, 2);
            RemoteSubscriptionHandle handle = new RemoteSubscriptionHandle
            {
                EventID = eventID,
                PublisherId = publisherID,
                PublisherNodeID = e.Message.Sender,
                SubscriberNodeID = EllaConfiguration.Instance.NodeId
            };
            byte[] data = new byte[e.Message.Data.Length - 4];
            Buffer.BlockCopy(e.Message.Data, 4, data, 0, data.Length);
            var subscriptions = EllaModel.Instance.FilterSubscriptions(s =>
            {
                var h = (s.Handle as RemoteSubscriptionHandle);

                return h != null && CompareSubscriptionHandles(handle, h);
            });
            var subs = subscriptions as SubscriptionBase[] ?? subscriptions.ToArray();
            _log.DebugFormat("Found {0} subscriptions for handle {1}", subs.Length, handle);
            foreach (var sub in subs)
            {
                (sub.Event.Publisher as Stub).NewMessage(data);
            }
        }

        private static bool CompareSubscriptionHandles(RemoteSubscriptionHandle handle, RemoteSubscriptionHandle h)
        {
            return
                handle.EventID == h.EventID &&
                handle.PublisherNodeID == h.PublisherNodeID &&
                handle.PublisherId == h.PublisherId &&
                handle.SubscriberNodeID == h.SubscriberNodeID;
        }
        #endregion
        /// <summary>
        /// Processes the application message response.
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        private void ProcessApplicationMessageResponse(MessageEventArgs e)
        {
            ApplicationMessage msg = Serializer.Deserialize<ApplicationMessage>(e.Message.Data);
            Send.DeliverApplicationMessage(msg, ((RemoteSubscriptionHandle)msg.Handle).SubscriberNodeID == EllaConfiguration.Instance.NodeId);
        }

        /// <summary>
        /// Processes the event correlation.
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        /// <exception cref="IllegalAttributeUsageException"></exception>
        private void ProcessEventCorrelation(MessageEventArgs e)
        {
            var correlation = Serializer.Deserialize<KeyValuePair<EventHandle, EventHandle>>(e.Message.Data);
            EventHandle first = correlation.Key;
            EventHandle second = correlation.Value;
            /*
             * Add to list of correlations?
             * Deliver to subscribers
             *      The subscriptions point directly to the subscriber instances, the handle is matching already
             */
            var groupings = EllaModel.Instance.FilterSubscriptions(s => true).GroupBy(s => s.Subscriber);
            var relevantsubscribers = groupings
                    .Where(
                        g =>
                            g.Any(g1 => Equals(g1.Handle.EventHandle, first)) &&
                            g.Any(g2 => Equals(g2.Handle.EventHandle, second)));
            var results = relevantsubscribers
                    .Select(
                        g =>
                            new
                            {
                                Object = g.Key,
                                Method =
                                    ReflectionUtils.GetAttributedMethod(g.Key.GetType(), typeof(AssociateAttribute))
                            });
            _log.DebugFormat("Found {0} relevant subscribers for event correlation", results.Count());
            foreach (var result in results)
            {
                if (result.Method != null)
                {
                    if (result.Method.GetParameters().Count() != 2 || result.Method.GetParameters().Any(p => p.ParameterType != typeof(SubscriptionHandle)))
                        throw new IllegalAttributeUsageException(String.Format("Method {0} attributed as Associate has invalid parameters (count or type)", result.Method));
                    int subscriberid = EllaModel.Instance.GetSubscriberId(result.Object);
                    var subscriber = relevantsubscribers.Single(g => g.Key == result.Object);
                    var subscription = subscriber
                        .Where(s => Equals(s.Handle.EventHandle, first));
                    SubscriptionHandle handle1 = subscription
                        .Select(s => s.Handle).First();
                    SubscriptionHandle handle2 = relevantsubscribers.Where(g => g.Key == result.Object).First().Where(s => Equals(s.Handle.EventHandle, second)).Select(s => s.Handle).First();
                    result.Method.Invoke(result.Object, new object[] { handle1, handle2 });
                    result.Method.Invoke(result.Object, new object[] { handle2, handle1 });
                }
            }
        }

        /// <summary>
        /// Processes the node shutdown.
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        private void ProcessNodeShutdown(MessageEventArgs e)
        {
            /*
             * Delete all subscriptions to this node
             * Delete all subscriptions from this node
             * Delete node from node dictionary
             * 
             * No communications to the node should be done, since it's shutdown already
             */
            //Subscriptions where local subscribers are subscribed to publishers from the node being shut down
            //in this case, the local stubs must be stopped and the subscriptions removed from the list
            SubscriptionController.PerformUnsubscribe(s => s.Handle.EventHandle.PublisherNodeId == e.Message.Sender, performRemoteUnsubscribe: false);
            //Subscriptions of modules on the remote node being subscribed to local publishers
            //Here, the proxy
            var subscriptionsFromRemoteNode = EllaModel.Instance.FilterSubscriptions(s => s.Handle is RemoteSubscriptionHandle).Where(s => (s.Handle as RemoteSubscriptionHandle).SubscriberNodeID == e.Message.Sender);
            foreach (var sub in subscriptionsFromRemoteNode)
            {
                Ella.Unsubscribe.From(sub.Subscriber as Proxy, sub.Handle);
            }

            RemoteHosts.Remove(e.Message.Sender);
        }

        /// <summary>
        /// Processes a "new publisher"- message. That message indicates that on a remote host, a new publisher has been started. As a consequence, the local node tries to resend old subscription requests
        /// </summary>
        /// <param name="e">The <see cref="MessageEventArgs"/> instance containing the event data.</param>
        private void ProcessNewPublisher(MessageEventArgs e)
        {
            Type type = null;
            if (e.Message.Data != null)
            {
                try
                {
                    type = Serializer.Deserialize<Type>(e.Message.Data);
                }
                catch (Exception ex)
                {
                    //TODO what to log here? if type is unknnown, this is not a problem
                }
            }
            IPEndPoint ep = (IPEndPoint)e.Address;


            RetryPendingSubscriptions((IPEndPoint)_remoteHosts[e.Message.Sender], type);
        }

    }
}
