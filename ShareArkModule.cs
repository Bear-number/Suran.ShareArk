using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Alife.Foundation;
using Alife.Function.FunctionCaller;
using Alife.Framework;
using Microsoft.Extensions.Logging;

namespace Suran.ShareArk;

public class ShareArkConfig
{
    [DisplayName("运行·功能文档常驻")]
    [Description("开：功能说明常驻系统提示词，模型随时看得到工具；关：隐式加载省 token，模型先输出入口标签再读文档。发卡片/解析链接多是主人明确要求时用，默认关")]
    public bool DocumentAlwaysResident { get; set; } = false;

    [DisplayName("第三方接口基地址")]
    [Description("第三方签名服务基地址。")]
    public string BaseUrl { get; set; } = "";

    [DisplayName("接口Token")]
    [Description("第三方接口凭证 key，可留空。")]
    public string ApiKey { get; set; } = "";

    [DisplayName("网络·代理地址")]
    [Description("出站请求走这个代理，支持 http:// 与 socks5://；留空直连。境外平台（YouTube/Spotify/Apple Music/X）抓不到时填这里，改完不用重载插件")]
    public string ProxyUrl { get; set; } = "";

    [DisplayName("网络·仅境外平台走代理")]
    [Description("开：只有 YouTube/Spotify/Apple Music/SoundCloud/Vimeo/TikTok/X/Instagram/GitHub/Steam 这类境外站走代理，国内接口与签名服务直连；关：全部出站请求都走代理")]
    public bool ProxyForeignOnly { get; set; } = true;

    [DisplayName("协议端·地址")]
    [Description("留空 = 自动跟随「QQ 聊天」插件的配置（推荐）。填了就用这里的，例如 ws://127.0.0.1:3001")]
    public string WsUrl { get; set; } = "";

    [DisplayName("协议端·访问令牌")]
    [Description("与协议端配置的 token 一致。留空 = 跟随「QQ 聊天」插件。填了地址却没填这里，协议端一开 token 就会连不上")]
    public string OneBotToken { get; set; } = "";

    [DisplayName("音乐卡签名接口地址")]
    [Description("完整地址，可留空；留空时用基地址自动探测后缀拼出")]
    public string MusicArkUrl { get; set; } = "";

    [DisplayName("分享卡签名接口地址")]
    [Description("完整地址，可留空；留空时用基地址自动探测后缀拼出")]
    public string ShareArkUrl { get; set; } = "";

    [DisplayName("歌曲搜索接口地址")]
    [Description("完整地址，可留空；留空时用基地址自动探测后缀拼出。可含{pf}占位符，会被替换为163/qq/kugou")]
    public string SearchUrl { get; set; } = "";

    [DisplayName("B站搜索接口地址")]
    [Description("B站视频搜索接口完整地址，留空走官方接口。")]
    public string BiliSearchUrl { get; set; } = "";

    [DisplayName("容灾·签名失败冷却秒")]
    [Description("签名接口报错后，这么多秒内不再重试（直接走备用方案），避免每次点歌都白等三四秒。0 = 每次都试")]
    public int SignFailureCooldownSeconds { get; set; } = 300;

    [DisplayName("容灾·启用官方接口回退")]
    [Description("第三方接口失败时是否自动改用官方接口。")]
    public bool EnableOfficialFallback { get; set; } = true;

    [DisplayName("容灾·备用签名基地址")]
    [Description("主站失败后按顺序尝试。多个用逗号或换行分隔；每个可写成 base|key（key 留空则沿用上面的 ApiKey）。"
        + "适合再填一家同协议（qq-musicArk / qq-shareArk / *-music-search）的签名站。")]
    public string BackupBaseUrls { get; set; } = "";

    [DisplayName("容灾·启用公共音乐卡签名")]
    [Description("主站与备用站都失败时，音乐卡再试 OIAPI 公共签名（免 key，实测可用）。分享卡没有公共备用，仍走图文兜底。")]
    public bool EnableBuiltinMusicSign { get; set; } = true;

    [DisplayName("容灾·公共音乐卡签名地址")]
    [Description("默认 OIAPI 的 QQMusicJSONArk 接口；换服务时只改这一行。")]
    public string BuiltinMusicArkUrl { get; set; } = "https://oiapi.net/api/QQMusicJSONArk";

    [DisplayName("容灾·额外音乐卡签名地址")]
    [Description("完整地址，一行一个，可写 url|key。适合 OIAPI 风格的第三方音乐卡签名（GET，参数 url/song/singer/cover/jump/format，format 用 163，成功码 0/200/1 都认）。")]
    public string ExtraMusicSignUrls { get; set; } = "";

    [DisplayName("容灾·额外分享卡签名地址")]
    [Description("完整地址，一行一个，可写 url|key。用于路径不标准的第三方分享卡签名（GET，参数 title/desc/url/preview/prompt，成功码 0/200）。")]
    public string ExtraShareSignUrls { get; set; } = "";

    [DisplayName("容灾·启用公共音乐平台接口")]
    [Description("免费、无需注册：洛苏API（网易云/QQ/酷狗点歌，详情带直链与封面）与糖豆子API（QQ 搜索）。官方音乐接口失败时自动补位。")]
    public bool EnablePublicMusicApis { get; set; } = true;

    [DisplayName("容灾·洛苏API基地址")]
    [Description("免费公共点歌接口，默认 https://api.luosu.top；换镜像只改这里。")]
    public string LuoSuBaseUrl { get; set; } = "https://api.luosu.top";

