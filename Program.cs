﻿// create AI team
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
// Create the CEO
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

// Create the cmo
var kernelBuilder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: gpt3_5, openAIClient: openaiClient);
var bingSearch = new BingConnector(bingSearchAPIKey);
var webSearchPlugin = new WebSearchEnginePlugin(bingSearch);
kernelBuilder.Plugins.AddFromObject(webSearchPlugin);
var kernel = kernelBuilder.Build();
var cmo = new SemanticKernelAgent(
       kernel: kernel,
       name: "CMO",
       systemMessage: """
       You are CMO, you report to Elon and you answer all market-related question.

       To make sure you have the most up-to-date information, you can use the web search plugin to search for information on the web before answering questions.
       """)
    .RegisterMessageConnector()
    .RegisterPrintFormatMessageHook();

// Create the hearing member
var user = new UserProxyAgent(name: "user");

// Create the group admin
var admin = new OpenAIChatAgent(
    openAIClient: openaiClient,
    name: "admin",
    modelName: gpt_4,
    seed: seed,
    systemMessage: "You are the group admin.")
    .RegisterMessageConnector();

// Create the AI team
// define the transition among group members
// we only allow the following transitions:
// user -> ceo
// ceo -> cmo
// cmo -> ceo
// ceo -> user

var user2Ceo = Transition.Create(user, ceo);
var ceo2Cmo = Transition.Create(ceo, cmo);
var cmo2Ceo = Transition.Create(cmo, ceo);
var ceo2User = Transition.Create(ceo, user);

var graph = new Graph([user2Ceo, ceo2Cmo, cmo2Ceo, ceo2User]);
var aiTeam = new GroupChat(
    members: [user, ceo, cmo],
    admin: admin,
    workflow: graph);

// start the chat
// generate a greeting message to hearing member from ceo
var greetingMessage = await ceo.SendAsync("You are in the QA session, generate a greeting message to the attenders");
var userQuestion = await user.SendAsync("create a question to ask the CEO");
await ceo.SendMessageToGroupAsync(
    groupChat: aiTeam,
    chatHistory: [greetingMessage, userQuestion],
    maxRound: 20);
