"""Bound training log writes; prune only this helper's completed successful logs."""
import argparse
from contextlib import redirect_stderr, redirect_stdout
import hashlib
import io
import json
from pathlib import Path
import re
import shutil
import sys
import time
import traceback

ROOT = Path(__file__).resolve().parents[1]
LOG_ROOT = ROOT / '.codex-tmp/yolo-training/run-logs'
PREFIX_BYTES = 1024**2
TAIL_BYTES = 64*1024
TOTAL_BYTES = 8*1024**2
KEEP_SUCCESSES = 2
RUN_ID = re.compile(r'[a-z0-9][a-z0-9-]{3,60}')


class BoundedLog(io.TextIOBase):
    """Write the first MiB once; retain a final 64 KiB in RAM, then write once."""
    def __init__(self, prefix_path, tail_path, prefix_bytes=PREFIX_BYTES, tail_bytes=TAIL_BYTES):
        if prefix_bytes <= 0 or tail_bytes <= 0:
            raise ValueError('Positive log budgets required')
        self.prefix_path, self.tail_path = Path(prefix_path), Path(tail_path)
        if self.prefix_path.exists() or self.tail_path.exists():
            raise FileExistsError('Never overwrite an existing training log')
        self.stream = self.prefix_path.open('xb', buffering=64*1024)
        self.remaining, self.tail_limit = prefix_bytes, tail_bytes
        self.tail = bytearray()
        self.received_bytes = self.prefix_written_bytes = self.tail_written_bytes = 0
        self.last_flush = time.monotonic()

    @property
    def encoding(self):
        return 'utf-8'

    def writable(self):
        return True

    def isatty(self):
        return False

    def write(self, value):
        if self.closed:
            raise ValueError('Training log is closed')
        if not isinstance(value, str):
            raise TypeError('Expected text')
        for start in range(0, len(value), 16*1024):
            data = value[start:start+16*1024].encode('utf-8', errors='replace')
            self.received_bytes += len(data)
            self.tail.extend(data)
            if len(self.tail) > self.tail_limit:
                del self.tail[:-self.tail_limit]
            if self.remaining:
                chunk = data[:self.remaining].decode('utf-8', errors='ignore').encode('utf-8')
                self.stream.write(chunk)
                self.prefix_written_bytes += len(chunk)
                self.remaining = max(0, self.remaining-len(data))
        self.flush()
        return len(value)

    def flush(self):
        # tqdm may flush every update; do not force a filesystem flush each time.
        if not self.closed and hasattr(self, 'stream') and not self.stream.closed and time.monotonic()-self.last_flush >= 5:
            self.stream.flush()
            self.last_flush = time.monotonic()

    def close(self):
        if self.closed:
            return
        if not hasattr(self, 'stream'):
            super().close()
            return
        self.stream.close()
        data = bytes(self.tail).decode('utf-8', errors='ignore').encode('utf-8')
        with self.tail_path.open('xb') as stream:
            stream.write(data)
        self.tail_written_bytes = len(data)
        self.tail.clear()
        super().close()


