using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Alife.Foundation;
using Alife.Function.FunctionCaller;
using Alife.Framework;

namespace Suran.ShareArk;

public partial class ShareArkModule
{
    static readonly Dictionary<string, string> FreeReferers = new() {
        ["lrclib.net"] = "https://lrclib.net/",
        ["music.163.com"] = "https://music.163.com/",
        ["itunes.apple.com"] = "https://music.apple.com/",
        ["api.microlink.io"] = "https://microlink.io/",
        ["mobilecdn.kugou.com"] = "https://www.kugou.com/"
    };
    static readonly Regex IdInUrl = new(@"(?:id=|song/|nm/)(\d{3,})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [XmlFunction(FunctionMode.OneShot)]
    [Description("取歌词（免费公开接口，不要 key）：带时间轴的优先走 LRCLIB，给了 neteaseId 或 song 是纯数字就走网易云官方接口。"
        + "duration 传秒数能大幅提高 LRCLIB 命中率；歌词全文会另存一份到本地，方便之后朗读或做字幕")]
    public async Task ArkLyric(string song, string artist = "", int duration = 0, string neteaseId = "")
    {
        if (FreeGuard())
        {
            return;
        }
        string id = neteaseId.Trim().Length > 0 ? neteaseId.Trim() : (IdInUrl.Match(song).Groups[1].Value ?? "");
        List<string> tries = new();
        try
        {
            ArkLyricText lyric;
            if (id.Length > 0)
            {
                tries.Add("网易云官方");
                lyric = ArkFreeSources.ParseNeteaseLyric(await GetJsonAsync(ArkFreeSources.NeteaseLyricUrl(id), "music.163.com"));
            }
            else if (artist.Trim().Length > 0 && duration > 0)
            {
                tries.Add("LRCLIB 精确");
                lyric = ArkFreeSources.ParseLrclib(await GetLrclibAsync(
                    ArkFreeSources.LrclibGetUrl(artist.Trim(), song.Trim(), "", duration)), false);
            }
            else
            {
                tries.Add("LRCLIB 搜索");
                string keyword = (artist.Trim() + " " + song.Trim()).Trim();
                if (keyword.Length == 0)
                {
                    throw new Exception("要给歌手+歌名，或者直接给网易云歌曲 id / 链接");
                }
                // 搜索结果是模糊匹配，必须按歌手/歌名校验，别把翻唱或同名歌的歌词贴上去
                lyric = ArkFreeSources.ParseLrclib(await GetLrclibAsync(
                    ArkFreeSources.LrclibSearchUrl(keyword)), true, artist.Trim(), song.Trim());
            }
            string file = SaveLyric(string.Concat((artist.Trim() + "-" + song.Trim()).Select(c => char.IsLetterOrDigit(c) ? c : '_')), lyric);
            interactor.Poke(string.Join("\n", new[] {
                "歌词来源 " + lyric.Source + "｜" + (lyric.TimeSynced ? "带时间轴" : "纯文本") + "｜" + lyric.Lines + " 行",
                file.Length > 0 ? "全文已存 " + file : "全文没写盘（写入失败，只回前若干行）",
                lyric.Preview(14)
            }));
        }
        catch (Exception e)
        {
            interactor.Poke("❌ 取歌词失败（试过：" + string.Join("、", tries) + "）：" + e.Message
                + "\n换写法再来一次：LRCLIB 认「歌手 歌名」，网易云要歌曲 id（https://music.163.com/song?id=xxx 里那串数字）");
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("找封面与试听地址（iTunes 公开接口，免 key、免代理）。给卡片补图、或想知道这首歌多久、有没有 30 秒试听时用")]
    public async Task ArkCover(string keyword, int take = 4)
    {
        if (FreeGuard())
        {
            return;
        }
        try
        {
            List<ArkTrackInfo> hits = ArkFreeSources.ParseItunes(await GetJsonAsync(ArkFreeSources.ItunesSearchUrl(keyword), "itunes.apple.com"));
            if (hits.Count == 0)
            {
                interactor.Poke("iTunes 没搜到「" + keyword + "」。iTunes 的中文曲库不全，换英文歌名或「歌手 歌名」再试；"
                    + "国内平台的封面还是走各自的详情接口更准");
                return;
            }
            interactor.Poke("封面/试听候选（" + hits.Count + " 条，取前 " + Math.Min(take, hits.Count) + "）：\n"
                + string.Join("\n", hits.Take(Math.Clamp(take, 1, 10)).Select((hit, i) => (i + 1) + ". " + hit.Describe())));
        }
        catch (Exception e)
        {
            interactor.Poke("❌ 找封面失败（试过：iTunes）：" + e.Message
                + "\n（iTunes 之外的国际源本机经代理时好时坏，没放进兜底链，免得把失败当常态；国内平台的封面走各自详情接口更准）");
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("给任意链接抓分享卡素材：自己下载页面解析 og/twitter/itemprop meta（不经过第三方）。"
        + "拿不到标题时，若配置里开了 microlink 才走它兜底（那会把链接交给外部）")]
    public async Task ArkLinkMeta(string url)
    {
        if (FreeGuard())
        {
            return;
        }
        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri parsed) == false)
        {
            throw new Exception("给个完整链接（要带 https://）");
        }
        try
        {
            ArkLinkInfo meta = ArkFreeSources.ParseOpenGraph(await GetHtmlAsync(url.Trim()), url.Trim());
            if (meta.HasCard || !Configuration.EnableMicrolink)
            {
                interactor.Poke(meta.Describe() + "\n拿着这些字段就能调 SendShareCard；标题为空时得自己写一个，别拿域名顶");
                return;
            }
            ArkLinkInfo viaThird = ArkFreeSources.ParseMicrolink(await GetJsonAsync(ArkFreeSources.MicrolinkUrl(url.Trim()), "api.microlink.io"), url.Trim());
            interactor.Poke(viaThird.Describe() + "\n（自抓没拿到标题，用了 microlink 兜底——这条链接已经交给外部服务解析过）");
        }
        catch (Exception e)
        {
            interactor.Poke("❌ 抓链接素材失败：" + e.Message
                + "\n有些站（B站视频页这类）没有标准 og 标签，改用 ArkCover/官方详情接口拿图，或让主人自己给标题");
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("体检免费公开接口：LRCLIB 歌词（精确与搜索两种）、网易云官方歌词、iTunes、酷狗 v3、自抓 og 各打一次，逐项报可用性与原因")]
    public async Task ArkFreeProbe()
    {
        // 六项互不依赖，并发打；串行时一个国际站超时会把整份体检拖到几十秒
        Task<string>[] probes = new[]
        {
            TryJson(() => ArkFreeSources.LrclibSearchUrl("晴天 周杰伦"), "lrclib.net",
                root => ArkFreeSources.ParseLrclib(root, true, "周杰伦", "晴天").Lines + " 行"),
            TryJson(() => ArkFreeSources.LrclibGetUrl("周杰伦", "晴天", "叶惠美", 269), "lrclib.net",
                root => ArkFreeSources.ParseLrclib(root, false).Lines + " 行"),
            TryJson(() => ArkFreeSources.NeteaseLyricUrl("186013"), "music.163.com",
                root => ArkFreeSources.ParseNeteaseLyric(root).Lines + " 行"),
            TryJson(() => ArkFreeSources.ItunesSearchUrl("周杰伦 晴天"), "itunes.apple.com",
                root => { List<ArkTrackInfo> list = ArkFreeSources.ParseItunes(root); return list.Count > 0 ? list[0].Describe() : "没结果"; }),
            TryJson(() => "https://mobilecdn.kugou.com/api/v3/search/song?format=json&pagesize=3&keyword=" + Uri.EscapeDataString("晴天"), "mobilecdn.kugou.com",
                root => root.TryGetProperty("data", out JsonElement data) && data.TryGetProperty("info", out JsonElement info) ? info.GetArrayLength() + " 条" : "结构不对"),
            TryHtml()
        };
        await Task.WhenAll(probes);
        List<string> lines = new() { "免费公开接口体检（都不需要 key）：" };
        lines.Add("· LRCLIB 歌词搜索：" + probes[0].Result);
        lines.Add("· LRCLIB 精确取词：" + probes[1].Result);
        lines.Add("· 网易云官方歌词：" + probes[2].Result);
        lines.Add("· iTunes 搜索：" + probes[3].Result);
        lines.Add("· 酷狗 v3（本插件容灾链里的一环）：" + probes[4].Result);
        lines.Add(Configuration.EnableMicrolink ? "· microlink：已允许兜底（会把查询的链接交给外部服务）" : "· microlink：默认不用（开了才会把链接交给外部解析）");
        lines.Add("自抓 og：" + probes[5].Result);
        interactor.Poke(string.Join("\n", lines));
    }

    bool FreeGuard() => Gate(Configuration.EnableFreeSources, "免费公开接口");
    static readonly ConcurrentDictionary<string, (DateTime At, string Body)> FreeBodyCache = new();
    const int FreeBodyCacheMax = 50;

    async Task<JsonElement> GetJsonAsync(string url, string host)
    {
        return ParseJsonBody(await GetStringAsync(url, host));
    }

    /// <summary>歌词查询会重复打同一组接口（同一首歌反复点），缓存 30 分钟；只缓存成功响应</summary>
    async Task<JsonElement> GetLrclibAsync(string url)
    {
        TimeSpan ttl = TimeSpan.FromMinutes(30);
        if (FreeBodyCache.TryGetValue(url, out (DateTime At, string Body) hit) && DateTime.Now - hit.At < ttl)
        {
            return ParseJsonBody(hit.Body);
        }
        string body = await GetStringAsync(url, "lrclib.net");
        if (FreeBodyCache.Count >= FreeBodyCacheMax)
        {
            // 简单清一批最老的，别让缓存无限长
            foreach (string stale in FreeBodyCache.Keys.Take(FreeBodyCache.Count - FreeBodyCacheMax + 1).ToList())
            {
                FreeBodyCache.TryRemove(stale, out _);
            }
        }
        FreeBodyCache[url] = (DateTime.Now, body);
        return ParseJsonBody(body);
    }

    static JsonElement ParseJsonBody(string body)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            string sample = ArkFreeSources.SafeText(body, 80);
            throw new Exception("返回不是有效 JSON（前 80 字：" + sample + "），可能被风控页或限流拦截");
        }
    }

