using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alife.Function.FunctionCaller;

namespace Suran.ShareArk;

/// <summary>
/// 豆瓣影视/图书/音乐条目卡：走豆瓣 App 的 rexxar 接口取评分、导演、演员、简介、海报，
/// 比抓网页 meta 准得多（豆瓣页面现在不登录看不到评分）。
/// 找条目走 subject_suggest：rexxar 的 search 接口返回的是小组讨论帖，不是条目。
/// 注意 rexxar 接口必须带 Referer: https://m.douban.com/，不带直接回 400。
/// </summary>
public partial class ShareArkModule
{
    [XmlFunction(FunctionMode.OneShot)]
    [Description("查豆瓣影视/图书/音乐条目：给名字自动找条目，也可以直接给豆瓣链接（movie/book/music.douban.com/subject/数字）。返回评分、类型、导演或作者、简介、海报；给了 targetId 就顺带发一张分享卡")]
    public async Task ArkDouban(
        [Description("作品名，或豆瓣条目链接")] string query,
        [Description("类型：movie(电影)/tv(剧集)/book(图书)/music(音乐)，默认 movie；给的是链接时自动认")] string kind = null,
        [Description("目标群号或QQ号，填了才发卡片")] long? targetId = null,
        [Description("group或private，默认group")] string type = null)
    {
        if (Gate(Configuration.EnableDouban, "豆瓣影视图书"))
        {
            return;
        }
        string text = (query ?? "").Trim();
        if (text.Length == 0)
        {
            throw new Exception("给个作品名或豆瓣链接");
        }

        string subjectKind = NormalizeKind(kind);
        string id = "";
        Match linkMatch = Regex.Match(text, @"douban\.com/(?:[a-z]+/)?(?:subject|movie|book|music|tv)/(\d+)", RegexOptions.IgnoreCase);
        if (linkMatch.Success)
        {
            id = linkMatch.Groups[1].Value;
            subjectKind = KindFromUrl(text);
        }
        else if (Regex.IsMatch(text, @"^\d{5,}$"))
        {
            id = text;
        }

        // 没给 id 就按名字搜：subject_suggest 免 cookie、免 key，回的就是条目
        if (id.Length == 0)
        {
            (string Id, string Title, string Year, string Image)? hit = await DoubanSuggestAsync(text, subjectKind);
            if (hit == null)
            {
                throw new Exception("豆瓣没搜到「" + text + "」"
                    + (subjectKind == "music" ? "（豆瓣音乐没有搜索接口，换电影或图书试试，或直接给条目链接）" : "，换个名字或直接给豆瓣链接"));
            }
            id = hit.Value.Id;
            if (subjectKind.Length == 0)
            {
                subjectKind = "movie";
            }
        }

        DoubanSubject subject = await DoubanSubjectAsync(id, subjectKind);
        interactor.Poke(subject.Describe());

        if (targetId != null)
        {
            await SendShareCardCoreAsync(subject.Url, subject.CardTitle, subject.CardDesc,
                subject.Image, "[豆瓣]" + subject.CardTitle, targetId, type, allowBili: true);
        }
    }

    static string NormalizeKind(string kind)
    {
        string value = (kind ?? "").Trim().ToLowerInvariant();
        return value switch
        {
            "movie" or "film" or "电影" => "movie",
            "tv" or "剧集" or "电视剧" or "综艺" => "tv",
            "book" or "图书" or "书" or "读书" => "book",
            "music" or "音乐" or "专辑" => "music",
            _ => ""
        };
    }

    static string KindFromUrl(string url)
    {
        if (url.Contains("book.douban.com", StringComparison.OrdinalIgnoreCase)) return "book";
        if (url.Contains("music.douban.com", StringComparison.OrdinalIgnoreCase)) return "music";
        if (url.Contains("/tv/", StringComparison.OrdinalIgnoreCase)) return "tv";
        return "movie";
    }

