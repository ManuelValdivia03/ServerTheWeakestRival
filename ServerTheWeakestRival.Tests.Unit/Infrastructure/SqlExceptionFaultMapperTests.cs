using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using System;
using System.Data.SqlClient;
using System.Reflection;

namespace ServerTheWeakestRival.Tests.Unit.Infrastructure
{
    [TestClass]
    public sealed class SqlExceptionFaultMapperTests
    {
        private const string MAPPER_TYPE_NAME = "ServicesTheWeakestRival.Server.Infrastructure.Faults.SqlExceptionFaultMapper";

        private const string RESULT_PROP_MESSAGE_KEY = "MessageKey";
        private const string RESULT_PROP_DETAILS = "Details";

        private const string SQL_DETAILS_NUMBER = "SqlNumber=";
        private const string SQL_DETAILS_STATE = "State=";
        private const string SQL_DETAILS_CLASS = "Class=";
        private const string SQL_DETAILS_PROCEDURE = "Procedure=";
        private const string SQL_DETAILS_LINE = "Line=";

        private const int SQL_NUMBER_UNIQUE = 2627;
        private const byte SQL_STATE = 1;
        private const byte SQL_CLASS = 14;

        private const string SQL_SERVER = "TestServer";
        private const string SQL_MESSAGE = "Test SQL error";
        private const string SQL_PROCEDURE = "dbo.usp_Test";
        private const int SQL_LINE = 10;

        [TestMethod]
        public void Map_WhenPrefixIsWhitespace_ThrowsArgumentException()
        {
            object mapper = GetMapperTypeOrThrow();
            MethodInfo mapMethod = GetMapMethodOrThrow(mapper);

            try
            {
                mapMethod.Invoke(null, new object[] { null, "  " });
                Assert.Fail("Expected ArgumentException.");
            }
            catch (TargetInvocationException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(ArgumentException));
            }
        }

        [TestMethod]
        public void Map_WhenSqlExceptionIsNull_ReturnsUnknownKeyAndEmptyDetails()
        {
            object mapper = GetMapperTypeOrThrow();
            MethodInfo mapMethod = GetMapMethodOrThrow(mapper);

            string prefix = AuthServiceConstants.KEY_PREFIX_BEGIN_REGISTER;

            object result = mapMethod.Invoke(null, new object[] { null, prefix });

            string messageKey = ReadStringProperty(result, RESULT_PROP_MESSAGE_KEY);
            string details = ReadStringProperty(result, RESULT_PROP_DETAILS);

            Assert.AreEqual(prefix + ".Sql.Unknown", messageKey);
            Assert.AreEqual(string.Empty, details);
        }

        [TestMethod]
        public void Map_WhenSqlExceptionProvided_ReturnsKeyWithSqlNumberAndDetails()
        {
            object mapper = GetMapperTypeOrThrow();
            MethodInfo mapMethod = GetMapMethodOrThrow(mapper);

            string prefixWithDot = AuthServiceConstants.KEY_PREFIX_BEGIN_REGISTER + ".";
            SqlException sqlEx = SqlExceptionFactory.Create(
                SQL_NUMBER_UNIQUE,
                SQL_STATE,
                SQL_CLASS,
                SQL_SERVER,
                SQL_MESSAGE,
                SQL_PROCEDURE,
                SQL_LINE);

            object result = mapMethod.Invoke(null, new object[] { sqlEx, prefixWithDot });

            string messageKey = ReadStringProperty(result, RESULT_PROP_MESSAGE_KEY);
            string details = ReadStringProperty(result, RESULT_PROP_DETAILS);

            Assert.AreEqual(AuthServiceConstants.KEY_PREFIX_BEGIN_REGISTER + ".Sql." + SQL_NUMBER_UNIQUE, messageKey);

            StringAssert.Contains(details, SQL_DETAILS_NUMBER + SQL_NUMBER_UNIQUE);
            StringAssert.Contains(details, SQL_DETAILS_STATE + SQL_STATE);
            StringAssert.Contains(details, SQL_DETAILS_CLASS + SQL_CLASS);
            StringAssert.Contains(details, SQL_DETAILS_PROCEDURE + SQL_PROCEDURE);
            StringAssert.Contains(details, SQL_DETAILS_LINE + SQL_LINE);
        }

