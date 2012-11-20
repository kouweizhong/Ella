﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Middleware.Attributes;

namespace Middleware
{
    /// <summary>
    /// <remarks>
    /// Name to be refactored
    /// </remarks>
    /// </summary>
    public class Middleware
    {
        /// <summary>
        /// Constructor for the middleware
        /// </summary>
        public Middleware()
        {
            Publishers = new List<Type>();
            Subscribers = new List<Type>();
        }

        /// <summary>
        /// List of all known publishers
        /// </summary>
        public ICollection<Type> Publishers
        {
            get;
            set;
        }

        /// <summary>
        /// List of all known subscriber types
        /// </summary>
        public ICollection<Type> Subscribers { get; set; }

        #region public methods
        /// <summary>
        /// Loads all publishers from a given assembly
        /// </summary>
        /// <param name="a">The assembly where to search publishers in</param>
        public void LoadPublishers(Assembly a)
        {
            //AssemblyName[] referencedAssemblies = a.GetReferencedAssemblies();
            //foreach (AssemblyName name in referencedAssemblies)
            //{
            //    Assembly.Load(name);
            //}
            Type[] exportedTypes = a.GetExportedTypes();
            foreach (Type t in exportedTypes)
            {
                if (IsPublisher(t))
                    Publishers.Add(t);
            }
        }
        /// <summary>
        /// Searches a given file for types that are publishers or subscribers
        /// </summary>
        /// <param name="fi">Fileinfo pointing to the file to inspect, must be a .dll or .exe file</param>
        public void DiscoverModules(System.IO.FileInfo fi)
        {
            if (!fi.Exists)
                throw new FileNotFoundException("Assembly file not found");
            if (fi.Extension != ".exe" && fi.Extension != ".dll")
                throw new ArgumentException("Assembly must be a .exe or .dll");

            Assembly a = Assembly.LoadFrom(fi.FullName);
            LoadPublishers(a);
            //TODO load subscribers

        }

        public object CreateModuleInstance(Type type)
        {
            if (IsPublisher(type) || IsSubscriber(type))
            {
                MethodInfo[] methodInfos = type.GetMethods();
                foreach (var methodInfo in methodInfos)
                {
                    if (methodInfo.GetCustomAttributes(typeof(FactoryAttribute), false).Count() > 0 && methodInfo.IsStatic)
                    {
                        object instance = methodInfo.Invoke(null, null);
                        return instance;
                    }
                }
            }
            throw new ArgumentException("Class does not define static factory method defining the [Factory] Attribute");
        }

        #endregion
        #region private helpers

        private bool IsPublisher(Type t)
        {
            return DefinesAttribute(t, typeof(PublishesAttribute));
        }

        private bool IsSubscriber(Type t)
        {
            return DefinesAttribute(t, typeof(SubscriberAttribute));
        }

        private bool DefinesAttribute(Type t, Type attribute)
        {
            List<object> atr = new List<object>(t.GetCustomAttributes(true));

            foreach (var a in atr)
            {
                if (a.GetType() == attribute)
                    return true;
            }
            return false;
        }
        #endregion
    }
}
