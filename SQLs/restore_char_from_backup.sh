#!/usr/bin/env bash

set -e

backup="$1"
shift

chars=""

for id in $*; do
	if [ -n "${chars}" ]; then
		chars="${chars},${id}"
	else
		chars="${id}"
	fi
done

name="character-recovery"

#docker down --rm "${name}" || true

echo "Building restoration docker image"
docker run --name "${name}" -e "MYSQL_ROOT_PASSWORD=temp" -d --mount type=bind,source="${backup}",target=/docker-entrypoint-initdb.d/backup.sql.gz mysql:latest || true

# gunzip -c "${backup}" | docker exec -i "${name}" sh -c 'exec mysql -uroot -ptemp'

docker exec "${name}" bash -c "while ! mysqladmin ping -h127.0.0.1 --silent; do echo waiting; sleep 2; done"

# charid tables

tables=()

queries=()

for table_name in character_quests character_rate_credits character_variables character_wishlist inventory_bundle inventory_eqp monsterbook teleport_rock_locations skills; do
	tables+=("${table_name}")
	queries+=("DELETE FROM ${table_name} WHERE charid NOT IN (${chars});")
done


# ID tables
for table_name in gamestats characters; do
	tables+=("${table_name}")
	queries+=("DELETE FROM ${table_name} WHERE id NOT IN (${chars});")
done

queries+=("DELETE FROM fame_log WHERE \`from\` NOT IN (${chars}) AND \`to\` NOT IN (${chars});")
queries+=("DELETE FROM itemlocker WHERE characterid NOT IN (${chars});")
queries+=("DELETE FROM memos WHERE to_charid NOT IN (${chars});")

tables+=("fame_log")
tables+=("itemlocker")
tables+=("memos")

for query in "${queries[@]}"; do
	echo "${query}"
	echo "${query}" | docker exec -i "${name}" sh -c 'exec mysql -uroot -ptemp rsvp'
done

docker exec -i "${name}" sh -c "exec mysqldump -uroot -ptemp --skip-triggers --no-create-db --compact --no-create-info --databases rsvp --tables ${tables[*]}" > backup.sql

docker stop "${name}"
docker rm -f "${name}"
