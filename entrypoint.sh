#!/bin/bash

while [ ! -d "/app/BinSvr" ]; do
    sleep 1
done

mkdir -p /shared/BinSvr
mkdir -p /shared/DataSvr
mkdir -p /app/BinSvr
mkdir -p /app/DataSvr

cp -r /app/BinSvr/* /shared/BinSvr/
cp -r /app/DataSvr/* /shared/DataSvr/

cd /shared/BinSvr
exec "$@"
