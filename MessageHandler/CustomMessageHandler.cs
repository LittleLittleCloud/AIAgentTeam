#pragma warning disable

using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.SemanticKernel;
using AutoGen;
using Azure.AI.OpenAI;
using global::Senparc.NeuChar.Entities;
using global::Senparc.Weixin;
using global::Senparc.Weixin.MP.Entities;
using global::Senparc.Weixin.MP.Entities.Request;
using global::Senparc.Weixin.MP.MessageContexts;
using global::Senparc.Weixin.MP.MessageHandlers;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel;
using Senparc.CO2NET.Helpers;
using Senparc.NeuChar.App.AppStore.Api;
using Senparc.NeuChar.Entities.Request;
using Senparc.Weixin.MP.Containers;
namespace AIAgentTeam.MessageHandler;

/// <summary>
/// 自定义MessageHandler
/// 把MessageHandler作为基类，重写对应请求的处理方法
/// </summary>
public partial class CustomMessageHandler : MessageHandler<DefaultMpMessageContext>  /*如果不需要自定义，可以直接使用：MessageHandler<DefaultMpMessageContext> */
{
    /*
     * 重要提示：v1.5起，MessageHandler提供了一个DefaultResponseMessage的抽象方法，
     * DefaultResponseMessage必须在子类中重写，用于返回没有处理过的消息类型（也可以用于默认消息，如帮助信息等）；
     * 其中所有原OnXX的抽象方法已经都改为虚方法，可以不必每个都重写。若不重写，默认返回DefaultResponseMessage方法中的结果。
     */


    private string appId = Config.SenparcWeixinSetting.MpSetting.WeixinAppId;
    private string appSecret = Config.SenparcWeixinSetting.MpSetting.WeixinAppSecret;

    /// <summary>
    /// 为中间件提供生成当前类的委托
    /// </summary>
    public static Func<Stream, PostModel, int, IServiceProvider, CustomMessageHandler> GenerateMessageHandler = (stream, postModel, maxRecordCount, serviceProvider)
                     => new CustomMessageHandler(stream, postModel, maxRecordCount, false /* 是否只允许处理加密消息，以提高安全性 */, serviceProvider: serviceProvider);

    /// <summary>
    /// 自定义 MessageHandler
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="inputStream"></param>
    /// <param name="postModel"></param>
    /// <param name="maxRecordCount"></param>
    /// <param name="onlyAllowEncryptMessage"></param>
    public CustomMessageHandler(Stream inputStream, PostModel postModel, int maxRecordCount = 0, bool onlyAllowEncryptMessage = false, IServiceProvider serviceProvider = null)
        : base(inputStream, postModel, maxRecordCount, onlyAllowEncryptMessage, serviceProvider: serviceProvider)
    {
        //这里设置仅用于测试，实际开发可以在外部更全局的地方设置，
        //比如MessageHandler<MessageContext>.GlobalGlobalMessageContext.ExpireMinutes = 3。
        GlobalMessageContext.ExpireMinutes = 3;

        OnlyAllowEncryptMessage = true;//是否只允许接收加密消息，默认为 false
    }


