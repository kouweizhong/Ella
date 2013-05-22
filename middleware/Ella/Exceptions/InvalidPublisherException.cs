﻿//=============================================================================
// Project  : Ella Middleware
// File    : InvalidPublisherException.cs
// Authors contact  : Bernhard Dieber (Bernhard.Dieber@uni-klu.ac.at)
// Copyright 2012 by Bernhard Dieber, Jennifer Simonjan
// This code is published under the Microsoft Public License (Ms-PL).  A copy
// of the license should be distributed with the code.  It can also be found
// at the project website: http://ella.CodePlex.com.   This notice, the
// author's name, and all copyright notices must remain intact in all
// applications, documentation, and source files.
//=============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ella.Exceptions
{
    /// <summary>
    /// This exception is thrown whenever a publisher module is found to be invalid<br />
    /// Possible reasons for this:
    /// <list type="Bullet">
    /// <item>
    /// No <see cref="Ella.Attributes.PublishesAttribute"/> is defined
    /// </item>
    /// <item>
    /// The publisher defines one event ID multiple times
    /// </item>
    /// <item>
    /// The publisher does not define start and stop methods using <see cref="Ella.Attributes.StartAttribute"/> and <see cref="Ella.Attributes.StopAttribute"/>
    /// </item>
    /// </list>
    /// 
    /// </summary>
    public class InvalidPublisherException : InvalidModuleException
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidPublisherException" /> class.
        /// </summary>
        public InvalidPublisherException()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidPublisherException" /> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public InvalidPublisherException(string message)
            : base(message)
        {

        }
    }
}
