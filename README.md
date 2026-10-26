
# Suran.ShareArk

QQ 分享卡片插件（音乐卡 + 通用链接卡），签名由插件自己完成，直塞协议端 ws，不依赖协议端自带的签名地址配置。

## 版本
5.1.0（对应 Alife 客户端 5.0.x）

## 5.1.0 的改动
- 接入一批**免注册免 key 的公开接口**做歌词/封面/链接素材来源（`ArkLyric` / `ArkCover` / `ArkLinkMeta` / `ArkFreeProbe`），详见下面「免费公开接口」
- 模块拆成分部类：原有卡片签名与发送逻辑还在 `ShareArkModule.cs`，公开接口那层在 `ArkFreeSources.cs`（纯解析）+ `ShareArkModule.FreeApi.cs`（工具与取数）
- **修：共享 HttpClient 现在开自动解压**。实测 B站视频页即使请求不带 `Accept-Encoding` 也照样回 `Content-Encoding: gzip`，之前 `FetchMetaAsync` 读到的是压缩字节，`og:`/`<title>` 一个也匹配不到——也就是发B站链接卡时自动补标题/预览图一直是失败的（网易云这类回 identity 的站才碰巧能用）
- **修：抓网页正文改成按字节读 + UTF-8 解码**（`charset=UTF-8` 这种大写 charset 会让 `ReadAsStringAsync()` 直接抛）
- **修：QQ 官方容灾搜索的字段名**。`c.y.qq.com` 的 smartbox 实测 `data.song.itemlist[]` 给的是 `mid`/`name`，插件原来读 `songmid`/`songname`，取到空 id 会让后面的详情与签名全废；现在 `mid`/`name` 优先、旧写法兜底
- kugou 搜索接口（`mobilecdn.kugou.com` v3）本机连不上时不再抛裸异常，改报「这个接口在当前网络连不上，搜歌先换 163 或 qq」

## 5.0.0 的改动
- 版本号对齐客户端主版本（官方约定：插件版本首位应与客户端一致，`PluginSystem.IsClientCompatible` 按主版本判定）
- 只走 OneBot WS 与公开 HTTP 接口，未涉及平台 API；本版只做版本对齐与在役客户端装载确认

## 官方容灾（4.1.0 新增）
配置开关「容灾·启用官方接口回退」（默认开）：第三方接口调用失败时**自动切换官方公开接口**，双失败才报错（附双方原因）。
- **音乐卡**：163/qq 走 OneBot 标准音乐段（`{"type":"music","data":{"type":"163"|"qq","id":...}}`，协议端原生生成卡片，歌名自动搜用平台公开接口）；bilibili/kugou 走 custom 音乐段（bilibili 的音频直链取自官方 playurl，kugou 降级为跳转卡）
- **搜索**：163（music.163.com 搜索）、qq（c.y.qq.com smartbox）、kugou（mobilecdn.kugou.com v3）
- **B站系列**：BiliParse（URL解析+view详情+playurl直链）、BiliUserInfo（wbi acc/info，自动WBI签名）、BiliUpdates（动态feed，仅视频投稿）、BiliHot（popular/related）、BiliQrLogin（passport扫码，**Cookie自动存本地**）、BiliCookie（本地管理）
- Cookie 本地保存于 `{存储目录}/ShareArk/bili_cookie.txt`，官方接口自动携带以降低风控
- 通用分享卡无官方替代（Ark签名必须第三方），第三方不可用时明确报错


## 免费公开接口（5.1.0）
不要 key、不要注册、不要 token 的一批公开接口，用来给卡片补素材。**下面每一行都是本机 .NET HttpClient 实打的结果，不是 curl、不是文档**（`ArkFreeProbe` 随时能重跑一遍）。

