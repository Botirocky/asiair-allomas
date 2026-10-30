#!/usr/bin/env bash
set -euo pipefail

verzio="${1:?Használat: ./kiadas.sh X.Y.Z}"
cd "$(dirname "$0")"

[ "$(git branch --show-current)" = main ] || { echo "Csak a main ágról adható ki." >&2; exit 1; }
[ -z "$(git status --porcelain)" ] || { echo "Előbb commitold a változásokat." >&2; exit 1; }
git pull -q --ff-only

sed -i -E "s#<Version>[^<]+</Version>#<Version>$verzio</Version>#" src/AsiairAllomas/AsiairAllomas.csproj
rm -rf publish
dotnet publish src/AsiairAllomas -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish

if ! git diff --quiet; then
  git add src/AsiairAllomas/AsiairAllomas.csproj
  git commit -q -m "Verzió: $verzio"
fi
git push -q origin main
gh release create "v$verzio" publish/AsiairAllomas.exe --title "ASIAIR Állomás $verzio" --generate-notes

scp -q publish/AsiairAllomas.exe picloud:/tmp/AsiairAllomas.exe
ssh picloud 'sudo install -m 644 -o root -g root /tmp/AsiairAllomas.exe /srv/szerverpark/web/alap/windows/AsiairAllomas.exe && rm /tmp/AsiairAllomas.exe'
echo "Kiadva: v$verzio"
