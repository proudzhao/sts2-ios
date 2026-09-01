# 游戏版本更新搬运指南

> 面向 Agent / 开发者:游戏在 Steam 更新后,如何把新版本同步到 iPhone。
> 原理:手机版本 = **构建时** Mac Steam 目录里的版本。任何更新都需要重新构建 + 重新装机 + 重推内容包。

## 更新前检查

1. **确认 Steam 已更新**:库条目显示「▶ 开始游戏」= 已最新;显示「更新」= 先更新。
   测试分支玩家(public-beta)同理,切换分支后先让 Steam 下载完。
2. **记录版本**:
   ```bash
   cat "$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/release_info.json"
   ```
   输出 `version` / `commit` / `date`,与上次对比确认游戏确实变了。
3. **手机存档备份(建议)**:更新前先 `bash ios-export/push-save.sh`(或按 `docs/SAVE_SYNC.md` 拉回电脑存档)。
   游戏更新可能改存档格式,留一份底总没错。

## 更新三步(用户终端执行)

前置:签名证书未过期(免费账号 7 天)、iPhone 已连 Mac、`config.sh` 就绪。

```bash
cd ~/Desktop/project/sts2-ios/sts2-ios

# 1) 重新织入 + NativeAOT + 导出 Xcode 工程 + 重打 pck(约 20-30 分钟)
bash ios-export/build-ios.sh

# 2) 换新 dylib 覆盖装瘦身版(保留手机 Documents 与存档;约 1 分钟)
bash ios-export/deploy-slim.sh

# 3) 重推素材包(游戏内容变了必须重推;1.77G USB 约数分钟)
bash ios-export/push-pck.sh
```

三步完成后手机上启动游戏验证。**只改补丁、游戏内容没动**时不需要第 3 步(见 `AGENTS.md` 日常维护表)。

## 何时必须重新走 Xcode GUI

- 签名证书 / 描述文件过期(免费账号 7 天):Xcode 打开 `ios-export/build/StS2.xcodeproj` → ▶ Run 重新签一次,再跑上面第 2、3 步。
- 更换 iPhone、新增 Capability、游戏更新导致 Godot 工程结构变化(罕见)。
- `deploy-slim.sh` 报「DerivedData 里找不到已构建的 StS2.app」时:说明基座丢了,回 Xcode Run 一次。

## 验证标准

- 手机上启动,进主菜单、能开局;游戏内版本号与新版本一致。
- 首次启动几分钟内卡顿属正常(1.77G 冷加载 + 着色器缓存建立),玩一局后应流畅。
- `push-pck.sh` 末尾的「容器查询没查到」警告在新版 iOS 常是假阴性:copy 输出有 `File on Device … 1.77 GB` 且游戏能启动即为成功。

## 已知坑(均已修复,复发时按此定位)

| 症状 | 根因 | 修复 |
|---|---|---|
| 织入报 `AssemblyResolutionException: <某程序集>` | 游戏更新引入新程序集引用,`.work/` 缺依赖 | build-ios.sh 步骤 1 已全量拷贝游戏 dll |
| Xcode 装机 `MismatchedBundleIDSigningIdentifier` | framework 的 plist bundle id 与签名 id 不一致(iOS 26 新校验) | build-ios.sh 步骤 5.16 统一标识符并重签 |
| 装机 `0xe8008015 valid provisioning profile not found` | 重签 entitlements 与 profile 不符(team id 以 profile 实际值为准,而非账号显示名) | deploy-slim.sh 改用 Xcode 生成的 `.xcent` 签名 |
| push-pck 校验假阴性 | `devicectl device info files` 在新版 iOS 查不到容器文件 | 已降级为警告,以启动结果为准 |

## 环境注意(新机器/新会话)

- 跑 Godot 命令行前:`export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"`,否则 hostfxr 找不到崩 signal 11。
- 跑构建会把 `STS2_TEAM_ID` 注入 `export_presets.cfg`;提交仓库变更前把 `application/app_store_team_id` 恢复为 `""`。
- 装机**绝不 uninstall**(会清存档),一律覆盖装。

## 版本参考(2026-09-01)

| 分支 | 版本 | 备注 |
|---|---|---|
| 正式(默认) | v0.107.1 | 本机当前 |
| public-beta | v0.111.0(2026-08-14) | Steam 属性 → 测试版 加入 |
