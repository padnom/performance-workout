import argparse
from pathlib import Path
import subprocess
import sys
from release_checks import PROJECT, check_version, project_version


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Check tracked version against a previous Git release/ref.')
    parser.add_argument('--repo', type=Path, default=Path('.'))
    parser.add_argument('--baseline-ref', default='HEAD')
    parser.add_argument('--require-increase', action='store_true')
    args = parser.parse_args()
    try:
        current = project_version((args.repo / PROJECT).read_text())
        old = subprocess.run(['git', '-C', str(args.repo), 'show', f'{args.baseline_ref}:{PROJECT}'],
                             capture_output=True, text=True)
        if old.returncode:
            # Only allow the initial Android release when the ref exists but predates the project.
            subprocess.run(['git', '-C', str(args.repo), 'rev-parse', '--verify',
                            f'{args.baseline_ref}^{{commit}}'], check=True,
                           stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            previous = ('1.0', 1)
            print('Baseline predates Android project; using initial installed version 1.0/code 1.')
        else:
            previous = project_version(old.stdout)
        check_version(current, previous, args.require_increase)
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        sys.exit(f'Version verification failed: {error}')
    print(f'Version verified: {previous[0]}/{previous[1]} -> {current[0]}/{current[1]}.')
