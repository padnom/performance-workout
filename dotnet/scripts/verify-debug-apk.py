import argparse
import sys
import zipfile


def verify_apk(path):
    with zipfile.ZipFile(path) as package:
        entries = set(package.namelist())
        architectures = sorted({name.split('/')[1] for name in entries if name.startswith('lib/')})
        if not architectures:
            sys.exit('APK has no Android runtime libraries.')
        required = {
            'assets/wwwroot/index.html',
            'assets/wwwroot/css/app.css',
            'assets/wwwroot/_framework/blazor.webview.js',
        }
        for architecture in architectures:
            for assembly in ('App', 'Application', 'Domain', 'Infrastructure'):
                required.add(f'lib/{architecture}/lib_Performance.{assembly}.dll.so')
        missing = sorted(required - entries)
        if missing:
            sys.exit('APK is not standalone; missing payload:\n' + '\n'.join(missing))
        damaged = package.testzip()
        if damaged:
            sys.exit(f'APK contains a damaged entry: {damaged}')
        print(f'Standalone Debug APK verified: {", ".join(architectures)}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Verify bundled .NET 10 Android Debug assemblies and Blazor UI.')
    parser.add_argument('apk')
    verify_apk(parser.parse_args().apk)
