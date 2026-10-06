
# Suran.ShareArk

QQ 分享卡片插件（音乐卡 + 通用链接卡），签名由插件自己完成，直塞协议端 ws，不依赖协议端自带的签名地址配置。

## 版本
4.1.0

## 官方容灾（4.1.0 新增）
配置开关「容灾·启用官方接口回退」（默认开）：第三方接口调用失败时**自动切换官方公开接口**，双失败才报错（附双方原因）。
- **音乐卡**：163/qq 走 OneBot 标准音乐段（`{"type":"music","data":{"type":"163"|"qq","id":...}}`，协议端原生生成卡片，歌名自动搜用平台公开接口）；bilibili/kugou 走 custom 音乐段（bilibili 的音频直链取自官方 playurl，kugou 降级为跳转卡）
- **搜索**：163（music.163.com 搜索）、qq（c.y.qq.com smartbox）、kugou（mobilecdn.kugou.com v3）
- **B站系列**：BiliParse（URL解析+view详情+playurl直链）、BiliUserInfo（wbi acc/info，自动WBI签名）、BiliUpdates（动态feed，仅视频投稿）、BiliHot（popular/related）、BiliQrLogin（passport扫码，**Cookie自动存本地**）、BiliCookie（本地管理）
- Cookie 本地保存于 `{存储目录}/ShareArk/bili_cookie.txt`，官方接口自动携带以降低风控
- 通用分享卡无官方替代（Ark签名必须第三方），第三方不可用时明确报错


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
- **功能开关区**：14 个函数各有独立开关，每个开关旁写明功能说明（含对应AI函数名）；默认全开，关闭后AI调用会收到「功能已关闭」提示
- 接口配置表单手写内置；改动后记得点界面底部的保存
- razor UI 不支持热编译：源码工程在 `../ShareArkUI/`（`dotnet build -c Release` 后把 `obj/Release/net10.0/generated/**/ShareArkUI_razor.g.cs` 拷到本插件文件夹即可）
- `../ShareArkUI/PluginCheck/` 可随时整体编译验证插件源码 + g.cs 的组合

## 函数
- ProbeBase() - 探测三个接口通不通（手动地址或自动后缀都会报出来），换新接口先跑一次
- ProbeAll() - 列出基地址下发现的所有接口（openapi/swagger 文档 + 站点首页接口链接，只列举不调用不耗次数），标注插件在用哪些
- SearchMusic(keyword, platform) - 搜歌拿 ID，platform: 163/qq/kugou
- SearchBili(keyword) - 搜 B 站拿 BV 号，配置了 BiliSearchUrl 走第三方，否则走官方
- SendArkCard(platform, id, targetId, type, title) - platform: 163/qq/bilibili/kugou
- SendShareCard(url, title, desc, preview, prompt, targetId, type) - 任意链接转分享卡，标题/描述/预览图自动抓
- IpLookup(ip) - 查IP的地理位置/运营商/ASN/时区，不填查本机出口IP

## B站系列（走 {BaseUrl}/bili-*，需 ApiKey，即咸鱼API哔哩哔哩系列）
- BiliParse(url, action, qn) - 解析B站链接（视频/短链/专栏/番剧/直播间），action=play 返回播放直链，qn 选清晰度 80/64/32/16
- BiliUserInfo(uid) - 查用户：昵称/等级/粉丝/签名/认证/投稿数
- BiliUpdates(uid, type, offset) - 查UP主最新动态/视频/专栏，type: all/video/article，offset 翻页
- BiliHot(action, bvid, pn) - 热门视频榜 popular 或视频相关推荐 related
- BiliQrLogin(action, qrcodeKey) - 扫码登录：generate 出二维码链接给人扫，poll 查状态，成功得 cookie_id
- BiliCookieRefresh(cookieId, action) - 刷新/检查登录Cookie有效期
- BiliCookie(action, cookieId, cookieString) - Cookie管理：list/check/delete/save；故意不开放导出完整Cookie的 get 动作，避免凭据进聊天记录

## 发送回报
成功后回「卡片已发出」/「分享卡已发出」，失败回失败原因。搜索结果纯换行直出，不带前缀。

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
