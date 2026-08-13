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
- `feature/<name>`：从 `main` 创建的单一功能分支，完成后通过 Pull Request 合并。
- `framework/<name>`：只放通用 EF 改动；验证通过后提交 Pull Request 到 EF 的 `main`。
- `framework`：EF 发布同步分支，保持与 EF `main` 的共同历史，不混入游戏玩法内容。
- `ef`：EF 上游远程，只用于获取框架更新，不直接在其远程分支上开发。

游戏代码与 EF 改动必须分开提交。一个提交只表达一个可回滚意图；框架升级先在独立分支验证，再合并到 `main`。

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

```powershell
# 获取 EF 上游更新
git fetch ef
git switch framework
git merge --ff-only ef/main

# 将框架更新合并到游戏主线并验证
git switch main
git merge --no-ff framework

# 将通用框架改动发布回项目仓库，随后创建到 EF 的 Pull Request
git switch framework/<name>
git push origin framework/<name>
```

向 EF 提交前，必须确认改动不依赖本项目玩法、资源、配置或私有服务，并完成 Unity 编译与相关测试。
