using System;
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
                lyric = ArkFreeSources.ParseLrclib(await GetJsonAsync(
                    ArkFreeSources.LrclibGetUrl(artist.Trim(), song.Trim(), "", duration), "lrclib.net"), false);
            }
            else
            {
                tries.Add("LRCLIB 搜索");
                string keyword = (artist.Trim() + " " + song.Trim()).Trim();
                if (keyword.Length == 0)
                {
                    throw new Exception("要给歌手+歌名，或者直接给网易云歌曲 id / 链接");
                }
                lyric = ArkFreeSources.ParseLrclib(await GetJsonAsync(ArkFreeSources.LrclibSearchUrl(keyword), "lrclib.net"), true);
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
        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? parsed) == false)
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
        List<string> lines = new() { "免费公开接口体检（都不需要 key）：" };
        lines.Add("· LRCLIB 歌词搜索：" + await TryJson(() => ArkFreeSources.LrclibSearchUrl("晴天 周杰伦"), "lrclib.net",
            root => ArkFreeSources.ParseLrclib(root, true).Lines + " 行"));
        lines.Add("· LRCLIB 精确取词：" + await TryJson(() => ArkFreeSources.LrclibGetUrl("周杰伦", "晴天", "叶惠美", 269), "lrclib.net",
            root => ArkFreeSources.ParseLrclib(root, false).Lines + " 行"));
        lines.Add("· 网易云官方歌词：" + await TryJson(() => ArkFreeSources.NeteaseLyricUrl("186013"), "music.163.com",
            root => ArkFreeSources.ParseNeteaseLyric(root).Lines + " 行"));
                lines.Add("· iTunes 搜索：" + await TryJson(() => ArkFreeSources.ItunesSearchUrl("周杰伦 晴天"), "itunes.apple.com",
            root => { List<ArkTrackInfo> list = ArkFreeSources.ParseItunes(root); return list.Count > 0 ? list[0].Describe() : "没结果"; }));
        lines.Add("· 酷狗 v3（本插件容灾链里的一环）：" + await TryJson(() => "https://mobilecdn.kugou.com/api/v3/search/song?format=json&pagesize=3&keyword=" + Uri.EscapeDataString("晴天"), "mobilecdn.kugou.com",
            root => root.TryGetProperty("data", out JsonElement data) && data.TryGetProperty("info", out JsonElement info) ? info.GetArrayLength() + " 条" : "结构不对"));
        lines.Add(Configuration.EnableMicrolink ? "· microlink：已允许兜底（会把查询的链接交给外部服务）" : "· microlink：默认不用（开了才会把链接交给外部解析）");
        lines.Add("自抓 og：" + await TryHtml());
        interactor.Poke(string.Join("\n", lines));
    }

    bool FreeGuard() => Gate(Configuration.EnableFreeSources, "免费公开接口");
    async Task<JsonElement> GetJsonAsync(string url, string host)
    {
        string body = await GetStringAsync(url, host);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }
    async Task<string> GetHtmlAsync(string url)
    {
        string host = new Uri(url).Host.ToLowerInvariant();
        string? key = FreeReferers.Keys.FirstOrDefault(known => host.EndsWith(known, StringComparison.OrdinalIgnoreCase));
        return await GetStringAsync(url, key ?? host);
    }
    async Task<string> GetStringAsync(string url, string host)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        if (FreeReferers.TryGetValue(host, out string? referer))
        {
            request.Headers.TryAddWithoutValidation("Referer", referer);
        }
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(Math.Clamp(Configuration.FreeSourceTimeoutSeconds, 3, 90)));
        using HttpResponseMessage response = await http.SendAsync(request, timeout.Token);
        // 有些站（实测网易云）回 Content-Type: text/html; charset=UTF-8，.NET 认不下这个大写 charset，
        // ReadAsStringAsync 会直接抛异常；按字节读回来自己解码就不受它摆布
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
        string body = Encoding.UTF8.GetString(bytes);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("HTTP " + (int)response.StatusCode + "（" + new Uri(url).Host + "）");
        }
        return body;
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
            string path = Path.Combine(folder, safe + ".lrc");
            File.WriteAllText(path, lyric.Lyrics, Encoding.UTF8);
            return path;
        }
        catch (Exception)
        {
            return "";
        }
    }
}
