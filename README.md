# FightingBroLikeGame

基于 [EasyFramework (EF)](https://github.com/mufengbufeng/EF) 的 Unity 游戏项目。

## 项目结构

```text
Configs/                    Luban 配置定义、数据表和生成脚本
LocalCDN/                   本地 CDN 输出目录，仅保留说明文件
Tools/                      Luban 与资源调试工具
UnityProject/               Unity 6 游戏项目
```

## 开发环境

- Unity `6000.3.12f1`
- EF 框架来源：`ef/main`

## 分支策略

- `main`：可运行的游戏开发主线，承载玩法、场景、美术、配置和业务代码。
- `feature/<name>`：从 `main` 创建的单一功能分支，完成后通过 Pull Request 合并到 `main`。
- `framework`：与 EF 共享提交历史的同步分支。只放可回灌到 EF 的通用改动。
- `ef`：EF 上游远程。回灌时把分支推到这里，不要推游戏仓库的 `origin`。

游戏代码与 EF 改动必须分开提交。一个提交只表达一个可回滚意图。

可回灌到 EF 的默认范围是 `UnityProject/Assets/EF/`。`GameScripts`、玩法资源、项目配置表、私有 CDN 地址不要带回 EF。

## 开始使用

1. 使用 Unity `6000.3.12f1` 打开 `UnityProject`。
2. 等待 Package Manager 和资源导入完成。
3. 在 `Assets/Resources/EFResourceModeConfig.asset` 选择资源模式并配置项目 CDN。
4. 在 `Assets/GameScripts/HotFix/GameLogic` 中实现项目流程、模型和 UI 逻辑。
5. 在 `Configs/GameConfig/Datas` 中替换最小示例表，并运行对应 `gen_code_bin_to_project` 脚本。

默认 `GameLogicEntry` 只初始化框架和 `InitProcedure`，不会打开任何业务窗口。项目接入首个窗口后，可将启动遮罩的关闭时机调整到窗口显示完成回调。

## 验证

Unity 已打开时优先使用 AIBridge：

```powershell
.\.aibridge\cli\AIBridgeCLI.exe compile unity
.\.aibridge\cli\AIBridgeCLI.exe get_logs --logType Error
.\.aibridge\cli\AIBridgeCLI.exe test run --mode EditMode
```

Unity 未打开时，可通过 Unity Test Framework 的 batchmode 执行 EditMode 测试。不要在同一项目已被编辑器打开时启动第二个 Unity 实例。

## EF 同步流程

### 从 EF 拉更新

```powershell
git fetch ef
git switch framework
git merge --ff-only ef/main
git switch main
git merge --no-ff framework
```

### 把 EF 改动推回上游

本仓库不是 EF 的 fork。不能从 `origin` 的功能分支直接给 EF 开 Pull Request。
必须先落到 `framework`（或从它拉出的分支），再推到 `ef` 远程。

优先在 `framework` 上改框架，验证后再合并进 `main`：

```powershell
git switch framework
git switch -c framework/event-lifecycle
# 只改 UnityProject/Assets/EF 等通用框架文件
git add UnityProject/Assets/EF
git commit -m "fix: tighten event channel lifecycle"
git push -u ef framework/event-lifecycle
gh pr create --repo mufengbufeng/EF --head framework/event-lifecycle --base main

git switch main
git merge --no-ff framework/event-lifecycle
```

如果已经在 `main` 上改完了 EF 文件，再抽到 `framework`：

```powershell
git switch framework
git restore --source=main --staged --worktree -- UnityProject/Assets/EF
git status
git commit -m "fix: tighten event channel lifecycle"
git push -u ef HEAD:refs/heads/framework/event-lifecycle
gh pr create --repo mufengbufeng/EF --head framework/event-lifecycle --base main
```

向 EF 提交前确认：不依赖本项目玩法、资源、配置或私有服务，并完成 Unity 编译与相关测试。
