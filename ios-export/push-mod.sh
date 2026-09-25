#!/usr/bin/env bash
# 把 Watcher mod 的素材包 Watcher.pck 推到手机 App 文档区 Documents/Watcher.pck。
# 游戏启动时(ModManager.Initialize 织入钩子)从 user://Watcher.pck 挂载观者的全部美术/音频资源。
# 与 push-pck.sh 的 StS2.pck 互补: 一个推游戏本体素材, 一个推 mod 素材。
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ -f "$SCRIPT_DIR/config.sh" ] || { echo "❌ 缺少 ios-export/config.sh，先 cp config.example.sh config.sh 并填写" >&2; exit 1; }
# shellcheck disable=SC1091
source "$SCRIPT_DIR/config.sh"
: "${STS2_DEVICE_UDID:?config.sh 未设置 STS2_DEVICE_UDID}"
: "${STS2_BUNDLE_ID_SIGNED:?config.sh 未设置 STS2_BUNDLE_ID_SIGNED（手机上已装 App 的 bundle；SideStore 签名会带团队后缀）}"

DEV="$STS2_DEVICE_UDID"
BUNDLE="$STS2_BUNDLE_ID_SIGNED"
# 默认从 Steam Workshop 订阅目录取; 也可传路径覆盖(如新版 mod 下载后)
WORKSHOP_DEFAULT="$HOME/Library/Application Support/Steam/steamapps/workshop/content/2868840/3747526116/Watcher.pck"
PCK="${1:-$WORKSHOP_DEFAULT}"
fail(){ echo "❌ $1"; exit 1; }

[ -f "$PCK" ] || fail "Watcher.pck 不存在: ${PCK}（Steam 里订阅 STS2 的 Watcher mod 后此处应有; 或传路径覆盖）"
[ -n "$DEV" ] || fail "STS2_DEVICE_UDID 为空（xcrun devicectl list devices 取 UDID）"

# 提示: mod 随游戏更新/换版本时重新跑一次即可。旧包同名覆盖。
echo "▶ 目标容器: $BUNDLE 的 Documents/  (设备 $DEV)"
echo "▶ 推送 Watcher mod 素材包 $(du -h "$PCK" | cut -f1) → Documents/Watcher.pck …"

ERRLOG="$(mktemp "${TMPDIR:-/tmp}/pushmod.XXXXXX")" || fail "创建临时错误日志失败"
trap 'rm -f "$ERRLOG"' EXIT

if ! xcrun devicectl device copy to --device "$DEV" \
      --domain-type appDataContainer --domain-identifier "$BUNDLE" \
      --source "$PCK" --destination "Documents/Watcher.pck" 2>"$ERRLOG"; then
  cat "$ERRLOG" >&2
  echo "── 若上面报容器不存在: 手机上先用 SideStore(或 Xcode) 把 App 装上再来。"
  echo "── 若 devicectl 拷贝不通, 可改用 Finder: 连线 → 手机 → 文件 → 找到该 App → 拖入(文件名须为 Watcher.pck)。"
  fail "推送失败"
fi

echo "✅ Watcher mod 素材包已就位(目标 Documents/Watcher.pck)。"
echo "   启动 App 后织入钩子会从 user://Watcher.pck 挂载; 角色选择界面出现「观者」即成功。"
