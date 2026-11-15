"""Fetch only the fixed Bee-concurrency patch and Android player, with hard bounds.

Does not execute installers, replace the current editor, download SDKs or train.
Official metadata integrity and local SHA-256 are recorded; Windows installer
signatures must also be verified before installation.
"""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import shutil
import time
import urllib.parse
import urllib.request

if not __debug__:
    raise RuntimeError('Run without Python -O; resource and integrity assertions must stay enabled')

VERSION = '6000.0.33f1'
REVISION = '433b0a79340b'
CAP = 5 * 1024**3
RESERVE = 30 * 1024**3


def hashes(path):
    md5, sha = hashlib.md5(), hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            md5.update(chunk)
            sha.update(chunk)
    return md5, sha


def fetch(item, directory):
    url = item['url']
    parsed = urllib.parse.urlparse(url)
    assert parsed.scheme == 'https' and parsed.netloc == 'download.unity3d.com'
    assert f'/download_unity/{REVISION}/' in parsed.path and parsed.path.endswith(VERSION + '.exe')
    listed_size = item['downloadSize']['value']
    assert item['downloadSize']['unit'] == 'BYTE' and 0 < listed_size < CAP
    # The release API rounds installer sizes (the editor differs by 680 bytes).
    # Bind the exact transfer length to HEAD, while retaining a strict margin
    # around official metadata and verifying the complete official MD5 below.
    with urllib.request.urlopen(urllib.request.Request(url, method='HEAD'), timeout=30) as head:
        size = int(head.headers['Content-Length'])
    assert abs(size - listed_size) < 1024 * 1024 and size < CAP, 'HTTP size differs materially from metadata'
    algorithm, expected = item['integrity'].split('-', 1)
    assert algorithm == 'md5'
    expected = base64.b64decode(expected, validate=True)
    target = directory / Path(parsed.path).name
    partial = target.with_suffix(target.suffix + '.partial')
    if target.exists():
        assert target.stat().st_size == size, f'Unexpected existing installer: {target}'
        md5, sha = hashes(target)
        assert md5.digest() == expected, f'Existing installer failed integrity: {target}'
        return dict(path=str(target), bytes=size, sha256=sha.hexdigest(), md5=md5.hexdigest())
    assert shutil.disk_usage(directory).free >= RESERVE, 'Require 30 GiB free on installer disk'
    offset = partial.stat().st_size if partial.exists() else 0
    assert 0 <= offset <= size, 'Unexpected partial file size'
    md5, sha = hashes(partial) if offset else (hashlib.md5(), hashlib.sha256())
    if offset < size:
        request = urllib.request.Request(url, headers={'Range': f'bytes={offset}-'} if offset else {})
        start, last = time.monotonic(), 0.0
        with urllib.request.urlopen(request, timeout=60) as response:
            if offset:
                assert response.status == 206 and response.headers['Content-Range'] == f'bytes {offset}-{size-1}/{size}', 'Server did not honor exact resume range'
            if response.headers.get('Content-Length'):
                assert int(response.headers['Content-Length']) == size - offset, 'Unexpected Content-Length'
            with partial.open('ab' if offset else 'xb') as stream:
                while True:
                    chunk = response.read(1024 * 1024)
                    if not chunk:
                        break
                    offset += len(chunk)
                    assert offset <= size, 'Download exceeded official size'
                    assert time.monotonic() - start < 45 * 60, 'Download exceeded 45 minute bound'
                    stream.write(chunk)
                    md5.update(chunk)
                    sha.update(chunk)
                    if time.monotonic() - last > 20:
                        print(f'{target.name}: {offset}/{size} bytes ({100*offset/size:.1f}%)', flush=True)
                        last = time.monotonic()
        assert offset == size, f'Incomplete bounded download: {offset}/{size}; partial retained'
    assert md5.digest() == expected, f'Official integrity mismatch: {partial}'
    partial.rename(target)
    print(f'VERIFIED {target.name}: {size} bytes, SHA256 {sha.hexdigest()}', flush=True)
    return dict(path=str(target), bytes=size, sha256=sha.hexdigest(), md5=md5.hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, default=Path('D:/Unity/Installers'))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    report = root / '.codex-tmp/unity-patch-downloads.json'
    metadata = root / '.codex-tmp/unity-6000.0.33f1-official.json'
    with urllib.request.urlopen(f'https://services.api.unity.com/unity/editor/release/v1/releases?version={VERSION}&limit=1', timeout=30) as response:
        data = response.read(2_000_001)
    assert len(data) <= 2_000_000, 'Metadata exceeds bound'
    payload = json.loads(data)
    release = payload['results'][0]
    assert release['version'] == VERSION and release['shortRevision'] == REVISION
    editor = next(d for d in release['downloads'] if d['platform'] == 'WINDOWS' and d['architecture'] == 'X86_64')
    android = next(m for m in editor['modules'] if m['id'] == 'android')
    items = [editor, android]
    assert sum(i['downloadSize']['value'] for i in items) < CAP
    assert sum(i['installedSize']['value'] for i in items) < 12 * 1024**3
    args.directory.mkdir(parents=True, exist_ok=True)
    metadata.parent.mkdir(parents=True, exist_ok=True)
    metadata.write_text(json.dumps(payload, ensure_ascii=True, indent=2), encoding='utf-8')
    # Sequential 1MiB streaming, no retry loop or unpacking into RAM.
    results = [fetch(item, args.directory) for item in items]
    report.write_text(json.dumps(dict(version=VERSION, revision=REVISION, installers=results), indent=2), encoding='utf-8')
    print('DOWNLOADS_COMPLETE: ' + str(report), flush=True)


if __name__ == '__main__':
    main()
