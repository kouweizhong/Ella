﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Ella.Attributes;
using Ella.Control;
using Ella.Internal;
using Ella.Model;
using Ella.Network;
using log4net;

namespace Ella
{
    /// <summary>
    /// This facade class is used to send application messages to other modules
    /// </summary>
    public static class Send
    {
        private static ILog _log = LogManager.GetLogger(typeof(Send));
        /// <summary>
        /// Sends the specified <paramref name="message" /> to the publisher identified by handle <paramref name="to" />.<br />
        /// This is used by subscribers to send messages directly to a certain publisher of an event.<br />
        /// However, it is not guaranteed, that the publisher is a) subscribed to application messages and b) can interpret this specific message type
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="to">The receiver identified by a subscription handle</param>
        /// <param name="sender">The sender instance.</param>
        public static bool Message(ApplicationMessage message, SubscriptionHandle to, object sender)
        {
            //TODO check if sender is subscriber
            message.Sender = EllaModel.Instance.GetSubscriberId(sender);
            message.Handle = to;
            /*Check if subscription is remote or local
             * if local: pass it to local module
             * if remote: serialize, wrap in Message, send
             */
            if (to is RemoteSubscriptionHandle)
            {
                return NetworkController.SendApplicationMessage(message, to as RemoteSubscriptionHandle);
            }
            else
            {
                return DeliverApplicationMessage(message);
            }
        }

        internal static bool DeliverApplicationMessage(ApplicationMessage message)
        {
            object publisher = (from s in EllaModel.Instance.Subscriptions
                                where s.Handle == message.Handle
                                select s.Event.Publisher).SingleOrDefault();
            if (publisher != null)
            {
                return DeliverMessage(message, publisher);
            }
            else
            {
                _log.FatalFormat("Found no suitable publisher for handle {0}", message.Handle);
                return false;
            }
        }

        /// <summary>
        /// Delivers a reply to a message
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        internal static bool DeliverMessageReply(ApplicationMessage message)
        {
            object subscriber = (from s in EllaModel.Instance.Subscriptions
                                where s.Handle == message.Handle
                                select s.Subscriber).SingleOrDefault();
            if (subscriber != null)
            {
                return DeliverMessage(message, subscriber);
            }
            else
            {
                _log.FatalFormat("Found no suitable subscriber for handle {0}", message.Handle);
                return false;
            }
        }

        /// <summary>
        /// Delivers an application message to a module
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="instance">The instance.</param>
        /// <returns></returns>
        private static bool DeliverMessage(ApplicationMessage message, object instance)
        {

            MethodBase method = ReflectionUtils.GetAttributedMethod(instance.GetType(),
                                                                    typeof(ReceiveMessageAttribute));
            if (method != null)
            {
                ParameterInfo[] parameterInfos = method.GetParameters();
                if (parameterInfos.Length == 1 && parameterInfos[0].ParameterType == typeof(ApplicationMessage))
                {
                    //TODO should this be done in a separate thread?
                    method.Invoke(instance, new object[] { message });
                }
            }
            else
            {
                _log.WarnFormat("Instance {0} does not define a ReceiveMessage method", instance);
                return false;
            }

            return true;
        }



        /// <summary>
        /// Replies to a previously received message
        /// </summary>
        /// <param name="reply">The reply message to be sent</param>
        /// <param name="inReplyTo">The message in reply to (which was originally received).</param>
        /// <param name="sender">The sender instance.</param>
        /// <returns></returns>
        public static bool Reply(ApplicationMessage reply, ApplicationMessage inReplyTo, object sender)
        {
            reply.Sender = EllaModel.Instance.GetPublisherId(sender);
            reply.Handle = inReplyTo.Handle;
            if (inReplyTo.Handle is RemoteSubscriptionHandle)
            {
                return NetworkController.SendApplicationMessage(reply, inReplyTo.Handle as RemoteSubscriptionHandle,
                                                                isReply: true);
            }
            else
            {
                DeliverMessageReply(reply);
                return true;
            }
        }
    }
}
