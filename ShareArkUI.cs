using System;
using System.Threading.Tasks;
using Alife.Framework;
using AntDesign;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Suran.ShareArk;

/// <summary>
/// 分享卡的设置页。
///
/// 原来是一份 1666 行的 razor 生成代码：23 个配置项逐个手写 FormItem，
/// 加一个字段就得再插一段，而且每次都得对着一堆 #line 指令改。
/// 现在配置部分交给 SuranUiKit 按 `分组·名称` 自动生成，
/// 这里只留真正属于本插件的两件事——探测接口通不通，以及把报告显示出来。
/// </summary>
public sealed class ShareArkUI : ModuleUIBase<ShareArkModule, ShareArkConfig>
{
    [Inject]
    public IMessageService MessageService { get; set; } = default!;

    bool probingBase;
    bool probingAll;
    string? report;

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        if (Configuration == null)
        {
            b.AddContent(0, "配置未加载");
            return;
        }

        SuranUiKit.Render(this, StateHasChanged, b, Configuration,
            "QQ 分享卡片",
            "自己完成音乐卡与分享卡签名并直塞协议端，不依赖协议端的签名地址配置",
            actions:
            [
                new SuranUiKit.UiAction(probingBase ? "探测中…" : "探测基地址", RunProbeBase),
                new SuranUiKit.UiAction(probingAll ? "列举中…" : "全部探测", RunProbeAll),
            ],
            notice: report);
    }

    /// <summary>没填任何接口地址就探测，只会得到一堆"连不上"，先挡住</summary>
    bool HasAnyApiConfig() =>
        !string.IsNullOrWhiteSpace(Configuration.BaseUrl)
        || !string.IsNullOrWhiteSpace(Configuration.MusicArkUrl)
        || !string.IsNullOrWhiteSpace(Configuration.ShareArkUrl)
        || !string.IsNullOrWhiteSpace(Configuration.SearchUrl)
        || !string.IsNullOrWhiteSpace(Configuration.BackupBaseUrls)
        || !string.IsNullOrWhiteSpace(Configuration.ExtraMusicSignUrls)
        || !string.IsNullOrWhiteSpace(Configuration.ExtraShareSignUrls)
        || Configuration.EnablePublicMusicApis
        || (Configuration.EnableBuiltinMusicSign && !string.IsNullOrWhiteSpace(Configuration.BuiltinMusicArkUrl));

    async Task RunProbeBase()
    {
        if (Module == null)
        {
            await MessageService.WarningAsync("角色激活后才能探测喵！");
            return;
        }
        if (!HasAnyApiConfig())
        {
            await MessageService.WarningAsync("请先填写第三方接口基地址或手动接口地址喵！");
            return;
        }

        probingBase = true;
        try
        {
            report = await Module.ProbeBaseReportAsync();
            await MessageService.SuccessAsync("探测完成喵！");
        }
        catch (Exception ex)
        {
            report = ex.Message;
            await MessageService.ErrorAsync("探测失败: " + ex.Message);
        }
        finally
        {
            probingBase = false;
            StateHasChanged();
        }
    }

    async Task RunProbeAll()
    {
        if (Module == null)
        {
            await MessageService.WarningAsync("角色激活后才能探测喵！");
            return;
        }
        if (string.IsNullOrWhiteSpace(Configuration.BaseUrl))
        {
            await MessageService.WarningAsync("列出全部接口需要先填写第三方接口基地址喵！");
            return;
        }

        probingAll = true;
        try
        {
            report = await Module.ProbeAllReportAsync();
            await MessageService.SuccessAsync("列举完成喵！");
        }
        catch (Exception ex)
        {
            report = ex.Message;
            await MessageService.ErrorAsync("列举失败: " + ex.Message);
        }
        finally
        {
            probingAll = false;
            StateHasChanged();
        }
    }
}
