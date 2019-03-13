using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TestFrameworkTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void AllTestsHaveContinuousIntegrationFriendlyCategories()
        {
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            List<MethodInfo> testsMissingCategoryTrait = currentAssembly.GetTypes()
                .SelectMany(type => type.GetMethods())
                .Where(MethodIsTest)
                .Where(MethodMissingCorrectCategoryTrait)
                .ToList();
            Assert.False(testsMissingCategoryTrait.Any());
        }

        private static bool MethodIsTest(MethodInfo method)
        {
            var factAttribute = method.GetCustomAttribute<FactAttribute>(true);
            var theoryAttribute = method.GetCustomAttribute<TheoryAttribute>(true);
            return factAttribute != null || theoryAttribute != null;
        }

        private static bool MethodMissingCorrectCategoryTrait(MethodInfo method)
        {
            IReadOnlyList<KeyValuePair<string, string>> methodTraits = TraitHelper.GetTraits(method);
            return !methodTraits.Any(TraitIsValidCategory);
        }

        private static bool TraitIsValidCategory(KeyValuePair<string, string> trait)
        {
            return trait.Key.Equals("Category") &&
                (trait.Value.Equals(PlatformSpecificHelpers.TestCategory) ||
                trait.Value.Equals(PlatformSpecificHelpers.TestCategory + "_BVT") ||
                trait.Value.Equals(PlatformSpecificHelpers.FlakeyTestCategory));
        }
    }
}
