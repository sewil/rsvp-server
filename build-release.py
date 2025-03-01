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
with open(os.path.join(pub_dir, "launch Center.bat"), "w") as f:
    f.write(f'TITLE Center\nWvsBeta.Center Center')
with open(os.path.join(pub_dir, "launch Game0.bat"), "w") as f:
    f.write(f'TITLE Game0\nWvsBeta.Game Game0')
with open(os.path.join(pub_dir, "launch Login1.bat"), "w") as f:
    f.write(f'TITLE Login1\nWvsBeta.Login Login1')
with open(os.path.join(pub_dir, "launch Login0.bat"), "w") as f:
    f.write(f'TITLE Login0\nWvsBeta.Login Login0')
with open(os.path.join(pub_dir, "launch Shop0.bat"), "w") as f:
    f.write(f'TITLE Shop0\nWvsBeta.Shop Shop0')
with open(os.path.join(pub_dir, "launch Redis.bat"), "w") as f:
    f.write(f'TITLE Redis\n"{os.path.join("..", "WvsBeta.Launcher", "redist", "redis", "redis-server.exe")}" "{os.path.join("..", "WvsBeta.Launcher", "redist", "redis", "redis.windows.conf")}"')

shutil.copytree(os.path.join(bin_dir, "evolutions"), os.path.join(pub_dir, "evolutions"))
