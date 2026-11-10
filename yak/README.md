# Publishing Shrike to the Rhino Package Manager

Shrike is published with Yak, the command-line tool that ships with Rhino 8
(`C:\Program Files\Rhino 8\System\Yak.exe`). The package is owned by the
Rhino account that pushes it first, so always log in with the same account.

## Files

| File | Purpose |
| --- | --- |
| `manifest.yml` | Package name, version, authors, description, keywords, icon |
| `icon.png` | 256 x 256 package icon shown in the Package Manager |
| `build-yak.ps1` | Builds Release, stages `dist/`, runs `yak build` |

## Release checklist

1. Bump the version in **both** `Shrike/Shrike.csproj` (`<Version>`) and
   `yak/manifest.yml` (`version:`). Yak rejects a version that was already pushed.
   The script stops if the two differ.
2. Build and pack (from the repository root):

   ```powershell
   powershell -ExecutionPolicy Bypass -File yak\build-yak.ps1
   ```

   Use `-SkipBuild` if you just built Release in Visual Studio.
   The result is `yak\dist\shrike-<version>-rh8_<n>-win.yak`.
3. Log in (opens a browser; sign in with the account that should own the package):

   ```powershell
   & "C:\Program Files\Rhino 8\System\Yak.exe" login
   ```

4. Try the test server first. It is wiped regularly and is safe to experiment on:

   ```powershell
   & "C:\Program Files\Rhino 8\System\Yak.exe" push --source https://test.yak.rhino3d.com yak\dist\shrike-0.1.0-rh8_34-win.yak
   & "C:\Program Files\Rhino 8\System\Yak.exe" search --source https://test.yak.rhino3d.com --all --prerelease shrike
   ```

5. Push to the public server:

   ```powershell
   & "C:\Program Files\Rhino 8\System\Yak.exe" push yak\dist\shrike-0.1.0-rh8_34-win.yak
   ```

Replace the file name with the one `build-yak.ps1` printed.

## Notes

- A pushed version can't be overwritten. To hide a bad release use
  `Yak.exe yank Shrike <version>`, then push a higher version.
- The `rh8_<n>` part of the file name comes from the RhinoCommon version the
  project references. It is the oldest Rhino 8 service release that will be
  offered the package.
- A version with a pre-release suffix (for example `0.2.0-beta.1`) is only
  shown to users who tick "Include pre-releases".
