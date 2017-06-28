#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static string Run(DurableActivityContext helloContext)
{
    string name = helloContext.GetInput<string>();
    return $"Hello {name}!";
}