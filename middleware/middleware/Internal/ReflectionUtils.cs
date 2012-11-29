﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Ella.Internal
{
    internal static class ReflectionUtils
    {
        #region private helpers

        /// <summary>
        /// Checks if <paramref name="t"/> defines the attribute <paramref name="attribute"/>.
        /// </summary>
        /// <param name="t">The type.</param>
        /// <param name="attribute">The attribute.</param>
        /// <returns></returns>
        internal static bool DefinesAttribute(Type t, Type attribute)
        {
            List<object> atr = new List<object>(t.GetCustomAttributes(attribute, true));
            return atr.Any();
        }

        /// <summary>
        /// Gets the first method defined in <paramref name="type"/> which is attributed with <paramref name="attribute"/> .
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="attribute">The attribute.</param>
        /// <param name="includeConstructors">if set to <c>true</c> <paramref name="attribute"/> is also searched for in constructors.</param>
        /// <returns>A <see cref="System.Reflection.MethodBase"/> object referring to the first method found or null if no method was found</returns>
        internal static MethodBase GetAttributedMethod(Type type, Type attribute, bool includeConstructors = false)
        {
            IEnumerable<MethodBase> methodInfos = type.GetMethods();
            if (includeConstructors)
            {
                var constructorInfos = type.GetConstructors();
                methodInfos = methodInfos.Concat(constructorInfos);
            }
            foreach (var methodInfo in methodInfos)
            {
                var customAttributes = methodInfo.GetCustomAttributes(attribute, true);
                if (customAttributes.Any())
                    return methodInfo;
            }
            return null;
        }

        /// <summary>
        /// Gets the all methods of the type <paramref name="type"/> which are attributed with <paramref name="attribute"/>
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="attribute">The attribute.</param>
        /// <param name="includeConstructors">if set to <c>true</c> <paramref name="attribute"/> is also searched for in constructors</param>
        /// <returns></returns>
        internal static IEnumerable<MethodBase> GetAttributedMethods(Type type, Type attribute, bool includeConstructors = false)
        {
            IEnumerable<MethodBase> methodInfos = type.GetMethods();
            if (includeConstructors)
            {
                var constructorInfos = type.GetConstructors();
                methodInfos = methodInfos.Concat(constructorInfos);
            }
            var methods = from m in methodInfos where m.GetCustomAttributes(attribute, true).Any() select m;
            return methods;
        }


        /// <summary>
        /// Searches for any <paramref name="type"/> members attributed with <paramref name="attribute"/>
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="attribute">The attribute.</param>
        /// <returns></returns>
        internal static IEnumerable<KeyValuePair<MemberInfo, IEnumerable<Attribute>>> GetAttributedMembers(Type type, Type attribute)
        {
            var memberInfos = type.GetMembers();
            var attributedMembers = from m in memberInfos let atr = m.GetCustomAttributes(attribute, true) where atr.Any() select new KeyValuePair<MemberInfo, IEnumerable<Attribute>>(m, atr.Cast<Attribute>());
            return attributedMembers;
        }

        #endregion

    }
}