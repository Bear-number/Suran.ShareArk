using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Suran.ShareArk;

/// <summary>
/// 公共音乐源的调用封装：GD Studio（网易云搜索+直链+封面+歌词）、VKeys（网易云/QQ 搜索）、洛苏歌词。
/// 这些站都免 key，但会随时挂或改结构，所以每个方法都只返回 null / 空表，不往外抛异常。
/// </summary>
public partial class ShareArkModule
{
    string GdApi => (Configuration.GdStudioApiUrl ?? "").Trim() is { Length: > 0 } configured
        ? configured
        : "https://music-api.gdstudio.xyz/api.php";

    async Task<List<ArkPublicSong>> GdStudioSearchAsync(string keyword, int count = 5)
    {
        if (Configuration.EnableGdStudioMusic == false || (keyword ?? "").Trim().Length == 0)
        {
            return new List<ArkPublicSong>();
        }
        try
        {
            string body = await Http.GetStringAsync(ArkPublicMusic.GdSearchUrl(GdApi, ArkPublicMusic.GdSourceNetease, keyword.Trim(), count));
            using JsonDocument doc = JsonDocument.Parse(body);
            return ArkPublicMusic.ParseGdSearch(doc.RootElement.Clone(), GdApi, ArkPublicMusic.GdSourceNetease);
        }
        catch (Exception error)
        {
            logger.LogWarning("GD Studio 搜索失败（{Keyword}）：{Msg}", keyword, error.Message);
            return new List<ArkPublicSong>();
        }
    }

    // 只取直链，换源修复时复用
    async Task<string> GdStudioPlayAsync(string id)
    {
        if (Configuration.EnableGdStudioMusic == false || (id ?? "").Length == 0)
        {
            return "";
        }
        try
        {
            string body = await Http.GetStringAsync(ArkPublicMusic.GdUrlUrl(GdApi, ArkPublicMusic.GdSourceNetease, id));
            using JsonDocument doc = JsonDocument.Parse(body);
            return ArkPublicMusic.ParseGdUrl(doc.RootElement);
        }
        catch (Exception error)
        {
            logger.LogWarning("GD Studio 取直链失败（{Id}）：{Msg}", id, error.Message);
            return "";
        }
    }

    // 网易云详情兜底：GD Studio 按歌名搜到 id，再换 320k 直链；拿不到直链就当没这条
    async Task<Dictionary<string, string>> GdStudioSongAsync(string keyword)
    {
        List<ArkPublicSong> hits = await GdStudioSearchAsync(keyword, 5);
        ArkPublicSong hit = PickBest(hits, keyword);
        if (hit == null)
        {
            return null;
        }
        string play = await GdStudioPlayAsync(hit.Id);
        if (play.Length == 0)
        {
            logger.LogWarning("GD Studio 没给直链（{Song} {Id}）", hit.Name, hit.Id);
            return null;
        }
        try
        {
            string jump = "https://music.163.com/song?id=" + hit.Id;
            string cover = hit.Cover.Length > 0 ? hit.Cover : await GdStudioPicAsync(hit.PicId);
            return new Dictionary<string, string>
            {
                ["song"] = hit.Name,
                ["singer"] = hit.Artist.Length > 0 ? hit.Artist : "未知歌手",
                ["cover"] = cover,
                ["format"] = "netease",
                ["url"] = play,
                ["jump"] = jump,
            };
        }
        catch (Exception error)
        {
            logger.LogWarning("GD Studio 取详情失败（{Song} {Id}）：{Msg}", hit.Name, hit.Id, error.Message);
            return null;
        }
    }

    // GD 的封面要先换一次真实图片地址（它那个 pic 接口回 JSON）
    async Task<string> GdStudioPicAsync(string picId)
    {
        if ((picId ?? "").Length == 0)
        {
            return "";
        }
        try
        {
            string body = await Http.GetStringAsync(ArkPublicMusic.GdPicUrl(GdApi, ArkPublicMusic.GdSourceNetease, picId));
            using JsonDocument doc = JsonDocument.Parse(body);
            return ArkPublicMusic.ParseGdPic(doc.RootElement);
        }
        catch (Exception error)
        {
            logger.LogWarning("GD Studio 取封面失败（{PicId}）：{Msg}", picId, error.Message);
            return "";
        }
    }

