// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace DFTestBot
{
    static class SupportedTests
    {
        private static readonly Dictionary<string, TestDescription> SupportedTestCollection;

        static SupportedTests()
        {
            SupportedTestCollection = new Dictionary<string, TestDescription>(StringComparer.OrdinalIgnoreCase)
            {
                ["ManySequences"] = new TestDescription
                {
                    Description = @"Runs `count` ""Hello cities"" sequencial orchestrations in parallel. Example: `/DFTest run ManySequences count=10000 end`.",
                    AppLensDetector = "DurableFunctions_ManySequencesTest",
                    StarterFunctionName = "StartManySequences",
                    IsEnabled = true,
                    HttpPath = "tests/StartManySequences",
                    DirPath = null
                },
                ["FanOutFanIn"] = new TestDescription
                {
                    Description = "Runs a single orchestration that fans out to `count` number of activity functions. Example: `/DFTest run FanOutFanIn count=10000 end`.",
                    AppLensDetector = "DurableFunctions_FanOutTest",
                    StarterFunctionName = "StartFanOutFanIn",
                    IsEnabled = true,
                    HttpPath = "tests/StartFanOutFanIn",
                    DirPath = null
                },
                ["SubOrchFanOutFanIn"] = new TestDescription
                {
                    Description = "Runs a single orchestration that fans out to `count` number of orchestrator functions. Example: `/DFTest run SubOrchestrationFanOutFanIn count=100000 end`.",
                    AppLensDetector = "DurableFunctions_SubOrchFanOutFanIn",
                    StarterFunctionName = "StartFanOutFanIn",
                    IsEnabled = true,
                    HttpPath = "tests/StartFanOutFanIn",
                    DirPath = null
                },
                ["SingleCounterEntity"] = new TestDescription
                {
                    Description = "Runs the entity counter test. Example: `/DFTest run SingleCounterEntity end`.",
                    AppLensDetector = "DurableFunctions_SingleCounterEntityTest",
                    StarterFunctionName = "Counter",
                    IsEnabled = true,
                    HttpPath = "tests/Counter",
                    DirPath = null
                },
                ["SmokeTestV3"] = new TestDescription
                {
                    Description = "Runs the entity counter test. Example: `/DFTest run SmokeTestV3 end`.",
                    AppLensDetector = null,
                    StarterFunctionName = "HttpStart",
                    IsEnabled = true,
                    HttpPath = "api/HttpStart",
                    DirPath = "test\\SmokeTests\\SmokeTestsV3"
                },
                ["SmokeTestV1"] = new TestDescription
                {
                    Description = "Runs the entity counter test. Example: `/DFTest run SmokeTestV1 end`.",
                    AppLensDetector = null,
                    StarterFunctionName = "HttpStart",
                    IsEnabled = true,
                    HttpPath = "api/HttpStart",
                    DirPath = "test\\SmokeTests\\SmokeTestsV1"
                }
            };
        }

        public static bool TryGetTestInfo(string testName, out TestDescription testInfo)
            => SupportedTestCollection.TryGetValue(testName, out testInfo);

        public static IEnumerable<KeyValuePair<string, TestDescription>> GetAll()
            => SupportedTestCollection;
    }

    class TestDescription
    {
        /// <summary>
        /// Gets or sets the markdown description of the test.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the AppLens detector that can analyze the test results.
        /// </summary>
        public string AppLensDetector { get; set; }

        /// <summary>
        /// Gets or sets whether the test is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the name of the starter function for this test.
        /// </summary>
        public string StarterFunctionName { get; set; }

        /// <summary>
        /// Gets or sets the HTTP path for the starter function (e.g. tests/ManySequences)
        /// </summary>
        public string HttpPath { get; set; }

        /// <summary>
        /// Gets or sets the directory path in the durable-extension repo for the starter function (e.g. test/SmokeTests/SmokeTestsV3)
        /// </summary>
        public string DirPath { get; set; }
    }
}
