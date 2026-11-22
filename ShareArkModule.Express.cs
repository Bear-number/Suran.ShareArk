using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alife.Function.FunctionCaller;
using Microsoft.Extensions.Logging;

namespace Suran.ShareArk;

/// <summary>
/// 快递查询：快递100 的免费网页接口（免注册、免 key）。
/// autoComNum 能按单号认出是哪家快递，query 拿轨迹；两个都是公开接口。
/// </summary>
public partial class ShareArkModule
{
    [XmlFunction(FunctionMode.OneShot)]
    [Description("查快递物流轨迹：只给单号会自动认是哪家快递，也可以指定 company（如 shunfeng/yuantong/zhongtong/yunda/jd/ems）。返回状态与最近几条轨迹")]
    public async Task ArkExpress(
        [Description("快递单号")] string number,
        [Description("快递公司代码，留空自动识别")] string company = null)
    {
        if (Gate(Configuration.EnableExpress, "快递查询"))
        {
            return;
        }
        string waybill = Regex.Replace((number ?? "").Trim(), @"\s+", "");
        if (waybill.Length < 6)
        {
            throw new Exception("单号太短，检查一下是不是少输了");
        }
        string code = (company ?? "").Trim().ToLowerInvariant();
        string name = CompanyName(code);

        if (code.Length == 0)
        {
            // 认不出公司就没法查，先让 autoComNum 按单号猜
            List<(string Code, string Name)> guesses = await GuessCompaniesAsync(waybill);
            if (guesses.Count == 0)
            {
                throw new Exception("认不出「" + waybill + "」是哪家快递，把 company 填上再查"
                    + "（常用：shunfeng 顺丰 / yuantong 圆通 / zhongtong 中通 / yunda 韵达 / jd 京东 / ems / shentong 申通 / jtexpress 极兔）");
            }
            (code, name) = guesses[0];
            if (guesses.Count > 1)
            {
                logger.LogInformation("单号 {Number} 认出 {Count} 家快递，按第一个 {Code} 查", waybill, guesses.Count, code);
            }
        }

        string url = "https://www.kuaidi100.com/query?type=" + Uri.EscapeDataString(code)
            + "&postid=" + Uri.EscapeDataString(waybill);
        string body = await GetTextWithRefererAsync(url, "https://www.kuaidi100.com/");
        using JsonDocument doc = JsonDocument.Parse(body);
        ExpressReport report = ParseExpress(doc.RootElement, name, waybill);
        if (report.NoResult)
        {
            throw new Exception(report.Text);
        }
        interactor.Poke(report.Text);
    }

    /// <summary>快递100 的响应转成轨迹文本；抽成静态是为了能拿真实响应离线测。</summary>
    static ExpressReport ParseExpress(JsonElement root, string name, string waybill)
    {
        string message = Text(root, "message");
        if (message.Length > 0 && !string.Equals(message, "ok", StringComparison.OrdinalIgnoreCase))
        {
            return new ExpressReport("快递100 回了「" + message + "」，可能是单号不对或该单号查不了（顺丰部分单号要手机号后四位）", true);
        }
        if (!root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
        {
            return new ExpressReport("快递100 没返回轨迹数据", true);
        }

        List<(string Time, string Context)> tracks = new();
        foreach (JsonElement item in data.EnumerateArray())
        {
            string context = Text(item, "context").Trim();
            if (context.Length == 0)
            {
                continue;
            }
            tracks.Add((Text(item, "time"), context));
        }
        bool noResult = tracks.Count == 0 || (tracks.Count == 1 && tracks[0].Context.Contains("查无结果", StringComparison.Ordinal));
        if (noResult)
        {
            return new ExpressReport("没查到「" + waybill + "」的轨迹（" + name + "）。"
                + "可能单号填错、还没揽收，或者这家快递的接口要手机号后四位", true);
        }

        int state = 0;
        if (root.TryGetProperty("state", out JsonElement stateNode) && stateNode.ValueKind == JsonValueKind.String
            && int.TryParse(stateNode.GetString(), out int parsed))
        {
            state = parsed;
        }

        List<string> lines = new()
        {
            name + " " + waybill + " · " + StateText(state)
        };
        // data 是倒序（最新在前），只报最近几条，轨迹很长时全发出来没法看
        foreach ((string time, string context) in tracks.Take(6))
        {
            lines.Add(ShortTime(time) + " " + context);
        }
        if (tracks.Count > 6)
        {
            lines.Add("…共 " + tracks.Count + " 条，上面是最新 6 条");
        }
        return new ExpressReport(string.Join("\n", lines), false);
    }

    sealed record ExpressReport(string Text, bool NoResult);

    async Task<List<(string Code, string Name)>> GuessCompaniesAsync(string waybill)
    {
        List<(string, string)> result = new();
        try
        {
            string body = await GetTextWithRefererAsync(
                "https://www.kuaidi100.com/autonumber/autoComNum?text=" + Uri.EscapeDataString(waybill),
                "https://www.kuaidi100.com/");
            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("auto", out JsonElement auto) && auto.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in auto.EnumerateArray())
                {
                    string code = Text(item, "comCode");
                    if (code.Length > 0)
                    {
                        result.Add((code, Text(item, "name") is string n && n.Length > 0 ? n : CompanyName(code)));
                    }
                }
            }
        }
        catch (Exception error)
        {
            logger.LogWarning("快递100 识别单号失败：{Msg}", error.Message);
        }
        return result;
    }

    /// <summary>state 是快递100 的状态码；没见过的值按「运输中」说，别乱猜成已签收。</summary>
    static string StateText(int state) => state switch
    {
        0 => "在途中",
        1 => "已揽收",
        2 => "疑难件",
        3 => "已签收",
        4 => "已退签",
        5 => "派送中",
        6 => "已退回",
        7 => "转投",
        8 or 10 or 11 or 12 or 13 => "清关中",
        14 => "拒签",
        _ => "运输中"
    };

    static string CompanyName(string code) => code switch
    {
        "shunfeng" => "顺丰速运",
        "yuantong" => "圆通速递",
        "zhongtong" => "中通快递",
        "yunda" => "韵达快递",
        "shentong" => "申通快递",
        "jd" => "京东物流",
        "ems" => "EMS",
        "youzhengguonei" => "邮政快递包裹",
        "debangwuliu" => "德邦快递",
        "huitongkuaidi" => "百世快递",
        "jtexpress" => "极兔速递",
        "zhongtongkuaiyun" => "中通快运",
        "yuantongkuaiyun" => "圆通快运",
        "shunfengkuaiyun" => "顺丰快运",
        "debangkuaidi" => "德邦快递",
        "tiantian" => "天天快递",
        "zhaijisong" => "宅急送",
        "suer" => "速尔快递",
        "quanfengkuaidi" => "全峰快递",
        "guotongkuaidi" => "国通快递",
        "youshuwuliu" => "优速快递",
        "cainiao" => "菜鸟裹裹",
        "amazon" => "亚马逊",
        "dhl" => "DHL",
        "fedex" => "FedEx",
        "ups" => "UPS",
        _ => code.Length > 0 ? code : "快递"
    };

    /// <summary>快递100 的时间是「2026-11-21 14:20:33」，只留月-日 时:分，轨迹列表更好读。</summary>
    static string ShortTime(string time)
    {
        Match match = Regex.Match(time ?? "", @"(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})");
        return match.Success ? match.Groups[2].Value + "-" + match.Groups[3].Value + " " + match.Groups[4].Value + ":" + match.Groups[5].Value : time;
    }
}
