using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class GetProfileImageWorkflowTests : AuthTestBase
    {
        private const string PROFILE_IMAGE_CODE_EMPTY = "";

        [TestInitialize]
        public void SetUp()
        {
            TokenStoreTestCleaner.ClearAllTokens();
            OnlineUserRegistryTestCleaner.ClearAll();
        }

        [TestCleanup]
        public void TearDown()
        {
            OnlineUserRegistryTestCleaner.ClearAll();
            TokenStoreTestCleaner.ClearAllTokens();
        }

        [TestMethod]
        public void Execute_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            var workflow = new GetProfileImageWorkflow(authRepository);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenTokenIsInvalid_ThrowsInvalidSession()
        {
            var workflow = new GetProfileImageWorkflow(authRepository);

            var request = new GetProfileImageRequest
            {
                Token = Guid.NewGuid().ToString("N"),
                AccountId = 1,
                ProfileImageCode = PROFILE_IMAGE_CODE_EMPTY
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }
    }
}
