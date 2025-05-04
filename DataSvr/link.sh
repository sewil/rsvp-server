#!/usr/bin/env bash
set -xe

rm -f Character
rm -f Etc
rm -f Item
rm -f Map
rm -f Mob
rm -f Npc
rm -f Reactor
rm -f scripts
rm -f Server
rm -f Skill
rm -f String

ln -s ../../rsvp-data/Character Character
ln -s ../../rsvp-data/Etc Etc
ln -s ../../rsvp-data/Item Item
ln -s ../../rsvp-data/Map Map 
ln -s ../../rsvp-data/Mob Mob
ln -s ../../rsvp-data/Npc Npc
ln -s ../../rsvp-data/Reactor Reactor
ln -s ../../rsvp-scripts scripts
ln -s ../../rsvp-data/Server Server
ln -s ../../rsvp-data/Skill Skill
ln -s ../../rsvp-data/String String