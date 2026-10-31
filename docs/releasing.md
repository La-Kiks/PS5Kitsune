# Releasing PS5Kitsune

A release is a tag `vX.Y.Z` on `main`. Pushing it runs `.github/workflows/release.yml`, which checks that the tag matches `<Version>` in `Directory.Build.props`, builds and tests everything with `ci.yml` (the same jobs as every pull request, license check included), then opens a **draft** GitHub Release with:

| File | What it is |
|---|---|
| `PS5Kitsune-X.Y.Z-win-x64.exe` | The Windows app, one self-contained file |
| `PS5Kitsune-X.Y.Z-x86_64.AppImage` | The Linux app and command-line tool |
| `SHA256SUMS.txt` | The SHA-256 of both |

Nothing is public until the draft is published by hand.

## Steps

1. **Pick the version.** While the project is 0.x, raise the minor version for new features (`0.2.0`) and the patch version for fixes only (`0.1.1`).
2. **Set it on a branch.** Change `<Version>` in `Directory.Build.props`, merge the pull request into `main` once CI is green.
3. **Tag `main`.**
   ```bash
   git switch main && git pull
   git tag -a vX.Y.Z -m "PS5Kitsune X.Y.Z"
   git push origin vX.Y.Z
   ```
4. **Wait for the Release workflow** (Actions, then Release; about 10 minutes). A tag that does not match `<Version>` fails in the first job: delete the tag (`git push origin :refs/tags/vX.Y.Z` and `git tag -d vX.Y.Z`), fix the version, tag again.
5. **Smoke-test the draft's files.** Download them from the draft release:
   - Windows: the `.exe` opens and the System check is green; `PS5Kitsune-X.Y.Z-win-x64.exe --probe-vulkan` prints JSON and exits with 0.
   - Linux: `./PS5Kitsune-X.Y.Z-x86_64.AppImage --version` prints `X.Y.Z`, and the app opens.
   - `sha256sum -c SHA256SUMS.txt` passes.
6. **Write the notes.** The draft lists the merged pull requests. Above them, add a few lines: what is new, what is known not to work (see KNOWN_ISSUES.md), and the AnyPS5 version the default profile installs. The `.exe` is not signed, so say that Windows SmartScreen asks for "More info, then Run anyway".
7. **Publish** the draft. It becomes the latest release, which is what the README's download links point at.

## Rollback

A bad release: edit it on GitHub and turn it back into a draft (or delete it), then delete its tag as in step 4. Ship the fix as the next patch version rather than reusing a version number: people may already have the old files.