    [DisplayName("容灾·糖豆子API基地址")]
    [Description("免费公共 QQ 音乐搜索接口，默认 https://api.tangdouz.com；换镜像只改这里。")]
    public string TangDouZiBaseUrl { get; set; } = "https://api.tangdouz.com";

    // ── 功能开关：关闭后对应函数对AI不可用（默认全开，老配置缺省即开） ──
    [DisplayName("开关·搜歌")] [Description("关闭后 SearchMusic 不可用")]
    public bool EnableSearchMusic { get; set; } = true;
    [DisplayName("开关·发音乐/视频卡")] [Description("关闭后 SendArkCard 不可用")]
    public bool EnableSendArkCard { get; set; } = true;
    [DisplayName("开关·发通用分享卡")] [Description("关闭后 SendShareCard 不可用")]
    public bool EnableSendShareCard { get; set; } = true;
    [DisplayName("开关·B站链接解析")] [Description("关闭后 BiliParse 不可用")]
    public bool EnableBiliParse { get; set; } = true;
    [DisplayName("开关·UP主更新查询")] [Description("关闭后 BiliUpdates 不可用")]
    public bool EnableBiliUpdates { get; set; } = true;
    [DisplayName("开关·B站扫码登录")] [Description("关闭后 BiliQrLogin 不可用")]
    public bool EnableBiliQrLogin { get; set; } = true;
    [DisplayName("开关·Cookie刷新")] [Description("关闭后 BiliCookieRefresh 不可用")]
    public bool EnableBiliCookieRefresh { get; set; } = true;
    [DisplayName("开关·Cookie管理")] [Description("关闭后 BiliCookie 不可用")]
    public bool EnableBiliCookie { get; set; } = true;
    [DisplayName("开关·IP查询")] [Description("关闭后 IpLookup 不可用")]
    public bool EnableIpLookup { get; set; } = true;
    [DisplayName("开关·探测接口")] [Description("关闭后 ProbeBase 不可用")]
    public bool EnableProbeBase { get; set; } = true;
    [DisplayName("开关·列出全部接口")] [Description("关闭后 ProbeAll 不可用")]
    public bool EnableProbeAll { get; set; } = true;
    [DisplayName("开关·免费公开接口")] [Description("歌词走 LRCLIB 与网易云官方、封面与试听走 iTunes、通用链接卡自己抓页面 meta 解析；全部免注册免 key")]
    public bool EnableFreeSources { get; set; } = true;
    [DisplayName("开关·允许 microlink 兜底")] [Description("自抓标题失败时是否用 microlink 解析，默认关。")]
    public bool EnableMicrolink { get; set; } = false;
    [DisplayName("免费接口超时（秒）")] [Description("单个免费接口的超时；国际站直连经常不通，超时给短点别拖着整条链")]
    public int FreeSourceTimeoutSeconds { get; set; } = 12;
}

[Module("QQ分享卡片",
    "自己完成音乐卡与通用分享卡签名并直塞协议端，不依赖协议端的签名地址配置",
    defaultCategory: "苏染的工具/媒体与分享",
    EditorUI = typeof(ShareArkUI))]