| 来源 | 用途 | 本机实测 | 要注意的 |
|---|---|---|---|
| LRCLIB `api/search`、`api/get` | 带时间轴歌词 | 通（搜索一条命中 44 行） | 给 `duration`（秒）才走精确取词，命中率明显高于纯搜索；只认「歌手 歌名」 |
| 网易云官方 `music.163.com/api/song/lyric` | 歌词正文 | 通（命中 5 行） | 必须带 `os=pc&lv=-1&kv=-1&tv=-1`；`lv=1` 实测回空 |
| iTunes `itunes.apple.com/search` | 封面大图 + 30 秒试听 + 时长 | 通（2 条） | **地址里不能带 `entity=song`**，带了中文区实测零结果；`artworkUrl100` 的 `100x100` 换成 `600x600` 就是大图 |
| 自抓页面 meta | 任意链接的标题/描述/预览图 | 通（网易云歌曲页拿到标题与封面） | 自己下载页面解析，不经任何第三方；链是 `og:` → `twitter:` → `itemprop` → `<title>` 逐层退，B站视频页这类没有 `og:` 的靠后面的退路 |
| microlink `api.microlink.io` | 自抓拿不到标题时的兜底 | 通 | **默认关**。它等于把「你在查哪个链接」交给外部服务，所以要在配置里显式打开才会用 |
| Deezer | 曾试过 | 直连不通（000），经代理时好时坏 | 已从兜底链里摘掉，源码里没有它 |
| open-meteo | 天气 | 通 | 与本插件无关，只是同批测过的公开接口 |

踩到的坑（都已按实测改掉，别再改回去）：
- 网易云回的 `Content-Type: text/html; charset=UTF-8` 大写 charset，.NET 的 `ReadAsStringAsync()` 认不下会**直接抛异常**，改成按字节读回来自己 UTF-8 解码
- iTunes 那个 `entity` 参数是隐性杀手，加上就零结果，报错也看不出来

歌词全文会另存一份到 `{存储目录}/ShareArk/lyrics/*.lrc`（拿去做字幕或朗读方便），写盘失败只回报「没写盘」，不影响歌词照回。

## 接口实测（2026-10-26，本机 .NET 直跑）
除「免费公开接口」外，签名/搜索/容灾三条链逐条打了一遍：**31 通 / 5 不通**，不通的五个都不是插件的锅：
- `bili-url-parse`、`bili-user-update`：签名服务上游回 `code=500`（「详情获取失败」「上游接口无响应」）——**官方容灾顶上**：`view`+`playurl`、`web-dynamic/v1/feed/space` 实测都通，所以 BiliParse / BiliUpdates 对用户仍可用
- `openapi.json`、`/docs`：签名服务这两个路径都是 404，没有文档可读；`ProbeAll` 的文档发现这条路是空的，只能靠基地址首页里挖接口链接
- 酷狗 `mobilecdn.kugou.com` v3 搜索：本机 TLS 握手就失败（`The SSL connection could not be established`），换代理也一样；`kugou-music-search`（签名服务）是通的，酷狗歌曲页也可达，所以酷狗只剩「跳转卡」这一条官方容灾形态
- 通的部分：`qq-musicArk` / `qq-shareArk` 签名、三平台搜索 `163-|qq-|kugou-music-search`、`bili-user-info`、`bili-hot-recommend`（popular 与 related）、`ip-lookup`、B站官方那套（`nav` 取 WBI 密钥、`view`、`playurl`、`wbi/acc/info`、`relation/stat`、`feed/space`、`popular`、`archive/related`、`search/all/v2`）、网易云（搜索/详情/外链 mp3）、QQ（smartbox→单曲详情→封面图整条链）、以及协议端 `get_version_info`/`get_login_info`
- 候选后缀 `qq_musicark`/`musicArk`/`music-card`/`qq_shareArk`/`shareArk`/`share-card` 都回 404，只有默认写法与 `qq-shareark`（全小写）可用——`Fit` 会自动跳过 404，这是预期行为
- **没测的**：真正往 QQ 发卡（要指定目标，且发出去就是公开可见）、`bili-qrcode-login` 出码与轮询、`bili-cookie-refresh`、`bili-user-cookie` 的存删

## 依赖
- Alife.Function.FunctionCaller

## 配置
| 键 | 说明 |
|---|---|
| BaseUrl | 第三方签名服务基地址，只需填这一个，音乐卡/分享卡/歌曲搜索地址自动适配 |
| MusicArkUrl | 音乐卡签名接口完整地址，可留空，留空走基地址自动探测 |
| ShareArkUrl | 分享卡签名接口完整地址，可留空，留空走基地址自动探测 |
| SearchUrl | 歌曲搜索接口完整地址，可留空；可含 {pf} 占位符（替换为 163/qq/kugou），不带占位符则三个平台共用同一地址 |
| BiliSearchUrl | B站视频搜索接口完整地址，可留空，留空走B站官方接口 |
| ApiKey | 第三方接口 Token，可留空（对方只要地址不要 key 时留空即可，插件不会带 key 参数）；以咸鱼API为例，注册/拿key：https://apii.xianyuw.cn |
| WsUrl | 协议端 ws，默认 ws://127.0.0.1:3001 |
| EnableFreeSources | 开关·免费公开接口，默认开；关掉后四个免费接口工具直接报「开关是关的」 |
| EnableMicrolink | 开关·允许 microlink 兜底，**默认关**；开着才会把要发的链接交给外部解析 |
| FreeSourceTimeoutSeconds | 免费接口超时（秒），默认 12（限 3~90） |

