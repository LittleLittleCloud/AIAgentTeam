// create AI team
// suppress SKEXP0005
#pragma warning disable

using AIAgentTeam;
using AutoGen;
using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.SemanticKernel;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;

var openaiAPI = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("OPENAI_API_KEY is not set");
var bingSearchAPIKey = Environment.GetEnvironmentVariable("BING_API_KEY") ?? throw new Exception("BING_API_KEY is not set");
var gpt3_5 = "gpt-3.5-turbo";
var gpt_4 = "gpt-4-turbo";
var openaiClient = new OpenAIClient(openaiAPI);
var seed = 1;

// Step 1: Create the user
var user = new UserProxyAgent(name: "user");

// Step 2: Create the CEO
var ceo = new OpenAIChatAgent(
    openAIClient: openaiClient,
    name: "Elon Musk",
    modelName: gpt_4,
    seed: seed,
    systemMessage: """
    You are Elon Musk, CEO of Tesla. You are in a QA about Tesla.
    When a question about tesla is asked, You can forward the question to your subordinates if the question is related to marketing.

    Here are your subordinates:
    - CMO: Chief Marketing Officer who is responsible for answering all market-related questions.
    """)
    .RegisterMessageConnector()
    .RegisterPrintFormatMessageHook();
