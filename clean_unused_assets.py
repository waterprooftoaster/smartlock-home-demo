import pandas as pd
import os
from dotenv import load_dotenv

#DEFINE GLOBAL VARIABLES==========================================================
DRY_RUN = False
PRINT_USED_ASSETS = False
PRINT_ALL_ASSETS = False
PRINT_UNUSED_ASSETS = False
PRINT_DELETED_ASSETS = False
SAFE_EXTENSIONS = {".asmdef", ".dll", ".csproj", ".json", ".shader", ".cginc", ".hlsl"}
SAFE_FILES = {"manifest.json", "package.json"}
SAFE_DIRS = {"ProjectSettings", "Packages", ".git", "Scenes"}
#Buildreport and project paths are defined in .env
#==============================================================================

load_dotenv()
BUILDREPORTCSV_PATH = os.getenv("BUILDREPORTCSV_PATH")
PROJECT_PATH = os.getenv("PROJECT_PATH")
BUILDREPORTCSV_PATH = BUILDREPORTCSV_PATH.replace("\\", "/")
PROJECT_PATH = PROJECT_PATH.replace("\\", "/")

if not BUILDREPORTCSV_PATH or not PROJECT_PATH:
    print ("===ERROR===\n"
        "Path(s) not defined in .env.")
else:
    print("== STEP1: Parsing Buildreport for Used Assets ===")

df = pd.read_csv(BUILDREPORTCSV_PATH, usecols=[0])
used_assets = set(df.iloc[:, 0])
used_assets = {PROJECT_PATH + "/" + path for path in used_assets}
print(f"Found {len(used_assets)} used assets.")

if PRINT_USED_ASSETS:
    print("Used Assets:")
    for path in used_assets:
        print (path)
else:
    # print("PRINT_USED_ASSETS set to false")
    print("Parsed all used assets found in buildreport")

print()
print("=== STEP2: Finding Assets to Delete ===")

all_assets = set()
total_asset_num = 0
for dirpath, dirnames, filenames in os.walk(PROJECT_PATH):
    for filename in filenames:
        total_asset_num += 1
        filepath = os.path.join(dirpath, filename).replace("\\", "/")
        if any(filename.endswith(ext) for ext in SAFE_EXTENSIONS):
            continue
        elif any(part in SAFE_DIRS for part in filepath.replace("\\", "/").split("/")):
            continue
        elif filename in SAFE_FILES:
            continue
        else:
            all_assets.add(filepath)
print(f"Found {total_asset_num} assets")
print(f"Found {len(all_assets)} total unprotected assets.")

if PRINT_ALL_ASSETS:
    print("Used Assets:")
    for path in all_assets:
        print (path)
else:
    print("Parsed all used assets found in buildreport.")

unused_assets = set()
for path in all_assets:
    if path not in used_assets:
        unused_assets.add(path)

print()
print("=== STEP3: Removing Bloat ===")
if DRY_RUN:
    print(f"DRY RUN: Found {len(unused_assets)} to be deleted.")
else:
    for file in unused_assets:
        os.remove(file)
        if PRINT_DELETED_ASSETS:
            print(f"Deleted: {file}")
    print(f"WET RUN: removed {len(unused_assets)} files.")
