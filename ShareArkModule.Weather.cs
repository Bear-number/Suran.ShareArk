using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Alife.Function.FunctionCaller;

namespace Suran.ShareArk;

/// <summary>
/// 天气卡：wttr.in 的 j1 接口（免注册、免 key，中文城市名可以直接拼进路径）。
/// 它回的天气描述是英文，lang=zh 对 j1 不生效，所以按 WWO 天气码自己映射中文。
/// </summary>
public partial class ShareArkModule
{
    [XmlFunction(FunctionMode.OneShot)]
    [Description("查天气：给中文或英文城市名，返回实况加未来三天预报；给了 targetId 就顺带发一张天气卡")]
    public async Task ArkWeather(
        [Description("城市名，中文英文都行，例如 上海 / Tokyo；留空用配置里的默认城市")] string city = null,
        [Description("目标群号或QQ号，填了才发卡片")] long? targetId = null,
        [Description("group或private，默认group")] string type = null)
    {
        if (Gate(Configuration.EnableWeather, "天气"))
        {
            return;
        }
        string place = (city ?? "").Trim();
        if (place.Length == 0)
        {
            place = (Configuration.WeatherDefaultCity ?? "").Trim();
        }
        if (place.Length == 0)
        {
            place = "北京";
        }

        string url = "https://wttr.in/" + Uri.EscapeDataString(place) + "?format=j1&lang=zh";
        string body = await GetStringAsync(url, "wttr.in");
        using JsonDocument doc = JsonDocument.Parse(body);
        WeatherReport report = ParseWeather(doc.RootElement, place);
        interactor.Poke(report.Text);

        if (targetId != null)
        {
            await SendShareCardCoreAsync(url, report.CardTitle, report.CardDesc, report.Preview,
                "[天气]" + report.CardTitle, targetId, type, allowBili: true);
        }
    }

    /// <summary>wttr j1 转成「给人看的文本 + 卡片三件套」；抽成静态是为了能拿真实响应离线测。</summary>
    static WeatherReport ParseWeather(JsonElement root, string place)
    {
        if (!root.TryGetProperty("current_condition", out JsonElement currentList)
            || currentList.ValueKind != JsonValueKind.Array || currentList.GetArrayLength() == 0
            || !root.TryGetProperty("weather", out JsonElement days)
            || days.ValueKind != JsonValueKind.Array || days.GetArrayLength() == 0)
        {
            throw new Exception("天气接口没返回数据，城市名可能没认出来（换个写法，比如「北京」或「Tokyo」）");
        }

        JsonElement current = currentList[0];
        string shown = NearestPlace(root, place);
        string temp = Text(current, "temp_C");
        string feels = Text(current, "FeelsLikeC");
        string nowText = WeatherText(Text(current, "weatherCode"), FirstValue(current, "weatherDesc"));
        string humidity = Text(current, "humidity");
        string wind = Text(current, "windspeedKmph");
        string windDir = WindDir(Text(current, "winddir16Point"));
        string uv = Text(current, "uvIndex");

        List<string> lines = new()
        {
            shown + " · " + temp + "°C " + nowText + "（体感 " + feels + "°C）"
        };
        int index = 0;
        foreach (JsonElement day in days.EnumerateArray())
        {
            if (index > 2)
            {
                break;
            }
            string dayText = DayDesc(day);
            string label = index switch { 0 => "今天", 1 => "明天", 2 => "后天", _ => "" };
            lines.Add(label + " " + DayLabel(Text(day, "date")) + "：" + Text(day, "mintempC") + "~" + Text(day, "maxtempC") + "°C"
                + (dayText.Length > 0 ? "，" + dayText : ""));
            index++;
        }
        List<string> extra = new() { "湿度 " + humidity + "%" };
        if (wind.Length > 0)
        {
            extra.Add((windDir.Length > 0 ? windDir + "风 " : "风速 ") + wind + "km/h");
        }
        if (uv.Length > 0)
        {
            extra.Add("紫外线 " + uv);
        }
        lines.Add(string.Join(" · ", extra));

        string cardTitle = shown + " " + temp + "°C " + nowText;
        List<string> descParts = new() { "体感 " + feels + "°C" };
        string todayText = DayDesc(days[0]);
        if (todayText.Length > 0)
        {
            descParts.Add("今天 " + todayText);
        }
        if (humidity.Length > 0)
        {
            descParts.Add("湿度 " + humidity + "%");
        }
        // wttr 的 png 图直接当封面；中文城市名取图偶尔 500，加载不出来只是没图，不影响卡片
        string preview = "https://wttr.in/" + Uri.EscapeDataString(place) + ".png";
        return new WeatherReport(string.Join("\n", lines), cardTitle, string.Join(" · ", descParts), preview);
    }

