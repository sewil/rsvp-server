#!/usr/bin/env bash

set -xe

branch=master
if [ "$1" != "" ]; then
	branch="$1"
fi

buildfolder="/opt/rsvp-server/"

echo "Building latest and greates..."
( \
	cd "$buildfolder";  \
	rm -rf BinSvr;  \
	git fetch; \
	git checkout "$branch"; \
	git pull; \
	dotnet build -p:PRODUCTION=1 --configuration Release; \
	git log -1 --oneline > BinSvr/buildver.txt \
)

curbuild="$(pwd)/BinSvr-$(date +%s)"
mkdir -p "$curbuild"
cp -rf ${buildfolder}BinSvr/* "$curbuild"

rm -f BinSvr
ln -s "$curbuild" BinSvr
