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

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TouchSocket.Core;
using TouchSocket.Http;
using TouchSocket.Rpc;
using TouchSocket.Sockets;

namespace TouchSocket.WebApi.Swagger;

/// <summary>
/// SwaggerPlugin
/// </summary>
public sealed class SwaggerPlugin : PluginBase, IServerStartedPlugin, IHttpPlugin
{
    private readonly ILog m_logger;
    private readonly IResolver m_resolver;
    private readonly Dictionary<string, byte[]> m_swagger = new Dictionary<string, byte[]>();

    /// <summary>
    /// SwaggerPlugin
    /// </summary>
    public SwaggerPlugin(IResolver resolver, ILog logger)
    {
        this.m_resolver = resolver;
        this.m_logger = logger;
    }

    /// <summary>
    /// 是否在浏览器打开Swagger页面
    /// </summary>
    public bool LaunchBrowser { get; set; }

    /// <summary>
    /// 访问Swagger的前缀，默认“swagger”
    /// </summary>
    public string Prefix { get; set; } = "swagger";

    /// <inheritdoc/>
    public async Task OnServerStarted(IServiceBase sender, ServiceStateEventArgs e)
    {
        if (e.ServerState != ServerState.Running)
        {
            return;
        }
        var webApiParserPlugin = sender.PluginManager.Plugins.OfType<WebApiParserPlugin>().FirstOrDefault();

        if (webApiParserPlugin == null)
        {
            this.m_logger.Warning($"该服务器中似乎没有添加{nameof(WebApiParserPlugin)}。");
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames();
        foreach (var item in names)
        {
            using (var stream = assembly.GetManifestResourceStream(item))
            {
                var bytes = stream.ReadAllToByteArray();
                var prefix = this.Prefix.IsNullOrEmpty() ? "/" : (this.Prefix.StartsWith("/") ? this.Prefix : $"/{this.Prefix}");
                var name = item.Replace("TouchSocket.WebApi.Swagger.api.", string.Empty);
                if (name == "openapi.json")
                {
                    try
                    {
                        bytes = this.BuildOpenApi(webApiParserPlugin);
                    }
                    catch (Exception ex)
                    {
                        this.m_logger?.Debug(this, ex);
                    }
                }
                var key = prefix == "/" ? $"/{name}" : $"{prefix}/{name}";
                this.m_swagger.Add(key, bytes);
            }
        }
        await e.InvokeNext().ConfigureAwait(EasyTask.ContinueOnCapturedContext);

        if (this.LaunchBrowser)
        {
            var monitor = (sender as ITcpServiceBase).Monitors.First();

            var iphost = monitor.Option.IpHost;
            string host;
            if (iphost.IsLoopback || iphost.DnsSafeHost == "127.0.0.1" || iphost.DnsSafeHost == "0.0.0.0")
            {
                host = "127.0.0.1";
            }
            else
            {
                host = iphost.DnsSafeHost;
            }

            var scheme = monitor.Option.UseSsl ? "https" : "http";

            var prefix = this.Prefix.IsNullOrEmpty() ? "/" : (this.Prefix.StartsWith("/") ? this.Prefix : $"/{this.Prefix}");
            var index = prefix == "/" ? $"/index.html" : $"{prefix}/index.html";
            this.OpenWebLink($"{scheme}://{host}:{iphost.Port}{index}");
        }
    }

    /// <summary>
    /// 设置访问Swagger的前缀，默认“swagger”
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public SwaggerPlugin SetPrefix(string value)
    {
        this.Prefix = value;
        return this;
    }

    /// <summary>
    /// 在浏览器打开Swagger页面
    /// </summary>
    /// <returns></returns>
    public SwaggerPlugin UseLaunchBrowser()
    {
        this.LaunchBrowser = true;
        return this;
    }

    /// <summary>
    /// 检查类型是否是 IFormFileCollection 类型
    /// </summary>
    /// <param name="type"><see cref="Type"/></param>
    /// <returns><see cref="bool"/></returns>
    private static bool IsFormFileCollection(Type type)
    {
        // 如果是 MultifileCollection 类型则直接返回
        if (typeof(IMultifileCollection).IsAssignableFrom(type))
        {
            return true;
        }

        // 处理 IFormFile 集合类型
        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            // 检查是否是 IFormFile 数组类型
            if (type.IsArray)
            {
                // 获取数组元素类型
                var elementType = type.GetElementType();

                // 检查元素类型是否是 IFormFile 类型
                if (elementType != null
                    && typeof(IFormFile).IsAssignableFrom(elementType))
                {
                    return true;
                }
            }
            // 检查是否是 IFormFile 集合类型
            else
            {
                // 检查集合项类型是否是 IFormFile 类型
                if (type.IsGenericType && type.GenericTypeArguments.Length == 1 && typeof(IFormFile).IsAssignableFrom(type.GenericTypeArguments[0]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void OpenWebLink(string url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            this.m_logger?.Debug(this, ex);
        }
    }

    #region Build

    private void AddSchemaType(Type type, in List<Type> types)
    {
        if (type.IsArray)
        {
            type = type.GetElementType();
        }
        else if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType && type.GenericTypeArguments.Length == 1)
        {
            type = type.GetGenericArguments()[0];
        }

        if (type.IsPrimitive || type == typeof(string))
        {
            return;
        }

        if (types.Contains(type))
        {
            return;
        }
        if (this.ParseDataTypes(type) != OpenApiDataTypes.Object)
        {
            return;
        }
        types.Add(type);

        foreach (var item in type.GetProperties())
        {
            this.AddSchemaType(item.PropertyType, types);
        }
    }

    private byte[] BuildOpenApi(WebApiParserPlugin webApiParserPlugin)
    {
        var openApiRoot = new OpenApiRoot();
        openApiRoot.Info = new OpenApiInfo();

        var paths = new Dictionary<string, OpenApiPath>();

        var schemaTypeList = new List<Type>();
        var rpcMethods = new List<RpcMethod>();
        foreach (var mappingMethod in webApiParserPlugin.Mapping)
        {
            if (rpcMethods.Contains(mappingMethod.RpcMethod))
            {
                continue;
            }

            if (mappingMethod.IsRegex)
            {
                continue;
            }

            this.BuildHttpMethod(mappingMethod.Url, mappingMethod.HttpMethod, mappingMethod.RpcMethod, schemaTypeList, paths);

            rpcMethods.Add(mappingMethod.RpcMethod);
        }

        openApiRoot.Paths = paths;

        openApiRoot.Components = this.GetComponents(schemaTypeList);

        var jsonSetting = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        return JsonConvert.SerializeObject(openApiRoot, Formatting.Indented, jsonSetting).ToUtf8Bytes();
    }

    private void BuildHttpMethod(string url, HttpMethod httpMethod, RpcMethod rpcMethod, in List<Type> schemaTypeList, in Dictionary<string, OpenApiPath> paths)
    {
        //解析post
        var openApiPath = new OpenApiPath();

        var openApiPathValue = new OpenApiPathValue
        {
            Tags = this.GetTags(rpcMethod),
            Description = rpcMethod.GetDescription(),
            Summary = rpcMethod.GetDescription()
        };

        var openApiParameters = new List<OpenApiParameter>();

        var parameters = rpcMethod.GetNormalParameters().ToList();
        if (parameters.Count > 0)
        {
            var last = parameters.LastOrDefault();
            if (!(last.Type.IsPrimitive || last.Type == typeof(string)))
            {
                parameters.Remove(last);

                if (!this.ParseDataTypes(last.Type).IsPrimitive())
                {
                    this.AddSchemaType(last.Type, schemaTypeList);

                    var body = new OpenApiRequestBody();
                    body.Content = new Dictionary<string, OpenApiContent>();
                    var content = new OpenApiContent();
                    content.Schema = this.CreateSchema(last.Type);
                    body.Content.Add("application/json", content);
                    body.Content.Add("text/xml", content);
                    body.Content.Add("text/plain", content);
                    body.Content.Add("text/json", content);
                    body.Content.Add("application/xml", content);
                    openApiPathValue.RequestBody = body;
                }
            }

            foreach (var parameter in parameters)
            {
                if (this.ParseDataTypes(parameter.Type).IsPrimitive())
                {
                    var openApiParameter = this.GetParameter(parameter.ParameterInfo);
                    openApiParameter.In = "query";
                    this.AddSchemaType(parameter.Type, schemaTypeList);
                    openApiParameters.Add(openApiParameter);
                }
            }
        }

        openApiPathValue.Parameters = openApiParameters.Count > 0 ? openApiParameters : default;

        this.BuildResponse(rpcMethod, openApiPathValue, schemaTypeList);

        openApiPath.Add(httpMethod.ToString().ToLower(), openApiPathValue);
        paths.Add(url, openApiPath);
    }

    private void BuildResponse(RpcMethod rpcMethod, in OpenApiPathValue openApiPathValue, in List<Type> schemaTypeList)
    {
        var openApiResponse = new OpenApiResponse();
        openApiResponse.Description = "Success";

        if (rpcMethod.HasReturn)
        {
            openApiResponse.Content = new Dictionary<string, OpenApiContent>();
            var openApiContent = new OpenApiContent();
            openApiResponse.Content.Add("application/json", openApiContent);
            openApiResponse.Content.Add("text/xml", openApiContent);
            openApiResponse.Content.Add("text/plain", openApiContent);
            openApiResponse.Content.Add("text/json", openApiContent);
            openApiResponse.Content.Add("application/xml", openApiContent);
            openApiContent.Schema = this.CreateSchema(rpcMethod.ReturnType);
            this.AddSchemaType(rpcMethod.ReturnType, schemaTypeList);
        }

        openApiPathValue.Responses = new Dictionary<string, OpenApiResponse>();
        openApiPathValue.Responses.Add("200", openApiResponse);
    }

    private OpenApiProperty CreateProperty(Type type, string description)
    {
        var openApiProperty = new OpenApiProperty();
        var dataTypes = this.ParseDataTypes(type);
        openApiProperty.Description = description;

        switch (dataTypes)
        {
            case OpenApiDataTypes.String:
            case OpenApiDataTypes.Number:
            case OpenApiDataTypes.Boolean:
            case OpenApiDataTypes.Integer:
                {
                    openApiProperty.Type = dataTypes;
                    break;
                }
            case OpenApiDataTypes.Binary:
                break;

            case OpenApiDataTypes.BinaryCollection:
                break;

            case OpenApiDataTypes.Record:
                break;

            case OpenApiDataTypes.Tuple:
                break;

            case OpenApiDataTypes.Array:
                {
                    openApiProperty.Type = dataTypes;

                    var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                    openApiProperty.Items = this.CreateProperty(elementType, elementType.GetDescription());
                }
                break;

            case OpenApiDataTypes.Object:
                {
                    openApiProperty.Ref = $"#/components/schemas/{type.Name}";
                    break;
                }
            case OpenApiDataTypes.Struct:
                break;

            case OpenApiDataTypes.Any:
                break;

            default:
                break;
        }

        openApiProperty.Format = this.GetSchemaFormat(type);

        return openApiProperty;
    }

    private OpenApiSchema CreateSchema(Type type)
    {
        var schema = new OpenApiSchema();
        var dataTypes = this.ParseDataTypes(type);

        switch (dataTypes)
        {
            case OpenApiDataTypes.String:
            case OpenApiDataTypes.Number:
            case OpenApiDataTypes.Boolean:
            case OpenApiDataTypes.Integer:
                {
                    schema.Type = dataTypes;
                    break;
                }
            case OpenApiDataTypes.Binary:
                break;

            case OpenApiDataTypes.BinaryCollection:
                break;

            case OpenApiDataTypes.Record:
                break;

            case OpenApiDataTypes.Tuple:
                break;

            case OpenApiDataTypes.Array:
                {
                    schema.Type = dataTypes;
                    schema.Items = this.CreateSchema(type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0]);
                }
                break;

            case OpenApiDataTypes.Object:
                {
                    if (type.IsEnum)
                    {
                        var list = new List<long>();
                        foreach (var item in Enum.GetValues(type))
                        {
                            list.Add(Convert.ToInt64(item));
                        }
                        schema.Enum = list.ToArray();

                        schema.Type = OpenApiDataTypes.Integer;
                    }
                    else
                    {
                        schema.Ref = $"#/components/schemas/{this.GetSchemaName(type)}";
                    }

                    break;
                }
            case OpenApiDataTypes.Struct:
                break;

            case OpenApiDataTypes.Any:
                break;

            default:
                break;
        }

        schema.Format = this.GetSchemaFormat(type);
        return schema;
    }

    private OpenApiComponent GetComponents(List<Type> types)
    {
        if (types.Count == 0)
        {
            return default;
        }
        var components = new OpenApiComponent();
        components.Schemas = new Dictionary<string, OpenApiSchema>();
        foreach (var type in types)
        {
            var schema = this.CreateSchema(type);
            var properties = new Dictionary<string, OpenApiProperty>();
            foreach (var propertyInfo in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                properties.Add(propertyInfo.Name, this.CreateProperty(propertyInfo.PropertyType, propertyInfo.GetDescription()));
            }
            schema.Properties = properties.Count == 0 ? default : properties;
            components.Schemas.TryAdd(this.GetSchemaName(type), schema);
        }
        return components;
    }

    private string GetIn(Type type)
    {
        if (type.IsPrimitive || type == typeof(string))
        {
            return "query";
        }

        return null;
    }

    private OpenApiParameter GetParameter(ParameterInfo parameterInfo)
    {
        var openApiParameter = new OpenApiParameter();
        openApiParameter.Name = parameterInfo.Name;
        openApiParameter.Description = parameterInfo.GetDescription();

        var openApiSchema = this.CreateSchema(parameterInfo.ParameterType);

        openApiParameter.Schema = openApiSchema;

        return openApiParameter;
    }

    private string GetSchemaFormat(Type type)
    {
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
                return "int32";

            case TypeCode.Int64:
            case TypeCode.UInt64:
                return "int64";

            case TypeCode.Single:
                return "float";

            case TypeCode.Double:
            case TypeCode.Decimal:
                return "double";

            case TypeCode.DateTime:
                return "date-time";

            case TypeCode.Boolean:
            case TypeCode.Char:
            case TypeCode.String:
                return default;

            default:
                return "object";
        }
    }

    private string GetSchemaName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var stringBuilder = new StringBuilder();
        foreach (var item in type.GetGenericArguments())
        {
            stringBuilder.Append(this.GetSchemaName(item));
        }
        stringBuilder.Append(type.Name.Substring(0, type.Name.IndexOf('`')));
        return stringBuilder.ToString();
    }

