using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alife.Function.FunctionCaller;
using Microsoft.Extensions.Logging;

namespace Suran.ShareArk;

/// <summary>
/// 「更多平台卡」：把抖音/小红书/微博/知乎/Steam/GitHub 这类链接，按平台抓出标题、简介、封面，
/// 再交给通用的分享卡签名链路发出。Steam 与 GitHub 走官方公开接口（信息最准），
/// 其余平台按页面 meta 解析，抓不到就退回现有的 FetchMetaAsync。
/// B 站不走这里：视频卡用 SearchBili + SendArkCard，解析用 BiliParse。
/// </summary>
public partial class ShareArkModule
{
    [XmlFunction(FunctionMode.OneShot)]
    [Description("全平台分享卡：音乐（网易云/QQ音乐/酷狗/酷我/咪咕/汽水/Spotify/Apple Music/SoundCloud/Bandcamp）、"
        + "视频（B站/抖音/快手/西瓜/腾讯视频/爱奇艺/优酷/YouTube/Vimeo/TikTok）、社交（小红书/微博/知乎/微信/Twitter/X/Instagram）、"
        + "Steam/GitHub 及任意其他网页，自动按平台抓标题、简介、封面后发卡。音乐与B站视频直接走 ark 卡，其余走分享卡。")]
    public async Task SendPlatformCard(
        [Description("链接（要带 https://）")] string url,
        [Description("目标群号或QQ号")] long? targetId = null,
        [Description("group或private，默认group")] string? type = null,
        [Description("标题，可留空自动抓")] string? title = null,
        [Description("预览图URL，可留空自动抓")] string? preview = null)
    {
        if (Gate(Configuration.EnableSendShareCard, "发通用分享卡"))
        {
            return;
        }
        string link = (url ?? "").Trim();
        if (Uri.TryCreate(link, UriKind.Absolute, out Uri? parsed) == false)
        {
            throw new Exception("给个完整链接（要带 https://）");
        }
        string host = parsed.Host.ToLowerInvariant();

        // 音乐与 B 站视频有专用 ark 卡，先试这条路
        if (await TryArkRouteAsync(link, host, targetId, type))
        {
            return;
        }

        string platform = PlatformName(host);
        if (platform.Length == 0)
        {
            platform = "通用网页";
        }

        (string Title, string Desc, string Image) meta;
        try
        {
            meta = await FetchPlatformMetaAsync(link, host);
        }
        catch (Exception ex)
        {
            logger.LogWarning("平台卡抓取失败（{Platform}）：{Message}", platform, ex.Message);
            meta = ("", "", "");
        }

        string useTitle = string.IsNullOrWhiteSpace(title) ? meta.Title : title.Trim();
        string usePreview = string.IsNullOrWhiteSpace(preview) ? meta.Image : preview.Trim();
        string useDesc = meta.Desc;
        if (useTitle.Length == 0)
        {
            throw new Exception(platform + " 页面没抓到标题：请用 title 参数自己写一个，别拿域名顶");
        }
        if (useDesc.Length == 0)
        {
            useDesc = useTitle;
        }

        logger.LogInformation("平台卡：{Platform}｜{Title}", platform, useTitle);
        await SendShareCardCoreAsync(link, useTitle, useDesc, usePreview, "[分享]" + useTitle, targetId, type, allowBili: true);
    }

