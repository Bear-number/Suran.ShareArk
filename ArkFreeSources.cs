using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Suran.ShareArk;

public sealed record ArkLyricText(string Source, string Lyrics, bool TimeSynced, int Lines)
{
    public string Preview(int take)
    {
        string[] rows = Lyrics.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("\n", rows.Take(Math.Clamp(take, 1, 80)));
    }
}

public sealed record ArkTrackInfo(string Source, string Title, string Artist, string Album, string Cover, string PreviewUrl, int DurationMs, string PageUrl)
{
    public string Describe()
    {
        List<string> parts = new() { Title };
        if (Artist.Length > 0)
        {
            parts.Add(Artist);
        }
        if (Album.Length > 0)
        {
            parts.Add("专辑 " + Album);
        }
        if (DurationMs > 0)
        {
            parts.Add(TimeSpan.FromMilliseconds(DurationMs).ToString(@"mm\:ss", CultureInfo.InvariantCulture));
        }
        if (Cover.Length > 0)
        {
            parts.Add("封面 " + Cover);
        }
        if (PreviewUrl.Length > 0)
        {
            parts.Add("试听 " + PreviewUrl);
        }
        return Source + "：" + string.Join(" ｜ ", parts);
    }
}

public sealed record ArkLinkInfo(string Source, string Title, string Description, string Image, string Site, string FinalUrl)
{
    public bool HasCard => Title.Length > 0;
    public string Describe()
    {
        return $"来源 {Source}｜标题 {Title}｜站点 {Site}\n简介 {Description}\n封面 {Image}\n跳转 {FinalUrl}";
    }
}

/// <summary>
/// 免费公开接口层（都不需要注册、不要 key）。2026-10-25 本机实测过：
/// LRCLIB、网易云歌词、iTunes 直连可用；酷狗 v3 两条路都不通（.NET 里是 TLS 握手失败）。
/// URL 拼装与响应解析全是纯函数，方便离线打探针。
/// </summary>
public static class ArkFreeSources
{
    public static string LrclibSearchUrl(string keyword) => "https://lrclib.net/api/search?q=" + Uri.EscapeDataString(keyword);
    /// <summary>duration 必须是整数秒，留空传 0：实测传空字符串会回 400「cannot parse float」</summary>
    public static string LrclibGetUrl(string artist, string track, string album, int duration)
    {
        return "https://lrclib.net/api/get?artist_name=" + Uri.EscapeDataString(artist)
            + "&track_name=" + Uri.EscapeDataString(track)
            + "&album_name=" + Uri.EscapeDataString(album)
            + "&duration=" + Math.Max(0, duration).ToString(CultureInfo.InvariantCulture);
    }
    /// <summary>实测：只给 lv=1 回的是空歌词，必须 os=pc&lv=-1&kv=-1&tv=-1 才吐词</summary>
    public static string NeteaseLyricUrl(string id)
    {
        return "https://music.163.com/api/song/lyric?os=pc&id=" + Uri.EscapeDataString(id) + "&lv=-1&kv=-1&tv=-1";
    }
    /// <summary>实测：加 entity=song 会让中国区返回空数组，只留 media=music 才有结果</summary>
    public static string ItunesSearchUrl(string term, string country = "CN")
    {
        return "https://itunes.apple.com/search?media=music&limit=5&term=" + Uri.EscapeDataString(term)
            + "&country=" + Uri.EscapeDataString(country.Length > 0 ? country : "CN");
    }
    /// <summary>microlink 会替你去访问那个链接，等于把你查的地址交给第三方，所以默认不用它</summary>
    public static string MicrolinkUrl(string url) => "https://api.microlink.io/?url=" + Uri.EscapeDataString(url);
    /// <summary>iTunes 给的是 100x100，它的 CDN 支持改尺寸；卡片封面要大的</summary>
    public static string Artwork(string artworkUrl100, int size = 600)
    {
        if (artworkUrl100.Length == 0)
        {
            return "";
        }
        return artworkUrl100.Replace("100x100bb.jpg", $"{size}x{size}bb.jpg", StringComparison.Ordinal)
            .Replace("100x100", $"{size}x{size}", StringComparison.Ordinal);
    }

    public static ArkLyricText ParseLrclib(JsonElement root, bool isList)
    {
        JsonElement hit = root;
        if (isList)
        {
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                throw new Exception("LRCLIB 没搜到这首歌（换“歌手 歌名”这种写法，或直接传网易云 id 走官方歌词）");
            }
            List<JsonElement> candidates = root.EnumerateArray().Where(e => Synced(e).Length > 0).ToList();
            hit = candidates.Count > 0 ? candidates[0] : root[0];
        }
        string synced = Synced(hit);
        string plain = Text(hit, "plainLyrics");
        string lyrics = synced.Length > 0 ? synced : plain;
        if (lyrics.Trim().Length == 0)
        {
            throw new Exception("LRCLIB 返回了条目但没有歌词正文");
        }
        return new ArkLyricText("LRCLIB", lyrics, synced.Length > 0, lyrics.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }
    static string Synced(JsonElement node) => Text(node, "syncedLyrics");
    static string Text(JsonElement node, string name)
    {
        return node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";
    }

