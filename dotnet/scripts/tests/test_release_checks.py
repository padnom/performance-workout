import io
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from release_checks import (ARCHITECTURES, ASSEMBLIES, WEB_PAYLOAD, check_version,
                            project_version, verify_certificate, verify_manifest, verify_package, verify_payload)


class ReleaseChecksTests(unittest.TestCase):
    def apk(self, omit=None, invalid_library=None, extra_architecture=None):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, 'w') as package:
            for entry in WEB_PAYLOAD:
                if entry != omit:
                    package.writestr(entry, '<html>offline</html>')
            for arch in ARCHITECTURES:
                libraries = [f'lib/{arch}/libassembly-store.so'] + [
                    f'lib/{arch}/libaot-Performance.{assembly}.dll.so' for assembly in ASSEMBLIES]
                for entry in libraries:
                    if entry != omit:
                        package.writestr(entry, b'broken' if entry == invalid_library else b'\x7fELFpayload')
            if extra_architecture:
                package.writestr(f'lib/{extra_architecture}/libassembly-store.so', b'\x7fELFpayload')
        stream.seek(0)
        return stream

    def test_both_architectures_and_all_aot_assemblies_are_required(self):
        verify_payload(self.apk())
        for arch in ARCHITECTURES:
            for assembly in ASSEMBLIES:
                with self.assertRaises(ValueError):
                    verify_payload(self.apk(omit=f'lib/{arch}/libaot-Performance.{assembly}.dll.so'))

    def test_missing_webview_or_store_is_not_a_standalone_release(self):
        for entry in [*WEB_PAYLOAD, 'lib/x86_64/libassembly-store.so']:
            with self.assertRaises(ValueError):
                verify_payload(self.apk(omit=entry))

    def test_named_but_invalid_native_payload_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Invalid Android native'):
            verify_payload(self.apk(invalid_library='lib/arm64-v8a/libassembly-store.so'))

    def test_wrong_or_extra_abi_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Expected arm64'):
            verify_payload(self.apk(extra_architecture='armeabi-v7a'))

    def test_unsigned_apk_is_rejected_by_external_signature_check(self):
        with tempfile.TemporaryDirectory() as folder:
            apk = Path(folder) / 'release.apk'
            apk.write_bytes(self.apk().getvalue())
            with patch('release_checks.subprocess.run', side_effect=subprocess.CalledProcessError(1, 'apksigner')):
                with self.assertRaises(subprocess.CalledProcessError):
                    verify_package(apk, ('1.1.0', 2), 'apksigner', 'aapt')

    def test_manifest_version_id_and_debug_flag(self):
        manifest = "package: name='fr.padnom.performance' versionCode='2' versionName='1.1.0'\n"
        verify_manifest(manifest, ('1.1.0', 2))
        for wrong in [manifest.replace("versionCode='2'", "versionCode='1'"),
                      manifest.replace('fr.padnom.performance', 'another.app'),
                      manifest + 'application-debuggable\n']:
            with self.assertRaises(ValueError):
                verify_manifest(wrong, ('1.1.0', 2))

    def test_other_or_multiple_signers_cannot_produce_a_compatible_update(self):
        digest = 'a' * 64
        verify_certificate('Signer #1 certificate SHA-256 digest: ' + digest, digest)
        for output in ['', 'certificate SHA-256 digest: ' + 'b' * 64,
                       ('certificate SHA-256 digest: ' + digest + '\n') * 2]:
            with self.assertRaises(ValueError):
                verify_certificate(output, digest)

    def test_initial_and_repeated_verification_but_not_repeated_distribution(self):
        check_version(('1.1.0', 2), ('1.0', 1), True)
        check_version(('1.1.0', 2), ('1.1.0', 2))
        with self.assertRaises(ValueError):
            check_version(('1.1.0', 2), ('1.1.0', 2), True)

    def test_version_regression_and_changed_label_without_code_increase(self):
        for current in [('1.0', 3), ('1.1.0', 1), ('1.2.0', 2)]:
            with self.assertRaises(ValueError):
                check_version(current, ('1.1.0', 2))

    def test_numeric_versions_compare_numerically(self):
        check_version(('1.10.0', 4), ('1.9.0', 3), True)
        check_version(('1.1.0', 2), ('1.1', 2))

    def test_project_version_validation(self):
        xml = '<Project><PropertyGroup><ApplicationDisplayVersion>1.1.0</ApplicationDisplayVersion><ApplicationVersion>2</ApplicationVersion></PropertyGroup></Project>'
        self.assertEqual(('1.1.0', 2), project_version(xml))
        for malformed in [xml.replace('1.1.0', 'latest'), xml.replace('>2<', '>0<'), '<Project/>']:
            with self.assertRaises(ValueError):
                project_version(malformed)


if __name__ == '__main__':
    unittest.main()
