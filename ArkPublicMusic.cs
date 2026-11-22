using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Suran.ShareArk;

/// <summary>公共音乐源搜到的一条结果。Id 是能直接喂给对应平台卡片接口的标识（网易云 id / QQ songmid）。</summary>
public sealed record ArkPublicSong(string Source, string Id, string Name, string Artist, string Album, string Cover, string Quality, string PicId = "")
{
    public string Line() => Id + " | " + ArkFreeSources.SafeText(Name, 80)
        + (Artist.Length > 0 ? " - " + ArkFreeSources.SafeText(Artist, 60) : "");
}

/// <summary>
/// 免费公共音乐源的 URL 拼装与解析（GD Studio、VKeys、酷我）。纯函数，方便离线打探针。
/// GD Studio：搜索 + 320k 直链 + 封面 + 歌词，只支持 netease / joox。
/// VKeys：netease / tencent 搜索列表，不给直链。
/// 酷我：搜索 + 直链 + 歌词 + 封面，id 是酷我自己的，不能当网易云/QQ 的 id 用。
/// </summary>
public static class ArkPublicMusic
{
    public const string GdSourceNetease = "netease";
    public const string VKeysNetease = "netease";
    public const string VKeysTencent = "tencent";

    public const string KuwoSearchBase = "https://search.kuwo.cn";
    public const string KuwoPlayBase = "https://antiserver.kuwo.cn";
    public const string KuwoLyricBase = "https://m.kuwo.cn";
    public const string KuwoImageBase = "https://img1.kuwo.cn";

    // rn 小于 3 时酷我只回分页头、不给 abslist，所以下限压到 3
    public static string KuwoSearchUrl(string keyword, int count)
        => KuwoSearchBase + "/r.s?client=kt&all=" + Uri.EscapeDataString(keyword)
           + "&pn=0&rn=" + Math.Clamp(count, 3, 20).ToString(CultureInfo.InvariantCulture)
           + "&uid=&vipver=1&show_copyright_off=1&pcvipver=1&encoding=utf8&rformat=json&mobi=1";

    /// <summary>rid 用 musicRid（MUSIC_数字），回的是纯文本直链</summary>
    public static string KuwoPlayUrlUrl(string musicRid)
        => KuwoPlayBase + "/anti.s?type=convert_url&format=mp3&response=url&rid=" + Uri.EscapeDataString(musicRid);

    public static string KuwoLyricUrl(string musicId)
        => KuwoLyricBase + "/newh5/singles/songinfoandlrc?musicId=" + Uri.EscapeDataString(musicId);

    public static string KuwoCoverUrl(string shortPath)
        => (shortPath ?? "").Length == 0 ? "" : KuwoImageBase + "/star/albumcover/" + shortPath.TrimStart('/');

    /// <summary>酷我页面，卡片跳转用</summary>
    public static string KuwoJumpUrl(string musicId) => "https://www.kuwo.cn/play_detail/" + musicId;

    /// <summary>搜索结果在 abslist 里，字段全大写；MUSICRID 形如 MUSIC_228908，id 只要数字部分</summary>
    public static List<ArkPublicSong> ParseKuwoSearch(JsonElement root)
    {
        List<ArkPublicSong> list = new();
        if (!root.TryGetProperty("abslist", out JsonElement abs) || abs.ValueKind != JsonValueKind.Array)
        {
            return list;
        }
        foreach (JsonElement item in abs.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            string name = Clean(Str(item, "SONGNAME"));
            string rid = Str(item, "MUSICRID");
            string id = rid.StartsWith("MUSIC_", StringComparison.OrdinalIgnoreCase) ? rid[6..] : rid;
            if (id.Length == 0 || name.Length == 0)
            {
                continue;
            }
            list.Add(new ArkPublicSong("酷我", id, name, Clean(Str(item, "ARTIST")).Replace("&", "/"),
                Clean(Str(item, "ALBUM")), KuwoCoverUrl(Str(item, "web_albumpic_short")), "mp3"));
        }
        return list;
    }

    /// <summary>直链接口回纯文本，拿不到时是一句提示而不是地址</summary>
    public static string ParseKuwoPlayUrl(string body)
    {
        string text = (body ?? "").Trim();
        return text.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? text : "";
    }

