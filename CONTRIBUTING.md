# 贡献指南

LazyTyper 是一个刻意保持简单的小项目：一个 WinForms HUD、一个全局键盘钩子、一套 SendInput 注入逻辑。没有 NuGet 依赖，也没有复杂的架构。

## 报告问题

发 Issue 之前，请先确认：

- 你用的是 Windows 10/11 和 .NET Framework（系统自带）。
- 你测试的目标程序不是以管理员权限运行的（普通权限的工具无法输入到高权限窗口）。
- 错误日志 `lazy-typer.log` 是否存在，里面有没有异常信息。
- F8/F9 是否被其他软件占用，HUD 是否显示了备用快捷键。

在 Issue 里写清楚：

- Windows 版本
- 目标程序（如 Notepad、Chrome、VS Code）
- 复现步骤
- 预期行为 vs 实际行为
- 相关日志（不要附带敏感剪贴板内容）

## 开发

```powershell
# 修改 src/LazyTyper.cs 后
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1

# 运行
.\dist\LazyTyper.exe
```

构建只依赖 Windows 自带的 .NET Framework C# 编译器，不需要 Visual Studio 或 .NET SDK。

## PR

- 保持改动小而聚焦。
- 不要引入网络请求、遥测或第三方运行时依赖。
- 涉及键盘钩子、焦点切换或输入注入的改动，请说明测试过的目标程序。
- 不要提交构建缓存、日志、测试输出或包含个人信息的截图。
- 提交前运行一次 `scripts/build.ps1`。

## 安全与使用边界

这个工具会拦截全局键盘输入并注入字符。请只在你有权限操作的环境和程序中使用。不要把项目用于考试作弊、绕过访问控制或未经授权的自动化。
