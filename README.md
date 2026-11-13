# Suran.ShareArk

把链接变成好看的卡片：探测站点、生成 ARK 分享卡，B 站链接自动解析。

## 功能

- 站点探测：单个站点或全部站点探一遍可用性
- 音乐：搜歌、发音乐卡片、取歌词与封面
- 通用分享卡：标题、描述、图标，可指定跳转链接
- 链接元信息抓取
- B 站解析：视频信息、UP 主更新、扫码登录与 cookie 刷新
- IP 归属地查询
- 按平台生成对应卡片

## 安装

先装依赖：`Alife.Function.FunctionCaller`。

装法二选一：插件市场里搜 `Suran.ShareArk`；或把目录放到 `Storage/Plugins/Suran.ShareArk/`，重启角色生效。

## 配置

在设置页里改，改完重启角色生效。

| 设置项 | 键 | 说明 | 默认 |
| --- | --- | --- | --- |
| 运行·功能文档常驻 | `DocumentAlwaysResident` | 开：功能说明常驻系统提示词，模型随时看得到工具；关：隐式加载省 token，模型先输出入口标签再读文档。发卡片/解析链接多是主人明确要求时用，默认关 | `false` |
| 第三方接口基地址 | `BaseUrl` | 第三方签名服务基地址。 | `""` |
| 接口Token | `ApiKey` | 第三方接口凭证 key，可留空。 | `""` |
| 网络·代理地址 | `ProxyUrl` | 出站请求走这个代理，支持 http:// 与 socks5://；留空直连。境外平台（YouTube/Spotify/Apple Music/X）抓不到时填这里，改完不用重载插件 | `""` |
| 网络·仅境外平台走代理 | `ProxyForeignOnly` | 开：只有 YouTube/Spotify/Apple Music/SoundCloud/Vimeo/TikTok/X/Instagram/GitHub/Steam 这类境外站走代理，国内接口与签名服务直连；关：全部出站请求都走代理 | `true` |
| 协议端·地址 | `WsUrl` | 留空 = 自动跟随「QQ 聊天」插件的配置（推荐）。填了就用这里的，例如 ws://127.0.0.1:3001 | `""` |
| 协议端·访问令牌 | `OneBotToken` | 与协议端配置的 token 一致。留空 = 跟随「QQ 聊天」插件。填了地址却没填这里，协议端一开 token 就会连不上 | `""` |
| 音乐卡签名接口地址 | `MusicArkUrl` | 完整地址，可留空；留空时用基地址自动探测后缀拼出 | `""` |
| 分享卡签名接口地址 | `ShareArkUrl` | 完整地址，可留空；留空时用基地址自动探测后缀拼出 | `""` |
| 歌曲搜索接口地址 | `SearchUrl` | 完整地址，可留空；留空时用基地址自动探测后缀拼出。可含{pf}占位符，会被替换为163/qq/kugou | `""` |
| B站搜索接口地址 | `BiliSearchUrl` | B站视频搜索接口完整地址，留空走官方接口。 | `""` |
| 容灾·签名失败冷却秒 | `SignFailureCooldownSeconds` | 签名接口报错后，这么多秒内不再重试（直接走备用方案），避免每次点歌都白等三四秒。0 = 每次都试 | `300` |
| 容灾·启用官方接口回退 | `EnableOfficialFallback` | 第三方接口失败时是否自动改用官方接口。 | `true` |
| 容灾·备用签名基地址 | `BackupBaseUrls` | 第三方接口失败时是否自动改用官方接口。 | `""` |
| 容灾·启用公共音乐卡签名 | `EnableBuiltinMusicSign` | 主站与备用站都失败时，音乐卡再试 OIAPI 公共签名（免 key，实测可用）。分享卡没有公共备用，仍走图文兜底。 | `true` |
| 容灾·公共音乐卡签名地址 | `BuiltinMusicArkUrl` | 默认 OIAPI 的 QQMusicJSONArk 接口；换服务时只改这一行。 | `"https://oiapi.net/api/QQMusicJSONArk"` |
| 容灾·额外音乐卡签名地址 | `ExtraMusicSignUrls` | 完整地址，一行一个，可写 url\|key。适合 OIAPI 风格的第三方音乐卡签名（GET，参数 url/song/singer/cover/jump/format，format 用 163，成功码 0/200/1 都认）。 | `""` |
| 容灾·额外分享卡签名地址 | `ExtraShareSignUrls` | 完整地址，一行一个，可写 url\|key。用于路径不标准的第三方分享卡签名（GET，参数 title/desc/url/preview/prompt，成功码 0/200）。 | `""` |
| 容灾·启用公共音乐平台接口 | `EnablePublicMusicApis` | 免费、无需注册：洛苏API（网易云/QQ/酷狗点歌，详情带直链与封面）与糖豆子API（QQ 搜索）。官方音乐接口失败时自动补位。 | `true` |
| 容灾·洛苏API基地址 | `LuoSuBaseUrl` | 免费公共点歌接口，默认 https://api.luosu.top；换镜像只改这里。 | `"https://api.luosu.top"` |
| 容灾·糖豆子API基地址 | `TangDouZiBaseUrl` | 免费公共 QQ 音乐搜索接口，默认 https://api.tangdouz.com；换镜像只改这里。 | `"https://api.tangdouz.com"` |
| 开关·搜歌 | `EnableSearchMusic` | 关闭后 SearchMusic 不可用 | `true` |
| 开关·发音乐/视频卡 | `EnableSendArkCard` | 关闭后 SendArkCard 不可用 | `true` |
| 开关·发通用分享卡 | `EnableSendShareCard` | 关闭后 SendShareCard 不可用 | `true` |
| 开关·B站链接解析 | `EnableBiliParse` | 关闭后 BiliParse 不可用 | `true` |
| 开关·UP主更新查询 | `EnableBiliUpdates` | 关闭后 BiliUpdates 不可用 | `true` |
| 开关·B站扫码登录 | `EnableBiliQrLogin` | 关闭后 BiliQrLogin 不可用 | `true` |
| 开关·Cookie刷新 | `EnableBiliCookieRefresh` | 关闭后 BiliCookieRefresh 不可用 | `true` |
| 开关·Cookie管理 | `EnableBiliCookie` | 关闭后 BiliCookie 不可用 | `true` |
| 开关·IP查询 | `EnableIpLookup` | 关闭后 IpLookup 不可用 | `true` |
| 开关·探测接口 | `EnableProbeBase` | 关闭后 ProbeBase 不可用 | `true` |
| 开关·列出全部接口 | `EnableProbeAll` | 关闭后 ProbeAll 不可用 | `true` |
| 开关·免费公开接口 | `EnableFreeSources` | 歌词走 LRCLIB 与网易云官方、封面与试听走 iTunes、通用链接卡自己抓页面 meta 解析；全部免注册免 key | `true` |
| 开关·允许 microlink 兜底 | `EnableMicrolink` | 自抓标题失败时是否用 microlink 解析，默认关。 | `false` |
| 免费接口超时（秒） | `FreeSourceTimeoutSeconds` | 单个免费接口的超时；国际站直连经常不通，超时给短点别拖着整条链 | `12` |
