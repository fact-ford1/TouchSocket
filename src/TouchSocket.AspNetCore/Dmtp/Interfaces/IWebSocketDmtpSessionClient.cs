//------------------------------------------------------------------------------
//  此代码版权（除特别声明或在XREF结尾的命名空间的代码）归作者本人若汝棋茗所有
//  源代码使用协议遵循本仓库的开源协议及附加协议，若本仓库没有设置，则按MIT开源协议授权
//  CSDN博客：https://blog.csdn.net/qq_40374647
//  哔哩哔哩视频：https://space.bilibili.com/94253567
//  Gitee源代码仓库：https://gitee.com/RRQM_Home
//  Github源代码仓库：https://github.com/RRQM
//  API首页：https://touchsocket.net/
//  交流QQ群：234762506
//  感谢您的下载和使用
//------------------------------------------------------------------------------

using Microsoft.AspNetCore.Http;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace TouchSocket.Dmtp.AspNetCore;

/// <summary>
/// 基于WebSocket协议的Dmtp服务器辅助客户端。
/// </summary>
public interface IWebSocketDmtpSessionClient : IDependencyClient, IIdClient, IDmtpActorObject, IClosableClient, IResolverConfigObject, IOnlineClient
{
    /// <summary>
    /// Http上下文
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// 包含该客户端的服务器。
    /// </summary>
    IWebSocketDmtpService Service { get; }
}