只填基地址就能用。首次调用时插件会拿默认后缀探一遍，不通就换候选后缀，试通的记住不再试：
- `{BaseUrl}/qq-musicArk` 音乐卡签名
- `{BaseUrl}/qq-shareArk` 通用链接卡签名
- `{BaseUrl}/163-music-search` 歌曲搜索（163/qq/kugou）

第三方后缀不一样也能自动认出：先试内置常见写法，还认不出的角色会去基地址首页和 openapi/swagger 文档里把接口路径挖出来再试一轮（按 share/search/music-ark 关键词归位），命中的直接用，所以后缀起得再非主流一般也能认出来。全都认不出才回落默认值，靠 ProbeBase 报出来；这时把三个完整接口地址填进 MusicArkUrl / ShareArkUrl / SearchUrl 即可，填了的手动地址不再探测，自带 query 参数也会自动用 & 接上。
自动认的前提是接口风格一致：GET 请求、key/msg/title 这类参数、返回 {code, data} 结构。协议完全不同的服务（POST、参数名不同、返回结构不同）没法自动适配，手动填地址也只对得上参数兼容的那种。音乐平台仍限 163/qq/kugou/bilibili。

## 示例：咸鱼API（apii.xianyuw.cn）

| 键 | 填法 |
|---|---|
| BaseUrl | https://apii.xianyuw.cn/api/v1 |
| ApiKey | 平台注册拿的 Token（搜索接口需 Token），注册/获取key：https://apii.xianyuw.cn |
| BiliSearchUrl | https://apii.xianyuw.cn/api/bili-video-search（可选，B站搜索走第三方，默认走官方） |

该站接口路径与默认后缀一致（/api/v1/qq-musicArk、/api/v1/qq-shareArk、/api/v1/163-music-search），填 BaseUrl 和 ApiKey 开箱即用，无需探测。

## 配置 UI
- 模块带自定义配置界面（ShareArkUI）：顶部「探测接口」「列出全部接口」两个按钮，直接调运行中的模块，结果显示在按钮下方
- **功能开关区**：17 个开关，每个开关旁写明功能说明（含对应AI函数名）；函数级开关默认全开，关闭后AI调用会收到「功能已关闭」提示；`EnableFreeSources` 是四个免费接口工具的总闸（默认开），`EnableMicrolink` 默认关
- 接口配置表单手写内置；改动后记得点界面底部的保存
- razor UI 不支持热编译：源码工程在 `../ShareArkUI/`（`dotnet build -c Release` 后把 `obj/Release/net10.0/generated/**/ShareArkUI_razor.g.cs` 拷到本插件文件夹即可）；这两个工程 5.1.0 起都改引在役客户端自带的程序集，不再指已删除的 AlifeSrc 源码树
- `../ShareArkUI/PluginCheck/` 可随时整体编译验证插件源码 + g.cs 的组合（与宿主热编译同一套引用环境）

## 函数
- ProbeBase() - 探测三个接口通不通（手动地址或自动后缀都会报出来），换新接口先跑一次
- ProbeAll() - 列出基地址下发现的所有接口（openapi/swagger 文档 + 站点首页接口链接，只列举不调用不耗次数），标注插件在用哪些
- SearchMusic(keyword, platform) - 搜歌拿 ID，platform: 163/qq/kugou
- SearchBili(keyword) - 搜 B 站拿 BV 号，配置了 BiliSearchUrl 走第三方，否则走官方
- SendArkCard(platform, id, targetId, type, title) - platform: 163/qq/bilibili/kugou
- SendShareCard(url, title, desc, preview, prompt, targetId, type) - 任意链接转分享卡，标题/描述/预览图自动抓
- IpLookup(ip) - 查IP的地理位置/运营商/ASN/时区，不填查本机出口IP
- ArkLyric(song, artist, duration, neteaseId) - 取歌词：给了网易云 id（或 song 就是 id/链接）走网易云官方，否则走 LRCLIB；带 duration 秒数提高命中；全文另存本地 lrc
- ArkCover(keyword, take) - iTunes 找封面大图与 30 秒试听，顺带告诉你时长
- ArkLinkMeta(url) - 自己下载页面抓标题/描述/预览图（不经第三方），拿来喂 SendShareCard
- ArkFreeProbe() - 免费接口体检，逐项报可用性与失败原因；怀疑哪个源挂了先跑这个

