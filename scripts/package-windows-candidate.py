#!/usr/bin/env python3
"""Create a reproducible local Windows x64 development package."""

from __future__ import annotations

import hashlib
import os
import platform
import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "FrameStudio.Avalonia" / "FrameStudio.Avalonia.csproj"
DIST = ROOT / "dist"
PACKAGE_NAME = "FrameStudio-win-x64"
PACKAGE_DIR = DIST / PACKAGE_NAME
ARCHIVE = DIST / f"{PACKAGE_NAME}.zip"
CHECKSUM_FILE = DIST / f"{PACKAGE_NAME}.zip.sha256"
EXECUTABLE = "FrameStudio.Avalonia.exe"


def run(command: list[str], *, capture_output: bool = True) -> str:
    result = subprocess.run(command, cwd=ROOT, check=True, text=True, capture_output=capture_output)
    return result.stdout.strip() if capture_output else ""


def main() -> None:
    DIST.mkdir(parents=True, exist_ok=True)
    version = run(["dotnet", "msbuild", str(PROJECT), "-getProperty:Version"])
    revision = run(["git", "rev-parse", "--short", "HEAD"])
    sdk_version = run(["dotnet", "--version"])
    modified = bool(run(["git", "status", "--porcelain", "--untracked-files=normal"]))
    host = f"{platform.system()} {platform.machine()}"

    with tempfile.TemporaryDirectory(prefix="framestudio-win-x64-") as temporary:
        publish_dir = Path(temporary) / "publish"
        run(
            [
                "dotnet",
                "publish",
                str(PROJECT),
                "--configuration",
                "Release",
                "--runtime",
                "win-x64",
                "--self-contained",
                "true",
                "--output",
                str(publish_dir),
            ],
            capture_output=False,
        )

        if not (publish_dir / EXECUTABLE).is_file():
            raise FileNotFoundError(f"Publish output is missing {EXECUTABLE}")

        (publish_dir / "README.txt").write_text(
            f"""Frame Studio {version} — Windows x64 development build

Source revision: {revision}{' (modified worktree)' if modified else ''}
Runtime: self-contained .NET 9
Build host: {host}, .NET SDK {sdk_version}

Run {EXECUTABLE} on Windows 10 version 2004 or later, or Windows 11. Screen recording uses Windows GDI and requires an interactive desktop session.

The executable passed a non-interactive startup smoke check on a hosted Windows runner. The app has not been validated in an interactive Windows desktop session; capture, mixed-DPI selection, and the end-to-end record → edit → GIF/MP4 workflow are not runtime verified. Do not treat it as a verified capture release.

MP4 export requires a separately installed FFmpeg build with the libx264 encoder available on PATH. FFmpeg is not included in this package.

Frame Studio is an independent Avalonia port based on ScreenToGif. It is not the official ScreenToGif application and is not affiliated with Nicke Manarin or N-Tech. The complete upstream license and attribution are included in LICENSE.txt.
""",
            encoding="utf-8",
        )
        shutil.copy2(ROOT / "LICENSE.txt", publish_dir / "LICENSE.txt")

        if PACKAGE_DIR.exists():
            shutil.rmtree(PACKAGE_DIR)
        shutil.copytree(publish_dir, PACKAGE_DIR)

        temporary_archive = DIST / f".{PACKAGE_NAME}.zip.tmp"
        try:
            with zipfile.ZipFile(temporary_archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
                for path in sorted(PACKAGE_DIR.rglob("*")):
                    if path.is_file():
                        archive.write(path, Path(PACKAGE_NAME) / path.relative_to(PACKAGE_DIR))

            with zipfile.ZipFile(temporary_archive) as archive:
                required = {
                    f"{PACKAGE_NAME}/{EXECUTABLE}",
                    f"{PACKAGE_NAME}/README.txt",
                    f"{PACKAGE_NAME}/LICENSE.txt",
                }
                missing = required - set(archive.namelist())
                if missing:
                    raise RuntimeError(f"Package is missing required files: {sorted(missing)}")
                bad_member = archive.testzip()
                if bad_member:
                    raise RuntimeError(f"Archive integrity check failed at {bad_member}")

            os.replace(temporary_archive, ARCHIVE)
        finally:
            temporary_archive.unlink(missing_ok=True)

    digest = hashlib.sha256(ARCHIVE.read_bytes()).hexdigest()
    CHECKSUM_FILE.write_bytes(f"{digest}  {ARCHIVE.name}\n".encode("ascii"))
    print(f"Package: {ARCHIVE}")
    print(f"SHA-256: {digest}")
    print("Status: preview package built; Windows runtime validation is still required.")


if __name__ == "__main__":
    main()
