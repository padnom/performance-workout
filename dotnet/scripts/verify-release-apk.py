import argparse
from pathlib import Path
import subprocess
import sys
import zipfile
from release_checks import project_version, verify_package


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Verify standalone Release AOT APK, signature and version.')
    parser.add_argument('apk', type=Path)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--build-tools', type=Path, required=True)
    parser.add_argument('--expected-certificate', type=Path, help='Public SHA-256 digest of the existing installation certificate.')
    args = parser.parse_args()
    try:
        version = project_version(args.project.read_text())
        certificate = args.expected_certificate.read_text().strip() if args.expected_certificate else None
        verify_package(args.apk, version, args.build_tools / 'apksigner', args.build_tools / 'aapt', certificate)
    except (ValueError, OSError, subprocess.CalledProcessError, zipfile.BadZipFile) as error:
        sys.exit(f'Release APK verification failed: {error}')
    print(f'Release {version[0]} (code {version[1]}), signature, arm64-v8a/x86_64 and AOT payload verified.')
