# ModSyncTool

[English Version](./README.en.md)

一个基于 .NET 8 WPF 的轻量 Mod 同步与启动工具。它从远程配置清单获取更新信息，一键完成校验、下载、清理与启动；界面跟随 Windows 浅/深色主题与系统强调色，提供管理本地文件与发布工具等实用功能。可以同步各种游戏的mod。

## 功能特性

- 一键“更新并启动”：按清单下载缺失/变更文件，清理废弃文件并启动目标程序
- 跟随系统：开箱支持浅/深色主题与强调色，无需重启即可切换
- 现代样式：统一的主/次按钮、圆角控件、进度与提示
- 本地文件管理：浏览本地目录、右键忽略等基础运维能力
- 设置中心：启动程序位置、远程配置链接、下载并发/多线程与证书忽略等选项
- 发布工具（Manifest 生成器）：从选中文件夹生成在线清单，辅助分发与更新
- 日志记录：Serilog 输出到 `logs/modsync.log`，便于定位问题

> 平台支持：Windows（WPF，`net8.0-windows`）。

## 界面展示

![](/docs/232237.webp)

## 快速开始

1) 下载发行版并解压后解压到应用或游戏根目录，直接运行 `ModSyncTool.exe`。
2) 首次启动会看到“欢迎”对话框：
	- 选择“输入链接开始同步”，填入远程配置链接（通常是在线 `online_manifest.json` 的地址）。
	- 或“本次忽略/永久忽略检测”，进入主界面后再在“设置”中填入链接。
3) 点击主界面“更新并启动”，工具会自动比对并下载更新，然后启动指定程序。

## 远程配置与设置

应用在本地维护配置文件（例如 `local_config.json`，由设置界面写入）。常见字段示例：

```json
{
  "LaunchExecutable": "C:/Games/MyGame/Game.exe",
  "UpdateUrl": "https://example.com/online_manifest.json",
  "IsCustomDownloadSettings": true,
  "EnableMultiFileDownload": true,
  "MaxConcurrentFiles": 10,
  "EnableMultiThreadDownload": true,
  "ThreadsPerFile": 8,
  "IgnoreSslErrors": false
}
```

说明：
- UpdateUrl 为空时，应用视为“未配置”，主界面会引导你先补全设置。
- 并发与多线程下载可在“设置 → 下载设置”中按需开启。

## 发布工具（Manifest 生成器）

内置“管理（发布工具）”用于生成 Manifest：

1) 选择包含要分发文件的根目录
2) 填写“包信息/版本/基础下载地址/启动程序”等元数据
3) 选择需要包含的文件（支持全选/多选）
4) 点击“生成 Manifest”输出在线清单，将其上传至你的静态托管或 CDN

随后客户端只需在“设置 → 远程配置链接”中填入该清单的 URL 即可进行更新。

## 构建与运行

前置条件：
- Windows 10/11
- .NET 8 SDK
- 可选：Visual Studio 2022（或使用命令行）

命令行（PowerShell）：

```powershell
# 还原并构建
dotnet build .\ModSyncTool.sln -c Release

# 运行（调试）
dotnet run --project .\ModSyncTool\ModSyncTool.csproj -c Debug

# 发布（示例：x64 便携）
dotnet publish .\ModSyncTool\ModSyncTool.csproj -c Release -r win-x64 --self-contained false -o .\publish\win-x64
```

构建产物位于 `ModSyncTool\bin\<配置>\net8.0-windows\`。

## 日志与排障

- 日志路径：`logs/modsync.log`
- 主题问题：应用会记录如 `ThemeService: applying Dark/Light theme` 与注册表读值，便于确认浅/深色识别
- 如遇异常崩溃，应用内置了全局异常捕获并写入日志，欢迎附日志提 Issue

## 目录结构（节选）

```
ModSyncTool/
├─ ModSyncTool.sln
└─ ModSyncTool/
	├─ App.xaml / App.xaml.cs           # 应用入口，主题/异常/主窗口初始化
	├─ Services/                        # Config、Sync、Hash、FileScanner、Dialog、Theme、Log 等
	├─ ViewModels/                      # Main/Settings 等 VM
	├─ Views/                           # MainWindow、Settings、Welcome、ManifestGenerator 等
	└─ Themes/                          # Colors/Controls/Light/Dark 主题资源
```

## 贡献

欢迎提交 Issue 与 PR：
- 遵循现有代码风格与 MVVM 分层
- 优先增加/更新对应的 UI 主题样式与单元测试（如适用）
- 在本地确保“能构建、能运行、基本功能可回归”

## 许可证

本项目使用仓库根目录的 `LICENSE.md` MIT 许可协议。

## 致谢

- [Serilog](https://serilog.net/)：结构化日志
- WPF 社区的设计与主题实践