        private static Type GetMapperTypeOrThrow()
        {
            Assembly serverAssembly = typeof(AuthService).Assembly;

            Type mapperType = serverAssembly.GetType(MAPPER_TYPE_NAME, throwOnError: false);
            if (mapperType == null)
            {
                Assert.Fail("Type not found: " + MAPPER_TYPE_NAME);
            }

            return mapperType;
        }

        private static MethodInfo GetMapMethodOrThrow(object mapperTypeObj)
        {
            var mapperType = (Type)mapperTypeObj;

            MethodInfo method = mapperType.GetMethod(
                "Map",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(SqlException), typeof(string) },
                modifiers: null);

            if (method == null)
            {
                Assert.Fail("Map method not found on: " + MAPPER_TYPE_NAME);
            }

            return method;
        }

        private static string ReadStringProperty(object instance, string propertyName)
        {
            if (instance == null) return string.Empty;

            PropertyInfo prop = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            object value = prop?.GetValue(instance, index: null);
            return value as string ?? string.Empty;
        }

        private static class SqlExceptionFactory
        {
            private const string TYPE_SQL_ERROR = "System.Data.SqlClient.SqlError";
            private const string TYPE_SQL_ERROR_COLLECTION = "System.Data.SqlClient.SqlErrorCollection";
            private const string TYPE_SQL_EXCEPTION = "System.Data.SqlClient.SqlException";

            private const string METHOD_ADD = "Add";
            private const string METHOD_CREATE_EXCEPTION = "CreateException";

            internal static SqlException Create(
                int number,
                byte state,
                byte @class,
                string server,
                string message,
                string procedure,
                int lineNumber)
            {
                Type sqlErrorType = typeof(SqlException).Assembly.GetType(TYPE_SQL_ERROR, throwOnError: true);
                Type sqlErrorCollectionType = typeof(SqlException).Assembly.GetType(TYPE_SQL_ERROR_COLLECTION, throwOnError: true);
                Type sqlExceptionType = typeof(SqlException).Assembly.GetType(TYPE_SQL_EXCEPTION, throwOnError: true);

                object sqlError = CreateSqlError(sqlErrorType, number, state, @class, server, message, procedure, lineNumber);
                object sqlErrorCollection = Activator.CreateInstance(sqlErrorCollectionType, nonPublic: true);

                MethodInfo addMethod = sqlErrorCollectionType.GetMethod(
                    METHOD_ADD,
                    BindingFlags.Instance | BindingFlags.NonPublic);

                addMethod.Invoke(sqlErrorCollection, new[] { sqlError });

                MethodInfo createException = sqlExceptionType.GetMethod(
                    METHOD_CREATE_EXCEPTION,
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { sqlErrorCollectionType, typeof(string) },
                    modifiers: null);

                object sqlException = createException.Invoke(null, new object[] { sqlErrorCollection, "0.0.0" });
                return (SqlException)sqlException;
            }

            private static object CreateSqlError(
                Type sqlErrorType,
                int number,
                byte state,
                byte @class,
                string server,
                string message,
                string procedure,
                int lineNumber)
            {
                ConstructorInfo ctor = sqlErrorType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[]
                    {
                        typeof(int),
                        typeof(byte),
                        typeof(byte),
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(int)
                    },
                    modifiers: null);

                if (ctor != null)
                {
                    return ctor.Invoke(new object[] { number, state, @class, server, message, procedure, lineNumber });
                }

                ConstructorInfo ctorAlt = sqlErrorType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[]
                    {
                        typeof(int),
                        typeof(byte),
                        typeof(byte),
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(int),
                        typeof(uint),
                        typeof(Exception)
                    },
                    modifiers: null);

                if (ctorAlt == null)
                {
                    throw new InvalidOperationException("SqlError constructor not found for this framework/runtime.");
                }

                const uint win32ErrorCode = 0;
                return ctorAlt.Invoke(new object[] { number, state, @class, server, message, procedure, lineNumber, win32ErrorCode, null });
            }
        }
    }
}
