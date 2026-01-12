using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace ServerTheWeakestRival.Tests.Unit.Infrastructure
{
    internal static class OnlineUserRegistryTestCleaner
    {
        private const string SERVER_AUTH_SERVICE_TYPE = "ServicesTheWeakestRival.Server.Services.AuthService";

        private const string TYPE_HINT_ONLINE_USER_REGISTRY = "OnlineUserRegistry";
        private const string TYPE_HINT_ONLINE_USERS = "OnlineUsers";
        private const string TYPE_HINT_SESSION_REGISTRY = "SessionRegistry";
        private const string TYPE_HINT_USER_SESSION_REGISTRY = "UserSessionRegistry";
        private const string TYPE_HINT_PRESENCE_REGISTRY = "PresenceRegistry";
        private const string TYPE_HINT_USER_PRESENCE = "UserPresence";

        public static void ClearAll()
        {
            Assembly serverAssembly = ResolveServerAssembly();
            if (serverAssembly == null)
            {
                return;
            }

            Type registryType = FindRegistryType(serverAssembly);
            if (registryType == null)
            {
                return;
            }

            ClearStaticMembers(registryType);
        }

        private static Assembly ResolveServerAssembly()
        {
            Type authServiceType = Type.GetType(SERVER_AUTH_SERVICE_TYPE, throwOnError: false);
            if (authServiceType != null)
            {
                return authServiceType.Assembly;
            }

            return AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => a.GetTypes().Any(t => string.Equals(t.FullName, SERVER_AUTH_SERVICE_TYPE, StringComparison.Ordinal)));
        }

        private static Type FindRegistryType(Assembly serverAssembly)
        {
            return serverAssembly
                .GetTypes()
                .FirstOrDefault(IsCandidateRegistryType);
        }

        private static bool IsCandidateRegistryType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (!type.IsClass || !type.IsAbstract || !type.IsSealed)
            {
                return false; // buscamos static class
            }

            string name = type.Name ?? string.Empty;

            return ContainsIgnoreCase(name, TYPE_HINT_ONLINE_USER_REGISTRY) ||
                   ContainsIgnoreCase(name, TYPE_HINT_ONLINE_USERS) ||
                   ContainsIgnoreCase(name, TYPE_HINT_SESSION_REGISTRY) ||
                   ContainsIgnoreCase(name, TYPE_HINT_USER_SESSION_REGISTRY) ||
                   ContainsIgnoreCase(name, TYPE_HINT_PRESENCE_REGISTRY) ||
                   ContainsIgnoreCase(name, TYPE_HINT_USER_PRESENCE);
        }

        private static void ClearStaticMembers(Type registryType)
        {
            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (PropertyInfo property in registryType.GetProperties(flags))
            {
                object instance = SafeGetValue(property);
                TryClear(instance);
            }

            foreach (FieldInfo field in registryType.GetFields(flags))
            {
                object instance = SafeGetValue(field);
                TryClear(instance);
            }
        }

        private static object SafeGetValue(PropertyInfo property)
        {
            try
            {
                return property.GetValue(null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object SafeGetValue(FieldInfo field)
        {
            try
            {
                return field.GetValue(null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void TryClear(object instance)
        {
            if (instance == null)
            {
                return;
            }

            MethodInfo clearMethod = instance.GetType().GetMethod(
                "Clear",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (clearMethod != null)
            {
                clearMethod.Invoke(instance, null);
                return;
            }

            if (instance is IDictionary dictionary)
            {
                dictionary.Clear();
                return;
            }

            if (instance is IList list)
            {
                list.Clear();
            }
        }

        private static bool ContainsIgnoreCase(string value, string fragment)
        {
            return value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
