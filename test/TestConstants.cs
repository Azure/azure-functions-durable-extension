namespace WebJobs.Extensions.DurableTask.Tests
{
    public static class TestConstants
    {
        public const string IntanceIdFactComplete = "8b59154ae666471993659902ed0ba742";
        public const string InstanceIdIterations = "7b59154ae666471993659902ed0ba749";
        public const string InstanceIdFailed = "8b59154ae666471993659902ed0ba749";
        public const string InstanceIdTerminated = "8a59154ae666471993659902ed0ba749";
        public const string InstanceIdCanceled = "8c59154ae666471993659902ed0ba749";
        public const string RequestUri = "http://localhost:7071/orchestrators/E1_HelloSequence";
        public const string NotificationUrl = "http://localhost:7071/admin/extensions/DurableTaskExtension?code=mykey";
        public const string InstanceId = "7b59154ae666471993659902ed0ba742";
        public const string RandomInstanceId = "9b59154ae666471993659902ed0ba749";
        public const string TaskHub = "SampleHubVS";
        public const string ConnectionName = "Storage";
    }
}