    // 酷我：搜索、直链、歌词、封面都有，是网易云外层直链失效时最靠谱的替补
    async Task<List<ArkPublicSong>> KuwoSearchAsync(string keyword, int count = 5)
    {
        if (Configuration.EnableKuwoMusic == false || (keyword ?? "").Trim().Length == 0)
        {
            return new List<ArkPublicSong>();
        }
        try
        {
            string body = await Http.GetStringAsync(ArkPublicMusic.KuwoSearchUrl(keyword.Trim(), count));
            using JsonDocument doc = JsonDocument.Parse(RepairKuwoJson(body));
            return ArkPublicMusic.ParseKuwoSearch(doc.RootElement.Clone());
        }
        catch (Exception error)
        {
            logger.LogWarning("酷我搜索失败（{Keyword}）：{Msg}", keyword, error.Message);
            return new List<ArkPublicSong>();
        }
    }

    // 部分镜像回的是单引号包裹的伪 JSON，按行修一遍再解析
    static string RepairKuwoJson(string body)
    {
        string text = (body ?? "").Trim();
        if (text.Length == 0 || text[0] == '{')
        {
            return text;
        }
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    /// <summary>按 musicRid 换 mp3 直链；拿不到返回空串</summary>
    async Task<string> KuwoPlayUrlAsync(string musicId)
    {
        if (Configuration.EnableKuwoMusic == false || (musicId ?? "").Trim().Length == 0)
        {
            return "";
        }
        try
        {
            string body = await Http.GetStringAsync(
                ArkPublicMusic.KuwoPlayUrlUrl("MUSIC_" + musicId.Trim()));
            return ArkPublicMusic.ParseKuwoPlayUrl(body);
        }
        catch (Exception error)
        {
            logger.LogWarning("酷我取直链失败（{Id}）：{Msg}", musicId, error.Message);
            return "";
        }
    }

    /// <summary>按「歌名 歌手」找一条匹配的酷我歌曲，并取直链；供直链修复和详情容灾用</summary>
    async Task<(ArkPublicSong Song, string PlayUrl)> KuwoPickAsync(string song, string artist, int count = 5)
    {
        string keyword = ((artist ?? "") + " " + (song ?? "")).Trim();
        List<ArkPublicSong> hits = await KuwoSearchAsync(keyword, count);
        // 修复直链时宁可不用，也不能把别的歌换上去
        ArkPublicSong hit = hits.FirstOrDefault(candidate =>
            MatchesSong(candidate.Name, candidate.Artist, song ?? "", artist ?? ""));
        if (hit == null)
        {
            return (null, "");
        }
        string play = await KuwoPlayUrlAsync(hit.Id);
        return (hit, play);
    }

    // 酷我详情兜底：元数据 + 直链都有，format 沿用调用方的平台标识
    async Task<Dictionary<string, string>> KuwoSongAsync(string keyword, string format)
    {
        List<ArkPublicSong> hits = await KuwoSearchAsync(keyword);
        ArkPublicSong hit = PickBest(hits, keyword);
        if (hit == null)
        {
            return null;
        }
        string play = await KuwoPlayUrlAsync(hit.Id);
        string jump = ArkPublicMusic.KuwoJumpUrl(hit.Id);
        return new Dictionary<string, string>
        {
            ["song"] = hit.Name,
            ["singer"] = hit.Artist.Length > 0 ? hit.Artist : "未知歌手",
            ["cover"] = hit.Cover,
            ["format"] = format,
            ["url"] = play.Length > 0 ? play : jump,
            ["jump"] = jump,
        };
    }

    /// <summary>酷我歌词：歌名/歌手都对上才用，别把翻唱的词贴到原唱上</summary>
    async Task<ArkLyricText> KuwoLyricAsync(string song, string artist)
    {
        if ((song ?? "").Trim().Length == 0)
        {
            return null;
        }
        List<ArkPublicSong> hits = await KuwoSearchAsync((artist + " " + song).Trim(), 5);
        ArkPublicSong hit = hits.FirstOrDefault(candidate => MatchesSong(candidate.Name, candidate.Artist, song, artist ?? ""));
        if (hit == null)
        {
            return null;
        }
        try
        {
            string body = await Http.GetStringAsync(ArkPublicMusic.KuwoLyricUrl(hit.Id));
            using JsonDocument doc = JsonDocument.Parse(body);
            return ArkPublicMusic.ParseKuwoLyric(doc.RootElement);
        }
        catch (Exception error)
        {
            logger.LogWarning("酷我取词失败（{Song}）：{Msg}", song, error.Message);
            return null;
        }
    }

    async Task<List<ArkPublicSong>> VKeysSearchAsync(string source, string keyword)    {
        if (Configuration.EnableVKeysMusic == false || (keyword ?? "").Trim().Length == 0)
        {
            return new List<ArkPublicSong>();
        }
        try
        {
            string baseUrl = (Configuration.VKeysBaseUrl ?? "").Trim();
            if (baseUrl.Length == 0)
            {
                return new List<ArkPublicSong>();
            }
            string body = await Http.GetStringAsync(ArkPublicMusic.VKeysSearchUrl(baseUrl, source, keyword.Trim()));
            using JsonDocument doc = JsonDocument.Parse(body);
            ArkPublicMusic.EnsureVKeysOk(doc.RootElement);
            return ArkPublicMusic.ParseVKeysSearch(doc.RootElement.Clone(), source);
        }
        catch (Exception error)
        {
            logger.LogWarning("VKeys 搜索失败（{Source} {Keyword}）：{Msg}", source, keyword, error.Message);
            return new List<ArkPublicSong>();
        }
    }

    // QQ 详情兜底：VKeys 只给元数据与封面，没有直链，卡片按跳转处理
    async Task<Dictionary<string, string>> VKeysSongAsync(string keyword)
    {
        List<ArkPublicSong> hits = await VKeysSearchAsync(ArkPublicMusic.VKeysTencent, keyword);
        ArkPublicSong hit = PickBest(hits, keyword);
        if (hit == null)
        {
            return null;
        }
        string jump = "https://y.qq.com/n/ryqq/songDetail/" + hit.Id;
        return new Dictionary<string, string>
        {
            ["song"] = hit.Name,
            ["singer"] = hit.Artist.Length > 0 ? hit.Artist : "未知歌手",
            ["cover"] = hit.Cover,
            ["format"] = "qq",
            ["url"] = jump,
            ["jump"] = jump,
        };
    }

    /// <summary>
    /// 歌词兜底链：GD Studio（网易云，320 库有词）→ 洛苏网易云。
    /// 两个都是模糊搜歌，必须校验歌名/歌手，别把翻唱的词贴到原唱上。
    /// </summary>
    async Task<ArkLyricText> PublicLyricAsync(string song, string artist)
    {
        if ((song ?? "").Trim().Length == 0)
        {
            return null;
        }
        List<ArkPublicSong> gdHits = await GdStudioSearchAsync((artist + " " + song).Trim(), 5);
        ArkPublicSong gd = PickBest(gdHits, (artist + " " + song).Trim());
        if (gd != null)
        {
            try
            {
                string body = await Http.GetStringAsync(ArkPublicMusic.GdLyricUrl(GdApi, ArkPublicMusic.GdSourceNetease, gd.Id));
                using JsonDocument doc = JsonDocument.Parse(body);
                return ArkPublicMusic.ParseGdLyric(doc.RootElement);
            }
            catch (Exception error)
            {
                logger.LogWarning("GD Studio 取词失败（{Song}）：{Msg}", song, error.Message);
            }
        }
        ArkLyricText kuwo = await KuwoLyricAsync(song, artist);
        if (kuwo != null)
        {
            return kuwo;
        }
        string lrc = await LuoSuLyricAsync(song, artist);
        return lrc.Trim().Length > 0
            ? new ArkLyricText("洛苏", lrc, lrc.Contains("[0"), lrc.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length)
            : null;
    }

    // 洛苏网易云点歌的返回里有 lyric 字段，单独取出来当歌词源
    async Task<string> LuoSuLyricAsync(string song, string artist)
    {
        if (Configuration.EnablePublicMusicApis == false)
        {
            return "";
        }
        string baseUrl = (Configuration.LuoSuBaseUrl ?? "").Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
        {
            return "";
        }
        try
        {
            string url = WithQuery(baseUrl + "/api/wymusic/",
                "ss=" + Uri.EscapeDataString((artist + " " + song).Trim()) + "&n=1&format=json");
            string body = await Http.GetStringAsync(url);
            using JsonDocument doc = JsonDocument.Parse(body);
            if (ReadCode(doc.RootElement) != 200 || doc.RootElement.TryGetProperty("data", out JsonElement data) == false
                || data.ValueKind != JsonValueKind.Object)
            {
                return "";
            }
            string lyric = Field(data, "lyric");
            string name = Field(data, "name");
            string singer = Field(data, "singer");
            // 洛苏也给模糊搜：歌名/歌手对不上就当没拿到
            return MatchesSong(name, singer, song, artist) ? lyric : "";
        }
        catch (Exception error)
        {
            logger.LogWarning("洛苏取词失败（{Song}）：{Msg}", song, error.Message);
            return "";
        }
    }

    // 封面兜底：iTunes 中文库不全时补一次，VKeys 直接给图片地址，GD 要再换一次
    async Task<List<ArkPublicSong>> PublicCoverAsync(string keyword)
    {
        List<ArkPublicSong> vk = await VKeysSearchAsync(ArkPublicMusic.VKeysNetease, keyword);
        if (vk.Count == 0)
        {
            vk = await VKeysSearchAsync(ArkPublicMusic.VKeysTencent, keyword);
        }
        if (vk.Count > 0)
        {
            return vk;
        }
        List<ArkPublicSong> gd = await GdStudioSearchAsync(keyword, 3);
        List<ArkPublicSong> resolved = new();
        foreach (ArkPublicSong hit in gd)
        {
            string cover = await GdStudioPicAsync(hit.PicId);
            if (cover.Length > 0)
            {
                resolved.Add(hit with { Cover = cover });
            }
        }
        if (resolved.Count == 0)
        {
            resolved.AddRange((await KuwoSearchAsync(keyword, 3)).Where(hit => hit.Cover.Length > 0));
        }
        return resolved;
    }

    /// <summary>模糊搜索挑最像的一条：歌名包含关键词、或歌手命中；都不命中就看第一条但要求歌名有交集</summary>
    static ArkPublicSong PickBest(List<ArkPublicSong> hits, string keyword)
    {
        if (hits == null || hits.Count == 0)
        {
            return null;
        }
        string want = (keyword ?? "").Trim();
        ArkPublicSong exact = hits.FirstOrDefault(hit =>
            hit.Name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0
            || want.IndexOf(hit.Name, StringComparison.OrdinalIgnoreCase) >= 0);
        if (exact != null)
        {
            return exact;
        }
        // 关键词常是「歌手 歌名」：退一步按词命中率挑
        string[] words = want.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        ArkPublicSong best = null;
        int bestScore = 0;
        foreach (ArkPublicSong hit in hits)
        {
            string text = hit.Name + " " + hit.Artist;
            int score = words.Count(word => word.Length > 0 && text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
            if (score > bestScore)
            {
                bestScore = score;
                best = hit;
            }
        }
        return bestScore > 0 ? best : null;
    }

    static bool MatchesSong(string name, string singer, string song, string artist)
    {
        // 名字都没拿到就别当命中，否则等于把任意一首歌的歌词贴上
        if (name.Trim().Length == 0 || song.Trim().Length == 0)
        {
            return false;
        }
        if (name.IndexOf(song.Trim(), StringComparison.OrdinalIgnoreCase) < 0
            && song.Trim().IndexOf(name.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }
        if (artist.Trim().Length > 0 && singer.Length > 0
            && singer.IndexOf(artist.Trim(), StringComparison.OrdinalIgnoreCase) < 0
            && artist.Trim().IndexOf(singer.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }
        return true;
    }
}
