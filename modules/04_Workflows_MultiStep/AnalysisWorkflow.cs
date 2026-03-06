using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Workshop.Common;

namespace WorkflowsMultiStep;

/// <summary>
/// Represents the structured JSON output produced by the workflow's Finalize step.
/// </summary>
internal sealed class WorkflowOutput
{
    public string Summary { get; set; } = "";
    public List<string> Evidence { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public double Confidence { get; set; }
}

/// <summary>
/// A 4-step analysis workflow:
///   1. Plan    - produce a short plan and risk notes
///   2. Gather  - use tools to collect evidence
///   3. Critique - identify gaps and propose improvements
///   4. Finalize - produce human summary + structured JSON
/// </summary>
internal static class AnalysisWorkflow
{
    public static async Task<WorkflowOutput> RunAsync(
        AIAgent agent,
        string userQuery,
        CancellationToken ct = default)
    {
        // ---- Step 1: Plan ----
        PrintStepHeader("PLAN", "Producing a concise plan and risk notes...");
        var planSession = await agent.CreateSessionAsync(ct);
        var planPrompt = $"""
            The user needs analysis on the following topic:
            {userQuery}

            Produce a SHORT analysis plan (3-5 bullet points) describing:
            - What information is needed
            - What tools to use (GetTime, ReadFile, SearchKb)
            - Potential risks or ambiguities
            """;
        var planResult = await RunAndPrintAsync(agent, planPrompt, planSession, "PLAN", ct);

        // ---- Step 2: Gather Evidence ----
        PrintStepHeader("EVIDENCE", "Gathering facts using available tools...");
        var evidenceSession = await agent.CreateSessionAsync(ct);
        var evidencePrompt = $"""
            Based on this analysis plan:
            {planResult}

            Execute the plan. Use the available tools (ReadFile, SearchKb, GetTime) to gather
            concrete evidence relevant to:
            {userQuery}

            List each piece of evidence you find with its source.
            """;
        var evidenceResult = await RunAndPrintAsync(agent, evidencePrompt, evidenceSession, "EVIDENCE", ct);

        // ---- Step 3: Critique ----
        PrintStepHeader("CRITIQUE", "Identifying gaps and evaluating evidence quality...");
        var critiqueSession = await agent.CreateSessionAsync(ct);
        var critiquePrompt = $"""
            Review this evidence gathered for: "{userQuery}"

            Evidence collected:
            {evidenceResult}

            Provide a brief critique:
            - What information is still missing?
            - Are there any potential hallucinations or unsupported claims?
            - What improvements would strengthen the analysis?
            """;
        var critiqueResult = await RunAndPrintAsync(agent, critiquePrompt, critiqueSession, "CRITIQUE", ct);

        // ---- Step 4: Finalize ----
        PrintStepHeader("FINAL", "Producing final answer and structured JSON output...");
        var finalSession = await agent.CreateSessionAsync(ct);
        var finalPrompt = $$"""
            Produce a final analysis for: "{{userQuery}}"

            Available context:
            Plan: {{planResult}}
            Evidence: {{evidenceResult}}
            Critique: {{critiqueResult}}

            Respond with ONLY a valid JSON object using this exact schema (no markdown fences):
            {
              "summary": "One-paragraph plain-language summary",
              "evidence": ["fact 1", "fact 2"],
              "recommendations": ["action 1", "action 2"],
              "confidence": 0.85
            }
            """;
        var finalResult = await RunAndPrintAsync(agent, finalPrompt, finalSession, "FINAL", ct);

        // Parse the JSON output
        var output = ParseOutput(finalResult);
        return output;
    }

    private static async Task<string> RunAndPrintAsync(
        AIAgent agent, string prompt, AgentSession session, string label, CancellationToken ct)
    {
        var sb = new StringBuilder();
        Console.WriteColorful($"[{label}] ", ConsoleColor.DarkGray);

        await foreach (var update in agent.RunStreamingAsync(prompt, session).WithCancellation(ct))
        {
            Console.Write(update.Text);
            sb.Append(update.Text);
        }
        Console.WriteLine();
        Console.WriteLine();
        return sb.ToString();
    }

    private static void PrintStepHeader(string step, string description)
    {
        Console.WriteLineColorful($"━━━ Step: {step} ━━━", ConsoleColor.Magenta);
        Console.WriteLineColorful($"  {description}", ConsoleColor.DarkGray);
    }

    private static WorkflowOutput ParseOutput(string raw)
    {
        // Try to extract JSON from the response (sometimes the model adds preamble)
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            var json = raw[start..(end + 1)];
            try
            {
                return JsonSerializer.Deserialize<WorkflowOutput>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new WorkflowOutput { Summary = raw };
            }
            catch { /* fall through */ }
        }
        return new WorkflowOutput { Summary = raw };
    }
}
