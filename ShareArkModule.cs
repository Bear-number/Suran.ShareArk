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


    HttpClient? httpClient;
    HttpClient? safeHttpClient;
    string? clientsKey;

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
        public ICredentials? Credentials
        {
            get => inner.Credentials;
            set => inner.Credentials = value;
        }

        public Uri? GetProxy(Uri destination) => ShouldProxy(destination) ? inner.GetProxy(destination) : null;

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

    static bool ShouldProxy(Uri? uri)
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
    async Task<FetchResult> SafeFetchAsync(string url, string? referer, TimeSpan timeout, bool page)
    {
        Exception? lastError = null;
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

    async Task<FetchResult> SafeFetchOnceAsync(string url, string? referer, TimeSpan timeout, bool page)
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
                Uri? location = response.Headers.Location;
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
    static string DecodeBody(byte[] bytes, string? contentType)
    {
        if (bytes.Length == 0)
        {
            return "";
        }
        if (codePagesRegistered == false)
        {
            codePagesRegistered = true;
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
        if (!Uri.TryCreate((raw ?? "").Trim(), UriKind.Absolute, out Uri? uri))
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
        if (IPAddress.TryParse(host, out IPAddress? literal))
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
        return b.Trim().TrimEnd('/');
    }
    static readonly Dictionary<string, string[]> _cands = new()
    {
        ["ark"] = new[] { "qq-musicArk", "qq_musicark", "musicArk", "music-card" },
        ["share"] = new[] { "qq-shareArk", "qq_shareArk", "qq-shareark", "shareArk", "share-card" },
        ["s-163"] = new[] { "163-music-search", "163_music_search", "163-search", "music-search" },
    };
    static readonly Dictionary<string, string> _sfx = new();
    static bool _discovered; // 首页/openapi 路径发现只跑一次
    string KeyQ() => KeyQ(Configuration.ApiKey);
    static string KeyQ(string? apiKey) => string.IsNullOrWhiteSpace(apiKey) ? "" : "key=" + Uri.EscapeDataString(apiKey) + "&";

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

    static string NormBase(string url) => (url ?? "").Trim().TrimEnd('/');

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
        return _cands.TryGetValue(role, out string[] cands) && cands.Length > 0 ? cands[0] : role;
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

    string? NodeBlocked(string url)
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
    async Task<string?> Fit(string key, string[] cands, string probe, string baseUrl = "", string? apiKey = null)
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
            catch { }
        }
        return null;
    }

    // 手动地址覆盖了的不再探测
    bool Manual(string key) => (key == "ark" && !string.IsNullOrWhiteSpace(Configuration.MusicArkUrl))
        || (key == "share" && !string.IsNullOrWhiteSpace(Configuration.ShareArkUrl))
        || (key == "s-163" && !string.IsNullOrWhiteSpace(Configuration.SearchUrl));

    // 每个角色的探测参数：必须是完整合法请求，让正确端点回 code==200（错误包也带 code，不能认）
    static string ProbeOf(string key) => key == "ark"
        ? "format=netease&song=t&singer=t&cover=https%3A%2F%2Fexample.com%2Fc.jpg&url=http%3A%2F%2Fmusic.163.com%2Fsong%2Fmedia%2Fouter%2Furl%3Fid%3D1.mp3&jump=http%3A%2F%2Fmusic.163.com%2Fsong%3Fid%3D1"
        : key == "share" ? "title=t&desc=t&url=https%3A%2F%2Fexample.com&preview=https%3A%2F%2Fexample.com%2Fa.jpg&prompt=t"
        : "msg=test";

    // 按命名猜路径是哪个角色：share 优先于 search，musicArk 落到 ark
    static string? RoleOf(string path)
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
        catch { }

        // 归一成候选：完整地址整条收；路径在基地址前缀下取余段，不在就取最后一段
        void AddCandidate(string c)
        {
            if (string.IsNullOrWhiteSpace(c)) return;
            if (c.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                found.Add(c.Split('?')[0].TrimEnd('/'));
                try { AddCandidate(new Uri(c).AbsolutePath); } catch { }
                return;
            }
            string p = c.Split('?')[0].Trim('/');
            if (p.Length == 0) return;
            string basePath = "";
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
            catch { }
        }
        await CrawlPage(root + "/");
        if (domainRoot.Length > 0 && domainRoot != root.TrimEnd('/') + "/") await CrawlPage(domainRoot);
        return found.ToList();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("探测三个接口通不通（音乐卡/分享卡/歌曲搜索），手动地址和自动后缀都会报出来，换了新第三方接口先跑一次这个")]
    public async Task ProbeBase()
    {
        if (Gate(Configuration.EnableProbeBase, "探测接口")) return;
        try { interactor.Poke(await ProbeBaseReportAsync()); }
        catch (Exception ex) { interactor.Poke("探测失败：" + ex.Message); }
    }

    // 探测三个接口，返回报告文本（UI 按钮与 AI 函数共用）
    public async Task<string> ProbeBaseReportAsync()
    {
        var rep = new List<string> {
            string.IsNullOrWhiteSpace(Configuration.BaseUrl)
                ? (SplitConfigList(Configuration.BackupBaseUrls).Any() ? "基地址未填，使用备用节点" : "基地址未填，只用手动地址")
                : "基地址 " + Base()
        };
        async Task Probe(string name, string url)
        {
            try
            {
                using var r = await Http.GetAsync(url);
                string b = await r.Content.ReadAsStringAsync();
                // 以前只要返回体带 code 就算"通"——服务端回 code:500 也是带 code 的，
                // 结果探测报告一片绿，实际一个也签不出来。
                bool ok = false;
                int code = -1;
                string msg = "";
                try
                {
                    using var d = JsonDocument.Parse(b);
                    if (d.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        ok = Ok(d.RootElement);
                        if (d.RootElement.TryGetProperty("code", out var c0) && c0.ValueKind == JsonValueKind.Number)
                            code = c0.GetInt32();
                        if (d.RootElement.TryGetProperty("msg", out var m0) && m0.ValueKind == JsonValueKind.String)
                            msg = m0.GetString() ?? "";
                    }
                }
                catch { }
                rep.Add(name + "：" + (ok
                    ? "通"
                    : "不通（HTTP " + (int)r.StatusCode
                      + (code >= 0 ? "，code=" + code : "")
                      + (msg.Length > 0 ? "，" + msg : "")
                      + "）"));
            }
            catch (Exception ex) { rep.Add(name + "：不通（" + ex.Message + "）"); }
        }
        string keyQ = KeyQ();
        await EnsureFit();
        rep.Add(UrlLine("音乐卡", Configuration.MusicArkUrl, "ark"));
        rep.Add(UrlLine("分享卡", Configuration.ShareArkUrl, "share"));
        rep.Add(UrlLine("歌曲搜索", Configuration.SearchUrl, "s-163"));
        string searchUrl = SearchUrl("163");
        if (searchUrl.Length > 0)
        {
            await Probe("歌曲搜索", WithQuery(searchUrl, keyQ + "msg=" + Uri.EscapeDataString("test")));
        }
        else
        {
            rep.Add("歌曲搜索：未配置地址");
        }
        string musicUrl = MusicArkUrl();
        if (musicUrl.Length > 0)
        {
            await Probe("音乐卡", WithQuery(musicUrl, keyQ + ProbeOf("ark")));
        }
        else
        {
            rep.Add("音乐卡：未配置地址");
        }
        string shareUrl = ShareArkUrl();
        if (shareUrl.Length > 0)
        {
            await Probe("分享卡", WithQuery(shareUrl, keyQ + ProbeOf("share")));
        }
        else
        {
            rep.Add("分享卡：未配置地址");
        }
        if (!string.IsNullOrWhiteSpace(Configuration.BiliSearchUrl))
            await Probe("B站搜索", WithQuery(Configuration.BiliSearchUrl.Trim(), keyQ + "msg=" + Uri.EscapeDataString("test")));

        // 多节点容灾：按实际调用顺序把每个节点都打一次，主站挂了能一眼看出备用站是否可用
        rep.Add("容灾节点（按调用顺序）：");
        foreach (SignNode node in await MusicNodesAsync())
        {
            rep.Add("  " + await ProbeNodeAsync(node, ProbeOf("ark"), node.Builtin));
        }
        foreach (SignNode node in await ShareNodesAsync())
        {
            rep.Add("  " + await ProbeNodeAsync(node, ProbeOf("share"), false));
        }
        foreach (SignNode node in await SearchNodesAsync("163"))
        {
            rep.Add("  " + await ProbeNodeAsync(node, "msg=test", false));
        }
        rep.Add("公共音乐平台：" + await ProbePublicMusicAsync());
        return string.Join("\n", rep);
    }

    // 单个容灾节点的体检：成功码规则和签名节点保持一致（OIAPI 认 1）
    async Task<string> ProbeNodeAsync(SignNode node, string probe, bool builtin)
    {
        string? cooling = NodeBlocked(node.Url);
        if (cooling != null)
        {
            return node.Name + "：" + cooling;
        }
        try
        {
            string effectiveProbe = builtin ? probe.Replace("format=netease", "format=163") : probe;
            using HttpResponseMessage response = await Http.GetAsync(WithQuery(node.Url, KeyQ(node.ApiKey) + effectiveProbe));
            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            bool ok = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("code", out JsonElement code)
                && code.ValueKind == JsonValueKind.Number
                && (builtin ? code.GetInt32() is 1 or 200 or 0 : code.GetInt32() is 200 or 0);
            return node.Name + "：" + (ok
                ? "通"
                : "不通（HTTP " + (int)response.StatusCode
                  + (ReadCode(root) >= 0 ? "，code=" + ReadCode(root) : "")
                  + (ReadMsg(root).Length > 0 ? "，" + ReadMsg(root) : "") + "）")
                + " · " + MaskKey(node.Url);
        }
        catch (Exception probeError)
        {
            return node.Name + "：不通（" + probeError.Message + "）· " + MaskKey(node.Url);
        }
    }

    // 公共音乐平台接口体检：洛苏（网易云/QQ/酷狗点歌）与糖豆子（QQ 搜索）
    async Task<string> ProbePublicMusicAsync()
    {
        List<string> parts = new();
        if (Configuration.EnablePublicMusicApis == false)
        {
            return "未启用（容灾·启用公共音乐平台接口 是关的）";
        }
        try
        {
            string url = WithQuery((Configuration.LuoSuBaseUrl ?? "").Trim().TrimEnd('/') + "/api/qqmusic/",
                "ss=" + Uri.EscapeDataString("晴天") + "&format=json&t=2");
            string body = await Http.GetStringAsync(url);
            using JsonDocument doc = JsonDocument.Parse(body);
            bool ok = doc.RootElement.ValueKind == JsonValueKind.Object && ReadCode(doc.RootElement) == 200;
            parts.Add("洛苏 QQ " + (ok ? "通" : "不通（code=" + ReadCode(doc.RootElement) + "）"));
        }
        catch (Exception error)
        {
            parts.Add("洛苏 QQ 不通（" + error.Message + "）");
        }
        try
        {
            string url = WithQuery((Configuration.TangDouZiBaseUrl ?? "").Trim().TrimEnd('/') + "/a/qqmusic.php",
                "nr=" + Uri.EscapeDataString("晴天") + "&return=json");
            string body = await Http.GetStringAsync(url);
            using JsonDocument doc = JsonDocument.Parse(body);
            parts.Add("糖豆子 QQ " + (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
                ? "通" : "回空"));
        }
        catch (Exception error)
        {
            parts.Add("糖豆子 QQ 不通（" + error.Message + "）");
        }
        return string.Join("；", parts);
    }

    // 报告每个接口最终用的地址来源：手动填的报地址，自动的报试通的后缀
    string UrlLine(string name, string manual, string key)
    {
        if (!string.IsNullOrWhiteSpace(manual)) return name + "地址 -> 手动 " + manual.Trim();
        if (_sfx.TryGetValue(key, out var v)) return v.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? name + "地址 -> 自动发现 " + v : name + "后缀 -> " + v;
        return name + "后缀 -> 未识别，回落默认（可手动填完整地址）";
    }

    // 猜接口用途，纯展示用
    static string TagOf(string url)
    {
        string l = url.ToLowerInvariant();
        if (l.Contains("bili")) return "［B站］";
        if (l.Contains("musicark")) return "［音乐卡］";
        if (l.Contains("shareark")) return "［分享卡］";
        if (l.Contains("search")) return "［搜索］";
        return "";
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("列出第三方基地址下发现的所有接口（来自openapi/swagger文档和站点首页的接口链接，只列举不调用，不消耗接口次数），并标注插件当前在用哪些")]
    public async Task ProbeAll()
    {
        if (Gate(Configuration.EnableProbeAll, "列出全部接口")) return;
        try { interactor.Poke(await ProbeAllReportAsync()); }
        catch (Exception ex) { interactor.Poke("列举失败：" + ex.Message); }
    }

    // 列出基地址下发现的全部接口，返回报告文本（UI 按钮与 AI 函数共用）
    public async Task<string> ProbeAllReportAsync()
    {
        string root = Base();
            string domainRoot = "";
            try { var u = new Uri(root); domainRoot = u.Scheme + "://" + u.Authority + "/"; } catch { }

            var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // openapi/swagger 文档：基地址和域名根都试
            foreach (var b in new[] { root, domainRoot.TrimEnd('/') }.Distinct())
            {
                if (b.Length == 0) continue;
                foreach (var docPath in new[] { "openapi.json", "swagger.json", "swagger/v1/swagger.json" })
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        string body = await Http.GetStringAsync(b + "/" + docPath, cts.Token);
                        using var d = JsonDocument.Parse(body);
                        if (d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("paths", out var ps))
                            foreach (var p in ps.EnumerateObject())
                                paths.Add(b + "/" + p.Name.Trim('/'));
                    }
                    catch { }
                }
            }

            // 首页链接：同域且路径含 /api/ 或 /series/ 的视为接口线索
            async Task Crawl(string pageUrl)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    string html = await Http.GetStringAsync(pageUrl, cts.Token);
                    foreach (Match m in Regex.Matches(html, @"href=[""']([^""']+?)[""']"))
                    {
                        string h = m.Groups[1].Value.Split('?')[0];
                        if (h.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            try { var u = new Uri(h); if (u.Host != new Uri(root).Host) continue; h = u.AbsolutePath; }
                            catch { continue; }
                        }
                        if (h.Length == 0 || !h.StartsWith("/")) continue;
                        if (h.Contains("/api/") || h.StartsWith("/api/") || h.Contains("/series/") || h.StartsWith("/series/"))
                            paths.Add(domainRoot.TrimEnd('/') + h);
                    }
                }
                catch { }
            }
            await Crawl(root + "/");
            if (domainRoot.Length > 0 && domainRoot.TrimEnd('/') != root.TrimEnd('/')) await Crawl(domainRoot);

            var rep = new List<string>();
            if (paths.Count == 0)
                rep.Add("基地址 " + root + " 下没有发现接口清单（没有openapi文档，首页也没有接口链接），只能靠 ProbeBase 探测已知路径");
            else
            {
                rep.Add("基地址 " + root + "，发现 " + paths.Count + " 个接口：");
                foreach (var p in paths) rep.Add("  " + TagOf(p) + p);
            }
            var used = new List<string>();
            if (!string.IsNullOrWhiteSpace(Configuration.MusicArkUrl)) used.Add("音乐卡（手动）");
            else if (_sfx.TryGetValue("ark", out var ua)) used.Add("音乐卡 -> " + ua);
            if (!string.IsNullOrWhiteSpace(Configuration.ShareArkUrl)) used.Add("分享卡（手动）");
            else if (_sfx.TryGetValue("share", out var us)) used.Add("分享卡 -> " + us);
            if (!string.IsNullOrWhiteSpace(Configuration.SearchUrl)) used.Add("歌曲搜索（手动）");
            else if (_sfx.TryGetValue("s-163", out var uq)) used.Add("歌曲搜索 -> " + uq);
            if (!string.IsNullOrWhiteSpace(Configuration.BiliSearchUrl)) used.Add("B站搜索（手动）");
            used.Add("B站系列 -> " + Base() + "/bili-*");
            rep.Add("插件在用：" + string.Join("；", used));
            return string.Join("\n", rep);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("搜索歌曲拿ID，再发卡片。platform取163(网易云)/qq(QQ音乐)/kugou(酷狗)，默认163，不填就搜网易云")]
    public async Task SearchMusic(
        [Description("歌名或关键词")] string keyword,
        [Description("平台：163(网易云)/qq(QQ音乐)/kugou(酷狗)，默认163")] string? platform = null)
    {
        if (Gate(Configuration.EnableSearchMusic, "搜歌")) return;
        string pf = (platform ?? "163").Trim().ToLowerInvariant();
        if (pf == "netease") pf = "163";
        try
        {
            if (pf != "163" && pf != "qq" && pf != "kugou")
                throw new Exception("搜索只支持 163/qq/kugou");
            JsonElement arr = await SearchRawAsync(pf, keyword);
            List<string> lines = new();
            int n = 0;
            foreach (var s0 in arr.EnumerateArray())
            {
                if (n >= 5) break;
                string t = s0.TryGetProperty("title", out var tv) ? tv.GetString() ?? "" : "";
                string a = s0.TryGetProperty("author", out var av) ? av.GetString() ?? "" : "";
                string q = s0.TryGetProperty("quality", out var qv) ? qv.GetString() ?? "" : "";
                string idv = s0.TryGetProperty("id", out var iv) ? iv.GetString() ?? "" : "";
                if (q.Length > 0) lines.Add(idv + " | " + t + " - " + a + " [" + q + "]");
                else lines.Add(idv + " | " + t + " - " + a);
                n++;
            }
            if (lines.Count == 0) lines.Add("没搜到");
            interactor.Poke(string.Join("\n", lines));
        }
        catch (Exception thirdPartyError)
        {
            if (Configuration.EnableOfficialFallback == false)
            {
                string? publicLines = await PublicMusicSearchLinesAsync(pf, keyword);
                if (publicLines != null)
                {
                    interactor.Poke("ℹ️ 第三方接口不可用（" + thirdPartyError.Message + "），已用公共音乐接口。\n" + publicLines);
                    return;
                }
                interactor.Poke("搜索失败：" + thirdPartyError.Message);
                return;
            }
            try
            {
                List<(string Id, string Name, string Singer)> results = await OfficialSearchAsync(pf, keyword);
                List<string> lines = results.Select(r => r.Id + " | " + r.Name + (r.Singer.Length > 0 ? " - " + r.Singer : "")).ToList();
                if (lines.Count == 0) lines.Add("没搜到");
                interactor.Poke("ℹ️ 第三方接口不可用（" + thirdPartyError.Message + "），已切换官方接口。\n" + string.Join("\n", lines));
            }
            catch (Exception officialError)
            {
                string? publicLines = await PublicMusicSearchLinesAsync(pf, keyword);
                if (publicLines != null)
                {
                    interactor.Poke("ℹ️ 第三方与官方都不可用，已切公共音乐接口（第三方：" + thirdPartyError.Message
                        + "；官方：" + officialError.Message + "）。\n" + publicLines);
                    return;
                }
                interactor.Poke("❌ 搜索：第三方（" + thirdPartyError.Message + "）；官方容灾（" + officialError.Message + "）");
            }
        }
    }


    // 搜歌多节点：主站 → 备用站；返回签名端 data 数组
    async Task<JsonElement> SearchRawAsync(string pf, string keyword)
        => await SearchDataAsync(pf, "msg=" + Uri.EscapeDataString(keyword));

    // 搜索类接口共用：queryPart 可以是 msg=...（搜索）或 id=...（酷狗详情）
    async Task<JsonElement> SearchDataAsync(string pf, string queryPart)
    {
        List<SignNode> nodes = await SearchNodesAsync(pf);
        if (nodes.Count == 0)
        {
            throw new Exception("搜索节点一个都没配置：先填第三方接口基地址，或填备用签名基地址");
        }
        List<string> errors = new();
        foreach (SignNode node in nodes)
        {
            string? cooling = NodeBlocked(node.Url);
            if (cooling != null)
            {
                errors.Add(node.Name + "：" + cooling);
                continue;
            }
            try
            {
                string u = WithQuery(node.Url, KeyQ(node.ApiKey) + queryPart);
                string body = await Http.GetStringAsync(u);
                using JsonDocument doc = JsonDocument.Parse(body);
                if (Ok(doc.RootElement) == false)
                {
                    throw new Exception("接口回 code=" + ReadCode(doc.RootElement) + "：" + ReadMsg(doc.RootElement));
                }
                JsonElement data = doc.RootElement.GetProperty("data").Clone();
                MarkSignOk(node.Url);
                return data;
            }
            catch (Exception nodeError)
            {
                MarkSignFailed(node.Url, nodeError.Message);
                errors.Add(node.Name + "：" + nodeError.Message);
                logger.LogWarning("搜索节点 {Node} 失败：{Msg}", node.Name, nodeError.Message);
            }
        }
        throw new Exception("所有搜索节点都失败（" + string.Join("；", errors) + "）");
    }

    // B站视频搜索统一入口：配置了第三方接口优先走第三方（失败按容灾配置转官方），否则走B站官方，都归一成 (BV号, 标题) 列表
    async Task<List<(string bv, string title)>> BiliSearchAsync(string keyword)
    {
        var list = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(Configuration.BiliSearchUrl))
        {
            try
            {
                string u = WithQuery(Configuration.BiliSearchUrl.Trim(), KeyQ() + "msg=" + Uri.EscapeDataString(keyword));
                string body = await Http.GetStringAsync(u);
                using var doc = JsonDocument.Parse(body);
                if (!Ok(doc.RootElement))
                    throw new Exception(doc.RootElement.TryGetProperty("msg", out var mm) ? mm.GetString() ?? "" : "搜索失败");
                foreach (var v in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    string bv = v.TryGetProperty("id", out var iv) ? iv.GetString() ?? "" : "";
                    string t = v.TryGetProperty("title", out var tv) ? Regex.Replace(tv.GetString() ?? "", "<[^>]*>", "") : "";
                    if (bv.Length > 0) list.Add((bv, t));
                }
                if (list.Count > 0)
                    return list;
                // 第三方回 200 但一条没有：继续走官方再搜一次。
                // 否则"这家没搜到"会变成"全都没有"，而官方那边往往能搜着
                if (Configuration.EnableOfficialFallback == false)
                    throw new Exception("第三方 B站搜索没结果，官方回退又是关着的");
                logger.LogInformation("B站第三方搜索没结果，转官方再试：" + keyword);
            }
            catch (Exception thirdPartyError)
            {
                if (Configuration.EnableOfficialFallback == false)
                {
                    throw;
                }
                // 容灾：第三方失败转B站官方搜索，失败原因得留个痕，不然排查时两头都查不到
                logger.LogWarning("B站第三方搜索失败，转官方搜索：" + thirdPartyError.Message);
            }
        }
        string ou = "https://api.bilibili.com/x/web-interface/search/all/v2?keyword=" + Uri.EscapeDataString(keyword);
        using var req = new HttpRequestMessage(HttpMethod.Get, ou);
        req.Headers.Referrer = new Uri("https://www.bilibili.com/");
        req.Headers.TryAddWithoutValidation("Cookie", "buvid3=1; b_nut=1");
        req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        string obody = await (await Http.SendAsync(req)).Content.ReadAsStringAsync();
        using var odoc = JsonDocument.Parse(obody);
        // B站成功也是 code:0，和签名接口的 200 不一样；被风控时回 -412 且没有 data.result，
        // 原来直接 GetProperty 会抛 KeyNotFoundException，报错里看不出是"被限流"还是"没搜到"
        if (!odoc.RootElement.TryGetProperty("code", out var oc) || oc.GetInt32() != 0)
        {
            string om = odoc.RootElement.TryGetProperty("message", out var omm) ? omm.GetString() ?? "" : "";
            throw new Exception("B站官方搜索失败（code=" + (odoc.RootElement.TryGetProperty("code", out var c1) ? c1.GetInt32().ToString() : "?")
                + (om.Length > 0 ? "，" + om : "") + "）"
                + (string.IsNullOrWhiteSpace(Configuration.BiliSearchUrl)
                    ? "，可以在配置里填 BiliSearchUrl 走第三方搜索"
                    : "（第三方搜索这次也没成功）"));
        }
        foreach (var sec in odoc.RootElement.GetProperty("data").GetProperty("result").EnumerateArray())
        {
            if (sec.GetProperty("result_type").GetString() != "video") continue;
            foreach (var v in sec.GetProperty("data").EnumerateArray())
                list.Add((v.GetProperty("bvid").GetString() ?? "", Regex.Replace(v.GetProperty("title").GetString() ?? "", "<[^>]*>", "")));
        }
        return list;
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("发送音乐/视频卡片到QQ（自签名版，不依赖协议端签名，比QQ增强的点歌更通用）。platform取163(网易云)/qq(QQ音乐)/bilibili(BV号)/kugou；id为歌曲ID或BV号；type不填默认group")]
    public async Task SendArkCard(
        [Description("平台：163/qq/bilibili/kugou")] string platform,
        [Description("歌曲ID或BV号")] string id,
        [Description("目标群号或QQ号")] long? targetId = null,
        [Description("group或private，默认group")] string? type = null,
        [Description("卡片标题，可留空")] string? title = null)
    {
        if (Gate(Configuration.EnableSendArkCard, "发音乐/视频卡片")) return;
        if (targetId == null)
            throw new Exception("没给目标，发群里还是私聊得说一个");
        long target = targetId.Value;
        bool isGroup = !string.Equals(type, "private", StringComparison.OrdinalIgnoreCase);
        string thirdPartyError;
        try
        {
            await EnsureFit();
            string card = await SignAsync(platform, id, title);
            (bool Success, string Message) sent = await SendJsonAsync(target, isGroup, card);
            if (sent.Success)
            {
                // 发卡成功不往上下文里塞系统推送：群里已经能看到卡片
                logger.LogInformation("卡片已发出");
                return;
            }
            thirdPartyError = sent.Message;
        }
        catch (Exception signError)
        {
            thirdPartyError = signError.Message;
        }

        if (Configuration.EnableOfficialFallback == false)
        {
            logger.LogWarning("发卡片失败：" + thirdPartyError);
            interactor.Poke("❌ 发卡片失败：" + Brief(thirdPartyError));
            return;
        }
        // 签名成功但协议端不吃 json 卡片时，退到 OneBot 标准音乐段
        try
        {
            JsonObject musicSegment = await BuildOfficialMusicSegmentAsync(platform, id, title);
            JsonArray segments = new();
            segments.Add(musicSegment);
            (bool Success, string Message) official = await SendSegmentsAsync(target, isGroup, segments);
            if (official.Success)
            {
                // 成功就不 Poke：之前的失败详情会把上下文刷屏，日志里留一份即可
                logger.LogInformation("卡片已发出（OneBot 音乐段兜底；json 卡片失败：{Third}）", Brief(thirdPartyError, 300));
                return;
            }
            interactor.Poke("❌ 发卡片失败：json 卡片（" + Brief(thirdPartyError) + "）；官方音乐段（" + Brief(official.Message) + "）");
        }
        catch (Exception officialError)
        {
            logger.LogWarning("发卡片失败：第三方 {Third}；官方容灾 {Official}", thirdPartyError, officialError.Message);
            interactor.Poke("❌ 发卡片：第三方（" + Brief(thirdPartyError) + "）；官方容灾（" + Brief(officialError.Message) + "）");
        }
    }

    [XmlFunction(FunctionMode.OneShot, order: -1)]
    [Description("把任意网页链接拼成QQ分享卡发出（通用链接卡，走分享卡签名接口）。title标题，desc描述，url跳转链接，preview预览图可留空，prompt提示文本可留空，type不填默认group")]
    public async Task SendShareCard(
        [Description("跳转链接")] string url,
        [Description("卡片标题，可留空，留空自动抓")] string? title = null,
        [Description("卡片描述，可留空，留空自动抓")] string? desc = null,
        [Description("预览图URL，可留空")] string? preview = null,
        [Description("提示文本，可留空")] string? prompt = null,
        [Description("目标群号或QQ号")] long? targetId = null,
        [Description("group或private，默认group")] string? type = null)
    {
        await SendShareCardCoreAsync(url, title, desc, preview, prompt, targetId, type, allowBili: false);
    }

    /// <summary>分享卡核心：allowBili=true 时放行B站非视频页（平台卡内部用），直接调用的工具仍挡住B站。</summary>
    async Task SendShareCardCoreAsync(string url, string? title, string? desc, string? preview, string? prompt,
        long? targetId, string? type, bool allowBili)
    {
        if (Gate(Configuration.EnableSendShareCard, "发通用分享卡")) return;
        if (targetId == null)
            throw new Exception("没给目标，发群里还是私聊得说一个");
        long target = targetId.Value;
        bool isGroup = !string.Equals(type, "private", StringComparison.OrdinalIgnoreCase);
        try
        {
            // 链接是用户给的：先确认是公网 http/https，再往下抓页面/发卡
            await VerifyPublicUriAsync(url);
            // 过滤B站链接：B站走专用搜索+音乐卡接口，不走通用分享卡
            string host = "";
            try { host = new Uri(url).Host.ToLowerInvariant(); } catch { }
                if (allowBili == false && (host.Contains("bilibili.com") || host.Contains("b23.tv") || host.Contains("biligame.com")) && !host.StartsWith("live."))
                    throw new Exception("B站视频/主页链接不走自定义卡，用 SearchBili 拿BV号再 SendArkCard，或用 BiliParse 解析这个链接，直播可直接发");

            // 免费源开关关掉后，发卡就不能再偷偷抓页面：只用调用方给的标题/描述/预览
            var meta = Configuration.EnableFreeSources ? await FetchMetaAsync(url) : new PageMeta();
            if (preview != null && preview.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) preview = null; // data URI 不收，回落自动抓
            if (string.IsNullOrWhiteSpace(title)) title = meta.title;
            if (string.IsNullOrWhiteSpace(desc)) desc = meta.desc;
            if (string.IsNullOrWhiteSpace(preview)) preview = meta.image;
            if (string.IsNullOrWhiteSpace(title)) title = url;
            if (string.IsNullOrWhiteSpace(desc)) desc = title;
            if (string.IsNullOrWhiteSpace(prompt))
                prompt = "[分享]" + title;
            // 依次尝试主站 / 备用站；全失败才抛给下面的图文兜底
            string card = await SignShareCardAsync(title, desc, url, preview, prompt);
            (bool Success, string Message) sent = await SendJsonAsync(target, isGroup, card);
            if (sent.Success)
            {
                logger.LogInformation("分享卡已发出");
            }
            else
            {
                interactor.Poke("❌ 分享卡没发出去：" + Brief(sent.Message));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("发分享卡失败：" + ex.Message);
            bool signProblem = ex.Message.Contains("签名", StringComparison.Ordinal);

            // 签名挂了的时候退成一条普通图文消息：卡片是没有了，但链接和图还在，
            // 群里看到的仍然是「标题 + 预览图 + 链接」，比一句"发卡片失败"有用。
            if (signProblem && Configuration.EnableOfficialFallback)
            {
                (bool Success, string Message) plain =
                    await SendLinkFallbackAsync(target, isGroup, title, desc, url, preview);
                if (plain.Success)
                {
                    logger.LogInformation("分享卡已发出（签名不可用，退成图文消息）");
                    return;
                }
                interactor.Poke("❌ 分享卡没发成：签名（" + Brief(ex.Message) + "）；图文兜底（" + Brief(plain.Message) + "）");
                return;
            }

            // B站链接被挡、网络抓取失败这类，不该假装发出去
            string hint = signProblem ? "。这次没发成卡片，退一步直接把链接当普通文本发出去" : "";
            interactor.Poke("发分享卡失败：" + Brief(ex.Message) + hint);
        }
    }

    // 分享卡多节点签名：主站 → 备用站；某个节点失败只冷却自己，不影响后面的节点
    async Task<string> SignShareCardAsync(string title, string? desc, string url, string? preview, string? prompt)
    {
        Dictionary<string, string> parameters = new()
        {
            ["title"] = title,
            ["desc"] = desc ?? "",
            ["preview"] = preview ?? "",
            ["url"] = url,
            ["prompt"] = prompt ?? "",
        };
        List<SignNode> nodes = await ShareNodesAsync();
        if (nodes.Count == 0)
        {
            throw new Exception("签名节点一个都没配置：先填第三方接口基地址，或填备用签名基地址");
        }
        List<string> errors = new();
        foreach (SignNode node in nodes)
        {
            string? cooling = NodeBlocked(node.Url);
            if (cooling != null)
            {
                errors.Add(node.Name + "：" + cooling);
                continue;
            }
            try
            {
                string card = await SignShareOnNodeAsync(node, parameters);
                MarkSignOk(node.Url);
                return card;
            }
            catch (Exception nodeError)
            {
                MarkSignFailed(node.Url, nodeError.Message);
                errors.Add(node.Name + "：" + nodeError.Message);
                logger.LogWarning("分享卡节点 {Node} 失败：{Msg}", node.Name, nodeError.Message);
            }
        }
        throw new Exception("所有分享卡签名节点都失败（" + string.Join("；", errors) + "）");
    }

    async Task<string> SignShareOnNodeAsync(SignNode node, Dictionary<string, string> parameters)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            Dictionary<string, string> q = new(parameters);
            if (!string.IsNullOrWhiteSpace(node.ApiKey))
            {
                q["key"] = node.ApiKey;
            }
            string query = string.Join("&", q.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value ?? "")));
            string full = WithQuery(node.Url, query);
            string body;
            try
            {
                body = await Http.GetStringAsync(full);
            }
            catch (Exception requestError)
            {
                throw new Exception("连不上：" + requestError.Message);
            }
            using JsonDocument doc = JsonDocument.Parse(body);
            if (Ok(doc.RootElement))
            {
                return CardJsonFrom(doc.RootElement.GetProperty("data"));
            }
            string msg = ReadMsg(doc.RootElement);
            // 缓存的接口后缀失效（服务端报 API 不存在）：清掉该节点缓存重探一次
            if (attempt == 0 && msg.Contains("不存在") && node.BaseUrl.Length > 0)
            {
                await RefitAsync(node.BaseUrl, "share", node.ApiKey);
                node = node with { Url = JoinUrl(node.BaseUrl, SfxOf(node.BaseUrl, "share")) };
                continue;
            }
            logger.LogWarning("分享卡节点 {Node} 签名失败：{Msg} | {Url}", node.Name, msg, MaskKey(full));
            throw new Exception("接口回 code=" + ReadCode(doc.RootElement) + (msg.Length > 0 ? "：" + msg : ""));
        }
        throw new Exception("重试后仍失败");
    }

    /// <summary>
    /// 签名不可用时的退路：预览图 + 文字，图不成再退纯文字。
    ///
    /// 只降级、不假装：消息段发成功才算成功，返回体里没有 message_id 一律当失败
    /// （LLOneBot 对不认识的消息段不报错，直接丢）。
    /// </summary>
    async Task<(bool Success, string Message)> SendLinkFallbackAsync(
        long target, bool isGroup, string? title, string? desc, string url, string? preview)
    {
        string head = string.IsNullOrWhiteSpace(title) ? url : title.Trim();
        if (head.Length > 60)
            head = head[..60];

        StringBuilder body = new();
        body.Append('【').Append(head).Append('】');
        string sub = (desc ?? "").Trim().Replace('\n', ' ');
        if (sub.Length > 0 && sub != head)
        {
            if (sub.Length > 120)
                sub = sub[..120] + "…";
            body.Append('\n').Append(sub);
        }
        body.Append('\n').Append(url);

        // 预览图只在是 http(s) 地址时带上：data: 那种协议端读不了，会把整条消息拖死
        if (!string.IsNullOrWhiteSpace(preview) && preview.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            JsonArray withImage = new()
            {
                new JsonObject { ["type"] = "image", ["data"] = new JsonObject { ["file"] = preview.Trim() } },
                new JsonObject { ["type"] = "text", ["data"] = new JsonObject { ["text"] = body.ToString() } }
            };
            (bool Success, string Message) sent = await SendSegmentsAsync(target, isGroup, withImage);
            if (sent.Success)
                return sent;
            logger.LogWarning("图文兜底失败，退纯文字：{Msg}", sent.Message);
        }

        JsonArray textOnly = new()
        {
            new JsonObject { ["type"] = "text", ["data"] = new JsonObject { ["text"] = body.ToString() } }
        };
        return await SendSegmentsAsync(target, isGroup, textOnly);
    }

    // ---------- B站系列（{BaseUrl}/bili-*，均需 ApiKey，文档见咸鱼API /series/bili） ----------

    // 第三方接口统一调用（B站系列/IP查询等，均在 {BaseUrl} 下）：拼 key 参数，code!=200 时抛出 msg
    async Task<JsonElement> ApiCall(string path, params (string k, string? v)[] ps)
    {
        List<SignProvider> providers = Providers().Where(provider => provider.BaseUrl.Length > 0).ToList();
        if (providers.Count == 0)
        {
            throw new Exception("B站系列接口需要第三方基地址，去模块配置里补上");
        }
        if (providers.All(provider => provider.ApiKey.Length == 0))
        {
            throw new Exception("B站系列接口需要 ApiKey，去模块配置里补上");
        }
        List<string> errors = new();
        foreach (SignProvider provider in providers)
        {
            string url = provider.BaseUrl + "/" + path;
            string? cooling = NodeBlocked(url);
            if (cooling != null)
            {
                errors.Add(provider.Name + "：" + cooling);
                continue;
            }
            List<string> q = new();
            if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                q.Add("key=" + Uri.EscapeDataString(provider.ApiKey));
            }
            foreach (var (k, v) in ps)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    q.Add(Uri.EscapeDataString(k) + "=" + Uri.EscapeDataString(v));
                }
            }
            try
            {
                string body = await Http.GetStringAsync(WithQuery(url, string.Join("&", q)));
                using JsonDocument doc = JsonDocument.Parse(body);
                if (ReadCode(doc.RootElement) != 200)
                {
                    throw new Exception(ReadMsg(doc.RootElement) is string m && m.Length > 0 ? m : "调用失败");
                }
                JsonElement data = doc.RootElement.GetProperty("data").Clone();
                MarkSignOk(url);
                return data;
            }
            catch (Exception nodeError)
            {
                MarkSignFailed(url, nodeError.Message);
                errors.Add(provider.Name + "：" + nodeError.Message);
                logger.LogWarning("第三方接口 {Path} 在 {Node} 失败：{Msg}", path, provider.Name, nodeError.Message);
            }
        }
        throw new Exception("所有第三方节点都失败（" + string.Join("；", errors) + "）");
    }

    // 字段取值兼容字符串和数字
    static string S(JsonElement e, string k) => e.TryGetProperty(k, out var v)
        ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : "")
        : "";

    static string GetStringField(JsonElement element, string fieldName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return "";
        }
        if (element.TryGetProperty(fieldName, out JsonElement field) == false)
        {
            return "";
        }
        return field.ValueKind switch
        {
            JsonValueKind.String => field.GetString() ?? "",
            JsonValueKind.Number => field.GetRawText(),
            _ => ""
        };
    }

    static long GetNumericField(JsonElement element, string fieldName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }
        if (element.TryGetProperty(fieldName, out JsonElement field) == false)
        {
            return 0;
        }
        return field.ValueKind switch
        {
            JsonValueKind.Number => field.GetInt64(),
            JsonValueKind.String => long.TryParse(field.GetString(), out long parsed) ? parsed : 0,
            _ => 0
        };
    }

    // 功能开关检查：关闭时向AI说明并返回 true（调用方据此直接返回）
    bool Gate(bool enabled, string name)
    {
        if (enabled) return false;
        interactor.Poke(name + "功能已在插件配置中关闭");
        return true;
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("解析B站链接（视频/b23.tv短链/专栏/番剧/直播间），返回类型、ID、标题、UP主等详情；action=play 额外返回视频播放直链——**取播放直链、解析短链/专栏/番剧/直播间只能用这个，Suran.Bili 没有这些能力**。只想要视频统计和分P列表的，用 Suran.Bili 的 BiliVideo")]
    public async Task BiliParse(
        [Description("B站链接")] string url,
        [Description("id=仅类型和ID；detail=详情（默认）；play=额外返回播放直链")] string? action = null,
        [Description("清晰度80/64/32/16，仅action=play生效，默认80")] int? qn = null)
    {
        if (Gate(Configuration.EnableBiliParse, "B站链接解析")) return;
        try
        {
            interactor.Poke(await BiliParseThirdPartyAsync(url, action, qn));
        }
        catch (Exception thirdPartyError)
        {
            await FallbackOrReportAsync("解析", thirdPartyError, () => OfficialBiliParseAsync(url, action, qn));
        }
    }

    async Task<string> BiliParseThirdPartyAsync(string url, string? action, int? qn)
    {
        var d = await ApiCall("bili-url-parse", ("url", url), ("action", action), ("qn", qn?.ToString()));
        var rep = new List<string> { "类型 " + S(d, "type") + "，ID " + S(d, "id") };
        string title = S(d, "title");
        if (title.Length > 0) rep.Add(title);
        if (d.TryGetProperty("owner", out var ow) && ow.ValueKind == JsonValueKind.Object)
            rep.Add("UP主 " + S(ow, "name"));
        if (d.TryGetProperty("author", out var au) && au.ValueKind == JsonValueKind.Object)
            rep.Add("作者 " + S(au, "name"));
        if (d.TryGetProperty("stat", out var st) && st.ValueKind == JsonValueKind.Object)
            rep.Add("播放 " + S(st, "view") + "，点赞 " + S(st, "like") + "，评论 " + S(st, "reply"));
        if (S(d, "duration_text").Length > 0) rep.Add("时长 " + S(d, "duration_text"));
        if (d.TryGetProperty("rating", out var rt) && rt.ValueKind == JsonValueKind.Number) rep.Add("评分 " + rt.GetDouble());
        if (d.TryGetProperty("ep_count", out var ec) && ec.ValueKind == JsonValueKind.Number) rep.Add("共 " + ec.GetInt32() + " 集");
        if (action == "play")
        {
            if (d.TryGetProperty("play", out var play))
            {
                if (play.TryGetProperty("quality_desc", out var qd)) rep.Add("清晰度 " + qd.GetString());
                string? direct = null;
                if (play.TryGetProperty("no_referer_url", out var nru) && nru.ValueKind == JsonValueKind.String) direct = nru.GetString();
                else if (play.TryGetProperty("durl", out var dl) && dl.ValueKind == JsonValueKind.Array && dl.GetArrayLength() > 0) direct = dl[0].GetString();
                else if (play.TryGetProperty("dash", out var dash) && dash.TryGetProperty("video", out var vd) && vd.ValueKind == JsonValueKind.Array && vd.GetArrayLength() > 0)
                    direct = vd[0].TryGetProperty("base_url", out var bu) ? bu.GetString() : null;
                if (!string.IsNullOrEmpty(direct)) rep.Add("直链 " + direct);
            }
        }
        rep.Add("链接 " + S(d, "web_url"));
        return string.Join("\n", rep);
    }



    [XmlFunction(FunctionMode.OneShot)]
    [Description("查询B站UP主最新动态/视频/专栏更新，可翻页；视频更新带BV号，可直接拿去发卡片")]
    public async Task BiliUpdates(
        [Description("UP主mid")] long uid,
        [Description("类型过滤：all=全部（默认）/video=仅视频/article=仅专栏")] string? type = null,
        [Description("翻页游标，填上一次结果里的 next_offset")] string? offset = null)
    {
        if (Gate(Configuration.EnableBiliUpdates, "UP主更新查询")) return;
        try
        {
            interactor.Poke(await BiliUpdatesThirdPartyAsync(uid, type, offset));
        }
        catch (Exception thirdPartyError)
        {
            await FallbackOrReportAsync("更新查询", thirdPartyError, () => OfficialBiliUpdatesAsync(uid));
        }
    }

    async Task<string> BiliUpdatesThirdPartyAsync(long uid, string? type, string? offset)
    {
        var d = await ApiCall("bili-user-update", ("uid", uid.ToString()), ("type", type ?? "all"), ("offset", offset));
        var rep = new List<string>();
        foreach (var it in d.GetProperty("items").EnumerateArray())
        {
            string line = S(it, "pub_time_text") + " " + (S(it, "title").Length > 0 ? S(it, "title") : S(it, "summary"));
            if (S(it, "bvid").Length > 0) line += "（" + S(it, "bvid") + "）";
            rep.Add(line);
        }
        if (rep.Count == 0) rep.Add("没有更新");
        if (d.TryGetProperty("has_more", out var hm) && hm.ValueKind == JsonValueKind.True && S(d, "next_offset").Length > 0)
            rep.Add("还有更多，用 offset=" + S(d, "next_offset") + " 翻页");
        return string.Join("\n", rep);
    }



    [XmlFunction(FunctionMode.OneShot)]
    [Description("【第三方接口的凭据，不是你的B站账号】扫码登录后拿到 cookie_id，供 ShareArk 的第三方接口调用使用。要登录本机B站账号（收藏/历史/动态等）请用 Suran.Bili 的 BiliLogin。action=generate 申请二维码（把二维码链接只发给主人本人），action=poll 用 qrcode_key 查状态")]
    public async Task BiliQrLogin(
        [Description("generate=申请二维码 / poll=轮询登录状态")] string action,
        [Description("action=poll时填 generate 返回的 qrcode_key")] string? qrcodeKey = null)
    {
        if (Gate(Configuration.EnableBiliQrLogin, "B站扫码登录")) return;
        try
        {
            interactor.Poke(await BiliQrLoginThirdPartyAsync(action, qrcodeKey));
        }
        catch (Exception thirdPartyError)
        {
            await FallbackOrReportAsync("扫码登录", thirdPartyError, () => OfficialBiliQrLoginAsync(action, qrcodeKey));
        }
    }

    async Task<string> BiliQrLoginThirdPartyAsync(string action, string? qrcodeKey)
    {
        var d = await ApiCall("bili-qrcode-login", ("action", action), ("qrcode_key", qrcodeKey));
        if (action == "generate")
            return "qrcode_key " + S(d, "qrcode_key") + "\n二维码链接（180秒内有效，打开后扫码）：\n" + S(d, "qrcode_url") + "\n扫完后用 action=poll、qrcode_key 查结果";
        string status = S(d, "status");
        string msg = S(d, "message").Length > 0 ? S(d, "message") : status;
        if (status == "success")
            msg += "\ncookie_id " + S(d, "cookie_id") + "，用户 " + S(d, "uname") + "，有效期至 " + S(d, "expire_at");
        return msg;
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("【第三方接口的凭据，不是你的B站账号】刷新或检查第三方接口保存的B站Cookie，延长有效期；action=check 仅检查是否需要刷新，refresh=执行刷新（默认）。本机B站登录态的问题请用 Suran.Bili 的 BiliEnv/BiliLogin")]
    public async Task BiliCookieRefresh(
        [Description("Cookie唯一标识ID")] string cookieId,
        [Description("check=仅检查 / refresh=执行刷新（默认）")] string? action = null)
    {
        if (Gate(Configuration.EnableBiliCookieRefresh, "Cookie刷新")) return;
        try
        {
            interactor.Poke(await BiliCookieRefreshThirdPartyAsync(cookieId, action));
        }
        catch (Exception thirdPartyError)
        {
            await FallbackOrReportAsync("Cookie刷新", thirdPartyError, () =>
                Task.FromResult("官方容灾暂不支持Cookie刷新（B站官方刷新流程复杂），请改用 BiliQrLogin 重新扫码登录，或恢复第三方服务后重试"));
        }
    }

    async Task<string> BiliCookieRefreshThirdPartyAsync(string cookieId, string? action)
    {
        var d = await ApiCall("bili-cookie-refresh", ("cookie_id", cookieId), ("action", action));
        bool refreshed = d.TryGetProperty("refreshed", out var rf) && rf.ValueKind == JsonValueKind.True;
        string extra = refreshed ? "已刷新，新有效期至 " + S(d, "new_expire_at")
            : S(d, "message").Length > 0 ? S(d, "message") : "未刷新";
        return "Cookie " + S(d, "cookie_id") + " " + extra;
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("【第三方接口的凭据，不是你的B站账号】管理第三方接口保存的B站Cookie：list=列出全部（默认，不含敏感字段）/check=验证登录态/delete=删除/save=导入Cookie字符串。故意不提供导出完整Cookie的动作，避免凭据进入聊天记录。本机B站账号请看 Suran.Bili 的 BiliLogin(action=check)")]
    public async Task BiliCookie(
        [Description("list=列出/check=验证/delete=删除/save=导入")] string? action = null,
        [Description("Cookie唯一标识ID，check/delete时填")] string? cookieId = null,
        [Description("action=save时必填，格式 k1=v1; k2=v2")] string? cookieString = null)
    {
        if (Gate(Configuration.EnableBiliCookie, "Cookie管理")) return;
        try
        {
            interactor.Poke(await BiliCookieThirdPartyAsync(action, cookieId, cookieString));
        }
        catch (Exception thirdPartyError)
        {
            await FallbackOrReportAsync("Cookie管理", thirdPartyError, () => OfficialBiliCookieAsync(action, cookieString));
        }
    }

    async Task<string> BiliCookieThirdPartyAsync(string? action, string? cookieId, string? cookieString)
    {
        string a = (action ?? "list").Trim().ToLowerInvariant();
        var d = await ApiCall("bili-user-cookie", ("action", a), ("cookie_id", cookieId), ("cookie_string", cookieString));
        switch (a)
        {
            case "list":
                var rep = new List<string>();
                foreach (var it in d.GetProperty("items").EnumerateArray())
                    rep.Add(S(it, "cookie_id") + " " + S(it, "uname") + "（" + S(it, "uid") + "）有效期至 " + S(it, "expire_at") + "，已用 " + S(it, "use_count") + " 次");
                if (rep.Count == 0) rep.Add("没有保存的Cookie");
                return string.Join("\n", rep);
            case "check":
                bool valid = d.TryGetProperty("valid", out var vv) && vv.ValueKind == JsonValueKind.True;
                return "Cookie " + S(d, "cookie_id") + " " + (valid ? "登录态有效：" + S(d, "uname") + "（" + S(d, "uid") + "）" : "无效：" + S(d, "message"));
            case "delete":
                return "Cookie " + S(d, "cookie_id") + (d.TryGetProperty("deleted", out var del) && del.ValueKind == JsonValueKind.True ? " 已删除" : " 删除失败");
            case "save":
                return "已导入，cookie_id " + S(d, "cookie_id") + "，用户 " + S(d, "uname") + "，有效期至 " + S(d, "expire_at");
            default:
                return "不支持的action：" + a + "（可用 list/check/delete/save）";
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("查询IP地址的地理位置、运营商、ASN、时区；不填ip则查询本机出口IP")]
    public async Task IpLookup([Description("要查询的IP地址，留空查本机出口IP")] string? ip = null)
    {
        if (Gate(Configuration.EnableIpLookup, "IP查询")) return;
        try
        {
            var d = await ApiCall("ip-lookup", ("ip", ip));
            bool self = d.TryGetProperty("is_self", out var sf) && sf.ValueKind == JsonValueKind.True;
            var rep = new List<string>
            {
                S(d, "ip") + (self ? "（本机出口）" : ""),
                S(d, "country") + " " + S(d, "region") + " " + S(d, "city"),
                "运营商 " + S(d, "isp") + "，组织 " + S(d, "org") + "，ASN " + S(d, "asn"),
                "时区 " + S(d, "timezone") + "，坐标 " + S(d, "latitude") + "," + S(d, "longitude")
            };
            interactor.Poke(string.Join("\n", rep));
        }
        catch (Exception ex) { interactor.Poke("查询失败：" + ex.Message); }
    }

    // ============================================================
    // 官方容灾：第三方失败时自动切换官方公开接口
    // ============================================================

    // 统一容灾路由：未开启容灾时只报第三方错误；开启则执行官方实现，双失败报双原因
    async Task FallbackOrReportAsync(string name, Exception thirdPartyError, Func<Task<string>> officialFallback)
    {
        if (Configuration.EnableOfficialFallback == false)
        {
            interactor.Poke("❌ " + name + "失败：" + thirdPartyError.Message);
            return;
        }
        try
        {
            interactor.Poke("ℹ️ 第三方接口不可用（" + thirdPartyError.Message + "），已切换官方接口。\n" + await officialFallback());
        }
        catch (Exception officialError)
        {
            interactor.Poke("❌ " + name + "：第三方（" + thirdPartyError.Message + "）；官方容灾（" + officialError.Message + "）");
        }
    }

    // ---- 本地B站Cookie（扫码登录成功后保存，供官方接口降低风控） ----
    string BiliCookieFilePath() => Path.Combine(AlifePath.StorageFolderPath, "ShareArk", "bili_cookie.txt");

    string LoadBiliCookie()
    {
        try
        {
            string path = BiliCookieFilePath();
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }
        catch
        {
            return "";
        }
    }

    void SaveBiliCookie(string cookie)
    {
        string path = BiliCookieFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, cookie.Trim());
    }

    // ---- B站官方GET基座 ----
    const string BiliApiBase = "https://api.bilibili.com";
    const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    async Task<JsonElement> BiliOfficialGetAsync(string url)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Referrer = new Uri("https://www.bilibili.com/");
        string cookie = LoadBiliCookie();
        request.Headers.TryAddWithoutValidation("Cookie", (cookie.Length > 0 ? cookie + "; " : "") + "buvid3=1; b_nut=1");
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        string body = await (await Http.SendAsync(request)).Content.ReadAsStringAsync();
        using JsonDocument parsed = JsonDocument.Parse(body);
        JsonElement root = parsed.RootElement.Clone();
        int code = root.TryGetProperty("code", out JsonElement codeElement) && codeElement.ValueKind == JsonValueKind.Number
            ? codeElement.GetInt32()
            : -1;
        if (code != 0)
        {
            string detail = root.TryGetProperty("message", out JsonElement messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? ""
                : "";
            throw new Exception("B站接口返回 " + code + (detail.Length > 0 ? "：" + detail : ""));
        }
        return root.TryGetProperty("data", out JsonElement dataElement) ? dataElement.Clone() : JsonDocument.Parse("null").RootElement.Clone();
    }

    // ---- WBI 签名（用户信息/投稿搜索接口需要） ----
    static readonly int[] WbiMixinTable = { 46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35, 27, 43, 5, 49, 33, 9, 42, 19, 29, 28, 14, 39, 12, 38, 41, 13, 37, 48, 7, 16, 24, 55, 40, 61, 26, 17, 0, 1, 60, 51, 30, 4, 22, 25, 54, 21, 56, 59, 6, 63, 57, 62, 11, 36, 20, 34, 44, 52 };
    string wbiMixinKey = "";
    DateTime wbiKeyFetchedTime = DateTime.MinValue;

    async Task<string> GetWbiMixinKeyAsync()
    {
        if (wbiMixinKey.Length > 0 && (DateTime.Now - wbiKeyFetchedTime).TotalHours < 24)
        {
            return wbiMixinKey;
        }
        JsonElement nav = await BiliOfficialGetAsync(BiliApiBase + "/x/web-interface/nav");
        JsonElement wbiImg = nav.GetProperty("wbi_img");
        string imgKey = GetStringField(wbiImg, "img_url").Split('/').Last().Split('.')[0];
        string subKey = GetStringField(wbiImg, "sub_url").Split('/').Last().Split('.')[0];
        string rawKey = imgKey + subKey;
        char[] mixin = new char[32];
        for (int i = 0; i < 32; i++)
        {
            mixin[i] = rawKey[WbiMixinTable[i]];
        }
        wbiMixinKey = new string(mixin);
        wbiKeyFetchedTime = DateTime.Now;
        return wbiMixinKey;
    }

    static string Md5Hex(string text)
    {
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    static string FilterWbiValue(string value)
    {
        return Regex.Replace(value, "[!'()*]", "");
    }

    // 对参数做 WBI 签名，返回带 w_rid/wts 的最终查询串
    async Task<string> BuildWbiQueryAsync(Dictionary<string, string> parameters)
    {
        string mixinKey = await GetWbiMixinKeyAsync();
        parameters["wts"] = DateTimeOffset.Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        string query = string.Join("&", parameters
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(FilterWbiValue(pair.Value))));
        return query + "&w_rid=" + Md5Hex(query + mixinKey);
    }

    // ---- BiliParse 官方实现：URL解析 + view 详情 + playurl 直链 ----
    async Task<string> OfficialBiliParseAsync(string url, string? action, int? qn)
    {
        string workUrl = url.Trim();
        if (workUrl.Contains("b23.tv"))
        {
            try
            {
                using HttpResponseMessage redirectResponse = await Http.GetAsync(workUrl);
                string? finalUrl = redirectResponse.RequestMessage?.RequestUri?.ToString();
                if (string.IsNullOrEmpty(finalUrl) == false)
                {
                    workUrl = finalUrl;
                }
            }
            catch
            {
                // 短链解析失败则按原文解析
            }
        }

        string bvid = Regex.Match(workUrl, "BV[0-9A-Za-z]{10}").Value;
        Match articleMatch = Regex.Match(workUrl, "cv([0-9]+)");
        Match seasonMatch = Regex.Match(workUrl, "ss([0-9]+)");
        Match liveMatch = Regex.Match(workUrl, @"live\.bilibili\.com/([0-9]+)");

        if (bvid.Length == 12)
        {
            var rep = new List<string> { "类型 video，ID " + bvid };
            if (action == "id")
            {
                rep.Add("链接 https://www.bilibili.com/video/" + bvid);
                return string.Join("\n", rep);
            }
            JsonElement view = await BiliOfficialGetAsync(BiliApiBase + "/x/web-interface/view?bvid=" + bvid);
            rep.Add(S(view, "title"));
            if (view.TryGetProperty("owner", out JsonElement owner) && owner.ValueKind == JsonValueKind.Object)
            {
                rep.Add("UP主 " + S(owner, "name"));
            }
            if (view.TryGetProperty("stat", out JsonElement stat) && stat.ValueKind == JsonValueKind.Object)
            {
                rep.Add("播放 " + S(stat, "view") + "，点赞 " + S(stat, "like") + "，评论 " + S(stat, "reply"));
            }
            rep.Add("时长 " + FormatSeconds(GetNumericField(view, "duration")));
            rep.Add("链接 https://www.bilibili.com/video/" + bvid);
            if (action == "play")
            {
                try
                {
                    long cid = GetNumericField(view, "cid");
                    JsonElement play = await BiliOfficialGetAsync(BiliApiBase + "/x/player/playurl?bvid=" + bvid
                        + "&cid=" + cid + "&qn=" + (qn ?? 80) + "&fnval=16");
                    string? direct = null;
                    if (play.TryGetProperty("durl", out JsonElement durl) && durl.ValueKind == JsonValueKind.Array && durl.GetArrayLength() > 0)
                    {
                        direct = durl[0].TryGetProperty("url", out JsonElement durlUrl) ? durlUrl.GetString() : null;
                    }
                    else if (play.TryGetProperty("dash", out JsonElement dash)
                        && dash.TryGetProperty("video", out JsonElement dashVideo)
                        && dashVideo.ValueKind == JsonValueKind.Array && dashVideo.GetArrayLength() > 0)
                    {
                        direct = dashVideo[0].TryGetProperty("base_url", out JsonElement baseUrl) ? baseUrl.GetString() : null;
                    }
                    rep.Add(string.IsNullOrEmpty(direct) ? "直链获取失败（高清晰度可能需要登录态）" : "直链 " + direct);
                }
                catch (Exception playError)
                {
                    rep.Add("直链获取失败：" + playError.Message);
                }
            }
            return string.Join("\n", rep);
        }
        if (liveMatch.Success)
        {
            return "类型 live，ID " + liveMatch.Groups[1].Value + "\n链接 https://live.bilibili.com/" + liveMatch.Groups[1].Value;
        }
        if (seasonMatch.Success)
        {
            return "类型 bangumi，ID ss" + seasonMatch.Groups[1].Value + "\n链接 https://www.bilibili.com/bangumi/play/ss" + seasonMatch.Groups[1].Value;
        }
        if (articleMatch.Success)
        {
            return "类型 article，ID cv" + articleMatch.Groups[1].Value + "\n链接 https://www.bilibili.com/read/cv" + articleMatch.Groups[1].Value
                + "\n（官方容灾不提供专栏详情）";
        }
        throw new Exception("无法从链接中识别B站资源（支持视频/短链/专栏/番剧/直播间）");
    }

    // ---- BiliUpdates 官方实现：动态接口（视频投稿，无需WBI） ----
    async Task<string> OfficialBiliUpdatesAsync(long uid)
    {
        JsonElement data = await BiliOfficialGetAsync(BiliApiBase + "/x/polymer/web-dynamic/v1/feed/space?host_mid="
            + uid.ToString(CultureInfo.InvariantCulture));
        var rep = new List<string>();
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (GetStringField(item, "type") != "DYNAMIC_TYPE_AV")
                {
                    continue;
                }
                string authorName = "";
                long pubTs = 0;
                string title = "";
                string bvid = "";
                if (item.TryGetProperty("modules", out JsonElement modules) && modules.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement module in modules.EnumerateArray())
                    {
                        if (module.TryGetProperty("module_author", out JsonElement author) && author.ValueKind == JsonValueKind.Object)
                        {
                            authorName = S(author, "name");
                            pubTs = GetNumericField(author, "pub_ts");
                        }
                        if (module.TryGetProperty("module_dynamic", out JsonElement dynamicModule)
                            && dynamicModule.ValueKind == JsonValueKind.Object
                            && dynamicModule.TryGetProperty("major", out JsonElement major)
                            && major.ValueKind == JsonValueKind.Object
                            && major.TryGetProperty("archive", out JsonElement archive)
                            && archive.ValueKind == JsonValueKind.Object)
                        {
                            title = S(archive, "title");
                            bvid = S(archive, "bvid");
                        }
                    }
                }
                if (bvid.Length == 0)
                {
                    continue;
                }
                string timeText = pubTs > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(pubTs).LocalDateTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + " "
                    : "";
                rep.Add(timeText + title + "（" + bvid + "）" + (authorName.Length > 0 ? " - " + authorName : ""));
            }
        }
        if (rep.Count == 0)
        {
            rep.Add("没有取到视频投稿（官方容灾仅支持视频投稿；动态/专栏请等第三方恢复后查询）");
        }
        return string.Join("\n", rep);
    }

    // ---- BiliQrLogin 官方实现：passport 扫码登录（官方网页原生接口） ----
    async Task<string> OfficialBiliQrLoginAsync(string action, string? qrcodeKey)
    {
        string normalized = (action ?? "").Trim().ToLowerInvariant();
        if (normalized == "generate")
        {
            JsonElement data = await BiliOfficialGetAsync("https://passport.bilibili.com/x/passport-login/web/qrcode/generate");
            // passport 这里回的字段是 url（实测 data 只有 url 与 qrcode_key），写成 qrcode_url 会打出空链接
            return "qrcode_key " + S(data, "qrcode_key") + "\n二维码链接（180秒内有效，打开后扫码）：\n" + S(data, "url")
                + "\n扫完后用 action=poll、qrcode_key 查结果；登录成功后 Cookie 自动存本地";
        }
        if (normalized == "poll")
        {
            if (string.IsNullOrWhiteSpace(qrcodeKey))
            {
                throw new Exception("poll 需要 qrcode_key");
            }
            using HttpRequestMessage request = new(HttpMethod.Get,
                "https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key=" + Uri.EscapeDataString(qrcodeKey));
            using HttpResponseMessage response = await Http.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument parsed = JsonDocument.Parse(body);
            JsonElement data = parsed.RootElement.GetProperty("data").Clone();
            long innerCode = GetNumericField(data, "code");
            if (innerCode == 0)
            {
                List<string> cookies = new();
                if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookieHeaders))
                {
                    foreach (string cookieHeader in setCookieHeaders)
                    {
                        cookies.Add(cookieHeader.Split(';')[0].Trim());
                    }
                }
                // 成功时 Cookie 也可能只拼在 data.url 的查询串里，两种下发形态都收，
                // 只认一种就会出现「说登录成功了，其实本地存了个空 Cookie」
                foreach (string urlPair in LoginCookiePairsFromUrl(S(data, "url")))
                {
                    if (cookies.Contains(urlPair) == false)
                    {
                        cookies.Add(urlPair);
                    }
                }
                if (cookies.Count == 0)
                {
                    return "B站回扫码成功，但响应里既没有 Set-Cookie 也没带 Cookie 的跳转链接，Cookie 没有保存。"
                        + "把这行发给开发者，用来定位是接口改了哪个字段";
                }
                SaveBiliCookie(string.Join("; ", cookies));
                return "登录成功，Cookie 已保存到本地（官方接口将自动携带）。可用 BiliCookie check 验证。";
            }
            if (innerCode == 86090)
            {
                return "已扫描，等待确认";
            }
            if (innerCode == 86038)
            {
                return "二维码已过期，请重新 generate";
            }
            if (innerCode == 86101)
            {
                return "未扫描";
            }
            return S(data, "message").Length > 0 ? S(data, "message") : "状态码 " + innerCode;
        }
        throw new Exception("action 仅支持 generate/poll");
    }

    // 扫码成功时 B 站也可能把 Cookie 拼在 data.url 的查询串里下发，只取登录相关这几对
    static List<string> LoginCookiePairsFromUrl(string url)
    {
        List<string> pairs = new();
        int mark = url.IndexOf('?');
        if (mark < 0)
        {
            return pairs;
        }
        foreach (string item in url[(mark + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = item.Split('=', 2);
            if (parts.Length == 2
                && parts[0] is "SESSDATA" or "bili_jct" or "DedeUserID" or "DedeUserID__ckMd5" or "sid")
            {
                pairs.Add(parts[0] + "=" + Uri.UnescapeDataString(parts[1]));
            }
        }
        return pairs;
    }

    // ---- BiliCookie 官方实现：本地Cookie管理 ----
    async Task<string> OfficialBiliCookieAsync(string? action, string? cookieString)
    {
        string normalized = (action ?? "list").Trim().ToLowerInvariant();
        string path = BiliCookieFilePath();
        switch (normalized)
        {
            case "list":
            {
                string cookie = LoadBiliCookie();
                return cookie.Length == 0
                    ? "本地未保存B站Cookie（可用 BiliQrLogin 扫码生成，或 BiliCookie save 导入）"
                    : "本地Cookie存在（" + cookie.Length + " 字符，不回显内容），保存于 " + path;
            }
            case "check":
            {
                string cookie = LoadBiliCookie();
                if (cookie.Length == 0)
                {
                    return "本地未保存B站Cookie";
                }
                try
                {
                    JsonElement nav = await BiliOfficialGetAsync(BiliApiBase + "/x/web-interface/nav");
                    bool isLogin = nav.TryGetProperty("isLogin", out JsonElement loginElement) && loginElement.ValueKind == JsonValueKind.True;
                    string uname = S(nav, "uname");
                    return isLogin ? "本地Cookie登录态有效：" + uname : "本地Cookie已失效，请重新扫码登录";
                }
                catch (Exception checkError)
                {
                    return "Cookie有效性验证失败：" + checkError.Message;
                }
            }
            case "delete":
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return "本地Cookie已删除";
            case "save":
                if (string.IsNullOrWhiteSpace(cookieString))
                {
                    throw new Exception("save 需要提供 cookieString");
                }
                SaveBiliCookie(cookieString);
                return "Cookie已保存到本地";
            default:
                return "不支持的action：" + normalized + "（可用 list/check/delete/save）";
        }
    }

    // ---- 搜索官方容灾：平台公开接口 ----
    async Task<List<(string Id, string Name, string Singer)>> OfficialSearchAsync(string platform, string keyword)
    {
        List<(string, string, string)> results = new();
        if (platform == "163")
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "https://music.163.com/api/search/get");
            request.Headers.Referrer = new Uri("https://music.163.com/");
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["s"] = keyword,
                ["type"] = "1",
                ["limit"] = "5"
            });
            string body = await (await Http.SendAsync(request)).Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement songs = doc.RootElement.GetProperty("result").GetProperty("songs").Clone();
            foreach (JsonElement song in songs.EnumerateArray())
            {
                string singer = "";
                if (song.TryGetProperty("artists", out JsonElement artists) && artists.ValueKind == JsonValueKind.Array && artists.GetArrayLength() > 0)
                {
                    singer = artists[0].GetProperty("name").GetString() ?? "";
                }
                results.Add((GetNumericField(song, "id").ToString(CultureInfo.InvariantCulture), GetStringField(song, "name"), singer));
            }
        }
        else if (platform == "qq")
        {
            using HttpRequestMessage request = new(HttpMethod.Get,
                "https://c.y.qq.com/splcloud/fcgi-bin/smartbox_new.fcg?format=json&key=" + Uri.EscapeDataString(keyword));
            request.Headers.Referrer = new Uri("https://y.qq.com/");
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            string body = await (await Http.SendAsync(request)).Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement items = doc.RootElement.GetProperty("data").GetProperty("song").GetProperty("itemlist").Clone();
            foreach (JsonElement item in items.EnumerateArray())
            {
                // 实测 smartbox 的 itemlist 字段是 mid/name（老写法 songmid/songname 在这接口上取不到，
                // 会静默给个空 id，后面详情与签名全废），两个名字都认一下
                string songMid = GetStringField(item, "mid");
                if (songMid.Length == 0)
                {
                    songMid = GetStringField(item, "songmid");
                }
                string songName = GetStringField(item, "name");
                if (songName.Length == 0)
                {
                    songName = GetStringField(item, "songname");
                }
                results.Add((songMid, songName, GetStringField(item, "singer")));
            }
        }
        else if (platform == "kugou")
        {
            using HttpRequestMessage request = new(HttpMethod.Get,
                "https://mobilecdn.kugou.com/api/v3/search/song?format=json&pagesize=5&page=1&keyword=" + Uri.EscapeDataString(keyword));
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            string kugouBody;
            try
            {
                using HttpResponseMessage kugou = await Http.SendAsync(request);
                kugouBody = await kugou.Content.ReadAsStringAsync();
            }
            catch (Exception kugouError)
            {
                throw new Exception("酷狗这个搜索接口在当前网络连不上（" + kugouError.Message.Split('\n')[0] + "），搜歌先换 163 或 qq");
            }
            using JsonDocument doc = JsonDocument.Parse(kugouBody);
            JsonElement infoList = doc.RootElement.GetProperty("data").GetProperty("info").Clone();
            foreach (JsonElement item in infoList.EnumerateArray())
            {
                results.Add((GetStringField(item, "hash"), GetStringField(item, "songname"), GetStringField(item, "singername")));
            }
        }
        else
        {
            throw new Exception("官方搜索不支持该平台：" + platform);
        }
        return results;
    }

    // ---- 音乐卡官方容灾：构造 OneBot 标准音乐消息段 ----
    async Task<JsonObject> BuildOfficialMusicSegmentAsync(string platform, string id, string? title)
    {
        string normalized = (platform ?? "163").Trim().ToLowerInvariant();
        if (normalized == "netease")
        {
            normalized = "163";
        }
        if (normalized == "163" || normalized == "qq")
        {
            // 官方模板类型：协议端自己生成卡片
            string songKey = id.Trim();
            bool idValid = normalized == "163" ? songKey.All(char.IsDigit) : songKey.Length == 14;
            if (idValid == false)
            {
                List<(string Id, string Name, string Singer)> hits = await OfficialSearchAsync(normalized, id);
                if (hits.Count == 0)
                {
                    throw new Exception("官方搜索没找到歌曲：" + id);
                }
                songKey = hits[0].Id;
            }
            return new JsonObject
            {
                ["type"] = "music",
                ["data"] = new JsonObject
                {
                    ["type"] = normalized,
                    ["id"] = songKey
                }
            };
        }

        // bilibili / kugou：custom 音乐段（官方无模板类型）
        Dictionary<string, string> info = await ResolveInfoAsync(normalized, id, title);
        string audio = "";
        if (normalized == "bilibili")
        {
            try
            {
                Match bvidMatch = Regex.Match(info.GetValueOrDefault("jump") ?? "", "BV[0-9A-Za-z]{10}");
                if (bvidMatch.Success)
                {
                    JsonElement view = await BiliOfficialGetAsync(BiliApiBase + "/x/web-interface/view?bvid=" + bvidMatch.Value);
                    long cid = GetNumericField(view, "cid");
                    JsonElement play = await BiliOfficialGetAsync(BiliApiBase + "/x/player/playurl?bvid=" + bvidMatch.Value
                        + "&cid=" + cid + "&qn=64&fnval=16");
                    if (play.TryGetProperty("dash", out JsonElement dash)
                        && dash.TryGetProperty("audio", out JsonElement dashAudio)
                        && dashAudio.ValueKind == JsonValueKind.Array && dashAudio.GetArrayLength() > 0)
                    {
                        audio = dashAudio[0].TryGetProperty("base_url", out JsonElement audioUrl) ? audioUrl.GetString() ?? "" : "";
                    }
                    else if (play.TryGetProperty("durl", out JsonElement playDurl) && playDurl.ValueKind == JsonValueKind.Array && playDurl.GetArrayLength() > 0)
                    {
                        audio = playDurl[0].TryGetProperty("url", out JsonElement playUrl) ? playUrl.GetString() ?? "" : "";
                    }
                }
            }
            catch
            {
                // 直链失败则降级为不可播放卡片
            }
        }
        else
        {
            // kugou 官方无公开直链：降级为跳转卡（audio 指向页面）
            audio = info.GetValueOrDefault("url") ?? "";
        }
        // SnowLuma 校验 custom 音乐段必须有非空 url/audio/title，拿不到播放直链时退回跳转地址，
        // 否则整条消息会被 1400 原子拒绝（NapCat 与 LLOneBot 容忍空 audio）
        string jumpUrl = info.GetValueOrDefault("jump") ?? "";
        if (audio.Length == 0)
        {
            audio = jumpUrl;
        }
        return new JsonObject
        {
            ["type"] = "music",
            ["data"] = new JsonObject
            {
                ["type"] = "custom",
                ["url"] = jumpUrl,
                ["audio"] = audio,
                ["title"] = info.GetValueOrDefault("song") ?? "未知",
                ["content"] = info.GetValueOrDefault("singer") ?? "",
                ["image"] = info.GetValueOrDefault("cover") ?? ""
            }
        };
    }

    static string FormatSeconds(long totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return "N/A";
        }
        return (totalSeconds / 60).ToString(CultureInfo.InvariantCulture) + ":"
            + (totalSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    class PageMeta { public string title = ""; public string desc = ""; public string image = ""; }

    async Task<PageMeta> FetchMetaAsync(string url)
    {
        var m = new PageMeta();
        try
        {
            // 页面抓取只给8秒：国外站连不上时别拖满20秒全局超时。
            // 走 SafeFetchAsync：链接是用户给的，必须逐跳校验重定向和内网地址
            FetchResult page = await SafeFetchAsync(url, null, TimeSpan.FromSeconds(8), true);
            string html = DecodeBody(page.Bytes, page.ContentType);
            // 和 ArkLinkMeta 共用同一套 meta 解析，避免两处正则各写一份、修一处漏一处
            ArkLinkInfo meta = ArkFreeSources.ParseOpenGraph(html, url);
            m.title = meta.Title;
            m.desc = meta.Description;
            m.image = meta.Image;
            if (string.IsNullOrWhiteSpace(m.image))
            {
                Match lm = Regex.Match(html, "<link[^>]+rel=[\"'][^\"']*icon[^\"']*[\"'][^>]*?href=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
                if (!lm.Success) lm = Regex.Match(html, "<link[^>]+href=[\"']([^\"']+)[\"'][^>]*?rel=[\"'][^\"']*icon[^\"']*[\"']", RegexOptions.IgnoreCase);
                if (lm.Success)
                {
                    try { m.image = new Uri(new Uri(url), lm.Groups[1].Value).ToString(); } catch { }
                }
            }
        }
        catch (Exception ex) { logger.LogWarning("抓网页meta失败：" + ex.Message); }
        // 页面抓不到（超时/被墙）也必须给非空预览：签名端空值当缺参，直接用站点favicon
        if (string.IsNullOrWhiteSpace(m.image))
        {
            try { m.image = new Uri(new Uri(url), "/favicon.ico").ToString(); } catch { }
        }
        return m;
    }

    // 传歌名不是ID时统一走搜索接口拿第一条：先试第三方多节点，全失败再走官方搜索
    async Task<(string id, string title, string author, string cover)> SearchFirstAsync(string pf, string kw)
    {
        try
        {
            JsonElement data = await SearchRawAsync(pf, kw);
            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            {
                throw new Exception("搜索没结果：" + kw);
            }
            JsonElement d = data[0];
            string G(string k) => d.TryGetProperty(k, out var v) ? (v.GetString() ?? "") : "";
            return (G("id"), G("title"), G("author"), G("cover"));
        }
        catch (Exception thirdPartyError)
        {
            // 第三方搜索全挂时，用官方搜索兜住；官方没有封面字段，后面详情接口会补
            List<(string Id, string Name, string Singer)> official = await OfficialSearchAsync(pf, kw);
            if (official.Count == 0)
            {
                throw new Exception("搜索没结果（第三方：" + thirdPartyError.Message + "；官方也没搜到）");
            }
            (string Id, string Name, string Singer) first = official[0];
            return (first.Id, first.Name, first.Singer, "");
        }
    }

    // 详情接口的第三方兜底：用第三方搜索端的 id= 参数再问一次（不同平台返回形状不一，做宽松映射）
    async Task<Dictionary<string, string>?> SearchDetailFallbackAsync(string pf, string id, string format, string jump, string playUrl)
    {
        try
        {
            JsonElement data = await SearchDataAsync(pf, "id=" + Uri.EscapeDataString(id));
            JsonElement d = data;
            if (d.ValueKind == JsonValueKind.Array)
            {
                if (d.GetArrayLength() == 0)
                {
                    return null;
                }
                d = d[0];
            }
            if (d.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            string title = Field(d, "title", "song", "name");
            string singer = Field(d, "author", "singer", "artist");
            string cover = Field(d, "cover", "pic", "picUrl", "img");
            string url = Field(d, "url", "play_url", "music_url");
            string link = Field(d, "link", "jump", "jump_url");
            if (title.Length == 0 || singer.Length == 0)
            {
                return null;
            }
            return new Dictionary<string, string>
            {
                ["song"] = title,
                ["singer"] = singer,
                ["cover"] = cover,
                ["format"] = format,
                ["url"] = url.Length > 0 ? url : playUrl,
                ["jump"] = link.Length > 0 ? link : jump,
            };
        }
        catch (Exception fallbackError)
        {
            logger.LogWarning("详情第三方兜底失败（{Platform} {Id}）：{Msg}", pf, id, fallbackError.Message);
            return null;
        }
    }

    static string Field(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.TryGetProperty(name, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    string text = value.GetString() ?? "";
                    if (text.Length > 0)
                    {
                        return text;
                    }
                }
                else if (value.ValueKind == JsonValueKind.Number)
                {
                    return value.GetRawText();
                }
            }
        }
        return "";
    }

    // ---- 公共音乐平台容灾：洛苏API（点歌详情）/ 糖豆子API（QQ 搜索） ----

    static string LuoSuPath(string platform)
    {
        return platform switch
        {
            "163" => "/api/wymusic/",
            "qq" => "/api/qqmusic/",
            "kugou" => "/api/kgmusic/",
            _ => "",
        };
    }

    // 洛苏点歌：一次请求就能拿到歌名/歌手/封面/播放直链，适合按歌名解析详情
    async Task<Dictionary<string, string>?> LuoSuSongAsync(string platform, string keyword, int index = 1)
    {
        if (Configuration.EnablePublicMusicApis == false)
        {
            return null;
        }
        string path = LuoSuPath(platform);
        if (path.Length == 0 || (keyword ?? "").Trim().Length == 0)
        {
            return null;
        }
        try
        {
            string baseUrl = (Configuration.LuoSuBaseUrl ?? "").Trim().TrimEnd('/');
            if (baseUrl.Length == 0)
            {
                return null;
            }
            string url = WithQuery(baseUrl + path,
                "ss=" + Uri.EscapeDataString(keyword.Trim())
                + "&n=" + Math.Max(1, index).ToString(CultureInfo.InvariantCulture)
                + "&format=json");
            string body = await Http.GetStringAsync(url);
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || ReadCode(root) != 200)
            {
                return null;
            }
            JsonElement data = root.GetProperty("data");
            if (data.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            string song = Field(data, "name");
            string singer = Field(data, "singer");
            string cover = Field(data, "cover_url");
            string play = Field(data, "play_url");
            string id = platform == "qq" ? Field(data, "songmid", "id")
                : platform == "kugou" ? Field(data, "songmid", "hash", "id")
                : Field(data, "id", "songmid");
            string jump;
            if (platform == "163")
            {
                jump = "https://music.163.com/song?id=" + id;
            }
            else if (platform == "qq")
            {
                jump = "https://y.qq.com/n/ryqq/songDetail/" + id;
            }
            else
            {
                jump = "https://www.kugou.com/song/#hash=" + id;
            }
            if (song.Length == 0 || singer.Length == 0)
            {
                return null;
            }
            return new Dictionary<string, string>
            {
                ["song"] = song,
                ["singer"] = singer,
                ["cover"] = cover,
                ["format"] = platform == "163" ? "netease" : platform,
                ["url"] = play.Length > 0 ? play : jump,
                ["jump"] = jump,
            };
        }
        catch (Exception luosuError)
        {
            logger.LogWarning("洛苏API 点歌失败（{Platform} {Keyword}）：{Msg}", platform, keyword, luosuError.Message);
            return null;
        }
    }

    // 搜歌兜底：洛苏列表 + 糖豆子（QQ），返回可直接展示的行
    async Task<string?> PublicMusicSearchLinesAsync(string pf, string keyword)
    {
        if (Configuration.EnablePublicMusicApis == false)
        {
            return null;
        }
        try
        {
            string path = LuoSuPath(pf);
            string baseUrl = (Configuration.LuoSuBaseUrl ?? "").Trim().TrimEnd('/');
            if (path.Length > 0 && baseUrl.Length > 0)
            {
                string url = WithQuery(baseUrl + path,
                    "ss=" + Uri.EscapeDataString(keyword) + "&format=json&t=5");
                string body = await Http.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(body);
                JsonElement root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && ReadCode(root) == 200)
                {
                    JsonElement songs = JsonValueOrDefault(root, "data", "songs");
                    if (songs.ValueKind == JsonValueKind.Array && songs.GetArrayLength() > 0)
                    {
                        List<string> lines = new() { "洛苏API：" };
                        foreach (JsonElement song in songs.EnumerateArray())
                        {
                            lines.Add(Field(song, "index") + " | " + Field(song, "name")
                                + " - " + Field(song, "artists"));
                        }
                        return string.Join("\n", lines);
                    }
                }
            }
        }
        catch (Exception luosuError)
        {
            logger.LogWarning("洛苏API 搜歌失败：{Msg}", luosuError.Message);
        }
        if (pf == "qq")
        {
            try
            {
                string baseUrl = (Configuration.TangDouZiBaseUrl ?? "").Trim().TrimEnd('/');
                if (baseUrl.Length > 0)
                {
                    string url = WithQuery(baseUrl + "/a/qqmusic.php",
                        "nr=" + Uri.EscapeDataString(keyword) + "&return=json");
                    string body = await Http.GetStringAsync(url);
                    using JsonDocument doc = JsonDocument.Parse(body);
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        List<string> lines = new() { "糖豆子API：" };
                        int count = 0;
                        foreach (JsonElement song in root.EnumerateArray())
                        {
                            if (count++ >= 5)
                            {
                                break;
                            }
                            string share = Field(song, "share");
                            Match idMatch = Regex.Match(share, @"songid=(\d+)");
                            lines.Add((idMatch.Success ? idMatch.Groups[1].Value : "?")
                                + " | " + Field(song, "name") + " - " + Field(song, "singer"));
                        }
                        return string.Join("\n", lines);
                    }
                }
            }
            catch (Exception tangError)
            {
                logger.LogWarning("糖豆子API 搜歌失败：{Msg}", tangError.Message);
            }
        }
        return null;
    }

    static JsonElement JsonValueOrDefault(JsonElement root, params string[] path)
    {
        JsonElement current = root;
        foreach (string key in path)
        {
            if (current.ValueKind != JsonValueKind.Object || current.TryGetProperty(key, out JsonElement next) == false)
            {
                return JsonDocument.Parse("null").RootElement.Clone();
            }
            current = next;
        }
        return current;
    }

    async Task<Dictionary<string, string>> ResolveInfoAsync(string platform, string id, string? title)
    {
        var info = new Dictionary<string, string>();
        string original = (id ?? "").Trim();
        platform = (platform ?? "163").Trim().ToLowerInvariant();
        if (platform == "163")
        {
            try
            {
                if (!id.All(char.IsDigit))
                {
                    var r = await SearchFirstAsync("163", id);
                    id = r.id;
                    if (string.IsNullOrWhiteSpace(title)) title = r.title;
                }
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://music.163.com/api/song/detail/?ids=[" + id + "]");
                req.Headers.Referrer = new Uri("https://music.163.com/");
                req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
                string body = await (await Http.SendAsync(req)).Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var s = doc.RootElement.GetProperty("songs")[0];
                info["song"] = s.GetProperty("name").GetString() ?? "未知歌曲";
                info["singer"] = string.Join("/", s.GetProperty("artists").EnumerateArray().Select(a => a.GetProperty("name").GetString()));
                info["cover"] = s.TryGetProperty("album", out var alb) && alb.TryGetProperty("picUrl", out var pu) ? pu.GetString() ?? "" : "";
            }
            catch (Exception detailError)
            {
                logger.LogWarning("网易云详情失败（id {Id}）：{Msg}", id, detailError.Message);
                Dictionary<string, string>? fallback = await SearchDetailFallbackAsync("163", id, "netease",
                    "http://music.163.com/song?id=" + id,
                    "http://music.163.com/song/media/outer/url?id=" + id + ".mp3");
                if (fallback != null)
                {
                    logger.LogInformation("网易云详情已用第三方兜底（id {Id}）", id);
                    return fallback;
                }
                Dictionary<string, string>? luosu = await LuoSuSongAsync("163", original);
                if (luosu != null)
                {
                    logger.LogInformation("网易云详情已用洛苏API兜底（{Keyword}）", original);
                    return luosu;
                }
                throw new Exception("拿不到网易云歌曲详情（id " + id + "）：" + detailError.Message
                    + "\n换个 id 或改用歌名搜索；接口被风控时过一会儿再试");
            }
            info["format"] = "netease";
            info["url"] = "http://music.163.com/song/media/outer/url?id=" + id + ".mp3";
            info["jump"] = "http://music.163.com/song?id=" + id;
        }
        else if (platform == "bilibili")
        {
            try
            {
                if (!id.StartsWith("BV", StringComparison.OrdinalIgnoreCase))
                {
                    var hits = await BiliSearchAsync(id);
                    string? pick = null;
                    var words = id.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var (bv, vt) in hits)
                    {
                        if (words.Length > 0 && words.All(w => vt.Contains(w))) { pick = bv; break; }
                    }
                    if (pick == null && hits.Count > 0) pick = hits[0].bv;
                    if (pick == null) throw new Exception("没搜到匹配的视频");
                    id = pick;
                }
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.bilibili.com/x/web-interface/view?bvid=" + id);
                req.Headers.Referrer = new Uri("https://www.bilibili.com/video/" + id);
                req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                req.Headers.TryAddWithoutValidation("Cookie", "buvid3=1; b_nut=1");
                req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
                string body = await (await Http.SendAsync(req)).Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var dd = doc.RootElement.GetProperty("data");
                string pic = dd.GetProperty("pic").GetString() ?? "";
                if (pic.StartsWith("//")) pic = "https:" + pic;
                info["song"] = dd.GetProperty("title").GetString() ?? "未知视频";
                info["singer"] = dd.GetProperty("owner").GetProperty("name").GetString() ?? "bilibili";
                info["cover"] = pic;
            }
            catch (Exception detailError)
            {
                logger.LogWarning("B站视频详情失败（{Id}）：{Msg}", id, detailError.Message);
                throw new Exception("拿不到 B站视频详情（" + id + "）：" + detailError.Message
                    + "\n确认 BV 号没过期，或改用 BiliParse 先解析链接");
            }
            info["url"] = "https://www.bilibili.com/video/" + id;
            info["jump"] = "https://www.bilibili.com/video/" + id;
            info["format"] = "bilibili";
        }
        else if (platform == "qq")
        {
            try
            {
                if (id.Length != 14)
                {
                    var r = await SearchFirstAsync("qq", id);
                    if (!string.IsNullOrWhiteSpace(r.id)) id = r.id;
                    if (string.IsNullOrWhiteSpace(title)) title = r.title;
                }
                string u = "https://c.y.qq.com/v8/fcg-bin/fcg_play_single_song.fcg?songmid=" + id + "&platform=yqq&format=json&inCharset=utf8&outCharset=utf-8";
                using var req = new HttpRequestMessage(HttpMethod.Get, u);
                req.Headers.Referrer = new Uri("https://y.qq.com/");
                req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
                string body = await (await Http.SendAsync(req)).Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var dd = doc.RootElement.GetProperty("data")[0];
                string albid = dd.GetProperty("album").GetProperty("mid").GetString() ?? "";
                info["song"] = dd.GetProperty("title").GetString() ?? "未知歌曲";
                info["singer"] = string.Join("/", dd.GetProperty("singer").EnumerateArray().Select(a => a.GetProperty("name").GetString()));
                info["cover"] = "https://y.qq.com/music/photo_new/T002R300x300M000" + albid + ".jpg";
            }
            catch (Exception detailError)
            {
                logger.LogWarning("QQ音乐详情失败（{Id}）：{Msg}", id, detailError.Message);
                Dictionary<string, string>? fallback = await SearchDetailFallbackAsync("qq", id, "qq",
                    "https://y.qq.com/n/ryqq/songDetail/" + id, "");
                if (fallback != null)
                {
                    logger.LogInformation("QQ音乐详情已用第三方兜底（{Id}）", id);
                    return fallback;
                }
                Dictionary<string, string>? luosu = await LuoSuSongAsync("qq", original);
                if (luosu != null)
                {
                    logger.LogInformation("QQ音乐详情已用洛苏API兜底（{Keyword}）", original);
                    return luosu;
                }
                throw new Exception("拿不到 QQ音乐歌曲详情（" + id + "）：" + detailError.Message
                    + "\n确认 songmid 是否正确，或改用歌名搜索");
            }
            info["url"] = "https://y.qq.com/n/ryqq/songDetail/" + id;
            info["jump"] = "https://y.qq.com/n/ryqq/songDetail/" + id;
            info["format"] = "qq";
        }
        else if (platform == "kugou")
        {
            // 酷狗：列表模式搜索没有 cover，且签名端要求 url 为播放地址；
            // 统一先拿到 hash（传歌名先搜），再用 hash 走详情接口（id 参数）拿直链/封面/播放页
            if (Regex.IsMatch(id, "^[0-9A-Fa-f]{32}$") == false)
            {
                var r = await SearchFirstAsync("kugou", id);
                if (!string.IsNullOrWhiteSpace(r.id)) id = r.id;
            }
            string songName, singerName, kugouCover, playUrl;
            string pageUrl = "https://www.kugou.com/song/#hash=" + id;
            try
            {
                JsonElement dd = await SearchDataAsync("kugou", "id=" + Uri.EscapeDataString(id));
                string D(string k) => dd.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                songName = D("title");
                singerName = D("author");
                kugouCover = D("cover").Length > 0 ? D("cover").Replace("{size}", "480") : "";
                playUrl = D("url");
                if (D("link").Length > 0) pageUrl = D("link");
            }
            catch (Exception detailError)
            {
                logger.LogWarning("酷狗详情失败（hash {Id}）：{Msg}", id, detailError.Message);
                Dictionary<string, string>? luosu = await LuoSuSongAsync("kugou", original);
                if (luosu != null)
                {
                    logger.LogInformation("酷狗详情已用洛苏API兜底（{Keyword}）", original);
                    return luosu;
                }
                throw new Exception("拿不到酷狗歌曲详情（hash " + id + "）：" + detailError.Message
                    + "\n没有播放地址和封面就没法发音乐卡，确认 hash 或改用歌名搜索");
            }
            if (songName.Length == 0) songName = "酷狗音乐";
            if (singerName.Length == 0) singerName = "酷狗音乐";
            if (playUrl.Length == 0) playUrl = pageUrl;
            if (!string.IsNullOrWhiteSpace(title)) songName = title;
            info["song"] = songName;
            info["singer"] = singerName;
            info["cover"] = kugouCover;
            info["url"] = playUrl.Length > 0 ? playUrl : pageUrl;
            info["jump"] = pageUrl;
            info["format"] = "kugou";
        }
        else throw new Exception("不支持的平台：" + platform);

        if (!string.IsNullOrEmpty(title) && platform != "kugou" && platform != "qq" && platform != "bilibili") info["song"] = title;
        return info;
    }

    async Task<string> SignAsync(string platform, string id, string? title)
    {
        var info = await ResolveInfoAsync(platform, id, title);
        // 签名端所有参数必填且空值当缺失：封面为空时回落抓播放页的 og:image。
        // 免费源关掉就不抓页面，行为要跟开关一致
        if (string.IsNullOrWhiteSpace(info.GetValueOrDefault("cover")) && Configuration.EnableFreeSources)
        {
            var meta = await FetchMetaAsync(info.GetValueOrDefault("jump") ?? "");
            if (!string.IsNullOrWhiteSpace(meta.image)) info["cover"] = meta.image;
            // 抓页面也拿不到封面时，再用 iTunes 公开接口按「歌名 歌手」兜一次
            if (string.IsNullOrWhiteSpace(info.GetValueOrDefault("cover")))
            {
                try
                {
                    string keyword = ((info.GetValueOrDefault("song") ?? "") + " " + (info.GetValueOrDefault("singer") ?? "")).Trim();
                    if (keyword.Length > 0)
                    {
                        List<ArkTrackInfo> hits = ArkFreeSources.ParseItunes(
                            await GetJsonAsync(ArkFreeSources.ItunesSearchUrl(keyword), "itunes.apple.com"));
                        string cover = hits.FirstOrDefault(hit => hit.Cover.Length > 0)?.Cover ?? "";
                        if (cover.Length > 0)
                        {
                            info["cover"] = cover;
                            logger.LogInformation("封面已用 iTunes 兜底（{Keyword}）", keyword);
                        }
                    }
                }
                catch (Exception coverError)
                {
                    logger.LogWarning("iTunes 封面兜底失败：{Msg}", coverError.Message);
                }
            }
        }
        // 缺字段不再拿"未知"糊过去：签出来的是一张信息错的卡，不如明确报错让人换参数
        if (string.IsNullOrWhiteSpace(info.GetValueOrDefault("cover")))
        {
            throw new Exception("拿不到封面（详情接口没给，免费源抓页面又" +
                (Configuration.EnableFreeSources ? "失败" : "关着") + "），签名端会拒收；确认链接可访问后重试");
        }
        if (string.IsNullOrWhiteSpace(info.GetValueOrDefault("song")))
        {
            throw new Exception("详情里没有歌名，没法做卡片标题；确认 id 或直接用歌名搜索");
        }
        if (string.IsNullOrWhiteSpace(info.GetValueOrDefault("singer")))
        {
            throw new Exception("详情里没有歌手/作者，没法做卡片副标题；确认 id 或改用歌名搜索");
        }
        // 依次尝试：主站 → 备用站 → OIAPI 公共音乐卡签名。
        // 全失败才抛出去，SendArkCard 仍会接住并退到 OneBot 标准音乐段。
        List<SignNode> nodes = await MusicNodesAsync();
        if (nodes.Count == 0)
        {
            throw new Exception("音乐卡签名节点一个都没配置：先填第三方接口基地址，或填备用签名基地址");
        }
        List<string> errors = new();
        foreach (SignNode node in nodes)
        {
            string? cooling = NodeBlocked(node.Url);
            if (cooling != null)
            {
                errors.Add(node.Name + "：" + cooling);
                continue;
            }
            try
            {
                string card = node.Builtin
                    ? await SignMusicOnBuiltinAsync(node, info)
                    : await SignMusicOnNodeAsync(node, info);
                MarkSignOk(node.Url);
                return card;
            }
            catch (Exception nodeError)
            {
                MarkSignFailed(node.Url, nodeError.Message);
                errors.Add(node.Name + "：" + nodeError.Message);
                logger.LogWarning("音乐卡节点 {Node} 失败：{Msg}", node.Name, nodeError.Message);
            }
        }
        throw new Exception("所有音乐卡签名节点都失败（" + string.Join("；", errors) + "）");
    }

    async Task<string> SignMusicOnNodeAsync(SignNode node, Dictionary<string, string> info)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            Dictionary<string, string> q = new(info);
            // 每个节点用自己那份 key，避免主站 key 被带到备用站
            q.Remove("key");
            if (!string.IsNullOrWhiteSpace(node.ApiKey))
            {
                q["key"] = node.ApiKey;
            }
            string query = string.Join("&", q.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value ?? "")));
            string fullUrl = WithQuery(node.Url, query);
            string body;
            try
            {
                body = await Http.GetStringAsync(fullUrl);
            }
            catch (Exception requestError)
            {
                throw new Exception("连不上：" + requestError.Message);
            }
            using JsonDocument doc = JsonDocument.Parse(body);
            if (Ok(doc.RootElement))
            {
                return NormalizeMusicCard(doc.RootElement.GetProperty("data"), info.GetValueOrDefault("song") ?? "");
            }
            string msg = ReadMsg(doc.RootElement);
            // 缓存的接口后缀失效（服务端报 API 不存在）：清掉该节点缓存重探一次
            if (attempt == 0 && msg.Contains("不存在") && node.BaseUrl.Length > 0)
            {
                await RefitAsync(node.BaseUrl, "ark", node.ApiKey);
                node = node with { Url = JoinUrl(node.BaseUrl, SfxOf(node.BaseUrl, "ark")) };
                continue;
            }
            logger.LogWarning("音乐卡节点 {Node} 签名失败：{Msg} | {Url}", node.Name, msg.Length > 0 ? msg : "（无说明）", MaskKey(fullUrl));
            throw new Exception("接口回 code=" + ReadCode(doc.RootElement) + (msg.Length > 0 ? "：" + msg : ""));
        }
        throw new Exception("重试后仍失败");
    }

    // OIAPI 公共签名：免 key，成功码是 1，data 直接是卡片对象；格式枚举要 163 而不是 netease
    async Task<string> SignMusicOnBuiltinAsync(SignNode node, Dictionary<string, string> info)
    {
        Dictionary<string, string> q = new()
        {
            ["url"] = info.GetValueOrDefault("url") ?? "",
            ["song"] = info.GetValueOrDefault("song") ?? "",
            ["singer"] = info.GetValueOrDefault("singer") ?? "",
            ["cover"] = info.GetValueOrDefault("cover") ?? "",
            ["jump"] = info.GetValueOrDefault("jump") ?? "",
            ["format"] = BuiltinFormat(info.GetValueOrDefault("format") ?? ""),
        };
        if (!string.IsNullOrWhiteSpace(node.ApiKey))
        {
            q["key"] = node.ApiKey;
        }
        string query = string.Join("&", q.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value ?? "")));
        string fullUrl = WithQuery(node.Url, query);
        string body;
        try
        {
            body = await Http.GetStringAsync(fullUrl);
        }
        catch (Exception requestError)
        {
            throw new Exception("连不上：" + requestError.Message);
        }
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        bool ok = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("code", out JsonElement code)
            && code.ValueKind == JsonValueKind.Number
            && code.GetInt32() is 1 or 200 or 0;
        if (!ok)
        {
            throw new Exception("接口回 code=" + ReadCode(root) + "：" + ReadMsg(root));
        }
        if (!root.TryGetProperty("data", out JsonElement data) || data.ValueKind == JsonValueKind.Null)
        {
            throw new Exception("公共签名没回 data");
        }
        return NormalizeMusicCard(data, info.GetValueOrDefault("song") ?? "");
    }

    static string BuiltinFormat(string format)
    {
        string normalized = (format ?? "").Trim().ToLowerInvariant();
        return normalized == "netease" ? "163" : normalized;
    }

    // 把各家的 data（对象 / JSON 字符串 / 数组）统一成一张可直塞的卡片
    static string NormalizeMusicCard(JsonElement data, string fallbackSong)
    {
        string raw = CardJsonFrom(data);
        JsonNode? node = JsonNode.Parse(raw);
        if (node is not JsonObject)
        {
            throw new Exception("签名接口回的卡片不是 JSON 对象");
        }
        if (node["meta"] is JsonObject)
        {
            string songName = fallbackSong;
            try
            {
                string title = node["meta"]!["music"]!["title"]!.GetValue<string>();
                if (title.Length > 0)
                {
                    songName = title;
                }
            }
            catch
            {
                // 拿不到标题就用搜索/详情里的歌名
            }
            node["prompt"] = "[分享]" + songName;
            if (node["ver"] == null)
            {
                node["ver"] = "0.0.0.1";
            }
            if (node["view"] == null)
            {
                node["view"] = "music";
            }
        }
        return node.ToJsonString();
    }

    static string CardJsonFrom(JsonElement data)
    {
        return data.ValueKind switch
        {
            JsonValueKind.String => data.GetString() ?? "",
            JsonValueKind.Array when data.GetArrayLength() > 0 => CardJsonFrom(data[0]),
            JsonValueKind.Object => data.GetRawText(),
            _ => throw new Exception("签名接口回的数据不是卡片"),
        };
    }

    static int ReadCode(JsonElement root)
        => root.ValueKind == JsonValueKind.Object
           && root.TryGetProperty("code", out JsonElement code)
           && code.ValueKind == JsonValueKind.Number ? code.GetInt32() : -1;

    static string ReadMsg(JsonElement root)
    {
        string msg = GetStringField(root, "msg");
        return msg.Length > 0 ? msg : GetStringField(root, "message");
    }

    // 某个节点的接口后缀被服务端判为不存在：只清这个节点的缓存并重新探测
    async Task RefitAsync(string baseUrl, string role, string apiKey)
    {
        string norm = NormBase(baseUrl);
        if (norm.Length == 0)
        {
            return;
        }
        _sfxBackup.Remove(norm + "|" + role);
        if (norm == NormBase(Configuration.BaseUrl))
        {
            _sfx.Remove(role);
        }
        string[] cands = _cands.TryGetValue(role, out string[] known) ? known : new[] { role };
        await Fit(role, cands, ProbeOf(role), norm, apiKey);
    }

    private static ClientWebSocket? _sharedWs;
    private static readonly SemaphoreSlim _wsLock = new(1, 1);
    static string providerName = "";
    static bool providerResolved;

    async Task<(bool Success, string Message)> SendJsonAsync(long target, bool isGroup, string cardJson)
    {
        JsonObject jsonSegment = new()
        {
            ["type"] = "json",
            ["data"] = new JsonObject { ["data"] = cardJson }
        };
        JsonArray segments = new();
        segments.Add(jsonSegment);
        return await SendSegmentsAsync(target, isGroup, segments);
    }

    string ProviderLabel() => providerName.Length > 0 ? providerName : "协议端";

    // 三端都回 app_name（NapCat.Onebot / LLOneBot / SnowLuma），报错风格不同所以失败信息要带署名
    async Task ResolveProviderAsync()
    {
        if (providerResolved)
        {
            return;
        }
        providerResolved = true;
        try
        {
            (int retcode, string message, JsonElement data) = await SendActionAsync("get_version_info", new JsonObject());
            if (retcode != 0)
            {
                throw new Exception(message);
            }
            providerName = (GetStringField(data, "app_name") + " " + GetStringField(data, "name")
                + " " + GetStringField(data, "vendor")).Trim();
            logger.LogInformation("[ShareArk] 协议端：{Provider} {Version}",
                providerName.Length > 0 ? providerName : "未识别", GetStringField(data, "app_version"));
        }
        catch (Exception versionError)
        {
            logger.LogInformation("[ShareArk] 未能识别协议端：{Message}", versionError.Message);
        }
    }

    // 发一段消息。三端对不认识的消息段处理方式完全不同：
    // NapCat 抛"未知的消息类型"回 1200、SnowLuma 整条原子拒绝回 1400、
    // LLOneBot 直接丢掉该段但照发其余内容并回 retcode 0——所以成功也必须验 message_id。
    async Task<(bool Success, string Message)> SendSegmentsAsync(long target, bool isGroup, JsonArray segments)
    {
        await ResolveProviderAsync();
        string action = isGroup ? "send_group_msg" : "send_private_msg";
        string idKey = isGroup ? "group_id" : "user_id";
        JsonObject parameters = new()
        {
            [idKey] = target,
            ["message"] = segments
        };
        try
        {
            (int retcode, string message, JsonElement data) = await SendActionAsync(action, parameters);
            if (retcode != 0)
            {
                return (false, ProviderLabel() + " 返回 " + retcode + "：" + message + FailureHint(retcode));
            }
            // 三端成功都会带 message_id；回成功却没有它，说明这一段被静默丢弃了
            if (data.ValueKind != JsonValueKind.Object || data.TryGetProperty("message_id", out _) == false)
            {
                return (false, ProviderLabel() + " 回成功但没给 message_id，卡片段很可能被静默丢弃"
                    + "（LLOneBot 对不认识的消息段不报错直接丢；音乐卡片还需在 LLOneBot 里配置签名地址）");
            }
            return (true, "");
        }
        catch (Exception sendError)
        {
            return (false, sendError.Message);
        }
    }

    string FailureHint(int retcode) => retcode switch
    {
        1404 => "（" + ProviderLabel() + " 未实现该接口）",
        // 1400=SnowLuma 的 UNKNOWN_TYPE/BAD_REQUEST，1200=NapCat 的未知消息类型：都是整条被拒
        1400 or 1200 => "（" + ProviderLabel() + " 不认识这个消息段类型，整条消息已退回；卡片段用 json 三端都认）",
        _ => ""
    };

    async Task<(int Retcode, string Message, JsonElement Data)> SendActionAsync(string action, JsonObject parameters)
    {
        await _wsLock.WaitAsync();
        try
        {
            Exception? lastError = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    return await SendActionOnceAsync(action, parameters);
                }
                catch (Exception attemptError)
                {
                    lastError = attemptError;
                    _sharedWs?.Dispose();
                    _sharedWs = null;
                }
            }
            throw new Exception(action + " 失败：" + lastError!.Message);
        }
        finally
        {
            _wsLock.Release();
        }
    }

    async Task<(int Retcode, string Message, JsonElement Data)> SendActionOnceAsync(string action, JsonObject parameters)
    {
        if (_sharedWs == null || _sharedWs.State != WebSocketState.Open)
        {
            _sharedWs?.Dispose();
            ClientWebSocket connection = new();
            using CancellationTokenSource connectTimeout = new(TimeSpan.FromSeconds(10));
            await ConnectOneBotAsync(connection, connectTimeout.Token);
            _sharedWs = connection;
        }

        string echo = Guid.NewGuid().ToString();
        JsonObject payload = new()
        {
            ["action"] = action,
            ["params"] = parameters,
            ["echo"] = echo
        };
        byte[] recvBuf = new byte[64 * 1024];
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        await _sharedWs.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload.ToJsonString())),
            WebSocketMessageType.Text, true, cts.Token);

        while (cts.IsCancellationRequested == false)
        {
            MemoryStream buffer = new();
            WebSocketReceiveResult received;
            do
            {
                received = await _sharedWs.ReceiveAsync(new ArraySegment<byte>(recvBuf), cts.Token);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    throw new Exception("协议端关闭了连接");
                }
                buffer.Write(recvBuf, 0, received.Count);
            } while (received.EndOfMessage == false);

            using JsonDocument doc = JsonDocument.Parse(Encoding.UTF8.GetString(buffer.ToArray()));
            // 事件推送与别的响应都会路过，只认自己 echo 的那一条
            if (doc.RootElement.TryGetProperty("echo", out JsonElement echoElement) == false
                || echoElement.GetString() != echo)
            {
                continue;
            }
            int retcode = doc.RootElement.TryGetProperty("retcode", out JsonElement retcodeElement)
                && retcodeElement.ValueKind == JsonValueKind.Number ? retcodeElement.GetInt32() : -1;
            string message = GetStringField(doc.RootElement, "message");
            // SnowLuma 的失败包只填 wording，LLOneBot/NapCat 两个键都填
            if (message.Length == 0)
            {
                message = GetStringField(doc.RootElement, "wording");
            }
            JsonElement data = doc.RootElement.TryGetProperty("data", out JsonElement dataElement)
                ? dataElement.Clone() : JsonDocument.Parse("null").RootElement.Clone();
            return (retcode, message, data);
        }
        throw new Exception("等协议端响应超时");
    }

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
