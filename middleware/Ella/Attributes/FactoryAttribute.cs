﻿//=============================================================================
// Project  : Ella Middleware
// File    : FactoryAttribute.cs
// Authors contact  : Bernhard Dieber (Bernhard.Dieber@aau.at)
// Copyright 2013 by Bernhard Dieber, Jennifer Simonjan
// This code is published under the Microsoft Public License (Ms-PL).  A copy
// of the license should be distributed with the code.  It can also be found
// at the project website: https://github.com/bedieber/Ella.   This notice, the
// author's name, and all copyright notices must remain intact in all
// applications, documentation, and source files.
//=============================================================================

using System;

namespace Ella.Attributes
{
    /// <summary>
    /// This attribute is used to mark a certain method as being a factory to create a new instance of a module
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = true)]
    public class FactoryAttribute : Attribute
    {

    }
}