    /// <summary>酷我给的是 [{"lineLyric","time"(秒)}]，拼回标准 LRC 时间戳</summary>
    public static ArkLyricText ParseKuwoLyric(JsonElement root)
    {
        if (!root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("lrclist", out JsonElement lines) || lines.ValueKind != JsonValueKind.Array)
        {
            throw new Exception("酷我没给歌词");
        }
        List<string> parts = new();
        foreach (JsonElement line in lines.EnumerateArray())
        {
            string text = Str(line, "lineLyric");
            double seconds = double.TryParse(Str(line, "time"), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed : -1;
            if (seconds < 0 || text.Length == 0)
            {
                continue;
            }
            int minute = (int)(seconds / 60);
            double rest = seconds - minute * 60;
            parts.Add("[" + minute.ToString("00", CultureInfo.InvariantCulture) + ":"
                + rest.ToString("00.00", CultureInfo.InvariantCulture) + "]" + text);
        }
        if (parts.Count == 0)
        {
            throw new Exception("酷我歌词是空的");
        }
        string lrc = string.Join("\n", parts);
        return new ArkLyricText("酷我", lrc, true, parts.Count);
    }

    // 酷我的字段里带 &nbsp; 这类实体，直接塞进消息/卡片会原样显示
    static string Clean(string text)
        => (text ?? "").Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Trim();

    public static string GdSearchUrl(string api, string source, string name, int count)
        => api + "?types=search&source=" + Uri.EscapeDataString(source)
           + "&name=" + Uri.EscapeDataString(name)
           + "&count=" + Math.Clamp(count, 1, 20).ToString(CultureInfo.InvariantCulture) + "&pages=1";

    public static string GdUrlUrl(string api, string source, string id, int br = 320)
        => api + "?types=url&source=" + Uri.EscapeDataString(source)
           + "&id=" + Uri.EscapeDataString(id) + "&br=" + br.ToString(CultureInfo.InvariantCulture);

    public static string GdPicUrl(string api, string source, string picId, int size = 300)
        => api + "?types=pic&source=" + Uri.EscapeDataString(source)
           + "&id=" + Uri.EscapeDataString(picId) + "&size=" + size.ToString(CultureInfo.InvariantCulture);

    public static string GdLyricUrl(string api, string source, string lyricId)
        => api + "?types=lyric&source=" + Uri.EscapeDataString(source) + "&id=" + Uri.EscapeDataString(lyricId);

    /// <summary>GD Studio 的搜索结果里 artist 是数组，pic_id / lyric_id 与 id 可能不同，改封面时要按 pic_id 取</summary>
    public static List<ArkPublicSong> ParseGdSearch(JsonElement root, string api, string source)
    {
        List<ArkPublicSong> list = new();
        if (root.ValueKind != JsonValueKind.Array)
        {
            return list;
        }
        foreach (JsonElement item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            string id = Str(item, "id");
            string name = Str(item, "name");
            if (id.Length == 0 || name.Length == 0)
            {
                continue;
            }
            string artist = item.TryGetProperty("artist", out JsonElement artists) && artists.ValueKind == JsonValueKind.Array
                ? string.Join("/", artists.EnumerateArray().Select(a => a.ValueKind == JsonValueKind.String ? a.GetString() ?? "" : "").Where(s => s.Length > 0))
                : Str(item, "artist");
            string picId = Str(item, "pic_id");
            // GD 的 pic 接口回的是 JSON（里面才是图片地址），所以这里只留 pic_id，用到封面时再换一次
            list.Add(new ArkPublicSong("GD Studio", id, name, artist, Str(item, "album"), "", "", picId));
        }
        return list;
    }

    /// <summary>types=url 回 {"url": "..."}；拿不到直链时返回空串</summary>
    public static string ParseGdUrl(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "";
        }
        string url = Str(root, "url");
        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : "";
    }

    /// <summary>types=pic 回 {"url": "真实图片地址"}，那个地址才能直接放进卡片</summary>
    public static string ParseGdPic(JsonElement root)
    {
        string url = root.ValueKind == JsonValueKind.Object ? Str(root, "url") : "";
        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : "";
    }

    public static ArkLyricText ParseGdLyric(JsonElement root)
    {
        string lyric = root.ValueKind == JsonValueKind.Object ? Str(root, "lyric") : "";
        if (lyric.Trim().Length == 0)
        {
            throw new Exception("GD Studio 没给歌词正文");
        }
        string[] lines = lyric.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return new ArkLyricText("GD Studio", lyric, lyric.Contains("[0"), lines.Length);
    }

    public static string VKeysSearchUrl(string baseUrl, string source, string word)
        => baseUrl.TrimEnd('/') + "/v2/music/" + Uri.EscapeDataString(source) + "?word=" + Uri.EscapeDataString(word);

    /// <summary>VKeys 的 QQ 结果用 mid 当 id（卡片接口认 songmid），网易云用 id</summary>
    public static List<ArkPublicSong> ParseVKeysSearch(JsonElement root, string source)
    {
        List<ArkPublicSong> list = new();
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out JsonElement data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return list;
        }
        foreach (JsonElement item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            string id = source == VKeysTencent ? Str(item, "mid") : Str(item, "id");
            string name = Str(item, "song");
            if (id.Length == 0 || name.Length == 0)
            {
                continue;
            }
            list.Add(new ArkPublicSong("VKeys", id, name, Str(item, "singer"), Str(item, "album"),
                Str(item, "cover"), Str(item, "quality")));
        }
        return list;
    }

    /// <summary>VKeys 的 code=200 才算成功，失败时把它的 message 带出来</summary>
    public static void EnsureVKeysOk(JsonElement root)
    {
        int code = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("code", out JsonElement c)
            && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out int parsed) ? parsed : -1;
        if (code != 200)
        {
            throw new Exception("VKeys 回 code=" + code + "：" + Str(root, "message"));
        }
    }

    static string Str(JsonElement node, string name)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(name, out JsonElement value))
        {
            return "";
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            _ => "",
        };
    }
}
