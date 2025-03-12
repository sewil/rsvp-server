import os
import shutil

def cp_dir(src_dir, dst_dir):
    shutil.rmtree(dst_dir, ignore_errors=True)
    shutil.copytree(src_dir, dst_dir, ignore=lambda directory, contents: ['.git', '.gitignore'])

def cp_file(src_file, dst_file):
    if os.path.exists(dst_file):
        os.remove(dst_file)
    shutil.copyfile(src_file, dst_file)

cur_dir = dir_path = os.path.dirname(os.path.realpath(__file__))
data_dir = os.path.join(cur_dir, "..", "rsvp-data")
datasvr_dir = os.path.join(cur_dir, "DataSvr")

data_dirs = [
    "Character",
    "Effect",
    "Etc",
    "Event Mods",
    "Item",
    "Map",
    "Mob",
    "Npc",
    "Reactor",
    "Resources",
    "Server",
    "Skill",
    "Sound",
    "String",
    "UI"
]
for d in data_dirs:
    cp_dir(os.path.join(data_dir, d), os.path.join(datasvr_dir, d))

cp_file(os.path.join(data_dir, "smap.img"), os.path.join(datasvr_dir, "smap.img"))
cp_file(os.path.join(data_dir, "zmap.img"), os.path.join(datasvr_dir, "zmap.img"))
cp_dir(os.path.join(cur_dir, "..", "rsvp-scripts-2"), os.path.join(datasvr_dir, "Scripts"))
