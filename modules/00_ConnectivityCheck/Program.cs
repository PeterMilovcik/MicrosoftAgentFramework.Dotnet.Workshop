using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Workshop.Common;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 00 - Azure OpenAI Connectivity Check");
Console.WriteLine("===========================================");
Console.WriteLine();

// Load and validate configuration
var config = AgentConfig.Load();
if (config is null)
{
    Environment.Exit(1);
    return;
}

Console.WriteLine($"✅ Configuration loaded.");
Console.WriteLine($"   Endpoint:   {config.Endpoint}");
Console.WriteLine($"   Deployment: {config.Deployment}");
Console.WriteLine($"   API Version:{config.ApiVersion}");
Console.WriteLine();

// Generate a correlation id for tracing this run
var correlationId = Guid.NewGuid();
Console.WriteLine($"🔗 Correlation ID: {correlationId}");
Console.WriteLine();

// Create the agent and a session
var agent = config.CreateAgent("You are a connectivity test assistant. Keep responses very short.");
var session = await agent.CreateSessionAsync();

// Send a minimal test message and measure elapsed time
Console.Write("📡 Sending test message: \"Say OK\" ... ");
var sw = Stopwatch.StartNew();
AgentResponse response;
try
{
    response = await agent.RunAsync("Say OK", session);
}
catch (Exception ex)
{
    sw.Stop();
    Console.WriteLineError($"\n❌ Request failed after {sw.ElapsedMilliseconds}ms");
    Console.WriteLineError($"   Error: {ex.GetType().Name}: {ex.Message}");
    Environment.Exit(2);
    return;
}

sw.Stop();
Console.WriteLine($"done ({sw.ElapsedMilliseconds}ms)");
Console.WriteLine();

Console.WriteLine("📨 Response:");
Console.WriteLineColorful($"   {response.Text}", ConsoleColor.Green);
Console.WriteLine();

Console.WriteLine("📊 Diagnostics:");
Console.WriteLine($"   Deployment used : {config.Deployment}");
Console.WriteLine($"   Elapsed time    : {sw.ElapsedMilliseconds}ms");
Console.WriteLine($"   Correlation ID  : {correlationId}");
if (response.Usage is { } usage)
{
    Console.WriteLine($"   Input tokens    : {usage.InputTokenCount}");
    Console.WriteLine($"   Output tokens   : {usage.OutputTokenCount}");
}

Console.WriteLine();
Console.WriteLine("✅ Connectivity check PASSED. Azure OpenAI is reachable.");
TokenTracker.PrintSummary();