    async Task<string> GetHtmlAsync(string url)
    {
        string host = new Uri(url).Host.ToLowerInvariant();
        string key = FreeReferers.Keys.FirstOrDefault(known => host.EndsWith(known, StringComparison.OrdinalIgnoreCase));
        return await GetStringAsync(url, key ?? host);
    }
    async Task<string> GetStringAsync(string url, string host)
    {
        TimeSpan timeout = TimeSpan.FromSeconds(Math.Clamp(Configuration.FreeSourceTimeoutSeconds, 3, 90));
        string referer = FreeReferers.TryGetValue(host, out string known) ? known : null;
        // 统一走 SafeFetchAsync：4MB 上限、逐跳校验重定向、拒绝二进制响应。
        // 按响应头/页面 meta 的 charset 解码：有些站回 text/html; charset=UTF-8（大写），
        // .NET 认不下；GBK 的站一律按 UTF-8 解会整页乱码
        FetchResult result = await SafeFetchAsync(url, referer, timeout, false);
        return DecodeBody(result.Bytes, result.ContentType);
    }
    async Task<string> TryJson(Func<string> url, string host, Func<JsonElement, string> read)
    {
        try
        {
            return read(await GetJsonAsync(url(), host));
        }
        catch (Exception e)
        {
            return "不通：" + e.Message;
        }
    }
    async Task<string> TryHtml()
    {
        try
        {
            ArkLinkInfo meta = ArkFreeSources.ParseOpenGraph(await GetHtmlAsync("https://music.163.com/song?id=186013"), "https://music.163.com/song?id=186013");
            return meta.HasCard ? "通（标题「" + meta.Title + "」，封面 " + (meta.Image.Length > 0 ? "有" : "无") + "）" : "通了但没解析出标题";
        }
        catch (Exception e)
        {
            return "不通：" + e.Message;
        }
    }
    string SaveLyric(string name, ArkLyricText lyric)
    {
        try
        {
            string folder = Path.Combine(AlifePath.StorageFolderPath, "ShareArk", "lyrics");
            Directory.CreateDirectory(folder);
            string safe = string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')).Trim('_');
            if (safe.Length == 0)
            {
                safe = "lyric";
            }
            // 歌名+歌手不设上限时会撞文件名长度限制；留 80 字足够区分
            if (safe.Length > 80)
            {
                safe = safe[..80];
            }
            string path = Path.Combine(folder, safe + ".lrc");
            // 先写临时文件再原子替换：写到一半断电/崩溃不会把上一份好歌词毁掉
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, lyric.Lyrics, Encoding.UTF8);
            File.Move(temp, path, true);
            return path;
        }
        catch (Exception)
        {
            return "";
        }
    }
}