    private IEnumerable<string> GetTags(RpcMethod rpcMethod)
    {
        var tags = new List<string>();
        foreach (var item in rpcMethod.ServerFromType.GetCustomAttributes())
        {
            if (item is SwaggerDescriptionAttribute descriptionAttribute)
            {
                if (descriptionAttribute.Groups != null)
                {
                    tags.AddRange(descriptionAttribute.Groups);
                }
            }
        }

        foreach (var item in rpcMethod.Info.GetCustomAttributes())
        {
            if (item is SwaggerDescriptionAttribute descriptionAttribute)
            {
                if (descriptionAttribute.Groups != null)
                {
                    tags.AddRange(descriptionAttribute.Groups);
                }
            }
        }

        if (tags.Count == 0)
        {
            tags.Add(rpcMethod.ServerFromType.Name);
        }

        return tags;
    }

    private OpenApiDataTypes ParseDataTypes(Type type)
    {
        // 空检查
        if (type is null)
        {
            return OpenApiDataTypes.Any;
        }

        return type switch
        {
            // 字符串
            _ when typeof(string).IsAssignableFrom(type)
                || typeof(char).IsAssignableFrom(type) => OpenApiDataTypes.String,
            // 整数
            _ when type.IsInteger() => OpenApiDataTypes.Integer,

            // 数值
            _ when type.IsDecimal() => OpenApiDataTypes.Number,
            // 布尔值
            _ when typeof(bool).IsAssignableFrom(type) => OpenApiDataTypes.Boolean,
            // 日期
            _ when typeof(DateTime).IsAssignableFrom(type)
                || typeof(DateTimeOffset).IsAssignableFrom(type) => OpenApiDataTypes.String,
            // 二进制
            _ when typeof(IFormFile).IsAssignableFrom(type) => OpenApiDataTypes.Binary,
            // 二进制集合
            _ when IsFormFileCollection(type) => OpenApiDataTypes.BinaryCollection,
            // 记录值
            _ when type.IsDictionary() => OpenApiDataTypes.Record,
            // 元组值
            _ when type.IsValueTuple() => OpenApiDataTypes.Tuple,
            // 数组
            _ when type.IsArray || (typeof(IEnumerable).IsAssignableFrom(type)
                && type.IsGenericType
                && type.GenericTypeArguments.Length == 1) => OpenApiDataTypes.Array,
            // 对象
            _ when type.IsEnum || (type != typeof(object)
                && type.IsClass
                && Type.GetTypeCode(type) == TypeCode.Object) => OpenApiDataTypes.Object,
            // 结构
            _ when type.IsValueType
                && !type.IsPrimitive
                && !type.IsEnum => OpenApiDataTypes.Struct,
            // 缺省值
            _ => OpenApiDataTypes.Any
        };
    }

    #endregion Build

    /// <inheritdoc/>
    public async Task OnHttpRequest(IHttpSessionClient client, HttpContextEventArgs e)
    {
        var context = e.Context;
        var request = context.Request;
        //var response = context.Response;

        if (this.m_swagger.TryGetValue(request.RelativeURL, out var bytes))
        {
            e.Handled = true;
            context.Response
                .SetStatus()
                .SetContentTypeByExtension(Path.GetExtension(request.RelativeURL))
                .SetContent(bytes);
            await context.Response.AnswerAsync().ConfigureAwait(EasyTask.ContinueOnCapturedContext);
            return;
        }
        await e.InvokeNext().ConfigureAwait(EasyTask.ContinueOnCapturedContext);
    }

}