    public static ArkLyricText ParseNeteaseLyric(JsonElement root)
    {
        string lrc = root.TryGetProperty("lrc", out JsonElement lv) ? Text(lv, "lyric") : "";
        if (lrc.Trim().Length == 0)
        {
            throw new Exception("网易云歌词接口没给正文（这首歌可能没歌词、需要 VIP，或 id 不对）");
        }
        return new ArkLyricText("网易云", lrc, lrc.Contains("[0"), lrc.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }
    public static string NeteaseLyricId(JsonElement root)
    {
        return root.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.Number
            ? id.GetRawText() : "";
    }

    public static List<ArkTrackInfo> ParseItunes(JsonElement root)
    {
        if (root.TryGetProperty("results", out JsonElement results) == false || results.ValueKind != JsonValueKind.Array)
        {
            return new List<ArkTrackInfo>();
        }
        List<ArkTrackInfo> list = new();
        foreach (JsonElement item in results.EnumerateArray())
        {
            int ms = item.TryGetProperty("trackTimeMillis", out JsonElement t) && t.TryGetInt32(out int parsed) ? parsed : 0;
            list.Add(new ArkTrackInfo("iTunes", Text(item, "trackName"), Text(item, "artistName"), Text(item, "collectionName"),
                Artwork(Text(item, "artworkUrl100")), Text(item, "previewUrl"), ms, Text(item, "permalinkUrl")));
        }
        return list;
    }
    static double Number(JsonElement node, string name)
    {
        return node.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double parsed) ? parsed : 0;
    }

    static readonly Regex[] TitlePatterns = new[] {
        new Regex("<meta[^>]+property=[\"']og:title[\"'][^>]+content=[\"']([^\"']{1,160})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex("<meta[^>]+name=[\"']twitter:title[\"'][^>]+content=[\"']([^\"']{1,160})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex("<meta[^>]+itemprop=[\"']name[\"'][^>]+content=[\"']([^\"']{1,160})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex("<meta[^>]+content=[\"']([^\"']{1,160})[\"'][^>]+property=[\"']og:title", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex("<title[^>]*>([^<]{1,160})</title>", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };
    static readonly Regex[] ImagePatterns = new[] {
        new Regex("<meta[^>]+property=[\"']og:image[\"'][^>]+content=[\"']([^\"']{1,300})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex("<meta[^>]+name=[\"']twitter:image[\"'][^>]+content=[\"']([^\"']{1,300})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex("<meta[^>]+itemprop=[\"']image[\"'][^>]+content=[\"']([^\"']{1,300})", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };
    static readonly Regex[] DescPatterns = new[] {
        new Regex("<meta[^>]+property=[\"']og:description[\"'][^>]+content=[\"']([^\"']{1,300})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex("<meta[^>]+name=[\"']description[\"'][^>]+content=[\"']([^\"']{1,300})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex("<meta[^>]+content=[\"']([^\"']{1,300})[\"'][^>]+name=[\"']description", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };
    static readonly Regex SitePattern = new("<meta[^>]+property=[\"']og:site_name[\"'][^>]+content=[\"']([^\"']{1,60})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>自己抓页面解析 meta：不经过任何第三方，是通用分享卡的首选路径。
    /// 实测 B站页面没有 og:title（走 itemprop/title 兜底），网易云有完整 og。</summary>
    public static ArkLinkInfo ParseOpenGraph(string html, string finalUrl)
    {
        string text = html ?? "";
        return new ArkLinkInfo("自抓 og", First(text, TitlePatterns), First(text, DescPatterns), First(text, ImagePatterns),
            First(text, new[] { SitePattern }), finalUrl);
    }
    static string First(string html, Regex[] patterns)
    {
        foreach (Regex pattern in patterns)
        {
            Match match = pattern.Match(html);
            if (match.Success)
            {
                return Decode(match.Groups[1].Value);
            }
        }
        return "";
    }
    static string Decode(string value)
    {
        return Regex.Replace(value, "&(#x?[0-9a-fA-F]+|[a-zA-Z]+);", m => m.Groups[1].Value.ToLowerInvariant() switch {
            "amp" => "&", "quot" => "\"", "gt" => ">", "lt" => "<", "nbsp" => " ", "apos" => "'",
            _ when m.Groups[1].Value.StartsWith("#x", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(m.Groups[1].Value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex) => char.ConvertFromUtf32(hex),
            _ when m.Groups[1].Value.StartsWith("#") && int.TryParse(m.Groups[1].Value[1..], out int dec) && dec > 0 => char.ConvertFromUtf32(dec),
            _ => m.Value
        });
    }
    public static ArkLinkInfo ParseMicrolink(JsonElement root, string finalUrl)
    {
        if (Text(root, "status") != "success" || root.TryGetProperty("data", out JsonElement data) == false)
        {
            throw new Exception("microlink 没解析成功：" + (Text(root, "message").Length > 0 ? Text(root, "message") : "返回里没有 data"));
        }
        string Value(string name) => data.TryGetProperty(name, out JsonElement node) && node.ValueKind == JsonValueKind.Object ? Text(node, "value") : Text(node, name);
        string image = data.TryGetProperty("image", out JsonElement img) && img.ValueKind == JsonValueKind.Object ? Text(img, "url") : "";
        return new ArkLinkInfo("microlink", Value("title"), Value("description"), image, Value("publisher"), finalUrl);
    }
}
