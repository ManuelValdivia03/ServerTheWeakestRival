using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Server.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayRandomTests
    {
        private const int MIN = 0;
        private const int MAX = 10;

        [TestMethod]
        public void Next_ReturnsWithinRange()
        {
            for (int i = 0; i < 200; i++)
            {
                int v = GameplayRandom.Next(MIN, MAX);
                Assert.IsTrue(v >= MIN);
                Assert.IsTrue(v < MAX);
            }
        }

        [TestMethod]
        public void Next_ConcurrentCalls_DoNotThrow_AndStayInRange()
        {
            List<int> values = new List<int>();

            Parallel.For(0, 500, _ =>
            {
                int v = GameplayRandom.Next(MIN, MAX);
                lock (values)
                {
                    values.Add(v);
                }
            });

            Assert.AreEqual(500, values.Count);
            foreach (int v in values)
            {
                Assert.IsTrue(v >= MIN);
                Assert.IsTrue(v < MAX);
            }
        }
    }
}
