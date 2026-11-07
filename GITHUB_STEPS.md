# Publishing Golden Hour on GitHub

The repository is prepared but **not pushed**. Run these commands from the project root in PowerShell. The GitHub CLI `gh` must be installed and logged in (`gh auth login`).

```powershell
cd 'C:\gdit\My project (3)\My project (2)'
git status                      # should report a clean tree
git branch -M main              # publish the work as 'main' (current branch: master)
gh repo create golden-hour-triage --public --source . --remote origin `
  --description "Educational first-person serious game: START mass-casualty triage, first aid and ambulance handover (Unity 6, URP)."
git push -u origin main
```

Without `gh`: create an empty repository on github.com (no README or licence), then run:

```powershell
git remote add origin https://github.com/nithinkumar8341/golden-hour-triage.git
git push -u origin main
```

## Release with the Windows build

`Release/GoldenHour_Windows.zip` is git-ignored. Attach it to a release:

```powershell
git tag -a v2.1.0 -m "Golden Hour v2.1: in-world examination, supply bag, patient tasks, visible treatment"
git push origin v2.1.0
gh release create v2.1.0 Release/GoldenHour_Windows.zip --title "Golden Hour v2.1 (Windows)" `
  --notes "Unzip and run GoldenHour.exe (Windows 10/11, 64-bit). Educational only - the content has NOT been medically reviewed. See README for controls."
```

## Repository description and topics

- **Description:** Educational first-person serious game: START mass-casualty triage, first aid and ambulance handover (Unity 6, URP).
- **Topics:**

```powershell
gh repo edit --add-topic unity --add-topic serious-game --add-topic triage --add-topic start-triage --add-topic first-aid `
  --add-topic emergency-medicine --add-topic education --add-topic csharp --add-topic blender --add-topic urp
```

## Before pushing

- No tracked file is larger than 50 MB. The largest was checked when this file was written; see DEV_LOG.
- `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Builds/`, `Release/` and IDE files are ignored by `.gitignore`.
