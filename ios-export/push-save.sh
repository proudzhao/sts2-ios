#!/usr/bin/env bash
# 一次性把【电脑存档】推到手机（首次移植完成后迁移你已有的进度；也可用于手动强推 电脑→手机）。
#
# 与 sts2_save_sync.sh 的区别：常规同步脚本要求手机上已有存档才动作(先拉手机侧比时间)，
# 手机全新装、还没任何存档时它会跳过。本脚本【不要求手机已有存档】，专治首次迁移。
#
# 机制：复用游戏内 SyncImportPatch 的收件箱导入(和常规同步同一条已验证路径)——
# 把电脑存档推到 Documents/sync_inbox/<版本>/1/**，下次【启动游戏】时 SyncImportPatch
# 校验后导入到 default/1(手机没存档时 localNewest=0，任何推送都会被导入)。
# 导入是崩溃安全的目录交换，旧树留时间戳备份，不会出半成品。
#
# 平台：本脚本用 xcrun devicectl，仅 macOS。Windows/Linux 用户见 docs/SAVE_SYNC.md 的跨平台做法。
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ -f "$SCRIPT_DIR/config.sh" ] || { echo "❌ 缺少 ios-export/config.sh，先 cp config.example.sh config.sh 并填写" >&2; exit 1; }
# shellcheck disable=SC1091
source "$SCRIPT_DIR/config.sh"
: "${STS2_DEVICE_UDID:?config.sh 未设置 STS2_DEVICE_UDID}"
: "${STS2_BUNDLE_ID_SIGNED:?config.sh 未设置 STS2_BUNDLE_ID_SIGNED（手机上已装 App 的 bundle）}"
: "${STS2_STEAM_ID:?config.sh 未设置 STS2_STEAM_ID（电脑存档目录名，SteamID64）}"

DEV="$STS2_DEVICE_UDID"
BUNDLE="$STS2_BUNDLE_ID_SIGNED"
fail(){ echo "❌ $1"; exit 1; }
DESK="$HOME/Library/Application Support/SlayTheSpire2/steam/$STS2_STEAM_ID"
STAGE="$(mktemp -d /tmp/sts2push.XXXXXX)" || fail "创建暂存目录失败"
ERRLOG="$(mktemp /tmp/sts2push.XXXXXX)" || fail "创建临时错误日志失败"  # 随机名(固定名 /tmp/pushsave.err 可被预埋符号链接截断任意文件)
trap 'rm -rf "$STAGE" "$ERRLOG"' EXIT

[ -d "$DESK" ] || fail "电脑存档目录不存在: $DESK（STS2_STEAM_ID 填对了吗？）"
[ -f "$DESK/profile.save" ] || echo "⚠️ 注意: $DESK 里没看到 profile.save，确认这是你的存档目录再继续"

# 版本号用电脑存档最新修改时间(与常规同步一致，保证"最新者胜"语义正确)
D=$(/usr/bin/find "$DESK" -type f -not -name "sync_*" -exec stat -f %m {} \; 2>/dev/null | sort -n | tail -1)
[ -n "$D" ] || fail "读不到电脑存档时间戳"

mkdir -p "$STAGE/inbox/$D"
cp -a "$DESK" "$STAGE/inbox/$D/1" || fail "暂存电脑存档失败"

echo "▶ 推送电脑存档(版本 $D) → 手机 $BUNDLE : Documents/sync_inbox/$D/1 …"
# devicectl copy to 对【目录】不递归(实测: destination 只出现空目录),必须文件级逐个推;
# 每个文件单独 copy to, 目标父目录自动创建。
FAILED=0
while IFS= read -r -d '' f; do
  rel="${f#"$DESK"/}"                        # DESK 相对路径, 如 profile1/saves/progress.save
  xcrun devicectl device copy to --device "$DEV" \
    --domain-type appDataContainer --domain-identifier "$BUNDLE" \
    --source "$f" --destination "Documents/sync_inbox/$D/1/$rel" >/dev/null 2>"$ERRLOG" \
    || { echo "❌ 推送失败: $rel"; cat "$ERRLOG" >&2; FAILED=1; break; }
done < <(find "$DESK" -type f -not -name "*.backup" -print0)
[ "$FAILED" = "0" ] || fail "推送失败(设备不可达/锁屏/bundle 错/App 没装？)"

# 校验(新版 iOS 该查询常假阴性, 最终以启动游戏导入结果为准)
if xcrun devicectl device info files --device "$DEV" \
     --domain-type appDataContainer --domain-identifier "$BUNDLE" 2>/dev/null \
     | grep -q "sync_inbox/$D"; then
  echo "✅ 已推入收件箱。现在【启动游戏】，进度会被 SyncImportPatch 自动导入。"
  echo "   导入后手机原有存档(若有)会留时间戳备份，可在容器 sync_replaced_* 里找到。"
else
  echo "⚠️ 容器查询没查到(新版 iOS 该查询常假阴性)。上面每个文件若都显示 File on Device 且 size>0 即已落地;"
  echo "   以【启动游戏】后角色/图鉴是否解锁为准。"
fi
