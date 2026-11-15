"""Install the actual standalone APK, verify launch/render and collect CI evidence."""
import argparse
from pathlib import Path
import re
import subprocess
import sys
import time

PACKAGE = 'fr.padnom.performance'
# Software-rendered CI emulators with two cores can take well over 30 s for the first WebView frame.
RENDER_TIMEOUT_SECONDS = 120
LOG_PATTERN = re.compile(r'performance|mono|dotnet|AndroidRuntime|chromium|WebView|FATAL| E ', re.IGNORECASE)


def adb(*args, check=True):
    return subprocess.run(['adb', *args], capture_output=True, text=True,
                          check=check, timeout=45)


def smoke(apk, output):
    output.mkdir(parents=True, exist_ok=True)
    adb('wait-for-device')
    adb('install', '-r', str(apk))
    adb('shell', 'am', 'force-stop', PACKAGE)
    adb('logcat', '-c')
    resolved = adb('shell', 'cmd', 'package', 'resolve-activity', '--brief', PACKAGE).stdout.strip().splitlines()
    if not resolved or '/' not in resolved[-1]:
        raise ValueError('No Android launcher activity found.')
    component = resolved[-1]
    adb('shell', 'am', 'start', '-W', '-n', component)
    started = time.monotonic()
    deadline = started + RENDER_TIMEOUT_SECONDS
    rendered = False
    while time.monotonic() < deadline:
        pid = adb('shell', 'pidof', PACKAGE, check=False).stdout.strip()
        if not pid:
            raise ValueError('Application process exited after launch.')
        adb('shell', 'rm', '-f', '/sdcard/performance-smoke.xml')
        adb('shell', 'uiautomator', 'dump', '/sdcard/performance-smoke.xml', check=False)
        ui = adb('shell', 'cat', '/sdcard/performance-smoke.xml', check=False).stdout
        (output / 'launch-ui.xml').write_text(ui)
        if PACKAGE in ui and 'Accueil' in ui and 'Programme actif' in ui:
            rendered = True
            break
        time.sleep(1)
    if not rendered:
        raise ValueError(f'Process started but the Blazor dashboard did not render within {RENDER_TIMEOUT_SECONDS} seconds.')
    print(f'Dashboard rendered after {time.monotonic() - started:.0f} s.')
    # Catch late startup crashes, including native crashes after the first frame.
    time.sleep(3)
    if not adb('shell', 'pidof', PACKAGE, check=False).stdout.strip():
        raise ValueError('Application process exited after dashboard rendering.')
    crashes = adb('logcat', '-b', 'crash', '-d').stdout
    if PACKAGE in crashes:
        raise ValueError('Android crash buffer contains an application crash.')
    print('Standalone Release APK installed; process and Blazor dashboard verified on emulator.')


def print_diagnostics(output):
    """Echo the visible text and relevant log lines so failures can be read from the job log."""
    try:
        ui = (output / 'launch-ui.xml').read_text() if (output / 'launch-ui.xml').exists() else ''
        texts = [text for text in re.findall(r'(?:text|content-desc)="([^"]+)"', ui) if text.strip()]
        print('--- Visible UI text ---')
        print(' | '.join(texts[:80]) or '(none)')
        log = adb('logcat', '-d', check=False).stdout.splitlines()
        print('--- Relevant logcat (last 150 lines) ---')
        print('\n'.join([line for line in log if LOG_PATTERN.search(line)][-150:]))
        print('--- Crash buffer ---')
        print(adb('logcat', '-b', 'crash', '-d', check=False).stdout[-4000:] or '(empty)')
    except (OSError, subprocess.SubprocessError) as error:
        print(f'Diagnostics unavailable: {error}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('apk', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        smoke(args.apk, args.output)
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        print_diagnostics(args.output)
        sys.exit(f'Android smoke test failed: {error}')
    finally:
        args.output.mkdir(parents=True, exist_ok=True)
        try:
            (args.output / 'logcat.txt').write_text(adb('logcat', '-d', check=False).stdout)
            with (args.output / 'launch.png').open('wb') as screenshot:
                subprocess.run(['adb', 'exec-out', 'screencap', '-p'], stdout=screenshot,
                               stderr=subprocess.PIPE, timeout=15, check=False)
        except (OSError, subprocess.SubprocessError):
            pass
