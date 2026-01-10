using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.ServiceModel;

namespace ServerTheWeakestRival.Tests.Unit.Infrastructure
{
    [TestClass]
    public sealed class SqlExceptionFaultGuardTests
    {
        private const string GUARD_TYPE_NAME = "ServicesTheWeakestRival.Server.Infrastructure.Faults.SqlExceptionFaultGuard";

        private const string OPERATION_KEY_PREFIX = ServicesTheWeakestRival.Server.Services.AuthRefactor.AuthServiceConstants.KEY_PREFIX_LOGIN;
        private const string TECHNICAL_ERROR_CODE = ServicesTheWeakestRival.Server.Services.AuthRefactor.AuthServiceConstants.ERROR_DB_ERROR;

        private const string CONTEXT_BASE = "UnitTest.SqlExceptionFaultGuard";
        private const string DETAILS_PREFIX_SQL_NUMBER = "SqlNumber=";

        private const int SQL_NUMBER_UNIQUE = 2627;
        private const byte SQL_STATE = 1;
        private const byte SQL_CLASS = 14;
        private const int SQL_LINE = 10;

        private const string SQL_SERVER = "TestServer";
        private const string SQL_MESSAGE = "Test SQL error";
        private const string SQL_PROCEDURE = "dbo.usp_Test";

        [TestMethod]
        public void Execute_Generic_WhenOperationSucceeds_ReturnsResult()
        {
            MethodInfo executeGeneric = GetExecuteGenericOrThrow(typeof(int));

            object result = executeGeneric.Invoke(
                null,
                new object[]
                {
                    new Func<int>(() => 123),
                    OPERATION_KEY_PREFIX,
                    TECHNICAL_ERROR_CODE,
                    CONTEXT_BASE,
                    new Func<string, string, string, SqlException, Exception>(CreateTestFault)
                });

            Assert.AreEqual(123, (int)result);
        }

        [TestMethod]
        public void Execute_Generic_WhenOperationIsNull_ThrowsArgumentNullException()
        {
            MethodInfo executeGeneric = GetExecuteGenericOrThrow(typeof(int));

            try
            {
                executeGeneric.Invoke(
                    null,
                    new object[]
                    {
                        null,
                        OPERATION_KEY_PREFIX,
                        TECHNICAL_ERROR_CODE,
                        CONTEXT_BASE,
                        new Func<string, string, string, SqlException, Exception>(CreateTestFault)
                    });

                Assert.Fail("Expected ArgumentNullException.");
            }
            catch (TargetInvocationException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(ArgumentNullException));
            }
        }

