// create AI team
// suppress SKEXP0005
#pragma warning disable SKEXP0054

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

// Create the CEO
var ceo = new OpenAIChatAgent(
    openAIClient: openaiClient,
    name: "Elon Musk",
    modelName: gpt_4,
    systemMessage: """
    You are Elon Musk, CEO of Tesla. You are in a hearing about Tesla.
    When a question about tesla is asked, You can ask your subordinates to answer the question.

    Here are your subordinates:
    - cmo: Chief Marketing Officer who is responsible for answering all market-related questions.
    """)
    .RegisterMessageConnector()
    .RegisterPrintFormatMessageHook();

// Create the cmo
var kernelBuilder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: gpt3_5, apiKey: openaiAPI);
var bingSearch = new BingConnector(bingSearchAPIKey);
var webSearchPlugin = new WebSearchEnginePlugin(bingSearch);
kernelBuilder.Plugins.AddFromObject(webSearchPlugin);
var kernel = kernelBuilder.Build();

var kernelMessageConnector = new SemanticKernelChatMessageContentConnector();
var cmo = new SemanticKernelAgent(
       kernel: kernel,
       name: "cmo",
       systemMessage: """
       You are cmo, you report to Elon and you answer all market-related question.

       To make sure you have the most up-to-date information, you can use the web search plugin to search for information on the web before answering questions.
       """)
    .RegisterMessageConnector()
    .RegisterPrintFormatMessageHook();

// Create the hearing member
var hearingMember = new UserProxyAgent(name: "hearingMember");

// Create the group admin
var admin = new OpenAIChatAgent(
    openAIClient: openaiClient,
    name: "admin",
    modelName: gpt_4,
    systemMessage: "You are the group admin.")
    .RegisterMessageConnector();

// Create the AI team
// define the transition among group members
// we only allow the following transitions:
// hearingMember -> ceo
// ceo -> cmo
// cmo -> ceo
// ceo -> hearingMember

var hearingMember2Ceo = Transition.Create(hearingMember, ceo);
var ceo2Ds = Transition.Create(ceo, cmo);
var ds2Ceo = Transition.Create(cmo, ceo);
var ceo2HearingMember = Transition.Create(ceo, hearingMember);

var graph = new Graph([hearingMember2Ceo, ceo2Ds, ds2Ceo, ceo2HearingMember]);
var aiTeam = new GroupChat(
    members: [hearingMember, ceo, cmo],
    admin: admin,
    workflow: graph);

// start the chat
// generate a greeting message to hearing member from ceo
var greetingMessage = await ceo.SendAsync("generate a greeting message to hearing memeber");
await ceo.SendMessageToGroupAsync(
    groupChat: aiTeam,
    chatHistory: [greetingMessage],
    maxRound: 20);