public partial class ShareArkModule(
    XmlFunctionCaller functionCaller,
    ILogger<ShareArkModule> logger,
    Interactor<ShareArkModule> interactor
) : ChatBehaviour, IConfigurable<ShareArkConfig>
{
    public ShareArkConfig Configuration { get; set; } = null!;

    /// <summary>
    /// 协议端连接信息。
    ///
    /// 地址留空时跟随「QQ 聊天」插件的配置——那是唯一值得人填一次的地方。
    /// 换端口、给协议端开 token，都不用回来改这个插件，读它的配置就够。
    /// 读不到就退回上一个版本的默认值，行为和以前完全一致。
    /// </summary>
    (string Url, string Token, string Source) ResolveOneBot()
    {
        string url = (Configuration.WsUrl ?? "").Trim();
        string token = (Configuration.OneBotToken ?? "").Trim();

        if (url.Length > 0)
            return (url, token, "本插件");

        // 按角色存，所以换角色读的是那个角色自己的 QQ 配置——不同角色可能是不同 bot 账号，这是对的。
        // 第二条是兜底：StorageFolderPath 若已含角色层、或 StorageKey 为空，靠第一条拼不出来。
        string[] candidates =
        [
            Path.Combine(AlifePath.StorageFolderPath, Character.StorageKey,
                "Configuration", "Alife.Function.QChat.QChatService.json"),
            Path.Combine(AlifePath.StorageFolderPath,
                "Configuration", "Alife.Function.QChat.QChatService.json"),
        ];

        foreach (string path in candidates)
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                string sharedUrl = document.RootElement.TryGetProperty("Url", out JsonElement u) ? u.GetString() ?? "" : "";
                string sharedToken = document.RootElement.TryGetProperty("Token", out JsonElement t) ? t.GetString() ?? "" : "";
                if (sharedUrl.Trim().Length > 0)
                    return (sharedUrl.Trim(), token.Length > 0 ? token : sharedToken.Trim(), "QQ 聊天插件");
            }
            catch (Exception)
            {
                // 这个候选读不了就试下一个。配置坏了也不该让发卡失败在读取配置上
            }
        }

        return ("ws://127.0.0.1:3001", token, "默认");
    }

    /// <summary>
    /// 建连并鉴权。
    /// token 必须在 ConnectAsync 之前写进请求头——连上之后 ClientWebSocket 不允许再改 Options。
    /// </summary>
    async Task ConnectOneBotAsync(ClientWebSocket connection, CancellationToken cancellationToken)
    {
        (string url, string token, _) = ResolveOneBot();
        if (token.Length > 0)
            connection.Options.SetRequestHeader("Authorization", "Bearer " + token);
        await connection.ConnectAsync(new Uri(url), cancellationToken);
    }


    HttpClient httpClient;
    HttpClient safeHttpClient;
    string clientsKey;

    /// <summary>普通出站请求客户端：按配置决定走直连还是代理。</summary>
    HttpClient Http => EnsureHttp(allowRedirect: true);

    /// <summary>抓用户链接的客户端：不自动跳转，代理规则同样生效。</summary>
    HttpClient SafeHttp => EnsureHttp(allowRedirect: false);

    /// <summary>代理地址或「仅境外」开关变了，就在下一次请求时重建客户端，不用重载插件。</summary>
    HttpClient EnsureHttp(bool allowRedirect)
    {
        string proxy = (Configuration?.ProxyUrl ?? "").Trim();
        bool foreignOnly = Configuration?.ProxyForeignOnly ?? true;
        string key = proxy + "|" + (foreignOnly ? "foreign" : "all");
        if (clientsKey != key)
        {
            httpClient?.Dispose();
            safeHttpClient?.Dispose();
            httpClient = null;
            safeHttpClient = null;
            clientsKey = key;
        }
        return allowRedirect
            ? httpClient ??= BuildClient(true, proxy, foreignOnly)
            : safeHttpClient ??= BuildClient(false, proxy, foreignOnly);
    }

    HttpClient BuildClient(bool allowRedirect, string proxy, bool foreignOnly)
    {
        // 不能包两层 HttpClient：外层 HttpClient 会把同一个 HttpRequestMessage 标记成已发送，
        // 内层再发就抛「The request message was already sent」。改成单个 handler + 按域名判断的 IWebProxy。
        return CreatePlainClient(allowRedirect, proxy, foreignOnly);
    }

    HttpClient CreatePlainClient(bool allowRedirect, string proxy, bool foreignOnly)
    {
        HttpClientHandler handler = new()
        {
            // 实测：B站这类站即使请求没带 Accept-Encoding 也照回 Content-Encoding: gzip，
            // 不自动解压就读到一坨压缩字节，抓网页 meta（标题/预览图）全军覆没
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect = allowRedirect
        };
        if (proxy.Length > 0)
        {
            handler.UseProxy = true;
            handler.Proxy = foreignOnly
                ? new SelectiveProxy(new WebProxy(proxy))
                : new WebProxy(proxy);
        }
        return allowRedirect
            ? new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) }
            : new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>「仅境外平台走代理」的落点：按请求域名决定交给代理还是直连。</summary>
    sealed class SelectiveProxy(WebProxy inner) : IWebProxy
    {
        public ICredentials Credentials
        {
            get => inner.Credentials;
            set => inner.Credentials = value;
        }

        public Uri GetProxy(Uri destination) => ShouldProxy(destination) ? inner.GetProxy(destination) : null;

        public bool IsBypassed(Uri host) => ShouldProxy(host) == false;
    }

    /// <summary>需要走代理的境外站点（按域名后缀匹配；签名服务、国内平台一律直连）。</summary>
    static readonly string[] proxyHosts =
    [
        "youtube.com", "youtu.be", "spotify.com", "music.apple.com", "itunes.apple.com",
        "soundcloud.com", "vimeo.com", "tiktok.com", "twitter.com", "x.com", "instagram.com",
        "github.com", "githubassets.com", "steampowered.com", "bandcamp.com",
        "lrclib.net", "microlink.io"
    ];

    static bool ShouldProxy(Uri uri)
    {
        if (uri == null)
        {
            return false;
        }
        string host = uri.Host.ToLowerInvariant();
        foreach (string suffix in proxyHosts)
        {
            if (host == suffix || host.EndsWith("." + suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    const long SafeFetchMaxBytes = 4 * 1024 * 1024;
    const int SafeFetchMaxHops = 4;

    sealed record FetchResult(byte[] Bytes, string ContentType);

    /// <summary>
    /// 用户可控链接的取数入口：只放行 http/https 公网地址，逐跳校验重定向，
    /// 响应按字节限流（4MB），明显是二进制的响应直接拒掉。
    /// page=true 时还要求返回网页/JSON，避免把图片当页面塞进 meta 解析。
    /// 网络层失败（连不上/被重置）重试一次；被安全策略拒掉的不重试。
    /// </summary>
    async Task<FetchResult> SafeFetchAsync(string url, string referer, TimeSpan timeout, bool page)
    {
        Exception lastError = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await SafeFetchOnceAsync(url, referer, timeout, page);
            }
            catch (Exception e) when (attempt == 0 && IsTransientFetchError(e))
            {
                lastError = e;
            }
        }
        throw lastError ?? new Exception("抓取失败");
    }

    static bool IsTransientFetchError(Exception error)
        => error is HttpRequestException || error is IOException || error is SocketException;

    async Task<FetchResult> SafeFetchOnceAsync(string url, string referer, TimeSpan timeout, bool page)
    {
        Uri current = await VerifyPublicUriAsync(url);
        using CancellationTokenSource cts = new(timeout);
        for (int hop = 0; hop <= SafeFetchMaxHops; hop++)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, current);
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            if (!string.IsNullOrEmpty(referer))
            {
                request.Headers.TryAddWithoutValidation("Referer", referer);
            }
            using HttpResponseMessage response = await SafeHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                Uri location = response.Headers.Location;
                if (location == null)
                {
                    throw new Exception("重定向没给目标地址（HTTP " + (int)response.StatusCode + "）");
                }
                Uri next = location.IsAbsoluteUri ? location : new Uri(current, location);
                current = await VerifyPublicUriAsync(next.AbsoluteUri);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("HTTP " + (int)response.StatusCode + "（" + current.Host + "）");
            }

            string media = response.Content.Headers.ContentType?.MediaType ?? "";
            if (media.Length > 0 && (media.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || media.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                || media.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                || media.Contains("octet-stream", StringComparison.OrdinalIgnoreCase)
                || media.Contains("pdf", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception("返回的不是网页/JSON（" + media + "），已中止");
            }
            if (page && media.Length > 0
                && !media.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                && !media.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !media.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("返回类型不是网页（" + media + "），已中止");
            }

            long? declared = response.Content.Headers.ContentLength;
            if (declared.HasValue && declared.Value > SafeFetchMaxBytes)
            {
                throw new Exception("响应过大（" + declared.Value + " 字节 > 4MB），已中止");
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using MemoryStream buffer = new();
            byte[] chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cts.Token)) > 0)
            {
                if (buffer.Length + read > SafeFetchMaxBytes)
                {
                    throw new Exception("响应超过 4MB 上限，已中止（目标可能不是普通网页）");
                }
                buffer.Write(chunk, 0, read);
            }
            return new FetchResult(buffer.ToArray(), response.Content.Headers.ContentType?.ToString() ?? "");
        }
        throw new Exception("重定向次数超过 " + SafeFetchMaxHops + " 次，已中止");
    }

    static readonly Regex CharsetPattern = new("charset\\s*=\\s*[\"']?([^;\"'\\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static bool codePagesRegistered;

    /// <summary>
    /// 按响应头/页面 meta 里的 charset 解码，读不到才退回 UTF-8。
    /// 原来一律按 UTF-8 硬解，GBK 的站会整页乱码，网易云那种大写 charset 还会被 .NET 直接拒。
    /// </summary>
    static string DecodeBody(byte[] bytes, string contentType)
    {
        if (bytes.Length == 0)
        {
            return "";
        }
        if (codePagesRegistered == false)
        {
            codePagesRegistered = true;
            // [静默] 编码提供者重复注册，忽略即可
            try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { }
        }
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        string charset = CharsetPattern.Match(contentType ?? "").Groups[1].Value;
        if (charset.Length == 0)
        {
            // 响应头没写就翻页面开头的 meta，只扫前 1KB，避免把整页按 ASCII 再解一遍
            string head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1024));
            charset = CharsetPattern.Match(head).Groups[1].Value;
        }
        if (charset.Length > 0)
        {
            try
            {
                return Encoding.GetEncoding(charset.Trim().Trim('"', '\'')).GetString(bytes);
            }
            catch
            {
                // 名字认不出来就按 UTF-8
            }
        }
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// 只允许公网 http/https。域名先解析再逐个查网段：光看字符串挡不住
    /// 127.0.0.1 的变形写法，也挡不住内网域名解析结果。
    /// </summary>
    static async Task<Uri> VerifyPublicUriAsync(string raw)
    {
        if (!Uri.TryCreate((raw ?? "").Trim(), UriKind.Absolute, out Uri uri))
        {
            throw new Exception("给个完整链接（要带 https://）");
        }
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new Exception("只允许 http/https 链接（" + uri.Scheme + " 不支持）");
        }
        string host = uri.Host.Trim('[', ']');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("这个地址指向内网（" + host + "），已拒绝");
        }
        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out IPAddress literal))
        {
            addresses = new[] { literal };
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host);
            }
            catch (Exception e)
            {
                throw new Exception("域名解析失败（" + host + "）：" + e.Message);
            }
        }
        if (addresses.Length == 0)
        {
            throw new Exception("域名解析不到地址（" + host + "）");
        }
        foreach (IPAddress address in addresses)
        {
            if (IsPrivateAddress(address))
            {
                throw new Exception("目标地址在内网/回环范围（" + host + "），已拒绝（SSRF 防护）");
            }
        }
        return uri;
    }

    static bool IsPrivateAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }
        byte[] b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return b[0] == 0 || b[0] == 10 || b[0] == 127
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)      // 运营商级 NAT
                || (b[0] == 169 && b[1] == 254)                    // 链路本地
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)         // 192.0.0.0/24 特殊用途
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))     // 基准测试
                || b[0] >= 224;                                    // 组播/保留
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.Equals(IPAddress.IPv6Loopback)
                || (b[0] & 0xFE) == 0xFC                            // fc00::/7 唯一本地
                || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)          // fe80::/10 链路本地
                || b[0] == 0xFF;                                    // 组播
        }
        return true; // 其它地址族一律不放行
    }

    // 由基地址推导三个接口地址，填一个基地址就能用全部功能
    string Base()
    {
        var b = Configuration.BaseUrl;
        if (string.IsNullOrWhiteSpace(b))
            throw new Exception("第三方接口基地址没填，去模块配置里补上");
        return NormBase(b);
    }
    static readonly Dictionary<string, string[]> _cands = new()
    {
        ["ark"] = new[] { "qq-musicArk", "qq_musicark", "musicArk", "music-card" },
        ["share"] = new[] { "qq-shareArk", "qq_shareArk", "qq-shareark", "shareArk", "share-card" },
        ["s-163"] = new[] { "163-music-search", "163_music_search", "163-search", "music-search" },
        // 咸鱼的搜索是三套独立路径；缺了这两组时 s-qq / s-kugou 会被拼成不存在的地址，
        // 日志里表现为「搜索节点 主站·搜索 失败：404」，随后只能退回官方搜索
        ["s-qq"] = new[] { "qq-music-search", "qq_music_search", "qq-search", "qqmusic-search" },
        ["s-kugou"] = new[] { "kugou-music-search", "kugou_music_search", "kugou-search", "kugoumusic-search" },
    };
    static readonly Dictionary<string, string> _sfx = new();
    static bool _discovered; // 首页/openapi 路径发现只跑一次
    string KeyQ() => KeyQ(Configuration.ApiKey);
    static string KeyQ(string apiKey) => string.IsNullOrWhiteSpace(apiKey) ? "" : "key=" + Uri.EscapeDataString(apiKey) + "&";

    // 签名端的成功码不统一：音乐卡是 code=200，分享卡文档写的是 code=0。
    // 只认 200 的话，分享卡签名成功也会被当成失败——这是"分享卡突然用不了"最可能的成因之一。
    static bool Ok(JsonElement root)
        => root.ValueKind == JsonValueKind.Object
           && root.TryGetProperty("code", out JsonElement code)
           && code.ValueKind == JsonValueKind.Number
           && code.GetInt32() is 200 or 0;

    // ---- 多节点容灾：主站、用户填的备用站、内置公共音乐卡签名各自独立冷却 ----
    sealed record SignProvider(string Name, string BaseUrl, string ApiKey);
    sealed record SignNode(string Name, string Url, string ApiKey, bool Builtin, bool Primary, string BaseUrl);

    static readonly Dictionary<string, string> _sfxBackup = new(); // key = 规范化base|role
    static readonly ConcurrentDictionary<string, (DateTime Until, string Reason)> NodeCooldowns = new();

    static string NormBase(string url)
    {
        string b = (url ?? "").Trim().TrimEnd('/');
        // 咸鱼文档页给的是域名根，所有接口实际挂在 /api/v1 下；只填根域名时自动补上
        if (b.Length > 0)
        {
            try
            {
                Uri u = new(b);
                if (u.Host.Equals("apii.xianyuw.cn", StringComparison.OrdinalIgnoreCase)
                    && (u.AbsolutePath.Length == 0 || u.AbsolutePath == "/"))
                {
                    b += "/api/v1";
                }
            }
            catch
            {
                // 不是合法 URI 就按原样用，交给后面的请求报错
            }
        }
        return b;
    }

    static IEnumerable<string> SplitConfigList(string raw)
    {
        foreach (string line in (raw ?? "").Replace("\r", "").Split('\n'))
        {
            foreach (string part in line.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length > 0)
                {
                    yield return part;
                }
            }
        }
    }

    List<SignProvider> Providers()
    {
        List<SignProvider> list = new();
        bool primary = !string.IsNullOrWhiteSpace(Configuration.BaseUrl)
            || !string.IsNullOrWhiteSpace(Configuration.MusicArkUrl)
            || !string.IsNullOrWhiteSpace(Configuration.ShareArkUrl)
            || !string.IsNullOrWhiteSpace(Configuration.SearchUrl);
        if (primary)
        {
            list.Add(new SignProvider("主站", NormBase(Configuration.BaseUrl), (Configuration.ApiKey ?? "").Trim()));
        }
        int index = 0;
        foreach (string raw in SplitConfigList(Configuration.BackupBaseUrls))
        {
            string[] parts = raw.Split('|', 2);
            string url = NormBase(parts[0]);
            if (url.Length == 0)
            {
                continue;
            }
            index++;
            string key = parts.Length > 1 ? parts[1].Trim() : (Configuration.ApiKey ?? "").Trim();
            list.Add(new SignProvider("备用" + index, url, key));
        }
        return list;
    }

    static string SfxOf(string baseUrl, string role)
    {
        string cacheKey = NormBase(baseUrl) + "|" + role;
        if (_sfxBackup.TryGetValue(cacheKey, out string cached))
        {
            return cached;
        }
        if (_cands.TryGetValue(role, out string[] cands) && cands.Length > 0)
        {
            return cands[0];
        }
        // 搜索角色统一按「平台-music-search」回落：缺候选表时也不会拼出一个不存在的 s-qq
        return role.StartsWith("s-", StringComparison.Ordinal) ? role[2..] + "-music-search" : role;
    }

    async Task EnsureFitOnBaseAsync(SignProvider provider, string role)
    {
        if (provider.BaseUrl.Length == 0)
        {
            return;
        }
        string cacheKey = provider.BaseUrl + "|" + role;
        if (_sfxBackup.ContainsKey(cacheKey))
        {
            return;
        }
        string[] cands = _cands.TryGetValue(role, out string[] known) ? known : new[] { role };
        await Fit(role, cands, ProbeOf(role), provider.BaseUrl, provider.ApiKey);
    }

    async Task<string> NodeUrlAsync(SignProvider provider, string role)
    {
        // 主站允许用「完整地址」手动覆盖；备用站一律按 base + 识别到的后缀拼
        if (provider.Name == "主站")
        {
            if (role == "ark" && !string.IsNullOrWhiteSpace(Configuration.MusicArkUrl))
            {
                return Configuration.MusicArkUrl.Trim();
            }
            if (role == "share" && !string.IsNullOrWhiteSpace(Configuration.ShareArkUrl))
            {
                return Configuration.ShareArkUrl.Trim();
            }
            if (role.StartsWith("s-", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(Configuration.SearchUrl))
            {
                return Configuration.SearchUrl.Trim().Replace("{pf}", role[2..]);
            }
            if (provider.BaseUrl.Length == 0)
            {
                return "";
            }
            if (_sfx.ContainsKey(role) == false)
            {
                await EnsureFit();
            }
            return JoinUrl(provider.BaseUrl, SfxOf(provider.BaseUrl, role));
        }
        if (provider.BaseUrl.Length == 0)
        {
            return "";
        }
        await EnsureFitOnBaseAsync(provider, role);
        return JoinUrl(provider.BaseUrl, SfxOf(provider.BaseUrl, role));
    }

    static IEnumerable<(string Name, string Url, string Key)> ExtraNodes(string raw, string label)
    {
        int index = 0;
        foreach (string line in SplitConfigList(raw))
        {
            string[] parts = line.Split('|', 2);
            string url = parts[0].Trim();
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase) == false)
            {
                continue;
            }
            index++;
            yield return (label + index, url, parts.Length > 1 ? parts[1].Trim() : "");
        }
    }

    async Task<List<SignNode>> MusicNodesAsync()
    {
        List<SignNode> nodes = new();
        foreach (SignProvider provider in Providers())
        {
            string url = await NodeUrlAsync(provider, "ark");
            if (url.Length > 0)
            {
                nodes.Add(new SignNode(provider.Name + "·音乐卡", url, provider.ApiKey, false, provider.Name == "主站", provider.BaseUrl));
            }
        }
        // 额外完整地址：按 OIAPI/公共签名那套参数发，成功码 0/200/1 都认，data 形状也做兼容
        foreach ((string name, string url, string key) in ExtraNodes(Configuration.ExtraMusicSignUrls, "额外音乐卡"))
        {
            nodes.Add(new SignNode(name, url, key, true, false, ""));
        }
        if (Configuration.EnableBuiltinMusicSign && !string.IsNullOrWhiteSpace(Configuration.BuiltinMusicArkUrl))
        {
            nodes.Add(new SignNode("OIAPI·公共音乐卡", Configuration.BuiltinMusicArkUrl.Trim(), "", true, false, ""));
        }
        return nodes;
    }

    async Task<List<SignNode>> ShareNodesAsync()
    {
        List<SignNode> nodes = new();
        foreach (SignProvider provider in Providers())
        {
            string url = await NodeUrlAsync(provider, "share");
            if (url.Length > 0)
            {
                nodes.Add(new SignNode(provider.Name + "·分享卡", url, provider.ApiKey, false, provider.Name == "主站", provider.BaseUrl));
            }
        }
        foreach ((string name, string url, string key) in ExtraNodes(Configuration.ExtraShareSignUrls, "额外分享卡"))
        {
            nodes.Add(new SignNode(name, url, key, false, false, ""));
        }
        return nodes;
    }

    async Task<List<SignNode>> SearchNodesAsync(string pf)
    {
        List<SignNode> nodes = new();
        foreach (SignProvider provider in Providers())
        {
            string url = await NodeUrlAsync(provider, "s-" + pf);
            if (url.Length > 0)
            {
                nodes.Add(new SignNode(provider.Name + "·搜索", url, provider.ApiKey, false, provider.Name == "主站", provider.BaseUrl));
            }
        }
        return nodes;
    }

    string NodeBlocked(string url)
    {
        if (NodeCooldowns.TryGetValue(NodeKey(url), out (DateTime Until, string Reason) hit) && hit.Until > DateTime.Now)
        {
            int left = (int)Math.Ceiling((hit.Until - DateTime.Now).TotalSeconds);
            return "冷却中（" + hit.Reason + "），" + left + " 秒后重试";
        }
        return null;
    }

    void MarkSignFailed(string url, string reason)
    {
        int seconds = Math.Max(0, Configuration.SignFailureCooldownSeconds);
        string shortReason = reason.Length > 40 ? reason[..40] : reason;
        NodeCooldowns[NodeKey(url)] = seconds == 0
            ? (DateTime.MinValue, shortReason)
            : (DateTime.Now.AddSeconds(seconds), shortReason);
    }

    void MarkSignOk(string url) => NodeCooldowns.TryRemove(NodeKey(url), out _);
    // 地址已带 ? 时用 & 接参数，支持手动地址自带 query
    static string WithQuery(string url, string query) => url.Contains('?') ? url + "&" + query : url + "?" + query;
    // 报错里附请求URL时把 key 打码
    static string MaskKey(string url) => Regex.Replace(url, "([?&]key=)[^&]+", "$1***", RegexOptions.IgnoreCase);
    static string Brief(string message, int max = 140)
    {
        string one = (message ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return one.Length <= max ? one : one[..max] + "…";
    }
    static string NodeKey(string url) => MaskKey(url);
    // 候选可以是后缀也可以是完整地址（自动发现的文档链接可能带全路径）
    static string JoinUrl(string b, string c) => c.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? c : b + "/" + c;
    string BaseOrEmpty() => string.IsNullOrWhiteSpace(Configuration.BaseUrl) ? "" : Base();
    string MusicArkUrl() => !string.IsNullOrWhiteSpace(Configuration.MusicArkUrl) ? Configuration.MusicArkUrl.Trim()
        : BaseOrEmpty().Length > 0 ? JoinUrl(BaseOrEmpty(), _sfx.TryGetValue("ark", out var a) ? a : "qq-musicArk") : "";
    string ShareArkUrl() => !string.IsNullOrWhiteSpace(Configuration.ShareArkUrl) ? Configuration.ShareArkUrl.Trim()
        : BaseOrEmpty().Length > 0 ? JoinUrl(BaseOrEmpty(), _sfx.TryGetValue("share", out var s) ? s : "qq-shareArk") : "";
    string SearchUrl(string pf) => !string.IsNullOrWhiteSpace(Configuration.SearchUrl) ? Configuration.SearchUrl.Trim().Replace("{pf}", pf)
        : BaseOrEmpty().Length > 0 ? JoinUrl(BaseOrEmpty(), _sfx.TryGetValue("s-" + pf, out var q) ? q : pf + "-music-search") : "";

    // 填一个基地址就够：默认后缀不通就挨个试候选，试通的记住（主站写进 _sfx，备用站写进 _sfxBackup）
    async Task<string> Fit(string key, string[] cands, string probe, string baseUrl = "", string apiKey = null)
    {
        string norm = NormBase(baseUrl.Length > 0 ? baseUrl : Configuration.BaseUrl);
        if (norm.Length == 0)
        {
            return null;
        }
        string keyQ = KeyQ(apiKey ?? Configuration.ApiKey);
        foreach (var c in cands)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var r = await Http.GetAsync(JoinUrl(norm, c) + "?" + keyQ + probe, cts.Token);
                string b = await r.Content.ReadAsStringAsync();
                using var d = JsonDocument.Parse(b);
                // 只认成功码：错误路径回的 {code:404,msg:"API不存在"} 也带 code，一律不认
                if (Ok(d.RootElement))
                {
                    string cacheKey = norm + "|" + key;
                    _sfxBackup[cacheKey] = c;
                    if (norm == NormBase(Configuration.BaseUrl))
                    {
                        _sfx[key] = c;
                    }
                    return c;
                }
            }
            // [静默] 失败时按默认值继续，不打断当前流程
            catch { }
        }
        return null;
    }

    // 手动地址覆盖了的不再探测
    bool Manual(string key) => (key == "ark" && !string.IsNullOrWhiteSpace(Configuration.MusicArkUrl))
        || (key == "share" && !string.IsNullOrWhiteSpace(Configuration.ShareArkUrl))
        || (key.StartsWith("s-", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(Configuration.SearchUrl));

    // 每个角色的探测参数：必须是完整合法请求，让正确端点回 code==200（错误包也带 code，不能认）
    static string ProbeOf(string key) => key == "ark"
        ? "format=netease&song=t&singer=t&cover=https%3A%2F%2Fexample.com%2Fc.jpg&url=http%3A%2F%2Fmusic.163.com%2Fsong%2Fmedia%2Fouter%2Furl%3Fid%3D1.mp3&jump=http%3A%2F%2Fmusic.163.com%2Fsong%3Fid%3D1"
        : key == "share" ? "title=t&desc=t&url=https%3A%2F%2Fexample.com&preview=https%3A%2F%2Fexample.com%2Fa.jpg&prompt=t"
        : "msg=test";

    // 按命名猜路径是哪个角色：share 优先于 search，musicArk 落到 ark
    static string RoleOf(string path)
    {
        string p = path.ToLowerInvariant();
        if (p.Contains("share") || p.Contains("link")) return "share";
        if (p.Contains("search") || p.Contains("sou")) return "search";
        if (p.Contains("ark") || p.Contains("music") || p.Contains("card") || p.Contains("sign")) return "ark";
        return null;
    }

    // 首次用到才适配：先试内置候选，缺的角色再去首页/openapi 挖一圈路径补试
    async Task EnsureFit()
    {
        // 只填了手动完整地址、没填基地址时，不需要（也不应该）自动探测
        if (string.IsNullOrWhiteSpace(Configuration.BaseUrl)) return;
        foreach (var kv in _cands)
            if (!Manual(kv.Key) && !_sfx.ContainsKey(kv.Key))
                await Fit(kv.Key, kv.Value, ProbeOf(kv.Key));
        if (_discovered || string.IsNullOrWhiteSpace(Configuration.BaseUrl)) return;
        if (!_cands.Any(kv => !Manual(kv.Key) && !_sfx.ContainsKey(kv.Key))) return;
        _discovered = true;
        var found = await DiscoverPathsAsync();
        foreach (var kv in _cands)
            if (!Manual(kv.Key) && !_sfx.ContainsKey(kv.Key))
            {
                string role = kv.Key == "s-163" ? "search" : kv.Key;
                var extra = found.Where(p => RoleOf(p) == role && !kv.Value.Contains(p)).Take(12).ToArray();
                if (extra.Length > 0) await Fit(kv.Key, extra, ProbeOf(kv.Key));
            }
    }

    // 从基地址首页/域名根文档页与 openapi/swagger 文档里挖接口路径：候选表对不上的第三方也能认出来
    async Task<List<string>> DiscoverPathsAsync()
    {
        var found = new HashSet<string>();
        string root = Base();
        string domainRoot = "";
        try { var u = new Uri(root); domainRoot = u.Scheme + "://" + u.Authority + "/"; }
        // [静默] 解析失败按无此项处理
        catch { }

        // 归一成候选：完整地址整条收；路径在基地址前缀下取余段，不在就取最后一段
        void AddCandidate(string c)
        {
            if (string.IsNullOrWhiteSpace(c)) return;
            if (c.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                found.Add(c.Split('?')[0].TrimEnd('/'));
                // [静默] 解析失败按无此项处理
                try { AddCandidate(new Uri(c).AbsolutePath); } catch { }
                return;
            }
            string p = c.Split('?')[0].Trim('/');
            if (p.Length == 0) return;
            string basePath = "";
            // [静默] 解析失败按无此项处理
            try { basePath = new Uri(root).AbsolutePath.TrimEnd('/'); } catch { }
            if (basePath.Length > 0 && p.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(basePath.Length + 1);
            else if (p.Contains('/'))
                p = p.Split('/').Last();
            if (RoleOf(p) != null) found.Add(p);
        }

        foreach (var docPath in new[] { "openapi.json", "swagger.json", "swagger/v1/swagger.json" })
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string b = await Http.GetStringAsync(root + "/" + docPath, cts.Token);
                using var d = JsonDocument.Parse(b);
                if (d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("paths", out var ps))
                    foreach (var p in ps.EnumerateObject())
                        if (domainRoot.Length > 0) AddCandidate(domainRoot + p.Name);
            }
            // [静默] 失败时按默认值继续，不打断当前流程
            catch { }
        }

        async Task CrawlPage(string pageUrl)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string html = await Http.GetStringAsync(pageUrl, cts.Token);
                foreach (Match m in Regex.Matches(html, @"href=[""']([^""']+?)[""']"))
                {
                    string h = m.Groups[1].Value;
                    if (h.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try { var u = new Uri(h); if (u.Host != new Uri(root).Host) continue; AddCandidate(u.Scheme + "://" + u.Authority + u.AbsolutePath); }
                        catch { continue; }
                    }
                    else if (h.StartsWith("/") && domainRoot.Length > 0) AddCandidate(domainRoot + h);
                    else AddCandidate(h);
                }
                foreach (Match m in Regex.Matches(html, @"[A-Za-z][A-Za-z0-9_\-]{2,40}"))
                    AddCandidate(m.Value);
            }
            // [静默] 失败时按默认值继续，不打断当前流程
            catch { }
        }
        await CrawlPage(root + "/");
        if (domainRoot.Length > 0 && domainRoot != root.TrimEnd('/') + "/") await CrawlPage(domainRoot);
        return found.ToList();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("探测三个接口通不通（音乐卡/分享卡/歌曲搜索），手动地址和自动后缀都会报出来，换了新第三方接口先跑一次这个")]
    protected override Task OnAwake()
    {
        XmlHandler handler = new(this)
        {
            Description = "把网页链接或歌曲/视频做成QQ分享卡片并发送，并提供B站链接解析/UP主更新/扫码登录；另有免key的歌词/封面/链接素材公开接口。",
            Explanation = "搜歌用 SearchMusic，发卡用 SendArkCard / SendShareCard；B站系列：BiliParse 解析链接（**播放直链、b23短链、专栏/番剧/直播间只有这里有**）、BiliUpdates 查UP主更新、BiliQrLogin 扫码登录（**这是本插件第三方接口的凭据池，不是你本机的B站账号**）、BiliCookieRefresh 刷新登录态、BiliCookie 管理登录Cookie；IpLookup 查IP归属。这些都要已配置第三方基地址和ApiKey。"
                + "搜索B站视频、查视频详情、查UP主资料、看热门榜，一律用 Suran.Bili 插件的 BiliSearch / BiliVideo / BiliUser / BiliHot（走本机 cookie，结果与你在B站看到的一致，也没有第三方限额）；本插件不再提供这些，只留发卡片和上面那些 Suran.Bili 没有的能力。"
                + "ProbeBase 探测三个签名/搜索接口通不通，ProbeAll 列出基地址下发现的所有接口。"
                + "另一批不要 key、不要配置的走公开接口：ArkLyric 取歌词（LRCLIB 与网易云官方）、ArkCover 找封面与试听（iTunes）、ArkLinkMeta 自己抓页面 meta 出卡片素材、ArkFreeProbe 体检这些源。"
                + "发卡片前的素材可以先用它们备着，但签名与发送仍走第三方签名服务。"
                + "容灾顺序：主站 → 配置里的备用签名基地址 → OIAPI 公共音乐卡签名（仅音乐卡）；任一节点失败只冷却它自己并立刻换下一个。"
                + "所有签名节点都失败时，音乐卡仍回退 OneBot 标准音乐段，分享卡仍回退图文/纯文本，B站系列仍回退官方接口——这些兜底没有删。"
        };
        functionCaller.RegisterHandler(handler,
            (Configuration?.DocumentAlwaysResident ?? false) ? DocumentMode.Explicit : DocumentMode.Implicit,
            cancellationToken: DestroyCancellationToken);
        return Task.CompletedTask;
    }

    protected override async Task OnDestroy()
    {
        // 销毁/热重载时断开协议端连接、清掉探测缓存，避免连接泄漏
        await _wsLock.WaitAsync();
        try { _sharedWs?.Dispose(); _sharedWs = null; _sfx.Clear(); _discovered = false; providerName = ""; providerResolved = false; }
        finally { _wsLock.Release(); }
    }
}
