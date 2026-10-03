"""Release checks shared by the CI commands and their regression tests."""
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile

PROJECT = 'dotnet/src/Performance.App/Performance.App.csproj'
ARCHITECTURES = {'arm64-v8a', 'x86_64'}
ASSEMBLIES = ('App', 'Application', 'Domain', 'Infrastructure')
WEB_PAYLOAD = {
    'assets/wwwroot/index.html', 'assets/wwwroot/css/app.css',
    'assets/wwwroot/_framework/blazor.webview.js',
}


def project_version(xml):
    root = ET.fromstring(xml)
    display = root.findtext('.//ApplicationDisplayVersion')
    code = root.findtext('.//ApplicationVersion')
    if not display or not re.fullmatch(r'\d+\.\d+(?:\.\d+)?', display):
        raise ValueError('ApplicationDisplayVersion must be a numeric major.minor[.patch].')
    if not code or not code.isdecimal() or not 0 < int(code) <= 2100000000:
        raise ValueError('ApplicationVersion must be a valid positive Android version code.')
    return display, int(code)


def check_version(current, previous, require_increase=False):
    def parts(display):
        numbers = tuple(map(int, display.split('.')))
        return numbers + (0,) * (3 - len(numbers))
    display, code = current
    old_display, old_code = previous
    if parts(display) < parts(old_display) or code < old_code:
        raise ValueError('Release version cannot move backwards.')
    if parts(display) != parts(old_display) and code <= old_code:
        raise ValueError('Changing the display version requires an increased Android version code.')
    if require_increase and code <= old_code:
        raise ValueError('A distributable release requires an increased Android version code.')


def verify_payload(path):
    with zipfile.ZipFile(path) as package:
        entries = set(package.namelist())
        architectures = {name.split('/')[1] for name in entries
                         if name.startswith('lib/') and len(name.split('/')) >= 3}
        if architectures != ARCHITECTURES:
            raise ValueError(f'Expected arm64-v8a and x86_64; found {sorted(architectures)}.')
        required = set(WEB_PAYLOAD)
        for arch in ARCHITECTURES:
            required.add(f'lib/{arch}/libassembly-store.so')
            required.update(f'lib/{arch}/libaot-Performance.{assembly}.dll.so'
                            for assembly in ASSEMBLIES)
        missing = sorted(required - entries)
        if missing:
            raise ValueError('Release APK missing standalone/AOT payload: ' + ', '.join(missing))
        for entry in required:
            if package.getinfo(entry).file_size == 0:
                raise ValueError(f'Empty APK payload: {entry}')
            if entry.endswith('.so'):
                with package.open(entry) as library:
                    if library.read(4) != b'\x7fELF':
                        raise ValueError(f'Invalid Android native library: {entry}')
        damaged = package.testzip()
        if damaged:
            raise ValueError(f'Damaged APK entry: {damaged}')


def verify_manifest(badging, expected):
    package = next((line for line in badging.splitlines() if line.startswith('package:')), '')
    fields = dict(re.findall(r"(\w+)='([^']*)'", package))
    display, code = expected
    if fields.get('name') != 'fr.padnom.performance':
        raise ValueError('Unexpected APK application ID.')
    if fields.get('versionName') != display or fields.get('versionCode') != str(code):
        raise ValueError('APK version differs from the project version.')
    if 'application-debuggable' in badging:
        raise ValueError('Release APK must not be debuggable.')


def verify_certificate(signing_output, expected_certificate):
    certificates = re.findall(r'certificate SHA-256 digest: ([0-9a-fA-F]{64})', signing_output)
    if not re.fullmatch(r'[0-9a-fA-F]{64}', expected_certificate):
        raise ValueError('Expected signing certificate must be a SHA-256 digest.')
    if [certificate.lower() for certificate in certificates] != [expected_certificate.lower()]:
        raise ValueError('Signing certificate differs from the existing installation; update would be rejected.')


def verify_package(path, expected, apksigner, aapt, expected_certificate=None):
    verify_payload(path)
    signature = subprocess.run([str(apksigner), 'verify', '--verbose', '--print-certs', str(path)], check=True,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    if expected_certificate is not None:
        verify_certificate(signature.stdout, expected_certificate)
    result = subprocess.run([str(aapt), 'dump', 'badging', str(path)], check=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    verify_manifest(result.stdout, expected)
