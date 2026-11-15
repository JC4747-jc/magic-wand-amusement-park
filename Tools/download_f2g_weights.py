"""Fetch the installer's pinned official weights via Windows' existing proxy settings."""
import ast
import hashlib
from pathlib import Path
import urllib.request


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def main():
    root=Path(__file__).resolve().parents[1]
    tree=ast.parse((root/'Tools/setup_f2g_runtime.py').read_text(encoding='utf-8'))
    entries=next(ast.literal_eval(node.value) for node in ast.walk(tree)
        if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='downloads' for t in node.targets))
    output=root/'.codex-tmp/f2g-verified-weights'
    output.mkdir(parents=True,exist_ok=True)
    for url,expected,name in entries:
        destination=output/name
        if destination.exists():
            if digest(destination)!=expected: raise ValueError('Existing weight hash mismatch: '+name)
            print('VERIFIED existing '+name,flush=True); continue
        partial=output/(name+'.partial')
        offset=partial.stat().st_size if partial.exists() else 0
        request=urllib.request.Request(url,headers={'Range':f'bytes={offset}-'} if offset else {})
        with urllib.request.urlopen(request,timeout=60) as response:
            append=offset>0 and response.status==206
            if append and not response.headers.get('Content-Range','').startswith(f'bytes {offset}-'):
                raise ValueError('Invalid resume range')
            total=offset if append else 0
            mark=total//(32*1024*1024)
            with partial.open('ab' if append else 'wb') as stream:
                while chunk:=response.read(1024*1024):
                    stream.write(chunk); total+=len(chunk)
                    if total//(32*1024*1024)>mark:
                        mark=total//(32*1024*1024)
                        print(name+f' {total//1048576} MiB',flush=True)
        if digest(partial)!=expected: raise ValueError('Downloaded weight hash mismatch: '+name)
        partial.rename(destination)
        print('VERIFIED '+name+' '+expected,flush=True)


if __name__=='__main__':
    # Hold an OS lock across downloads; an interrupted chat may leave its process alive.
    import msvcrt
    lock_path=Path(__file__).resolve().parents[1]/'.codex-tmp/f2g-download.lock'
    lock_path.parent.mkdir(parents=True,exist_ok=True)
    with lock_path.open('a+b') as lock:
        lock.seek(0); lock.write(b'0'); lock.flush(); lock.seek(0)
        try:
            msvcrt.locking(lock.fileno(),msvcrt.LK_NBLCK,1)
        except OSError:
            raise SystemExit('A weight download is already running; wait for it to finish.')
        main()
