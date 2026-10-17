#!/usr/bin/env sh
# Сборка OmniBox для LG webOS: app + service -> dist/com.omnibox.launcher_<версия>_all.ipk
# Нужен webOS CLI: npm install -g @webos-tools/cli
set -e
cd "$(dirname "$0")"
mkdir -p dist
ares-package app service -o dist
