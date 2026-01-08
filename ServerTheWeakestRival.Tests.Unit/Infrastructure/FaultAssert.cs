using System;
using System.ServiceModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServerTheWeakestRival.Tests.Unit.Infrastructure
{
    internal static class FaultAssert
    {
        internal static ServiceFault Capture(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected FaultException<ServiceFault> but no exception was thrown.");
                return new ServiceFault();
            }
            catch (FaultException<ServiceFault> ex)
            {
                return ex.Detail;
            }
        }

        internal static void AssertFaultCode(Action action, string expectedCode)
        {
            ServiceFault fault = Capture(action);
            Assert.AreEqual(expectedCode, fault.Code);
        }
    }
}
