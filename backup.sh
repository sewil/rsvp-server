#!/usr/bin/env bash

dir=$(date +'%F')
mkdir -p "${dir}"

cd "${dir}"

filename="$(date +"%F_%H%M%S").sql"

mysqldump -u rsvp-backup -pblah --databases rsvp > "${filename}"
gzip "${filename}"
# mysqldump -u rsvp-backup -pblah --databases rsvp | gzip - > "${filename}"

