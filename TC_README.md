# XIVLauncher TC (Taiwan) Version

## 概述

此版本是針對台灣地區 FFXIV 玩家的特殊修改版本，包含台灣伺服器支援和 reCAPTCHA 驗證整合。

## 主要功能

### 台灣伺服器支援
- 支援台灣地區登入 API
- 台灣補丁伺服器整合
- 台灣遊戲大廳和伺服器連線
- 支援 Email 登入方式

### reCAPTCHA 驗證
- 整合 Google reCAPTCHA 驗證
- WebView2 控制項載入驗證頁面
- 虛擬主機映射技術繞過域名限制
- 自動 token 獲取和傳遞

### 遊戲啟動優化
- 台灣地區遊戲參數配置
- Session ID 交換機制
- 優化網路連線設定

## 建置說明

### 關閉自動更新建置
```bash
# 使用 ReleaseNoUpdate 配置（推薦）
dotnet build src\XIVLauncher\XIVLauncher.csproj -c ReleaseNoUpdate

# 或手動指定條件編譯符號
dotnet build src\XIVLauncher\XIVLauncher.csproj -c Release -p:DefineConstants="TRACE;XL_NOAUTOUPDATE"
```

### 一般建置
```bash
dotnet build src\XIVLauncher\XIVLauncher.csproj -c Release
```

## 設定說明

### reCAPTCHA 設定
在使用前需要設定 `recaptcha_page.html` 中的 SITE_KEY。

## 已知問題

1. reCAPTCHA 需要有效的 SITE_KEY 才能運作
2. WebView2 運行時需要在系統上安裝

## 相容性

- .NET 9.0
- Windows 平台
- WebView2 Runtime

## TODO

### 高優先級
- [ ] 最新消息版面嵌入 [參考連結](https://user-cdn.ffxiv.com.tw/news/251115/launcher_left.html)
    - [ ] 考慮調整CSS讓風格一致化。
- [ ] boot更新流程
- [ ] Launcher更新流程
- [ ] 功能驗證：
    - [ ] Dalamud注入驗證 (現在版本還不對)