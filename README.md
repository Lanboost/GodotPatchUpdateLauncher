# GodotPatchUpdateLauncher

A patcher / updater written for Godot (And PCK files), but should work fine for other stuff too...

# W.I.P and currently not fully working

- [Done] Able to apply patches
- [Done] Able to create patches
- Able to run without PCK files
- [Done] Able to fetch patches from the internet
- Ensure everything actually works good...

# Issues

- If a patch changes the patcher code, it will crash, add force reload to patch api...

# How to use

Build the project / add launch.cs to you current project. Update settings
for how patches are fetched etc.

# Create a patch

```
launcher.exe --createpatch --output=mypatchfile.patch \
	--from=path/from/old/version --to=path/to/new/version
```

# Testing

Currently a bad "testcase" can be found in "patchlibrary/PathLibraryTest/testfiles".
Start the pythin server
```
python3 patchserver.py
```
