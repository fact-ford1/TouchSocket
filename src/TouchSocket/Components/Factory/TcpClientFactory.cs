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

using System.Threading;
using System.Threading.Tasks;
using TouchSocket.Core;

namespace TouchSocket.Sockets;

/// <summary>
/// 适用于Tcp客户端的连接工厂。
/// </summary>
/// <typeparam name="TClient">表示Tcp客户端的类型参数，必须实现ITcpClient接口。</typeparam>
public abstract class TcpClientFactory<TClient> : ConnectableClientFactory<TClient> where TClient : class, ITcpClient
{
    /// <summary>
    /// 处理Tcp客户端的释放操作。
    /// </summary>
    /// <param name="client">要释放的Tcp客户端。</param>
    public override void DisposeClient(TClient client)
    {
        client.ShutdownAsync(System.Net.Sockets.SocketShutdown.Both).GetFalseAwaitResult();
        base.DisposeClient(client);
    }
}

/// <summary>
/// 适用于基于<see cref="TcpClient"/>的连接工厂。
/// </summary>
public sealed class TcpClientFactory : TcpClientFactory<TcpClient>
{
    /// <inheritdoc/>
    protected override async Task<TcpClient> CreateClient(TouchSocketConfig config)
    {
        var client = new TcpClient();
        await client.SetupAsync(config).ConfigureAwait(EasyTask.ContinueOnCapturedContext);
        await client.ConnectAsync((int)this.ConnectTimeout.TotalMilliseconds, CancellationToken.None).ConfigureAwait(EasyTask.ContinueOnCapturedContext);
        return client;
    }
}