def clean_logs(folder=LOG_ROOT, *, reserve_bytes=0):
    """Keep failures and metadata; unlink only recorded SUCCESS prefix/tail files."""
    folder = Path(folder)
    folder = folder.resolve()
    if not folder.is_relative_to(ROOT.resolve()):
        raise ValueError('Log cleanup must stay inside this project')
    folder.mkdir(parents=True, exist_ok=True)
    files = list(folder.iterdir())
    if len(files) > 512 or any(p.is_symlink() or not p.is_file() for p in files):
        raise RuntimeError('Refusing unexpected log directory entries')
    total = sum(p.stat().st_size for p in files)
    completed = []
    for receipt in folder.glob('*.run.json'):
        if receipt.stat().st_size > 32*1024:
            raise RuntimeError('Oversized log receipt')
        record = json.loads(receipt.read_text(encoding='utf8'))
        name = receipt.name.removesuffix('.run.json')
        if record.get('owner') != 'magicmr-bounded-training-log-v1' or not RUN_ID.fullmatch(name):
            continue
        if record.get('status') == 'SUCCESS':
            completed.append((record['finished_unix'], name))
    completed.sort(reverse=True)
    removed = []
    for rank, (_, name) in reversed(list(enumerate(completed))):
        if rank < KEEP_SUCCESSES and total+reserve_bytes <= TOTAL_BYTES:
            continue
        for suffix in ('.prefix.log', '.tail.log'):
            path = folder/(name+suffix)
            if not path.exists():
                continue
            if path.is_symlink() or path.resolve().parent != folder:
                raise RuntimeError('Log deletion escaped owned directory')
            size = path.stat().st_size
            digest = hashlib.sha256()
            with path.open('rb') as stream:
                for chunk in iter(lambda:stream.read(64*1024), b''):
                    digest.update(chunk)
            removed.append({'name':path.name,'bytes':size,'sha256':digest.hexdigest()})
            path.unlink()
            total -= size
    if total+reserve_bytes > TOTAL_BYTES:
        raise RuntimeError('Log budget full; retained failure evidence prevents another run')
    return {'removed':removed[:16],'removed_count':len(removed),
            'removed_bytes':sum(r['bytes'] for r in removed),
            'removed_manifest_sha256':hashlib.sha256(json.dumps(removed,sort_keys=True).encode('utf8')).hexdigest(),
            'remaining_bytes':total,'limit_bytes':TOTAL_BYTES}


def run_with_bounded_logs(main):
    """Training entry point; framework imports occur after stdout/stderr capture."""
    if '--help' in sys.argv[1:] or '-h' in sys.argv[1:]:
        return main()
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument('--run-id', required=True)
    args, _ = parser.parse_known_args()
    if not RUN_ID.fullmatch(args.run_id):
        raise ValueError('Invalid training run id')
    # These guards also apply to log writes before the trainer's own resource gate.
    for path, minimum in ((ROOT,20*1024**3), ('/mnt/d',25*1024**3)):
        if Path(path).exists() and shutil.disk_usage(path).free < minimum:
            raise RuntimeError('Disk floor crossed before training log creation: '+str(path))
    cleanup = clean_logs(reserve_bytes=PREFIX_BYTES+TAIL_BYTES+32*1024)
    receipt = LOG_ROOT/(args.run_id+'.run.json')
    if receipt.exists():
        raise FileExistsError('Existing training receipt; choose a new run id')
    record = {'owner':'magicmr-bounded-training-log-v1','run_id':args.run_id,'status':'RUNNING',
              'status_scope':'Invocation exit only; not accuracy or deployment acceptance',
              'started_unix':time.time(),'prefix_limit_bytes':PREFIX_BYTES,'tail_limit_bytes':TAIL_BYTES,
              'helper_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              'cleanup':cleanup,'scope':'Python stdout/stderr; no native file-descriptor or external-tool log capture'}
    receipt.write_text(json.dumps(record,indent=2)+'\n',encoding='utf8')
    code = 0
    capture = BoundedLog(LOG_ROOT/(args.run_id+'.prefix.log'), LOG_ROOT/(args.run_id+'.tail.log'))
    with capture, redirect_stdout(capture), redirect_stderr(capture):
        try:
            main()
            record['status'] = 'SUCCESS'
        except BaseException as error:
            code = error.code if isinstance(error,SystemExit) and isinstance(error.code,int) else 1
            record['status'] = 'FAILED' if code else 'SUCCESS'
            record['error'] = (type(error).__name__+': '+str(error))[:2000]
            traceback.print_exc()
    record.update(finished_unix=time.time(),received_bytes=capture.received_bytes,
                  written_log_bytes=capture.prefix_written_bytes+capture.tail_written_bytes,
                  log_middle_omitted=capture.received_bytes>PREFIX_BYTES+TAIL_BYTES)
    receipt.write_text(json.dumps(record,indent=2)+'\n',encoding='utf8')
    record['cleanup_after'] = clean_logs()
    receipt.write_text(json.dumps(record,indent=2)+'\n',encoding='utf8')
    print(json.dumps({'training_status':record['status'],'receipt':str(receipt),
                      'written_log_bytes':record['written_log_bytes'],'exit_code':code}))
    if code:
        raise SystemExit(code)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cleanup', action='store_true', required=True)
    parser.parse_args()
    print(json.dumps(clean_logs()))