    /// <summary>音乐平台与B站视频直接走 ark 卡：链接里一般就有 ID，不用再抓页面。</summary>
    async Task<bool> TryArkRouteAsync(string url, string host, long? targetId, string? type)
    {
        if (targetId == null)
        {
            return false;   // 没目标就交给下面报错，让调用方补参数
        }
        string id = "";
        string platform = "";
        if (host.Contains("music.163.com"))
        {
            Match match = Regex.Match(url, @"[?&#]id=(\d+)");
            if (match.Success == false)
            {
                match = Regex.Match(url, @"/song/(\d+)");
            }
            platform = "163";
            id = match.Success ? match.Groups[1].Value : "";
        }
        else if (host.Contains("y.qq.com"))
        {
            Match match = Regex.Match(url, @"songDetail/([0-9A-Za-z]+)");
            platform = "qq";
            id = match.Success ? match.Groups[1].Value : "";
        }
        else if (host.Contains("kugou.com"))
        {
            Match match = Regex.Match(url, @"[?&#]hash=([0-9A-Fa-f]+)");
            if (match.Success == false)
            {
                match = Regex.Match(url, @"/mixsong/([0-9A-Za-z]+)");
            }
            platform = "kugou";
            id = match.Success ? match.Groups[1].Value : "";
        }
        else if (host.Contains("bilibili.com") || host.Contains("b23.tv"))
        {
            Match match = Regex.Match(url, @"(BV[0-9A-Za-z]{10})");
            platform = "bilibili";
            id = match.Success ? match.Groups[1].Value : "";
        }

        if (id.Length == 0)
        {
            return false;
        }
        logger.LogInformation("平台卡走专用 ark：{Platform}｜{Id}", platform, id);
        await SendArkCard(platform, id, targetId, type);
        return true;
    }

    /// <summary>按域名判断平台（只用于日志与提示；认不出就当通用网页）。</summary>
    static string PlatformName(string host)
    {
        if (host.Contains("steampowered.com") || host.Contains("s.team")) return "Steam";
        if (host.Contains("github.com")) return "GitHub";
        if (host.Contains("douyin.com") || host.Contains("iesdouyin.com")) return "抖音";
        if (host.Contains("xiaohongshu.com") || host.Contains("xhslink.com")) return "小红书";
        if (host.Contains("weibo.com") || host.Contains("weibo.cn")) return "微博";
        if (host.Contains("zhihu.com")) return "知乎";
        if (host.Contains("mp.weixin.qq.com")) return "微信文章";
        if (host.Contains("bilibili.com") || host.Contains("b23.tv")) return "B站";
        if (host.Contains("youtube.com") || host.Contains("youtu.be"))
            return host.Contains("music.youtube.com") ? "YouTube Music" : "YouTube";
        if (host.Contains("vimeo.com")) return "Vimeo";
        if (host.Contains("twitter.com") || host.Contains("x.com")) return "Twitter/X";
        if (host.Contains("instagram.com")) return "Instagram";
        if (host.Contains("tiktok.com")) return "TikTok";
        if (host.Contains("kuaishou.com")) return "快手";
        if (host.Contains("ixigua.com")) return "西瓜视频";
        if (host.Contains("v.qq.com")) return "腾讯视频";
        if (host.Contains("iqiyi.com")) return "爱奇艺";
        if (host.Contains("youku.com")) return "优酷";
        if (host.Contains("music.163.com")) return "网易云音乐";
        if (host.Contains("y.qq.com")) return "QQ音乐";
        if (host.Contains("kugou.com")) return "酷狗音乐";
        if (host.Contains("kuwo.cn")) return "酷我音乐";
        if (host.Contains("music.migu.cn")) return "咪咕音乐";
        if (host.Contains("qishui.douyin.com")) return "汽水音乐";
        if (host.Contains("open.spotify.com")) return "Spotify";
        if (host.Contains("music.apple.com")) return "Apple Music";
        if (host.Contains("soundcloud.com")) return "SoundCloud";
        if (host.Contains("bandcamp.com")) return "Bandcamp";
        return "";
    }