## B站系列（走 {BaseUrl}/bili-*，需 ApiKey，即咸鱼API哔哩哔哩系列）
- BiliParse(url, action, qn) - 解析B站链接（视频/短链/专栏/番剧/直播间），action=play 返回播放直链，qn 选清晰度 80/64/32/16
- BiliUserInfo(uid) - 查用户：昵称/等级/粉丝/签名/认证/投稿数
- BiliUpdates(uid, type, offset) - 查UP主最新动态/视频/专栏，type: all/video/article，offset 翻页
- BiliHot(action, bvid, pn) - 热门视频榜 popular 或视频相关推荐 related
- BiliQrLogin(action, qrcodeKey) - 扫码登录：generate 出二维码链接给人扫，poll 查状态，成功得 cookie_id
- BiliCookieRefresh(cookieId, action) - 刷新/检查登录Cookie有效期
- BiliCookie(action, cookieId, cookieString) - Cookie管理：list/check/delete/save；故意不开放导出完整Cookie的 get 动作，避免凭据进聊天记录

## 发送回报
成功后回「卡片已发出」/「分享卡已发出」，失败回失败原因（带协议端署名）。搜索结果纯换行直出，不带前缀。

## 三端兼容（NapCat / LLOneBot / SnowLuma）
三端对消息段的处理完全不同，卡片"发成功却看不见"就是这么来的，现已逐端核对源码处理：
- 首次发卡前用 `get_version_info` 识别协议端（三端都回 `app_name`：`NapCat.Onebot` / `LLOneBot` / `SnowLuma`），失败原因都带上协议端署名
- **未知消息段**：NapCat 抛「未知的消息类型」回 retcode 1200；SnowLuma 原子拒绝整条消息回 1400（`MessageElementValidationError(UNKNOWN_TYPE)`）；LLOneBot 本机 7.12.14 实测也回 **1200 并整条原子退回**（`消息体无法解析，请检查是否发送了不支持的消息类型`，早先按源码 switch 无 default 推断的「丢掉该段、其余照发、retcode 仍 0」已被实测推翻）。因此成功也校验返回里必须有 `message_id`，没有就报「卡片段很可能被静默丢弃」而不是谎报成功
- 卡片段统一用 `json`（`data.data` 放卡片 JSON 字符串）：三端都认这种写法，NapCat 会自动 stringify、SnowLuma 要求 data 是对象或非空 JSON 字符串
- 容灾音乐段 `music`：平台卡（163/qq）三端都要求 `id`，但 LLOneBot 还需要在它自己的设置里配「音乐卡片签名地址」，未配时返回的错误信息现在会原样报出来；custom 卡补上 SnowLuma 的校验要求（`url`/`audio`/`title` 必须非空，拿不到播放直链时回落跳转地址，否则整条消息被 1400 退回）；SnowLuma 平台卡的签名走它配置里的 `musicSignUrl`（默认 `ss.xingzhige.com`），拿不到就退化成 structmsg 卡
- retcode 1404 报「该协议端未实现此接口」；失败原因同时读 `message` 与 `wording`——SnowLuma 的失败包只填 `wording`，只读 `message` 会让它所有报错都显示成空
- 发送层重构成通用动作调用（连接复用 + echo 配对 + 断线重连一次），`get_version_info` 与发卡走同一条通道

## 自动搜 ID
报歌名就行，插件自己走搜索接口：
- 163：非纯数字 -> 搜索接口取第一条
- qq：id 长度 != 14 -> 取 songmid
- kugou：非 32 位 hex -> 去搜真 FileHash
- bilibili：非 BV 开头 -> 搜视频，关键词逐词匹配标题，全命中才用，否则落第一条

## 封面与预览图
- 抓取链：og:image -> twitter:image -> link rel=icon -> /favicon.ico，抓不到就空着，不留硬编码默认图；签名端要求封面必填，为空时回落抓播放页 og:image
- kugou 封面走搜索详情接口的 cover 字段（含 {size} 时替换为 480），url 优先用详情返回的播放直链
- preview 不收 data URI（传了会被忽略、回落自动抓图），本地图要先传图床；页面抓不到（国外站超时/被墙）时预览图回落站点 favicon，保证非空（签名端空值当缺参）
