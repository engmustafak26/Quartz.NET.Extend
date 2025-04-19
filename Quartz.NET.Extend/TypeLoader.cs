using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Quartz.NET.Extend
{
    internal static class TypeLoader
    {

        /// <summary>
        /// Loads a type from fully qualified assembly name and type full name
        /// </summary>
        /// <param name="assemblyQualifiedName">Full assembly name (e.g., "MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")</param>
        /// <param name="typeFullName">Full type name including namespace (e.g., "MyNamespace.MyClass")</param>
        /// <returns>Loaded Type object</returns>
        public static Type LoadType(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName))
                throw new ArgumentNullException(nameof(assemblyQualifiedName));


            // First try Type.GetType with assembly-qualified name
            string fullyQualifiedTypeName = assemblyQualifiedName;
            Type type = Type.GetType(fullyQualifiedTypeName, throwOnError: false);
            if (type != null)
                return type;


            int assemblySeparator = assemblyQualifiedName.IndexOf(", ", StringComparison.Ordinal);
            if (assemblySeparator < 0)
                throw new ArgumentException("Invalid assembly qualified name format");

            string typeFullName = assemblyQualifiedName.Substring(0, assemblySeparator).Trim();
            string assemblyName = assemblyQualifiedName.Substring(assemblySeparator + 2).Trim();

            Assembly assembly = Assembly.Load(assemblyName);
            type = assembly.GetType(typeFullName);

            if (type != null)
                return type;

            // Try case-insensitive search if exact match fails
            type = assembly.GetTypes()
                .FirstOrDefault(t => t.FullName.Equals(typeFullName, StringComparison.OrdinalIgnoreCase));

            if (type != null)
                return type;

            throw new TypeLoadException($"Type '{typeFullName}' not found in assembly '{assemblyQualifiedName}'");


        }
    }
}