        [TestMethod]
        public void Execute_Generic_WhenTechnicalFaultFactoryIsNull_ThrowsArgumentNullException()
        {
            MethodInfo executeGeneric = GetExecuteGenericOrThrow(typeof(int));

            try
            {
                executeGeneric.Invoke(
                    null,
                    new object[]
                    {
                        new Func<int>(() => 1),
                        OPERATION_KEY_PREFIX,
                        TECHNICAL_ERROR_CODE,
                        CONTEXT_BASE,
                        null
                    });

                Assert.Fail("Expected ArgumentNullException.");
            }
            catch (TargetInvocationException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(ArgumentNullException));
            }
        }

        [TestMethod]
        public void Execute_Generic_WhenSqlExceptionThrown_ThrowsFaultWithMappedMessageKeyAndDetails()
        {
            MethodInfo executeGeneric = GetExecuteGenericOrThrow(typeof(int));

            SqlException sqlEx = SqlExceptionFactory.Create(
                SQL_NUMBER_UNIQUE,
                SQL_STATE,
                SQL_CLASS,
                SQL_SERVER,
                SQL_MESSAGE,
                SQL_PROCEDURE,
                SQL_LINE);

            try
            {
                executeGeneric.Invoke(
                    null,
                    new object[]
                    {
                        new Func<int>(() => throw sqlEx),
                        OPERATION_KEY_PREFIX,
                        TECHNICAL_ERROR_CODE,
                        CONTEXT_BASE,
                        new Func<string, string, string, SqlException, Exception>(CreateTestFault)
                    });

                Assert.Fail("Expected FaultException<ServiceFault>.");
            }
            catch (TargetInvocationException ex)
            {
                var faultEx = ex.InnerException as FaultException<ServiceFault>;
                Assert.IsNotNull(faultEx);

                Assert.AreEqual(TECHNICAL_ERROR_CODE, faultEx.Detail.Code);
                Assert.AreEqual(OPERATION_KEY_PREFIX + ".Sql." + SQL_NUMBER_UNIQUE, faultEx.Detail.Message);

                Assert.IsFalse(string.IsNullOrWhiteSpace(faultEx.Detail.Details));
                StringAssert.Contains(faultEx.Detail.Details, CONTEXT_BASE);
                StringAssert.Contains(faultEx.Detail.Details, DETAILS_PREFIX_SQL_NUMBER + SQL_NUMBER_UNIQUE);
            }
        }

        [TestMethod]
        public void Execute_Generic_WhenContextIsNull_UsesOnlySqlDetailsInFaultDetails()
        {
            MethodInfo executeGeneric = GetExecuteGenericOrThrow(typeof(int));

            SqlException sqlEx = SqlExceptionFactory.Create(
                SQL_NUMBER_UNIQUE,
                SQL_STATE,
                SQL_CLASS,
                SQL_SERVER,
                SQL_MESSAGE,
                SQL_PROCEDURE,
                SQL_LINE);

            try
            {
                executeGeneric.Invoke(
                    null,
                    new object[]
                    {
                        new Func<int>(() => throw sqlEx),
                        OPERATION_KEY_PREFIX,
                        TECHNICAL_ERROR_CODE,
                        null,
                        new Func<string, string, string, SqlException, Exception>(CreateTestFault)
                    });

                Assert.Fail("Expected FaultException<ServiceFault>.");
            }
            catch (TargetInvocationException ex)
            {
                var faultEx = ex.InnerException as FaultException<ServiceFault>;
                Assert.IsNotNull(faultEx);

                Assert.AreEqual(TECHNICAL_ERROR_CODE, faultEx.Detail.Code);
                Assert.AreEqual(OPERATION_KEY_PREFIX + ".Sql." + SQL_NUMBER_UNIQUE, faultEx.Detail.Message);

                Assert.IsFalse(string.IsNullOrWhiteSpace(faultEx.Detail.Details));
                StringAssert.Contains(faultEx.Detail.Details, DETAILS_PREFIX_SQL_NUMBER + SQL_NUMBER_UNIQUE);
            }
        }

        [TestMethod]
        public void Execute_ActionOverload_WhenSqlExceptionThrown_ThrowsFaultWithMappedMessageKey()
        {
            MethodInfo executeAction = GetExecuteActionOrThrow();

            SqlException sqlEx = SqlExceptionFactory.Create(
                SQL_NUMBER_UNIQUE,
                SQL_STATE,
                SQL_CLASS,
                SQL_SERVER,
                SQL_MESSAGE,
                SQL_PROCEDURE,
                SQL_LINE);

            try
            {
                executeAction.Invoke(
                    null,
                    new object[]
                    {
                        new Action(() => throw sqlEx),
                        OPERATION_KEY_PREFIX,
                        TECHNICAL_ERROR_CODE,
                        CONTEXT_BASE,
                        new Func<string, string, string, SqlException, Exception>(CreateTestFault)
                    });

                Assert.Fail("Expected FaultException<ServiceFault>.");
            }
            catch (TargetInvocationException ex)
            {
                var faultEx = ex.InnerException as FaultException<ServiceFault>;
                Assert.IsNotNull(faultEx);

                Assert.AreEqual(TECHNICAL_ERROR_CODE, faultEx.Detail.Code);
                Assert.AreEqual(OPERATION_KEY_PREFIX + ".Sql." + SQL_NUMBER_UNIQUE, faultEx.Detail.Message);
            }
        }

        private static Exception CreateTestFault(
            string technicalErrorCode,
            string messageKey,
            string context,
            SqlException ex)
        {
            var fault = new ServiceFault
            {
                Code = technicalErrorCode ?? string.Empty,
                Message = messageKey ?? string.Empty,
                Details = context ?? string.Empty
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(fault.Message));
        }

        private static MethodInfo GetExecuteGenericOrThrow(Type resultType)
        {
            Type guardType = GetGuardTypeOrThrow();

            MethodInfo methodDef = guardType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m =>
                    string.Equals(m.Name, "Execute", StringComparison.Ordinal) &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 5);

            if (methodDef == null)
            {
                Assert.Fail("Generic Execute<TResult> not found in SqlExceptionFaultGuard.");
            }

            return methodDef.MakeGenericMethod(resultType);
        }

        private static MethodInfo GetExecuteActionOrThrow()
        {
            Type guardType = GetGuardTypeOrThrow();

            MethodInfo method = guardType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m =>
                    string.Equals(m.Name, "Execute", StringComparison.Ordinal) &&
                    !m.IsGenericMethod &&
                    m.GetParameters().Length == 5 &&
                    m.GetParameters()[0].ParameterType == typeof(Action));

            if (method == null)
            {
                Assert.Fail("Execute(Action, ...) not found in SqlExceptionFaultGuard.");
            }

            return method;
        }

        private static Type GetGuardTypeOrThrow()
        {
            Assembly serverAssembly = typeof(AuthService).Assembly;

            Type guardType = serverAssembly.GetType(GUARD_TYPE_NAME, throwOnError: false);
            if (guardType == null)
            {
                Assert.Fail("Type not found: " + GUARD_TYPE_NAME);
            }

            return guardType;
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
                    throw new InvalidOperationException("SqlError constructor not found for this runtime.");
                }

                const uint win32ErrorCode = 0;
                return ctorAlt.Invoke(new object[] { number, state, @class, server, message, procedure, lineNumber, win32ErrorCode, null });
            }
        }
    }
}
