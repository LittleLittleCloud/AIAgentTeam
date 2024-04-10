using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIAgentTeam;

public static class AgentExtension
{
    public static MiddlewareStreamingAgent<OpenAIChatAgent> RegisterMessageConnector(this OpenAIChatAgent agent)
    {
        var messageConnector = new OpenAIChatRequestMessageConnector();
        return agent.RegisterStreamingMiddleware(messageConnector)
            .RegisterMiddleware(messageConnector);
    }

    public static MiddlewareStreamingAgent<SemanticKernelAgent> RegisterMessageConnector(this SemanticKernelAgent agent)
    {
        var messageConnector = new SemanticKernelChatMessageContentConnector();
        return agent.RegisterStreamingMiddleware(messageConnector)
            .RegisterMiddleware(messageConnector);
    }
}