    async Task<(string Title, string Desc, string Image)> FetchPlatformMetaAsync(string url, string host)
    {
        string platform = PlatformName(host);
        if (platform == "Steam")
        {
            (string Title, string Desc, string Image)? steam = await SteamMetaAsync(url);
            if (steam != null)
            {
                return steam.Value;
            }
        }
        if (platform == "GitHub")
        {
            (string Title, string Desc, string Image)? github = await GitHubMetaAsync(url);
            if (github != null)
            {
                return github.Value;
            }
        }
        if (platform == "Apple Music")
        {
            (string Title, string Desc, string Image)? apple = await AppleMusicMetaAsync(url);
            if (apple != null)
            {
                return apple.Value;
            }
        }
        if (platform is "YouTube" or "YouTube Music" or "Vimeo" or "Spotify" or "SoundCloud")
        {
            (string Title, string Desc, string Image)? video = await OEmbedMetaAsync(url, host);
            if (video != null)
            {
                return video.Value;
            }
        }

        // 其余平台按页面 meta 解析；短视频站把描述塞在脚本里，再补一层正则
        string html = await GetHtmlAsync(url);
        ArkLinkInfo info = ArkFreeSources.ParseOpenGraph(html, url);
        string title = info.Title;
        string desc = info.Description;
        string image = info.Image;
        if (title.Length == 0 && (platform is "抖音" or "TikTok" or "小红书" or "微博" or "快手" or "西瓜视频"))
        {
            Match titleMatch = Regex.Match(html, "\"(?:desc|caption|title)\"\\s*:\\s*\"([^\"]{2,300})\"");
            if (titleMatch.Success)
            {
                title = Regex.Unescape(titleMatch.Groups[1].Value);
            }
            Match coverMatch = Regex.Match(html, "\"(?:cover|coverUrl|image)\"\\s*:\\s*\"([^\"]+)\"");
            if (coverMatch.Success && image.Length == 0)
            {
                image = coverMatch.Groups[1].Value.Replace("\\/", "/", StringComparison.Ordinal);
            }
        }
        if (title.Length == 0)
        {
            // 最后一层：通用抓取（含 microlink 开关逻辑）
            PageMeta fallback = await FetchMetaAsync(url);
            title = fallback.title;
            desc = desc.Length > 0 ? desc : fallback.desc;
            image = image.Length > 0 ? image : fallback.image;
        }
        return (title, desc, image);
    }

    /// <summary>YouTube / Vimeo / Spotify / SoundCloud 的公开 oEmbed：标题、作者、封面一次拿全，不需要 key。</summary>
    async Task<(string Title, string Desc, string Image)?> OEmbedMetaAsync(string url, string host)
    {
        string endpoint;
        if (host.Contains("spotify.com"))
        {
            endpoint = "https://open.spotify.com/oembed?url=";
        }
        else if (host.Contains("soundcloud.com"))
        {
            endpoint = "https://soundcloud.com/oembed?format=json&url=";
        }
        else if (host.Contains("vimeo.com"))
        {
            endpoint = "https://vimeo.com/api/oembed.json?url=";
        }
        else
        {
            endpoint = "https://www.youtube.com/oembed?format=json&url=";
        }
        JsonElement root = await GetJsonAsync(endpoint + Uri.EscapeDataString(url), "oembed");
        string title = Text(root, "title");
        if (title.Length == 0)
        {
            return null;
        }
        string author = Text(root, "author_name");
        string image = Text(root, "thumbnail_url");
        if (image.Length == 0)
        {
            image = Text(root, "thumbnail_url_high");
        }
        return (title, author.Length > 0 ? "作者 " + author : title, image);
    }

    /// <summary>Apple Music / iTunes：链接里带曲目 id 时查 lookup，拿歌名、歌手、专辑和高清封面。</summary>
    async Task<(string Title, string Desc, string Image)?> AppleMusicMetaAsync(string url)
    {
        Match match = Regex.Match(url, @"(?:[?&]i=|\/)(\d{6,})");
        if (match.Success == false)
        {
            return null;
        }
        string api = "https://itunes.apple.com/lookup?id=" + match.Groups[1].Value + "&country=cn";
        JsonElement root = await GetJsonAsync(api, "itunes.apple.com");
        if (root.TryGetProperty("resultCount", out JsonElement count) == false
            || count.ValueKind != JsonValueKind.Number
            || count.GetInt32() == 0
            || root.TryGetProperty("results", out JsonElement results) == false
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            return null;
        }
        JsonElement item = results[0];
        string title = Text(item, "trackName");
        if (title.Length == 0)
        {
            title = Text(item, "collectionName");
        }
        if (title.Length == 0)
        {
            return null;
        }
        string artist = Text(item, "artistName");
        string album = Text(item, "collectionName");
        string image = Text(item, "artworkUrl100").Replace("100x100", "600x600", StringComparison.Ordinal);
        string desc = artist + (album.Length > 0 && album != title ? "｜" + album : "");
        return (title, desc.Length > 0 ? desc : title, image);
    }

