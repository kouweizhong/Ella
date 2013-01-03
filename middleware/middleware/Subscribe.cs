﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Ella.Attributes;
using Ella.Internal;
using Ella.Data;
using Ella.Internal;
using Ella.Model;
using Ella.Network;
using log4net;

namespace Ella
{
    /// <summary>
    /// This class allows access to subscriptions, it provides facilities to make new subscriptions
    /// </summary>
    public static class Subscribe
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Subscribe));
        /// <summary>
        /// Subscribes the <paramref name="subscriberInstance" /> to any event matching <typeparamref name="T" /> as event data type
        /// </summary>
        /// <typeparam name="T">The type to subscribe to</typeparam>
        /// <param name="subscriberInstance">The instance of a subscriber to be subscribed to the event</param>
        /// <param name="newDataCallback">A callback method acceptiong <typeparamref name="T" /> as argument, which will be called when new data from publishers is available</param>
        /// <param name="policy">The data modify policy, default is <see cref="DataModifyPolicy.NoModify" /></param>
        /// <param name="evaluateTemplateObject">Pass a Func to subscribe using template objects. If no Func is given, <paramref name="subscriberInstance" /> will be subscribed to every event with matching <typeparamref name="T" />.<br />
        /// As an alternative, a <paramref name="evaluateTemplateObject" /> can be provided to request templates for the data to be published from every single publisher.</param>
        /// <param name="forbidRemote">if set to <c>true</c> no remote publishers will be considered.</param>
        /// <exception cref="System.ArgumentException">subscriberInstance must be a valid subscriber</exception>
        public static void To<T>(object subscriberInstance, Action<T> newDataCallback, DataModifyPolicy policy = DataModifyPolicy.NoModify, Func<T, bool> evaluateTemplateObject = null, bool forbidRemote = false)
        {
            _log.DebugFormat("Subscribing {0} to type {1} {2}", subscriberInstance, typeof(T),
                             (evaluateTemplateObject != null ? "with template object" : string.Empty));

            EllaModel.Instance.AddActiveSubscriber(subscriberInstance);
            if (!forbidRemote)
            {
                if (NetworkController.IsRunning)
                {
                    //Stub s = new Stub { DataType = typeof(T) };
                    //Event e = new Event { Publisher = s, EventDetail = new Attributes.PublishesAttribute(typeof(T), 1) };
                    //Start.Publisher(s);
                    //EllaModel.Instance.Subscriptions.Add(new Subscription<T>(subscriberInstance, e, newDataCallback));
                    Func<T, bool> eval = evaluateTemplateObject;
                    Action<RemoteSubscriptionHandle> callback =
                        handle => ToRemotePublisher<T>(handle, subscriberInstance, newDataCallback, policy,
                                                       eval);
                    NetworkController.SubscribeToRemoteHost<T>(callback);
                }
            }
            if (evaluateTemplateObject == null)
                evaluateTemplateObject = (o => true);

            DoLocalSubscription(subscriberInstance, newDataCallback, evaluateTemplateObject);

        }

        private static void DoLocalSubscription<T>(object subscriberInstance, Action<T> newDataCallback,
                                                   Func<T, bool> evaluateTemplateObject)
        {
            /*
                         * find all matching events from currently active publishers
                         * check if subscriber instace is valid subscriber
                         * hold a list of subscriptions
                         */
            if (!Is.Subscriber(subscriberInstance.GetType()))
            {
                _log.ErrorFormat("{0} is not a valid subscriber", subscriberInstance.GetType().ToString());
                throw new ArgumentException("subscriberInstance must be a valid subscriber");
            }
            var matches = EllaModel.Instance.ActiveEvents.FirstOrDefault(g => g.Key == typeof(T));

            if (matches != null)
            {
                _log.DebugFormat("Found {0} matches for subsription to {1}", matches.Count(), typeof(T));
                foreach (var m in matches)
                {
                    object templateObject = Create.TemplateObject(m.Publisher, m.EventDetail.ID);
                    T template = templateObject != null ? (T)templateObject : default(T);
                    //SubscriptionID in the handle is set automatically when assigning it to a subscription
                    SubscriptionHandle handle = new SubscriptionHandle
                    {
                        EventID = m.EventDetail.ID,
                        PublisherId = EllaModel.Instance.GetPublisherId(m.Publisher),
                        SubscriberId = EllaModel.Instance.GetSubscriberId(subscriberInstance)
                    };

                    var subscription = new Subscription<T>
                    {
                        Event = m,
                        Subscriber = subscriberInstance,
                        Callback = newDataCallback,
                        Handle = handle
                    };
                    if (templateObject == null || evaluateTemplateObject(template))
                    {
                        if (!EllaModel.Instance.Subscriptions.Contains(subscription))
                        {
                            _log.InfoFormat("Subscribing {0} to {1} for type {2}", subscriberInstance, m.Publisher,
                                            m.EventDetail.DataType);
                            EllaModel.Instance.Subscriptions.Add(subscription);
                        }
                    }
                    else
                        _log.DebugFormat("Templateobject from {0} was rejected by {1}", m.Publisher, subscriberInstance);
                }
            }
        }


        /// <summary>
        /// Performs a local subscription for a remote subscriber
        /// </summary>
        /// <param name="type">The type to subscribe to.</param>
        /// <param name="nodeId">The node id of the remote node.</param>
        internal static IEnumerable<RemoteSubscriptionHandle> RemoteSubscriber(Type type, int nodeId, IPEndPoint subscriberAddress)
        {

            _log.DebugFormat("Performing remote subscription for type {0}", type);
            var matches = EllaModel.Instance.ActiveEvents.FirstOrDefault(g => g.Key == type);


            if (matches != null)
            {
                List<RemoteSubscriptionHandle> handles = new List<RemoteSubscriptionHandle>();
                foreach (var match in matches)
                {
                    var proxy = new Proxy() { EventToHandle = match, TargetNode = subscriberAddress };
                    EllaModel.Instance.AddActiveSubscriber(proxy);
                    RemoteSubscriptionHandle handle = new RemoteSubscriptionHandle
                        {
                            EventID = match.EventDetail.ID,
                            PublisherId = EllaModel.Instance.GetPublisherId(match.Publisher),
                            RemoteNodeID = nodeId,
                            SubscriberId = EllaModel.Instance.GetSubscriberId(proxy)
                        };
                    SubscriptionBase subscription = ReflectionUtils.CreateGenericSubscription(type, match, proxy);
                    subscription.Handle = handle;
                    _log.InfoFormat("Subscribing remote subscriber to {0} for type {1}", match.Publisher,
                                    match.EventDetail.DataType);
                    EllaModel.Instance.Subscriptions.Add(subscription);
                    handles.Add(handle);
                }
                return handles;
            }
            return null;
        }

        /// <summary>
        /// Performs a subscription to a remote publisher for a local subscriber
        /// </summary>
        internal static void ToRemotePublisher<T>(RemoteSubscriptionHandle handle, object subscriberInstance, Action<T> newDataCallBack, DataModifyPolicy policy, Func<T, bool> evaluateTemplateObject)
        {
            _log.DebugFormat("Completing subscription to remote publisher {0} on node {1}, remote event id: {2}",
                             handle.PublisherId, handle.RemoteNodeID, handle.EventID);
            //TODO template object

            //Create a stub
            Stub s = new Stub {DataType = typeof(T), Handle = handle};
            Start.Publisher(s);
            Event ev = new Event
                {
                    Publisher = s,
                    EventDetail = (PublishesAttribute)s.GetType().GetCustomAttributes(typeof (PublishesAttribute), false).First()
                };
            Subscription<T> sub = new Subscription<T>(subscriberInstance, ev, newDataCallBack);
            EllaModel.Instance.Subscriptions.Add(sub);
        }


    }
}
