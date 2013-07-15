﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ella.Attributes;
using Ella;
using Ella.Control;
using Ella.Data;
using System.Threading;

namespace Ella
{
    [Subscriber]
    public class TestSubscriber
    {
        internal String rec = "";
        internal SubscriptionHandle h = new SubscriptionHandle();
        internal int numEventsReceived = 0;
        internal List<SubscriptionHandle> SubscriptionCallBackHandle = new List<SubscriptionHandle>();
        internal List<SubscriptionHandle> NewDataHandle = new List<SubscriptionHandle>();
        internal int NumAssociationsReceived = 0;


        [Factory]
        public TestSubscriber() { }

        internal bool ReplyReceived { get; set; }



        internal void Subscribe()
        {
            Ella.Subscribe.To<string>(this, Callback, subscriptionCallback: SubscriptionCallback);
        }

        private void SubscriptionCallback(Type arg1, SubscriptionHandle arg2)
        {
            h = arg2;
            SubscriptionCallBackHandle.Add(arg2);
        }

        private void Callback(string s, SubscriptionHandle handle)
        {
            
            NewDataHandle.Add(handle);
            rec = (string)s;
            if (rec == "hello")
                numEventsReceived++;
        }
        #region Subscriptions
        internal void SubscribeWithObject()
        {
            Ella.Subscribe.To<String>(this, Callback, evaluateTemplateObject: EvaluateTemplateObject);
        }

        private bool EvaluateTemplateObject(string s)
        {
            return s == "hello";
        }

        internal void SubscribeWithModifyTrue()
        {
            Ella.Subscribe.To<String>(this, Callback, DataModifyPolicy.Modify);
        }

        internal void SubscribeWithModifyFalse()
        {
            Ella.Subscribe.To<String>(this, Callback, DataModifyPolicy.NoModify);
        }

        internal void UnsubscribeFromString()
        {
            Ella.Unsubscribe.From<String>(this);
        }

        internal void UnsubscribeByHandle()
        {
            Ella.Unsubscribe.From(this, SubscriptionCallBackHandle[0]);
        }

        internal void UnsubscribeFromRemote()
        {
            Ella.Unsubscribe.From(this,h);
        }

        #endregion
        internal void SendMessage()
        {
            ApplicationMessage msg = new ApplicationMessage { Data = new byte[1], MessageId = 0, MessageType = 1 };

            Send.Message(msg, SubscriptionCallBackHandle[0], this);
        }

        [ReceiveMessage]
        public void ReceiveMessage(ApplicationMessage message)
        {
            if (message != null)
                ReplyReceived = true;
        }

        [Associate]
        public void Associate(SubscriptionHandle first, SubscriptionHandle second)
        {
            if (SubscriptionCallBackHandle.Contains(first) && SubscriptionCallBackHandle.Contains(second))
                Interlocked.Increment(ref NumAssociationsReceived);
        }
    }

    [Subscriber]
    public class TestSubscriberMethodFactory
    {
        [Factory]
        public TestSubscriberMethodFactory CreateInstance() { return new TestSubscriberMethodFactory(); }
    }

    [Subscriber]
    public class TestSubscriberStaticMethodFactory
    {
        [Factory]
        public static TestSubscriberStaticMethodFactory CreateInstance() { return new TestSubscriberStaticMethodFactory(); }
    }

    [Subscriber]
    public class TestSubscriberNoFactory
    {

    }

    [Subscriber]
    public class TestSubscriberStaticConstructerFactory
    {
        [Factory]
        static TestSubscriberStaticConstructerFactory() { }
    }


}
