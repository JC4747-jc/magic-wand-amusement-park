"""Record both successful and rejected responses without changing the wire."""
import json
import time
from pathlib import Path


def append_response(directory, session, payload):
    if directory is None:
        return
    with (Path(directory)/'responses.jsonl').open('a',encoding='utf-8') as journal:
        journal.write(json.dumps(dict(session=session,response=payload,returned_utc_ms=time.time_ns()//1_000_000),allow_nan=False)+'\n')
