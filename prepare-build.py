import os
import shutil
shutil.copytree(os.path.join("..", "rsvp-data"), os.path.join(".", "DataSvr"), dirs_exist_ok=True)
shutil.copytree(os.path.join("..", "rsvp-scripts-2"), os.path.join(".", "DataSvr", "Scripts"), dirs_exist_ok=True)
