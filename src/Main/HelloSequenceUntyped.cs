// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Main;

/// <remarks>
/// The set of functions in this class demonstrate how to use Durable Functions using untyped orchestration and activities.
/// The programming model used is the most similar to the WebJobs-based Durable Functions experience for .NET in-process.
/// See the <see cref="TypedSample.HelloCitiesTyped"/> implementation for how to use the newer "typed" programming model.
/// </remarks>
public class HelloSequenceUntyped
{
    private IHttpClientFactory _httpClientFactory;

    public HelloSequenceUntyped(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [Function(nameof(Regular))]
    public void Regular(
    [TimerTrigger("0 */1 * * * *")] TimerInfo timer, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(Regular));
        logger.LogInformation("Bam!");
    }

    /// <summary>
    /// HTTP-triggered function that starts the <see cref="HelloCitiesUntyped"/> orchestration.
    /// </summary>
    /// <param name="req">The HTTP request that was used to trigger this function.</param>
    /// <param name="client">The DurableTask client that is used to start and manage orchestration instances.</param>
    /// <param name="executionContext">The Azure Functions execution context, which is available to all function types.</param>
    /// <returns>Returns an HTTP response with more information about the started orchestration instance.</returns>
    [Function(nameof(StartHelloCitiesUntyped))]
    public async Task<HttpResponseData> StartHelloCitiesUntyped(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(StartHelloCitiesUntyped));

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(HelloCitiesUntyped));
        logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);

        return client.CreateCheckStatusResponse(req, instanceId);
    }

    /// <summary>
    /// Orchestrator function that calls the <see cref="SayHelloUntyped"/> activity function several times consecutively.
    /// </summary>
    /// <param name="requestState">The serialized orchestration state that gets passed to the function.</param>
    /// <returns>Returns an opaque output string with instructions about what actions to persist into the orchestration history.</returns>
    [Function(nameof(HelloCitiesUntyped))]
    public async Task<string> HelloCitiesUntyped([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await context.CallActivityAsync(nameof(InvokeSwapiAsync));

        string result = "";
        result += await context.CallActivityAsync<string>(nameof(SayHelloUntyped), "Tokyo") + " ";
        result += await context.CallActivityAsync<string>(nameof(SayHelloUntyped), "London") + " ";
        result += await context.CallActivityAsync<string>(nameof(SayHelloUntyped), "Seattle");
        return result;
    }

    /// <summary>
    /// Simple activity function that returns the string "Hello, {input}!".
    /// </summary>
    /// <param name="cityName">The name of the city to greet.</param>
    /// <returns>Returns a greeting string to the orchestrator that called this activity.</returns>
    [Function(nameof(SayHelloUntyped))]
    public string SayHelloUntyped([ActivityTrigger] string cityName, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(SayHelloUntyped));
        logger.LogInformation("Saying hello to {name}", cityName);
        return $"Hello, {cityName}!";
    }

    [Function(nameof(InvokeSwapiAsync))]
    public async Task InvokeSwapiAsync(
    [ActivityTrigger] FunctionContext executionContext)
    {
        static HttpRequestMessage BuildRequestMessage()
        {
            HttpRequestMessage req = new(HttpMethod.Get, "https://swapi.dev/api/people/1/");
            req.Headers.Add("accept", "application/json");

            return req;
        }

        ILogger logger = executionContext.GetLogger(nameof(InvokeSwapiAsync));

        logger.LogInformation("Invoking Swapi");

        using (HttpClient client = _httpClientFactory.CreateClient())
        using (HttpRequestMessage req = BuildRequestMessage())
        using (HttpResponseMessage resp = await client.SendAsync(req))
        {
            string payload = await resp.Content.ReadAsStringAsync();
            logger.LogInformation("Invoked Swapi. Retrieved {Size} bytes.", payload.Length);
        }
    }
}