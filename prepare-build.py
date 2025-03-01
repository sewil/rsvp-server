import os
import shutil

cur_dir = dir_path = os.path.dirname(os.path.realpath(__file__))
shutil.copytree(os.path.join(cur_dir, "..", "rsvp-data"), os.path.join(cur_dir, "DataSvr"), dirs_exist_ok=True, ignore=lambda directory, contents: ['.git'])
shutil.copytree(os.path.join(cur_dir, "..", "rsvp-scripts-2"), os.path.join(cur_dir, "DataSvr", "Scripts"), dirs_exist_ok=True, ignore=lambda directory, contents: ['.git'])
