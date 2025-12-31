# MajSoulHelper 文档中心

MajSoulHelper 是一个 BepInEx 插件，用于雀魂(MajSoul)游戏的本地功能增强。

## 📖 文档索引

| 文档 | 说明 |
|------|------|
| [README.md](README.md) | 本文档 - 总览和快速开始 |
| [FEATURES.md](FEATURES.md) | 功能详解 - 所有解锁功能说明 |
| [WEBAPI.md](WEBAPI.md) | Web API - REST接口和控制面板 |
| [TECHNICAL.md](TECHNICAL.md) | 技术文档 - 架构和实现细节 |
| [../patches/README.md](../patches/README.md) | 补丁目录 - Lua补丁参考实现 |

---

## ⚡ 快速开始

### 安装

1. 确保已安装 BepInEx IL2CPP 版本
2. 将 `MajSoulHelper.dll` 放入 `BepInEx/plugins/` 目录
3. 启动游戏

### Web 控制面板

游戏启动后，访问 **http://127.0.0.1:23333/** 打开控制面板

---

## 🔓 核心功能

### 解锁功能（仅本地生效）
- ✅ 所有角色皮肤
- ✅ 所有角色
- ✅ 所有语音（包括等级限制）
- ✅ 所有称号
- ✅ 所有装饰道具（牌桌/牌背等）
- ✅ 装扮方案槽位
- ✅ 所有表情

### 安全特性
- 🛡️ 所有更换请求被拦截，不发送到服务器
- 🛡️ 日志上传默认被阻止
- 🛡️ 原始数据保护机制
- 🛡️ Web 服务器仅监听本地

### 性能设置
- ⚡ 自定义帧率 (30-240 FPS)
- ⚡ 时间倍率调整 (0.5x-4.0x)

---

## ⚙️ 配置项

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| EnableSkinUnlock | true | 解锁所有皮肤 |
| EnableCharacterUnlock | true | 解锁所有角色 |
| EnableVoiceUnlock | true | 解锁所有语音 |
| EnableTitleUnlock | true | 解锁所有称号 |
| EnableItemUnlock | true | 解锁所有道具 |
| EnableViewsUnlock | true | 解锁装扮方案 |
| EnableEmojiUnlock | true | 解锁所有表情 |
| HideLockUI | true | 隐藏锁定图标 |
| EnableInGameSkinReplace | true | 对局中皮肤替换 |
| BlockLogToServer | true | 阻止日志上传 |
| BlockMatchInfo | true | 阻止对局信息上报 |
| EnableBlockedLogDisplay | true | 显示被屏蔽的内容 |
| EnableDebugLog | false | 调试日志 |
| EnableWebServer | true | 启用 Web 服务器 |
| WebServerPort | 23333 | Web 服务器端口 |
| FrameRateBase | 120 | 目标帧率 |
| TargetTimeScale | 1.0 | 时间倍率 |

配置文件位置：`BepInEx/config/MajSoulHelper.json`

---

## ⚠️ 重要说明

### 本地生效
- 所有解锁功能**仅在本地客户端生效**
- 其他玩家看到的是你服务器上实际拥有的皮肤/角色
- 服务器数据保持不变，重新登录会恢复

### 对局中限制
- **自己**可以看到本地设置的皮肤
- **其他玩家**看到的是服务器原始数据
- 战绩/排行榜显示服务器数据

---

## 📁 目录结构

```
BepInEx/
├── plugins/
│   └── MajSoulHelper.dll    # 插件主文件
├── config/
│   └── MajSoulHelper.json   # 配置文件
└── patches/                  # 外部补丁目录（可选）

MajSoulHelper/
├── doc/                      # 文档目录
│   ├── README.md             # 文档索引
│   ├── FEATURES.md           # 功能详解
│   ├── WEBAPI.md             # Web API文档
│   └── TECHNICAL.md          # 技术文档
├── patches/                  # Lua补丁参考实现
│   ├── README.md             # 补丁目录说明
│   ├── config.json           # 配置模板
│   ├── GameUtility.lua       # 物品拥有检查
│   ├── GameMgr.lua           # 角色皮肤数据
│   ├── LobbyNetMgr.lua       # 网络请求拦截
│   ├── DesktopMgr.lua        # 对局皮肤替换
│   ├── Tools.lua             # 语音解锁
│   ├── LogTool.lua           # 日志阻止
│   └── UI_UI_Bag_SkinCell.lua# 皮肤单元格UI
└── leak/                     # 反编译的Lua代码（参考）
```

---

## 🔗 相关链接

- 项目仓库：[GitHub](https://github.com/)
- BepInEx：https://github.com/BepInEx/BepInEx

---

## 📝 更新日志

### v1.0.0
- 初始版本
- 皮肤/角色/语音解锁
- Web 配置面板
- 网络请求拦截
- 日志上传阻止
