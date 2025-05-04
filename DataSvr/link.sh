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

ln -s Character ../../rsvp-data/Character
ln -s Etc ../../rsvp-data/Etc
ln -s Item ../../rsvp-data/Item
ln -s Map ../../rsvp-data/Map
ln -s Mob ../../rsvp-data/Mob
ln -s Npc ../../rsvp-data/Npc
ln -s Reactor ../../rsvp-data/Reactor
ln -s scripts ../../rsvp-scripts
ln -s Server ../../rsvp-data/Server
ln -s Skill ../../rsvp-data/Skill
ln -s String ../../rsvp-data/String