    /// <summary>
    /// 处理文字请求
    /// </summary>
    /// <param name="requestMessage">请求消息</param>
    /// <returns></returns>
    public override async Task<IResponseMessageBase> OnTextRequestAsync(RequestMessageText requestMessage)
    {
        var defaultResponseMessage = base.CreateResponseMessage<ResponseMessageText>();

        // 下方 RequestHandler 为消息关键字提供了便捷的处理方式，当然您也可以使用传统的 if...else... 对 requestMessage.Content 进行判断
        var requestHandler = await requestMessage.StartHandler()
            //关键字不区分大小写，按照顺序匹配成功后将不再运行下面的逻辑
            .Keyword("马斯克", () =>
            {
                var requestMessageContent = requestMessage.Content;


                var openaiAPI = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("OPENAI_API_KEY is not set");
                var bingSearchAPIKey = Environment.GetEnvironmentVariable("BING_API_KEY") ?? throw new Exception("BING_API_KEY is not set");
                var gpt3_5 = "gpt-3.5-turbo";
                var gpt_4 = "gpt-4-turbo";
                var openaiClient = new OpenAIClient(openaiAPI);

                // Create the CEO
                var ceo = new OpenAIChatAgent(
                    openAIClient: openaiClient,
                    name: "Elon Musk",
                    modelName: gpt3_5,
                    systemMessage: """
    You are Elon Musk, CEO of Tesla. You are in a hearing about Tesla.
    When a question about tesla is asked, You can ask your subordinates to answer the question.

    Here are your subordinates:
    - cmo: Chief Marketing Officer who is responsible for answering all market-related questions.
    """)
                    .RegisterMessageConnector()
                    .RegisterPrintMessage();

                // Create the cmo
                var kernelBuilder = Kernel.CreateBuilder()
                    .AddOpenAIChatCompletion(modelId: gpt3_5, apiKey: openaiAPI);
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
                var user = new UserProxyAgent(name: "hearingMember")
                .RegisterMiddleware(async (messages, option, innerAgent, ct) =>
                {
                    // If the next speaker is user again, it indicates that the previous question from user is answered
                    // and the group chat is waiting for another question from user.
                    // In this case, we can simply terminate the conversation by sending a specific message.
                    return new TextMessage(Role.Assistant, "Wait for user input");
                });

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

                var hearingMember2Ceo = Transition.Create(user, ceo);
                var ceo2Ds = Transition.Create(ceo, cmo);
                var ds2Ceo = Transition.Create(cmo, ceo);
                var ceo2HearingMember = Transition.Create(ceo, user);

                var graph = new Graph([hearingMember2Ceo, ceo2Ds, ds2Ceo, ceo2HearingMember]);
                var aiTeam = new GroupChat(
                    members: [user, ceo, cmo],
                    admin: admin,
                    workflow: graph);

                // start the chat
                var question = new TextMessage(Role.User, requestMessageContent, from: user.Name);
                var maxRound = 20;
                var chatHistory = new List<IMessage> { question };
                for(var i = 0; i < maxRound; i++)
                {
                    var reply = user.SendMessageToGroupAsync(
                        groupChat: aiTeam,
                        chatHistory: chatHistory,
                        maxRound: 1).Result;
                    var lastReply = reply.Last();

                    if (lastReply.From == user.Name && lastReply.GetContent() == "Wait for user input")
                    {
                        var lastReplyFromCEO = chatHistory.Last(m => m.From == ceo.Name);
                        var lastReplyFromCEOContent = lastReplyFromCEO.GetContent();
                        defaultResponseMessage.Content = lastReplyFromCEOContent;

                        return defaultResponseMessage;
                    }

                    chatHistory.Add(lastReply);
                }

                throw new Exception("The code should not reach here");
            })
            //当 Default 使用异步方法时，需要写在最后一个，且 requestMessage.StartHandler() 前需要使用 await 等待异步方法执行；
            //当 Default 使用同步方法，不一定要在最后一个,并且不需要使用 await
            .Default(async () =>
            {
                defaultResponseMessage.Content = $"您刚才发送了文字信息：{requestMessage.Content}";
                return defaultResponseMessage;
            });

        return requestHandler.GetResponseMessage() as IResponseMessageBase;
    }


    public override IResponseMessageBase DefaultResponseMessage(IRequestMessageBase requestMessage)
    {
        /* 所有没有被处理的消息会默认返回这里的结果，
        * 因此，如果想把整个微信请求委托出去（例如需要使用分布式或从其他服务器获取请求），
        * 只需要在这里统一发出委托请求，如：
        * var responseMessage = MessageAgent.RequestResponseMessage(agentUrl, agentToken, RequestDocument.ToString());
        * return responseMessage;
        */

        var responseMessage = this.CreateResponseMessage<ResponseMessageText>();
        responseMessage.Content = $"这条消息来自DefaultResponseMessage。\r\n您收到这条消息，表明该公众号没有对【{requestMessage.MsgType}】类型做处理。";
        return responseMessage;
    }


    public override async Task<IResponseMessageBase> OnUnknownTypeRequestAsync(RequestMessageUnknownType requestMessage)
    {
        /*
         * 此方法用于应急处理SDK没有提供的消息类型，
         * 原始XML可以通过requestMessage.RequestDocument（或this.RequestDocument）获取到。
         * 如果不重写此方法，遇到未知的请求类型将会抛出异常（v14.8.3 之前的版本就是这么做的）
         */
        var msgType = Senparc.NeuChar.Helpers.MsgTypeHelper.GetRequestMsgTypeString(requestMessage.RequestDocument);
        var responseMessage = this.CreateResponseMessage<ResponseMessageText>();
        responseMessage.Content = "未知消息类型：" + msgType;

        WeixinTrace.SendCustomLog("未知请求消息类型", requestMessage.RequestDocument.ToString());//记录到日志中

        return responseMessage;
    }

    #region 重写执行过程

    public override async Task OnExecutingAsync(CancellationToken cancellationToken)
    {
        //演示：MessageContext.StorageData

        var currentMessageContext = await base.GetUnsafeMessageContext();//为了在分布式缓存下提高读写效率，使用此方法，如果需要获取实时数据，应该使用 base.GetCurrentMessageContext()
        if (currentMessageContext.StorageData == null || !(currentMessageContext.StorageData is int))
        {
            currentMessageContext.StorageData = (int)0;
            //await GlobalMessageContext.UpdateMessageContextAsync(currentMessageContext);//储存到缓存
        }
        await base.OnExecutingAsync(cancellationToken);
    }

    public override async Task OnExecutedAsync(CancellationToken cancellationToken)
    {
        //演示：MessageContext.StorageData

        var currentMessageContext = await base.GetUnsafeMessageContext();//为了在分布式缓存下提高读写效率，使用此方法，如果需要获取实时数据，应该使用 base.GetCurrentMessageContext()
        currentMessageContext.StorageData = ((int)currentMessageContext.StorageData) + 1;
        GlobalMessageContext.UpdateMessageContext(currentMessageContext);//储存到缓存
        await base.OnExecutedAsync(cancellationToken);
    }

    #endregion
}