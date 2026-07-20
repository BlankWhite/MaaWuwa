# MaaWuwa

MaaWuwa 是基于 MaaFramework 的鸣潮自动化资源项目。目前包含：启动/登录/索拉指南领取流程，以及 C#/.NET 10 自动战斗、自动日常首版。

## 自动日常首版

入口：MFAAvalonia 中选择“自动日常（第一版/调试）”。

当前实现：

- 进入游戏内后打开索拉指南日常页。
- OCR 读取已消耗体力和日常活跃度。
- 活跃度不足且需要消耗体力时，优先刷“模拟领域”。
- 副本内复用 C# 自动战斗识别与通用策略。
- 领取日常奖励、邮件、纪行奖励。

配置文件：

```text
assets/resource/config/daily.json
```

首版限制：主要基于 1280×720 相对坐标；优先支持模拟领域（默认贝币），无音区、凝素领域、梦魇声骸、周常花园和死亡恢复仍待补齐。缺失/建议补充模板见 `docs/daily-missing-templates.md`。

## 自动战斗首版

实现位置：

- `src/MaaWuwa.Core/`：自动战斗状态机、输入抽象、OpenCvSharp 图像识别、角色策略。
- `src/MaaWuwa.Agent/`：MaaFramework C# Agent，注册 `AutoCombat` 自定义动作。
- `assets/resource/config/auto_combat.json`：固定三人队、ROI、阈值、超时等配置。
- `assets/resource/pipeline/my_task.json`：`AutoCombat`/`RunAutoCombat` Maa pipeline 入口。

首版限制：前台可见游戏窗口、固定 1280×720/labwc 画面、固定三人队；仅做敌人血条、技能亮度、当前槽位等基础识别。

## 本地构建

需要 .NET 10 SDK：

```bash
dotnet build MaaWuwa.sln -c Release
```

Linux 发布 Agent：

```bash
dotnet publish src/MaaWuwa.Agent/MaaWuwa.Agent.csproj -c Release -f net10.0 -r linux-x64 --self-contained false -o install/agent
```

运行时需要 MaaFramework/MaaAgentBinary 原生库由客户端或 `install/libs/MaaAgentBinary` 提供。

## 调试配置

编辑：

```text
assets/resource/config/auto_combat.json
```

可设置：

- `team`：固定三人队名称。
- `durationSeconds`：单次自动战斗最长时长。
- `enableDebugCapture`：保存截图、敌人 mask 和技能 ROI 到 `debug/auto-combat`。
- `recognition.*Roi`：敌人血条、Boss 血条、技能、角色槽位 ROI。

## 常用检查

```bash
npm ci
npx @nekosu/maa-tools check
python tools/validate_schema.py --schema-dir deps/tools --resource-dirs assets/resource --exclude-dirs assets/resource/announcement --interface-files assets/interface.json
```
