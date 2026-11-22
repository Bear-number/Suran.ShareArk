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
/// 热榜：抖音（iesdouyin 榜单）、百度（top.baidu.com）、B 站（ranking/v2）。
/// 三家都免注册免 key；微博与知乎的榜单接口要登录态（403/401），没接。
/// </summary>
public partial class ShareArkModule
{
    [XmlFunction(FunctionMode.OneShot)]
    [Description("看热榜：platform 取 douyin(抖音)/baidu(百度)/bili(B站)/all(三家各前几条)，默认 douyin。B 站那条带 BV 号，可以直接拿去 SendArkCard 发视频卡")]
    public async Task ArkHotList(
        [Description("平台：douyin/baidu/bili/all，默认 douyin")] string platform = null,
        [Description("条数，默认 10")] int limit = 10)
    {
        if (Gate(Configuration.EnableHotList, "热榜"))
        {
            return;
        }
        string which = (platform ?? "").Trim().ToLowerInvariant();
        if (which.Length == 0)
        {
            which = "douyin";
        }
        int count = Math.Clamp(limit, 1, 30);
        List<string> blocks = new();
        try
        {
            if (which is "douyin" or "all" || which.Contains("抖音"))
            {
                blocks.Add(await DouyinHotAsync(which == "all" ? Math.Min(count, 5) : count));
            }
            if (which is "baidu" or "all" || which.Contains("百度"))
            {
                blocks.Add(await BaiduHotAsync(which == "all" ? Math.Min(count, 5) : count));
            }
            if (which is "bili" or "bilibili" or "all" || which.Contains("B站") || which.Contains("b站"))
            {
                blocks.Add(await BiliRankAsync(which == "all" ? Math.Min(count, 5) : count));
            }
        }
        catch (Exception error)
        {
            throw new Exception("热榜没取到：" + error.Message);
        }
        if (blocks.Count == 0)
        {
            throw new Exception("platform 只有 douyin / baidu / bili / all 四个选项");
        }
        interactor.Poke(string.Join("\n\n", blocks.Where(b => b.Length > 0)));
    }

    async Task<string> DouyinHotAsync(int limit)
    {
        // 这个端点是网页版榜单的原始 JSON，比 www.douyin.com 那套带签名的接口省事
        string body = await GetStringAsync("https://www.iesdouyin.com/web/api/v2/hotsearch/billboard/word/", "iesdouyin.com");
        using JsonDocument doc = JsonDocument.Parse(body);
        return ParseDouyinHot(doc.RootElement, limit);
    }

    static string ParseDouyinHot(JsonElement root, int limit)
    {
        if (!root.TryGetProperty("word_list", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            return "";
        }
        List<string> lines = new() { "抖音热榜" };
        int index = 1;
        foreach (JsonElement item in list.EnumerateArray())
        {
            if (index > limit)
            {
                break;
            }
            string word = Text(item, "word");
            if (word.Length == 0)
            {
                continue;
            }
            long hot = 0;
            if (item.TryGetProperty("hot_value", out JsonElement hotNode) && hotNode.ValueKind == JsonValueKind.Number)
            {
                hot = hotNode.GetInt64();
            }
            lines.Add(index + ". " + word + (hot > 0 ? " · " + HotText(hot) : ""));
            index++;
        }
        return lines.Count > 1 ? string.Join("\n", lines) : "";
    }

    async Task<string> BaiduHotAsync(int limit)
    {
        string body = await GetStringAsync("https://top.baidu.com/api/board?platform=wise&tab=realtime", "top.baidu.com");
        using JsonDocument doc = JsonDocument.Parse(body);
        return ParseBaiduHot(doc.RootElement, limit);
    }

    static string ParseBaiduHot(JsonElement root, int limit)
    {
        if (!root.TryGetProperty("data", out JsonElement data)
            || !data.TryGetProperty("cards", out JsonElement cards)
            || cards.ValueKind != JsonValueKind.Array)
        {
            return "";
        }
        List<string> lines = new() { "百度热搜" };
        int index = 1;
        // 结构是 cards[].content[][].content[]，层级会随活动卡片变化，逐层找含 word 的项比写死下标稳
        foreach (JsonElement card in cards.EnumerateArray())
        {
            if (!card.TryGetProperty("content", out JsonElement groups) || groups.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (JsonElement group in groups.EnumerateArray())
            {
                if (!group.TryGetProperty("content", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (JsonElement item in items.EnumerateArray())
                {
                    if (index > limit)
                    {
                        break;
                    }
                    string word = Text(item, "word");
                    if (word.Length == 0)
                    {
                        continue;
                    }
                    lines.Add(index + ". " + word + (item.TryGetProperty("isTop", out JsonElement top) && top.ValueKind == JsonValueKind.True ? "（置顶）" : ""));
                    index++;
                }
            }
        }
        return lines.Count > 1 ? string.Join("\n", lines) : "";
    }

    async Task<string> BiliRankAsync(int limit)
    {
        string body = await GetStringAsync("https://api.bilibili.com/x/web-interface/ranking/v2?rid=0&type=all", "api.bilibili.com");
        using JsonDocument doc = JsonDocument.Parse(body);
        return ParseBiliRank(doc.RootElement, limit);
    }

    static string ParseBiliRank(JsonElement root, int limit)
    {
        if (!root.TryGetProperty("code", out JsonElement code) || code.GetInt32() != 0
            || !root.TryGetProperty("data", out JsonElement data)
            || !data.TryGetProperty("list", out JsonElement list)
            || list.ValueKind != JsonValueKind.Array)
        {
            return "";
        }
        List<string> lines = new() { "B站排行榜" };
        int index = 1;
        foreach (JsonElement item in list.EnumerateArray())
        {
            if (index > limit)
            {
                break;
            }
            // 标题里带 <em class="keyword"> 高亮标签，直接发出去是乱码样
            string title = Regex.Replace(Text(item, "title"), "<[^>]*>", "").Trim();
            if (title.Length == 0)
            {
                continue;
            }
            string bvid = Text(item, "bvid");
            long view = 0;
            if (item.TryGetProperty("stat", out JsonElement stat) && stat.ValueKind == JsonValueKind.Object
                && stat.TryGetProperty("view", out JsonElement viewNode) && viewNode.ValueKind == JsonValueKind.Number)
            {
                view = viewNode.GetInt64();
            }
            lines.Add(index + ". " + title + (view > 0 ? " · " + HotText(view) + "播放" : "") + (bvid.Length > 0 ? " · " + bvid : ""));
            index++;
        }
        return lines.Count > 1 ? string.Join("\n", lines) : "";
    }

    static string HotText(long value) => value >= 10000
        ? (value / 10000.0).ToString("0.0", CultureInfo.InvariantCulture) + " 万"
        : value.ToString(CultureInfo.InvariantCulture);
}
