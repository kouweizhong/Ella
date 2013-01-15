﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ella
{
    [TestClass]
    public class Messaging
    {
        [TestMethod]
        public void SendLocalApplicationMessage()
        {
            TestPublisher tp = new TestPublisher();
            Start.Publisher(tp);
            TestSubscriber s = new TestSubscriber();
            s.Subscribe();
            s.SendMessage();
            Assert.IsTrue(tp.MessageReceived);
        }

        //TODO tests for: Not subscribed
    }
}