    /// <summary>Steam 官方 appdetails：名字、简介、头图、价格都比抓商店页可靠。</summary>
    async Task<(string Title, string Desc, string Image)?> SteamMetaAsync(string url)
    {
        Match match = Regex.Match(url, @"store\.steampowered\.com/app/(\d+)");
        if (match.Success == false)
        {
            match = Regex.Match(url, @"s\.team/a/(\d+)");
        }
        if (match.Success == false)
        {
            return null;
        }
        string appId = match.Groups[1].Value;
        string api = "https://store.steampowered.com/api/appdetails?appids=" + appId + "&cc=cn&l=schinese";
        JsonElement root = await GetJsonAsync(api, "store.steampowered.com");
        if (root.TryGetProperty(appId, out JsonElement entry) == false
            || entry.TryGetProperty("success", out JsonElement ok) == false
            || ok.ValueKind != JsonValueKind.True
            || entry.TryGetProperty("data", out JsonElement data) == false)
        {
            return null;
        }
        string name = Text(data, "name");
        string shortDesc = Text(data, "short_description");
        string header = Text(data, "header_image");
        string price = "";
        if (data.TryGetProperty("price_overview", out JsonElement priceInfo) && priceInfo.ValueKind == JsonValueKind.Object)
        {
            price = Text(priceInfo, "final_formatted");
        }
        else if (data.TryGetProperty("is_free", out JsonElement free) && free.ValueKind == JsonValueKind.True)
        {
            price = "免费";
        }
        string desc = (price.Length > 0 ? price + "｜" : "") + shortDesc;
        return (name, Trim(desc, 200), header);
    }

    /// <summary>GitHub 公开 API：仓库全名、描述、star 数；封面用 GitHub 的 OG 图。</summary>
    async Task<(string Title, string Desc, string Image)?> GitHubMetaAsync(string url)
    {
        Match match = Regex.Match(url, @"github\.com/([^/]+)/([^/?#]+)");
        if (match.Success == false)
        {
            return null;
        }
        string owner = match.Groups[1].Value;
        string repo = match.Groups[2].Value;
        JsonElement root = await GetJsonAsync("https://api.github.com/repos/" + owner + "/" + repo, "api.github.com");
        string fullName = Text(root, "full_name");
        if (fullName.Length == 0)
        {
            return null;
        }
        string desc = Text(root, "description");
        long stars = 0;
        if (root.TryGetProperty("stargazers_count", out JsonElement starNode) && starNode.ValueKind == JsonValueKind.Number)
        {
            stars = starNode.GetInt64();
        }
        string language = Text(root, "language");
        string meta = "★ " + stars.ToString(CultureInfo.InvariantCulture)
            + (language.Length > 0 ? "｜" + language : "")
            + (desc.Length > 0 ? "｜" + desc : "");
        return (fullName, Trim(meta, 200), "https://opengraph.githubassets.com/1/" + owner + "/" + repo);
    }

    static string Text(JsonElement node, string name)
    {
        return node.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? "").Trim()
            : "";
    }

    static string Trim(string text, int max)
    {
        string flat = Regex.Replace((text ?? "").Replace('\r', ' ').Replace('\n', ' '), "\\s{2,}", " ").Trim();
        return flat.Length > max ? flat[..max] + "…" : flat;
    }
}