    sealed record WeatherReport(string Text, string CardTitle, string CardDesc, string Preview);

    /// <summary>wttr 的 nearest_area 有时给的是英文区名（Shanghai → Pootung），优先用用户给的名字。</summary>
    static string NearestPlace(JsonElement root, string fallback)
    {
        if (root.TryGetProperty("nearest_area", out JsonElement areas) && areas.ValueKind == JsonValueKind.Array
            && areas.GetArrayLength() > 0 && areas[0].TryGetProperty("areaName", out JsonElement names)
            && names.ValueKind == JsonValueKind.Array && names.GetArrayLength() > 0)
        {
            string name = FirstValue(names[0], "value");
            // 英文区名比用户给的中文名难认，只有用户给的是英文时才用它
            if (name.Length > 0 && fallback.All(c => c < 128))
            {
                return name;
            }
        }
        return fallback;
    }

    static string DayDesc(JsonElement day)
    {
        // 取正午那一档当代表：00 点是夜里，天气描述对白天没参考价值
        if (!day.TryGetProperty("hourly", out JsonElement hours) || hours.ValueKind != JsonValueKind.Array)
        {
            return "";
        }
        JsonElement pick = default;
        foreach (JsonElement hour in hours.EnumerateArray())
        {
            if (Text(hour, "time") == "1200")
            {
                pick = hour;
                break;
            }
        }
        if (pick.ValueKind != JsonValueKind.Object && hours.GetArrayLength() > 0)
        {
            pick = hours[hours.GetArrayLength() / 2];
        }
        if (pick.ValueKind != JsonValueKind.Object)
        {
            return "";
        }
        string desc = WeatherText(Text(pick, "weatherCode"), FirstValue(pick, "weatherDesc"));
        string rain = Text(pick, "chanceofrain");
        if (rain.Length > 0 && rain != "0")
        {
            desc += "，降水概率 " + rain + "%";
        }
        return desc;
    }

    static string DayLabel(string date)
    {
        if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            string[] week = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];
            return parsed.ToString("MM-dd", CultureInfo.InvariantCulture) + " " + week[(int)parsed.DayOfWeek];
        }
        return date;
    }

    static string FirstValue(JsonElement parent, string arrayName)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(arrayName, out JsonElement array)
            && array.ValueKind == JsonValueKind.Array && array.GetArrayLength() > 0)
        {
            return Text(array[0], "value");
        }
        return "";
    }

    static string WindDir(string point) => point switch
    {
        "N" => "北", "NNE" or "NE" or "ENE" => "东北", "E" => "东",
        "ESE" or "SE" or "SSE" => "东南", "S" => "南",
        "SSW" or "SW" or "WSW" => "西南", "W" => "西",
        "WNW" or "NW" or "NNW" => "西北",
        _ => point
    };

    /// <summary>WWO 天气码转中文；表里没有的码退回接口给的英文描述。</summary>
    static string WeatherText(string code, string fallback)
    {
        string text = code switch
        {
            "113" => "晴",
            "116" => "多云",
            "119" => "阴",
            "122" => "阴",
            "143" => "薄雾",
            "176" => "零星阵雨",
            "179" => "零星阵雪",
            "182" => "雨夹雪",
            "185" => "冻毛毛雨",
            "200" => "雷阵雨",
            "227" => "吹雪",
            "230" => "暴风雪",
            "248" => "雾",
            "260" => "冻雾",
            "263" or "266" => "毛毛雨",
            "281" or "284" => "冻毛毛雨",
            "293" => "小雨",
            "296" => "小雨",
            "299" => "中雨",
            "302" => "中雨",
            "305" => "大雨",
            "308" => "大雨",
            "311" or "314" => "冻雨",
            "317" or "320" => "雨夹雪",
            "323" => "小雪",
            "326" => "小雪",
            "329" => "中雪",
            "332" => "中雪",
            "335" => "大雪",
            "338" => "大雪",
            "350" => "冰粒",
            "353" => "小阵雨",
            "356" => "中阵雨",
            "359" => "暴雨",
            "362" or "365" => "阵雨夹雪",
            "368" => "小阵雪",
            "371" => "中阵雪",
            "374" or "377" => "冰粒",
            "386" => "雷阵雨",
            "389" => "强雷阵雨",
            "392" => "雷阵雪",
            "395" => "强雷阵雪",
            _ => ""
        };
        return text.Length > 0 ? text : (fallback.Length > 0 ? fallback : "未知");
    }
}
