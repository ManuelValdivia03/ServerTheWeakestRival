using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayFaultsTests
    {
        private const string CODE = "X";
        private const string MESSAGE = "M";

        [TestMethod]
        public void ThrowFault_ReturnsFaultExceptionWithDetail()
        {
            FaultException<ServiceFault> ex = GameplayFaults.ThrowFault(CODE, MESSAGE);

            Assert.IsNotNull(ex);
            Assert.IsNotNull(ex.Detail);

            Assert.AreEqual(CODE, ex.Detail.Code);
            Assert.AreEqual(MESSAGE, ex.Detail.Message);

            Assert.IsFalse(string.IsNullOrWhiteSpace(ex.Reason.ToString()));
        }

        [TestMethod]
        public void ThrowFault_Nulls_AreNormalizedToEmptyStrings()
        {
            FaultException<ServiceFault> ex = GameplayFaults.ThrowFault(null, null);

            Assert.IsNotNull(ex.Detail);
            Assert.AreEqual(string.Empty, ex.Detail.Code);
            Assert.AreEqual(string.Empty, ex.Detail.Message);
        }

        [TestMethod]
        public void ThrowTechnicalFault_ReturnsFaultExceptionWithUserMessageAndCode()
        {
            Exception inner = new InvalidOperationException("tech");

            FaultException<ServiceFault> ex = GameplayFaults.ThrowTechnicalFault(
                code: "TECH",
                userMessage: "User message",
                context: "CTX",
                ex: inner);

            Assert.IsNotNull(ex.Detail);
            Assert.AreEqual("TECH", ex.Detail.Code);
            Assert.AreEqual("User message", ex.Detail.Message);
        }
    }
}