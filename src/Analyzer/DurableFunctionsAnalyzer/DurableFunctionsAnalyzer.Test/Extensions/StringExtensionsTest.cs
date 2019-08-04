using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace DurableFunctionsAnalyzer.Test.Extensions
{
    [TestClass]
    public class StringExtensionsTest
    {
        [TestMethod]
        public void ProximityWorks()
        {
            var strings = new string[] { "cat", "cactus", "cattle" };
            Assert.AreEqual(1, DurableFunctionsAnalyzer.Extensions.StringExtensions.LevenshteinDistance("cab", "cat"));
            Assert.AreEqual(4, DurableFunctionsAnalyzer.Extensions.StringExtensions.LevenshteinDistance("cab", "cactus"));
            Assert.AreEqual(4, DurableFunctionsAnalyzer.Extensions.StringExtensions.LevenshteinDistance("cab", "cattle"));
            Assert.AreEqual(17, DurableFunctionsAnalyzer.Extensions.StringExtensions.LevenshteinDistance("HireEmployee", "ApplicationsFiltered"));
            Assert.AreEqual(6, DurableFunctionsAnalyzer.Extensions.StringExtensions.LevenshteinDistance("ApplicationsFilteredNicely", "ApplicationsFiltered"));
        }
    }
}
