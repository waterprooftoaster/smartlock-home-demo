import os
import re
import unicodedata

# === CONFIGURATION ===
PROJECT_PATH = r"C:\Users\swnar\projects\SmartlockGUI" #path to root of project, change if needed
LOG_PATH = os.path.join(PROJECT_PATH, "Logs", "Editor.log")  #to log, change if needed
ASSETS_PATH = os.path.join(PROJECT_PATH, "Assets")
SAFE_EXTENSIONS = {'.cs', '.meta', '.asmdef', '.cginc', '.hlsl', '.shader', '.uxml', '.uss'}
DRY_RUN = True  # Set to False to actually delete files

def clean_line(line):
    return unicodedata.normalize('NFKC', line).strip()

print(" === STEP 1: Parsing Editor.log for used asset paths ===")

used_assets = set()
asset_line_re = re.compile(r'(Assets\/.+?\.\w{2,5})')

with open(LOG_PATH, 'r', encoding='utf-8') as log_file:
    for i, line in enumerate(log_file):
        if 'Assets/' in line:
            print(f"[{i}] {repr(line)}")  # shows invisible characters

with open(LOG_PATH, 'r', encoding='utf-8') as log_file:
    for raw_line in log_file:
        cleaned_line = clean_line(raw_line)
        match = asset_line_re.search(cleaned_line)
        if match:
            relative_path = match.group(1).strip()
            norm_path = os.path.normpath(os.path.join(PROJECT_PATH, relative_path))
            used_assets.add(norm_path)

print()
print("printing parsed used asset paths")
for i in used_assets:
    print(i)
    
print()
print()
print("=== STEP 2: Get all assets ===") 

all_asset_files = []
for root, _, files in os.walk(ASSETS_PATH):
    for f in files:
        full_path = os.path.normpath(os.path.join(root, f))
        all_asset_files.append(full_path)

# === STEP 3: Compare and find unused ===
unused_files = []
for fpath in all_asset_files:
    ext = os.path.splitext(fpath)[1].lower()
    if ext in SAFE_EXTENSIONS:
        continue  # Never delete these
    if fpath not in used_assets:
        unused_files.append(fpath)

# === STEP 4: Delete or dry run ===
if DRY_RUN:
    print()
    print()
    print("\n-- DRY RUN -- Files that would be deleted:")
    for f in unused_files:
        print(f)
else:
    print("\n-- DELETING UNUSED FILES --")
    for f in unused_files:
        try:
            os.remove(f)
            print(f"Deleted: {f}")
        except Exception as e:
            print(f"Error deleting {f}: {e}")

print()
print()
print("===================SUMMARY===================")
print(f"Found {len(all_asset_files)} total files in Assets")
print(f"Found {len(used_assets)} used asset paths.")
print(f"{len(unused_files)} files are not in build log and are eligible for deletion.")
print(f"{len(all_asset_files) - len(used_assets)}")
print("\nDone.")
