import os
import subprocess
import shutil
from datetime import datetime, timezone

cur_dir = dir_path = os.path.dirname(os.path.realpath(__file__))

bin_dir=os.path.join(cur_dir, "BinSvr")
pub_dir=os.path.join(cur_dir, f"BinSvr-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M')}")
shutil.rmtree(bin_dir, ignore_errors=True)

subprocess.run(["git", "fetch"])
subprocess.run(["git", "pull"])
subprocess.run(["dotnet", "publish", "WvsBeta_REVAMP.sln", "-r", "win-x64", "--self-contained", "false", "-c", "Release", "-o", pub_dir, "-p:DebugType=embedded"])
os.system(f'git log -1 --oneline > {os.path.join(pub_dir, "buildver.txt")}')
shutil.copytree(os.path.join(bin_dir, "evolutions"), os.path.join(pub_dir, "evolutions"))
