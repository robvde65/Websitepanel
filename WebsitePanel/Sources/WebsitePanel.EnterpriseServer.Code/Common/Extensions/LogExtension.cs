﻿using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using WebsitePanel.Providers;

namespace WebsitePanel.EnterpriseServer.Extensions
{
    public static class LogExtension
    {
        private const string OLD_PREFIX = "Old ";
        private const string NEW_PREFIX = "New ";

        /// <summary>
        /// Log properties of the object which have [LogProperty] or [LogParentPropery] attributes
        /// </summary>
        public static void WriteObject<T>(T obj) 
            where T : class
        {
            if (obj == null)
                return;

            // Log Parent Properties
            var logPropertyAttributes = GetAttributes<T, LogParentPropertyAttribute>();
            foreach (var logPropertyAttribute in logPropertyAttributes)
            {
                TaskManager.Write(logPropertyAttribute.GetLogString(obj));
            }

            // Log Properties
            var properties = typeof (T).GetProperties().Where(prop => prop.IsDefined(typeof (LogPropertyAttribute), false));
            foreach (var property in properties)
            {
                var attributes = GetAttributes<LogPropertyAttribute>(property);
                foreach (var logPropertyAttribute in attributes)
                {
                    TaskManager.Write(logPropertyAttribute.GetLogString(obj, property));
                }
            }
        }

        /// <summary>
        /// Log properties of the object which have [LogProperty] or [LogParentPropery] attributes
        /// </summary>
        public static void WriteObject<T, TU>(T obj, params Expression<Func<T, TU>>[] additionalProperties)
            where T : class
        {
            WriteObject(obj);

            LogProperties(obj, additionalProperties);
        }

        /// <summary>
        /// Log a value of the property from expression parameter
        /// </summary>
        public static T LogProperty<T, TU>(this T obj, Expression<Func<T, TU>> expression, string nameInLog = null) 
            where T : class
        {
            var property = ObjectUtils.GetProperty(obj, expression);

            var propertyName = string.IsNullOrEmpty(nameInLog) ? LogExtensionHelper.DecorateName(property.Name) : nameInLog;
            var propertyValue = LogExtensionHelper.GetString(property.GetValue(obj, null));

            TaskManager.Write(LogExtensionHelper.CombineString(propertyName, propertyValue));

            return obj;
        }

        /// <summary>
        /// Log a value of the property from expression parameter
        /// </summary>
        public static void LogProperties<T, TU>(this T obj, params Expression<Func<T, TU>>[] expressions) 
            where T : class
        {
            foreach (var expression in expressions)
            {
                LogProperty(obj, expression);
            }
        }

        /// <summary>
        /// Log a value of the property only when it is different from newValue parameter. Also the old value can be written.
        /// </summary>
        public static T LogPropertyIfChanged<T, TU>(this T obj, Expression<Func<T, TU>> expression, object newValue, bool withOld = true) where T : class
        {
            var property = ObjectUtils.GetProperty(obj, expression);

            var oldValue = property.GetValue(obj, null);

            if (!oldValue.Equals(newValue))
            {
                WriteChangedProperty(obj, property, LogExtensionHelper.GetString(oldValue),
                    LogExtensionHelper.GetString(newValue), withOld);
            }

            return obj;
        }

        /// <summary>
        /// Log values of variables which will be written with variable name
        /// </summary>
        /// <param name="variablesObs">Dynamic object with variables instead of properties like: new {varA, varB}</param>
        /// <param name="prefix">Add the string before a variable name</param>
        public static void WriteVariables(object variablesObs, string prefix = "")
        {
            if (variablesObs == null)
                throw new ArgumentException();
            
            foreach (var property in variablesObs.GetType().GetProperties())
            {
                var parameterName = LogExtensionHelper.DecorateName(property.Name);
                var parameterValue = LogExtensionHelper.GetString(property.GetValue(variablesObs, null));

                TaskManager.Write(prefix + LogExtensionHelper.CombineString(parameterName, parameterValue));
            }
        }

        /// <summary>
        /// Write log in the format "name: value"
        /// </summary>
        public static void WriteVariable(string name, string value)
        {
            TaskManager.Write(LogExtensionHelper.CombineString(name, value));
        }

        /// <summary>
        /// Set item name for current Task
        /// </summary>
        /// <param name="itemName"></param>
        public static void SetItemName(string itemName)
        {
            TaskManager.ItemName = itemName;
        }

        #region Private methods

        private static TAttr[] GetAttributes<TObj, TAttr>()
            where TObj : class
            where TAttr : class
        {
            return (TAttr[]) typeof (TObj).GetCustomAttributes(typeof (TAttr), false);
        }

        private static T[] GetAttributes<T>(ICustomAttributeProvider member) where T : class
        {
            return (T[]) member.GetCustomAttributes(typeof (T), false);
        }

        private static void WriteChangedProperty(object obj, PropertyInfo property, string oldValue, string newValue, bool withOld = true)
        {
            var attributes = GetAttributes<LogPropertyAttribute>(property);

            if (attributes.Length > 0)
            {
                foreach (var logPropertyAttribute in attributes)
                {
                    if (withOld)
                        TaskManager.Write(OLD_PREFIX + logPropertyAttribute.GetLogString(obj, property));

                    TaskManager.Write(NEW_PREFIX + logPropertyAttribute.GetLogString(property.Name, newValue));
                }
            }
            else
            {
                var name = LogExtensionHelper.DecorateName(property.Name);

                if (withOld)
                    TaskManager.Write(OLD_PREFIX + LogExtensionHelper.CombineString(name, newValue));

                TaskManager.Write(NEW_PREFIX + LogExtensionHelper.CombineString(name, oldValue));
            }

        }

        #endregion

    }
}
