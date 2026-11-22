using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Suran.ShareArk;

/// <summary>
/// 播放直链探活与修复。
/// 网易云外层直链对无版权/会员歌会 302 到 music.163.com/404，酷狗、QQ 的旧直链也会过期，
/// 这类地址签出来的卡片点开没声音，所以发卡前先探一次，播不了就换能播的源。
/// </summary>
public partial class ShareArkModule
{
    static readonly TimeSpan PlayProbeTimeout = TimeSpan.FromSeconds(5);

    // 几百 KB 以下的音频基本是平台塞的「版权受限」提示音，不是歌本身
    const long MinAudioBytes = 50 * 1024;

    /// <summary>
    /// 只取 1 字节，看 Content-Type 与文件大小；探不动时按可用处理，不擅自换地址。
    /// 网易云的死链是 302 到 http 的 404 页，而 .NET 不跟 https→http 降级，所以要自己跟一次。
    /// </summary>
    async Task<bool> IsPlayableAsync(string url)
    {
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        using CancellationTokenSource cts = new(PlayProbeTimeout);
        string target = url;
        try
        {
            for (int hop = 0; hop < 4; hop++)
            {
                using HttpRequestMessage req = new(HttpMethod.Get, target);
                req.Headers.Range = new RangeHeaderValue(0, 0);
                req.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
                using HttpResponseMessage resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if ((int)resp.StatusCode is >= 300 and < 400)
                {
                    if (resp.Headers.Location == null)
                    {
                        return false;
                    }
                    target = new Uri(new Uri(target), resp.Headers.Location).ToString();
                    continue;
                }
                return JudgePlayResponse(resp);
            }
            // 跳了四层还在跳，当成不可播
            return false;
        }
        catch (Exception error)
        {
            logger.LogWarning("直链探活没探成，按可用处理（{Url}）：{Msg}", MaskKey(url), error.Message);
            return true;
        }
    }

    static bool JudgePlayResponse(HttpResponseMessage resp)
    {
        if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return false;
        }
        // 5xx / 429 这类是服务端临时问题，别当成死链把好好的地址换掉
        if ((int)resp.StatusCode is < 200 or >= 300)
        {
            return true;
        }
        string type = resp.Content.Headers.ContentType?.MediaType ?? "";
        if (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || type.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || type.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        long total = TotalBytes(resp);
        return total <= 0 || total >= MinAudioBytes;
    }

    static long TotalBytes(HttpResponseMessage resp)
    {
        if (resp.Content.Headers.ContentRange is { HasLength: true } range && range.Length.HasValue)
        {
            return range.Length.Value;
        }
        return resp.Content.Headers.ContentLength ?? 0;
    }

    /// <summary>详情页地址本来就不是直链，没什么可探的</summary>
    static bool IsPageLink(string url, string jump)
    {
        if (string.Equals(url, jump, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        // 只认带 ? 的详情页：music.163.com/song/media/outer/url 是直链，不能一起放过
        return Regex.IsMatch(url, @"music\.163\.com/song/?\?", RegexOptions.IgnoreCase)
            || url.Contains("y.qq.com/n/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("kugou.com/song", StringComparison.OrdinalIgnoreCase)
            || url.Contains("kuwo.cn/play_detail", StringComparison.OrdinalIgnoreCase)
            || url.Contains("bilibili.com/video", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 播不了就换源：酷我（曲库最全）→ GD Studio（网易云 320k）→ 详情页。
    /// 换源只用歌名与歌手都匹配得上的结果，宁可不换也不换错歌。
    /// </summary>
    async Task<string> RepairPlayUrlAsync(string play, string jump, string song, string singer)
    {
        string current = (play ?? "").Trim();
        if (Configuration.VerifyMusicLink == false || current.Length == 0 || IsPageLink(current, jump))
        {
            return play;
        }
        if (await IsPlayableAsync(current))
        {
            return play;
        }
        logger.LogWarning("播放直链不可用，换源（{Song} - {Singer}）：{Url}", song, singer, MaskKey(current));
        if ((song ?? "").Trim().Length > 0)
        {
            (ArkPublicSong hit, string kuwoPlay) = await KuwoPickAsync(song, singer);
            if (kuwoPlay.Length > 0)
            {
                logger.LogInformation("直链已换成酷我源（{Song} {Id}）", hit.Name, hit.Id);
                return kuwoPlay;
            }
            List<ArkPublicSong> gd = await GdStudioSearchAsync(((singer ?? "") + " " + song).Trim(), 5);
            ArkPublicSong gdHit = gd.FirstOrDefault(candidate =>
                MatchesSong(candidate.Name, candidate.Artist, song, singer ?? ""));
            if (gdHit != null)
            {
                string gdPlay = await GdStudioPlayAsync(gdHit.Id);
                if (gdPlay.Length > 0)
                {
                    logger.LogInformation("直链已换成 GD Studio 源（{Song} {Id}）", gdHit.Name, gdHit.Id);
                    return gdPlay;
                }
            }
        }
        if ((jump ?? "").Length > 0)
        {
            logger.LogInformation("换源没成功，改用详情页当播放地址（{Jump}）", jump);
            return jump;
        }
        return play;
    }
}
