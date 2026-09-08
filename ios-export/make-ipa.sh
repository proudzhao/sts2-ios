#!/usr/bin/env bash
# 把瘦身版打成 IPA 交给 SideStore 永久续签(免费证书 7 天 → 手机自签, 免每周插 Mac)。
# 复用 deploy-slim.sh 同款"换 dylib + 剥 pck + 注入 --main-pack + 重签"逻辑,
# 末尾打包 zip 而非 USB 装机。产物: ios-export/build/StS2-slim.ipa (~55M)。
# 用法: 先 build-ios.sh + Xcode GUI Run 一次(生成最新 .app 与 .xcent), 再跑本脚本。
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ -f "$SCRIPT_DIR/config.sh" ] || { echo "❌ 缺少 ios-export/config.sh，先 cp config.example.sh config.sh 并填写" >&2; exit 1; }
# shellcheck disable=SC1091
source "$SCRIPT_DIR/config.sh"
: "${STS2_SIGN_IDENTITY:?config.sh 未设置 STS2_SIGN_IDENTITY}"

IDENT="$STS2_SIGN_IDENTITY"
fail(){ echo "❌ $1"; exit 1; }

# 已构建的 .app(DerivedData 里的哈希目录不固定;Debug/Release 都收, 取最新)
APP=$(find "$HOME/Library/Developer/Xcode/DerivedData" -maxdepth 6 -type d -path "*-iphoneos/StS2.app" 2>/dev/null \
      | xargs -I{} stat -f '%m %N' {} 2>/dev/null | sort -rn | head -1 | cut -d' ' -f2-)
PUB="$SCRIPT_DIR/.godot/mono/temp/bin/ExportRelease/ios-arm64/publish/sts2.dylib"
STG="$(mktemp -d /tmp/sts2ipa.XXXXXX)/StS2.app"
OUT="$SCRIPT_DIR/build/StS2-slim.ipa"

[ -f "$PUB" ] || fail "新 dylib 不存在: $PUB（先跑 build-ios.sh）"
[ -n "$APP" ] && [ -d "$APP" ] || fail "DerivedData 里找不到已构建的 StS2.app（先用 Xcode GUI 完整构建一次）"

# 1) 换新织入 dylib 进 .app
cp "$PUB" "$APP/Frameworks/sts2.framework/sts2" || fail "换 dylib 失败"
echo "✅ 新 dylib 已换入 ($(shasum "$PUB" | cut -c1-12))"

# 2) 造瘦身包
mkdir -p "$(dirname "$STG")"; cp -R "$APP" "$STG"
rm -f "$STG/StS2.pck"
plutil -remove godot_cmdline "$STG/Info.plist" 2>/dev/null
plutil -insert godot_cmdline -json '["--main-pack", "user://StS2.pck"]' "$STG/Info.plist" || fail "注入 cmdline 失败"
echo "✅ 瘦身包 $(du -sh "$STG" | cut -f1), 已注入 --main-pack user://StS2.pck"

# 3) 签名(与 deploy-slim.sh 一致: entitlements 用 Xcode 生成的 .xcent, 与 embedded profile 完全一致)
XCENT=$(find "$HOME/Library/Developer/Xcode/DerivedData" -maxdepth 8 \
  -type f -path "*/StS2.build/*-iphoneos/StS2.build/StS2.app.xcent" 2>/dev/null \
  | xargs -I{} stat -f '%m %N' {} 2>/dev/null | sort -rn | head -1 | cut -d' ' -f2-)
[ -n "$XCENT" ] && [ -f "$XCENT" ] && grep -q increased-memory "$XCENT" \
  || fail "找不到 Xcode 生成的 entitlements(.xcent)或缺大内存权限"
for fw in "$STG/Frameworks/"*.framework; do
  codesign -f -s "$IDENT" --generate-entitlement-der "$fw" >/dev/null 2>&1 || fail "签框架失败 $fw"
done
codesign -f -s "$IDENT" --generate-entitlement-der --entitlements "$XCENT" "$STG" >/dev/null 2>&1 || fail "签 .app 失败"
codesign -d --entitlements - "$STG" 2>/dev/null | grep -q increased-memory && echo "✅ 大内存权限已嵌入" || fail "大内存权限缺失"

# 4) bump CFBundleVersion(SideStore 对同 bundle 同版本用缓存包, 必须每次 +1;
#    版本号是形如 0.107.1 的字符串, 只对最后一段数字 +1)
CUR=$(plutil -extract CFBundleVersion raw "$STG/Info.plist")
BASE="${CUR%.*}"; LAST="${CUR##*.}"
if [[ "$LAST" =~ ^[0-9]+$ ]]; then NEW="$BASE.$((LAST + 1))"; else NEW="$CUR.1"; fi
plutil -replace CFBundleVersion -string "$NEW" "$STG/Info.plist" || fail "bump CFBundleVersion 失败"
echo "✅ CFBundleVersion: $CUR → $NEW"

# 5) 打包 IPA(zip Payload/)
rm -rf "$(dirname "$STG")/Payload" && mkdir -p "$(dirname "$STG")/Payload"
cp -R "$STG" "$(dirname "$STG")/Payload/" || fail "组装 Payload 失败"
rm -f "$OUT"
( cd "$(dirname "$STG")" && zip -qry "$OUT" Payload ) || fail "zip 打包失败"
rm -rf "$(dirname "$STG")/Payload"
echo "✅ IPA 已生成: $OUT ($(du -h "$OUT" | cut -f1))"
echo "   交给 SideStore sideload; 签名后 bundle 会带团队后缀(记下来填 config.sh 的 STS2_BUNDLE_ID_SIGNED)"
echo "   之后把 pck 推到新 bundle: bash ios-export/push-pck.sh"
