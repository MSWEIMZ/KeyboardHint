# 键盘小助手 (KeyboardHint)

仿华为 MateBook Fold 虚拟键盘的"显示快捷键"功能 —— 但给**实体键盘**用。

**按住 Ctrl 不放，屏幕底部弹出一个亚克力质感键盘，键位上直接标注当前应用的 `Ctrl+快捷键` 功能**（C=复制、V=粘贴、S=保存……）。Fluent Design 风格：DWM 背景模糊、原生圆角、上浮淡入动画。按前台进程自动匹配应用，WPS 全家桶优先适配。

![交互示意](docs/screenshot.png)

## 使用方式

1. 启动 `KeyboardHint.exe`（无窗口，驻留系统托盘）
2. 在任意应用中**按住 Ctrl 约 0.4 秒** → 底部弹出快捷键键盘
3. **保持 Ctrl 不松，加按 Shift 或 Alt** → 切换到 `Ctrl+Shift` / `Ctrl+Alt` 组合层，对应修饰键同步高亮；松开 Shift/Alt 回到 Ctrl 层
4. 松开 Ctrl → 键盘隐藏
5. 按住 Ctrl 时实际按下的组合键会在键盘上高亮闪烁
6. 托盘图标右键：启用/禁用、打开配置目录、重新加载快捷键、退出

### 防误触发

以下情况**不会**弹出键盘：
- 按住 Ctrl 的同时按住了鼠标任意键（Ctrl+点击多选、Ctrl+拖拽）
- 按住 Ctrl 滚动鼠标滚轮（Ctrl+滚轮缩放）；已弹出时再点按/滚动也会立即隐藏
- 弹出之前同时按住了 Shift / Alt / Win（非"纯 Ctrl"手势，避免切输入法等组合键误触发；**弹出之后**再加按 Shift/Alt 则是切换层）
- 前台是无边框全屏应用（游戏/全屏视频）
- 前台进程在 `settings.json` 的 `excludedProcesses` 列表中

## 内置快捷键配置

| 配置文件 | 匹配进程 | 应用 |
|---|---|---|
| `default.json` | —（兜底） | Windows 通用 |
| `wps-writer.json` | `wps` | WPS 文字 |
| `wps-et.json` | `et` | WPS 表格 |
| `wps-wpp.json` | `wpp` | WPS 演示 |
| `wps-pdf.json` | `wpspdf` | WPS PDF |
| `msoffice-word.json` | `winword` | Word |
| `msoffice-excel.json` | `excel` | Excel |
| `msoffice-ppt.json` | `powerpnt` | PowerPoint |
| `browser.json` | `chrome` `msedge` `firefox` `brave` `vivaldi` `opera` `360chrome` `qqbrowser` `2345explorer` | 浏览器 |
| `vscode.json` | `code` | VS Code |
| `explorer.json` | `explorer` | 资源管理器 |
| `terminal.json` | `windowsterminal` `wt` `warp` | Windows Terminal |
| `wechat.json` | `wechat` `weixin` | 微信 |
| `qq.json` | `qq` `qqnt` | QQ |
| `dingtalk.json` | `dingtalk` `dingtalklite` | 钉钉 |
| `jianying.json` | `jianyingpro` `capcut` `vegaspro` | 剪映 / CapCut |
| `photoshop.json` | `photoshop` | Photoshop |
| `obsidian.json` | `obsidian` | Obsidian |
| `notion.json` | `notion` | Notion |

大部分配置同时带 `ctrl`、`ctrl+shift`（部分含 `ctrl+alt`）三层数据。

### 添加自己的应用

在以下两个位置放 JSON 文件（后者覆盖前者同名 id）：
- `exe目录\shortcuts\*.json` —— 内置
- `%LOCALAPPDATA%\KeyboardHint\shortcuts\*.json` —— 用户自定义（托盘菜单可一键打开目录）

格式：

```json
{
  "id": "myapp",
  "displayName": "我的应用",
  "processes": ["myapp"],
  "layers": {
    "ctrl": {
      "S": "保存",
      "F5": "刷新"
    },
    "ctrl+shift": {
      "S": "另存为"
    },
    "ctrl+alt": {
      "V": "选择性粘贴"
    }
  }
}
```

`processes` 填进程名（不含 `.exe`，任务管理器→详细信息里看）。`layers` 下可配 `ctrl` / `ctrl+shift` / `ctrl+alt` 三层（没有的层弹出时按对应修饰键会显示"暂无数据"）。每层的键是**键 ID**：

- 字母：`A`–`Z`；数字：`D0`–`D9`（`D1`=主键区1）；功能键：`F1`–`F12`
- 符号：`Oem3`(`) `OemMinus`(-) `OemPlus`(=) `Oem4`([) `Oem6`(]) `Oem5`(\\) `Oem1`(;) `Oem7`(') `OemComma`(,) `OemPeriod`(.) `Oem2`(/)
- 其他：`Esc` `Tab` `Caps` `Back` `Enter` `Space` `Ins` `Del` `Home` `End` `PgUp` `PgDn` `Left` `Up` `Down` `Right`
- 修饰键：`LControl` `RControl` `LShift` `RShift` `LAlt` `RAlt` `LWin` `RWin`

改完后托盘菜单点"重新加载快捷键配置"即可，不用重启。

## 设置 `settings.json`

同目录覆盖顺序：`%LOCALAPPDATA%\KeyboardHint\settings.json` > `exe目录\settings.json`

```json
{
  "enabled": true,
  "delayMs": 400,
  "opacity": 0.92,
  "excludedProcesses": ["game1", "game2"]
}
```

- `delayMs`：按住 Ctrl 多久后弹出（150ms 起）
- `opacity`：键盘整体不透明度 0–1
- `excludedProcesses`：永不弹出的进程名列表

## 构建

需要 .NET 8 SDK：

```powershell
dotnet build KeyboardHint.sln -c Release

# 发布（自包含单文件，无需装运行时；exe 约 170MB + 少量原生 dll）
dotnet publish src\KeyboardHint\KeyboardHint.csproj -c Release -o publish
```

测试触发（模拟按住 Ctrl 1 秒）：`powershell scripts\hold-ctrl.ps1 1000`

## 技术说明

- **被动监听**：`WH_KEYBOARD_LL` 低级钩子只观察、永远放行（`CallNextHookEx`），不吞键、不记录、不上传任何按键内容 —— 不是键盘记录器
- 前台应用识别：`GetForegroundWindow` + 进程名匹配；`MonitorFromWindow` 定位到前台窗口所在显示器
- 全屏检测：窗口覆盖整个显示器且无标题栏样式（`WS_CAPTION`）才视为独占全屏，普通最大化不受影响
- 单实例（Mutex）、PerMonitorV2 DPI 感知、无主进程依赖

## 已知限制 / Roadmap

- 组合层为 `ctrl+shift` / `ctrl+alt` 两层；`ctrl+win` 层 schema 预留但默认不做（避免与系统级快捷键冲突）
- 不能点击键位直接触发快捷键（二期：overlay 需切换 `IsHitTestVisible` + `SendInput`）
- 无快捷键配置编辑界面，手工编辑 JSON
- 杀软可能因键盘钩子误报 —— 代码开源可自证；如需分发建议做代码签名
- 未签名 exe 首次运行 Windows SmartScreen 会提示，点"仍要运行"

## License

MIT