    /// <summary>按名字找条目。豆瓣音乐没有 suggest，返回 null 让调用方给出提示。</summary>
    async Task<(string Id, string Title, string Year, string Image)?> DoubanSuggestAsync(string keyword, string kind)
    {
        if (kind == "music")
        {
            return null;
        }
        string host = kind == "book" ? "https://book.douban.com" : "https://movie.douban.com";
        string url = host + "/j/subject_suggest?q=" + Uri.EscapeDataString(keyword);
        string body;
        try
        {
            body = await GetTextWithRefererAsync(url, host + "/");
        }
        catch (Exception error)
        {
            throw new Exception("豆瓣搜索没通：" + error.Message);
        }
        using JsonDocument doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return null;
        }
        JsonElement first = doc.RootElement[0];
        string id = Text(first, "id");
        if (id.Length == 0 || !Regex.IsMatch(id, @"^\d+$"))
        {
            return null;
        }
        return (id, Text(first, "title"), Text(first, "year"), Text(first, "img"));
    }

    async Task<DoubanSubject> DoubanSubjectAsync(string id, string kind)
    {
        string apiKind = kind.Length == 0 ? "movie" : kind;
        string url = "https://m.douban.com/rexxar/api/v2/" + apiKind + "/" + id;
        string body;
        try
        {
            // 少了这个 Referer 豆瓣回 400（117 字节的 {"msg":"...","code":...}），拿不到任何字段
            body = await GetTextWithRefererAsync(url, "https://m.douban.com/");
        }
        catch (Exception error)
        {
            throw new Exception("豆瓣条目没取到（" + apiKind + " " + id + "）：" + error.Message
                + "\n条目 id 或类型可能不对，确认链接是 movie/book/music.douban.com/subject/数字 这种");
        }
        using JsonDocument doc = JsonDocument.Parse(body);
        return ParseDoubanSubject(doc.RootElement, id, apiKind);
    }

    /// <summary>豆瓣条目 JSON 转卡片字段；抽成静态是为了能拿真实响应离线测。</summary>
    static DoubanSubject ParseDoubanSubject(JsonElement root, string id, string apiKind)
    {
        if (root.ValueKind != JsonValueKind.Object || Text(root, "title").Length == 0)
        {
            throw new Exception("豆瓣返回里没有标题，这条目可能已删除或被限制");
        }

        double rating = 0;
        long ratingCount = 0;
        if (root.TryGetProperty("rating", out JsonElement ratingNode) && ratingNode.ValueKind == JsonValueKind.Object)
        {
            if (ratingNode.TryGetProperty("value", out JsonElement value) && value.ValueKind == JsonValueKind.Number)
            {
                rating = value.GetDouble();
            }
            if (ratingNode.TryGetProperty("count", out JsonElement count) && count.ValueKind == JsonValueKind.Number)
            {
                ratingCount = count.GetInt64();
            }
        }

        string image = "";
        if (root.TryGetProperty("pic", out JsonElement pic) && pic.ValueKind == JsonValueKind.Object)
        {
            image = Text(pic, "large");
            if (image.Length == 0)
            {
                image = Text(pic, "normal");
            }
        }
        if (image.Length == 0)
        {
            image = Text(root, "cover_url");
        }

        // 电影用片长、图书用出版年月，两者字段不同
        string extra = JoinNames(root, "durations");
        if (extra.Length == 0)
        {
            extra = JoinNames(root, "pubdate");
        }

        return new DoubanSubject
        {
            Kind = apiKind,
            Id = id,
            Title = Text(root, "title"),
            OriginalTitle = Text(root, "original_title"),
            Year = Text(root, "year"),
            Rating = rating,
            RatingCount = ratingCount,
            Intro = Text(root, "intro"),
            Image = image,
            CardSubtitle = Text(root, "card_subtitle"),
            Url = KindHost(apiKind) + "/subject/" + id + "/",
            Genres = JoinNames(root, "genres"),
            Directors = JoinNames(root, "directors", "name"),
            Actors = JoinNames(root, "actors", "name", 5),
            Authors = JoinNames(root, "author", null, 3),
            Extra = extra
        };
    }

    static string KindHost(string kind) => kind switch
    {
        "book" => "https://book.douban.com",
        "music" => "https://music.douban.com",
        _ => "https://movie.douban.com"
    };

    /// <summary>取数组里的 name（或字符串本身），拼成「甲 / 乙 / 丙」，超过 limit 个加「等」。</summary>
    static string JoinNames(JsonElement parent, string arrayName, string field = null, int limit = 0)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(arrayName, out JsonElement array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return "";
        }
        List<string> names = new();
        foreach (JsonElement item in array.EnumerateArray())
        {
            string name = field == null ? (item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : "") : Text(item, field);
            name = name.Trim();
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }
        if (names.Count == 0)
        {
            return "";
        }
        bool trimmed = limit > 0 && names.Count > limit;
        IEnumerable<string> shown = trimmed ? names.Take(limit) : names;
        return string.Join(" / ", shown) + (trimmed ? " 等" : "");
    }

    sealed class DoubanSubject
    {
        public string Kind = "movie";
        public string Id = "";
        public string Title = "";
        public string OriginalTitle = "";
        public string Year = "";
        public double Rating;
        public long RatingCount;
        public string Intro = "";
        public string Image = "";
        public string CardSubtitle = "";
        public string Url = "";
        public string Genres = "";
        public string Directors = "";
        public string Actors = "";
        public string Authors = "";
        public string Extra = "";

        public string RatingText => Rating > 0
            ? Rating.ToString("0.0", CultureInfo.InvariantCulture) + "（" + CountText(RatingCount) + "人评）"
            : "暂无评分";

        public string CardTitle => "《" + Title + "》" + (Rating > 0 ? " 豆瓣 " + Rating.ToString("0.0", CultureInfo.InvariantCulture) : "");

        public string CardDesc
        {
            get
            {
                List<string> parts = new();
                if (Year.Length > 0) parts.Add(Year);
                if (Genres.Length > 0) parts.Add(Genres);
                if (Directors.Length > 0) parts.Add("导演 " + Directors);
                if (Actors.Length > 0) parts.Add(Actors);
                if (Kind == "book" && Authors.Length > 0) parts.Add(Authors);
                if (parts.Count == 0) parts.Add(RatingText);
                return string.Join(" / ", parts);
            }
        }

        public string Describe()
        {
            List<string> lines = new();
            string head = "《" + Title + "》";
            if (OriginalTitle.Length > 0 && !string.Equals(OriginalTitle, Title, StringComparison.OrdinalIgnoreCase))
            {
                head += " " + OriginalTitle;
            }
            lines.Add(head);
            List<string> meta = new() { "豆瓣 " + RatingText };
            if (Year.Length > 0) meta.Add(Year);
            if (Genres.Length > 0) meta.Add(Genres);
            if (Extra.Length > 0) meta.Add(Extra);
            lines.Add(string.Join(" · ", meta));
            if (Directors.Length > 0) lines.Add("导演：" + Directors);
            if (Actors.Length > 0) lines.Add("主演：" + Actors);
            if (Kind == "book" && Authors.Length > 0) lines.Add("作者：" + Authors);
            if (CardSubtitle.Length > 0 && Directors.Length == 0 && Authors.Length == 0) lines.Add(CardSubtitle);
            string intro = Regex.Replace(Intro, @"\s+", " ").Trim();
            if (intro.Length > 0)
            {
                lines.Add("简介：" + (intro.Length > 180 ? intro[..180] + "…" : intro));
            }
            lines.Add(Url);
            return string.Join("\n", lines);
        }

        static string CountText(long count) => count >= 10000
            ? (count / 10000.0).ToString("0.0", CultureInfo.InvariantCulture) + " 万"
            : count.ToString(CultureInfo.InvariantCulture);
    }
}
