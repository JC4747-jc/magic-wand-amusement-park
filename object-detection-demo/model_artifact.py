"""Check a deployment artifact once at load time, not on the frame hot path."""
import hashlib
import json
from pathlib import Path


def verify_artifact(path, profile, loaded_hash=None):
    path = Path(path)
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if loaded_hash is not None and digest != loaded_hash:
        raise ValueError(f'MODEL_CHANGED_RESTART_REQUIRED: {path}; cached model differs from disk; restart or use a new version path')
    result = dict(profile)
    metadata_path = path.parent / 'model.json'
    if metadata_path.is_file():
        try:
            metadata = json.loads(metadata_path.read_text(encoding='utf-8-sig'))
        except (ValueError, OSError) as error:
            raise ValueError(f'MODEL_METADATA_INVALID: {metadata_path}') from error
        if not isinstance(metadata, dict):
            raise ValueError(f'MODEL_METADATA_INVALID: {metadata_path}')
        expected = metadata.get('sha256')
        if not isinstance(expected, str) or expected.lower() != digest:
            raise ValueError(f'MODEL_HASH_MISMATCH: {path}; check model.json or restore the matching weight')
        # Combine, never replace: metadata cannot promote a pending/rejected
        # profile, and a profile cannot hide a rejected artifact status.
        if 'status' in metadata:
            status = metadata['status']
            status = status.strip() if isinstance(status, str) and status.strip() else 'UNVALIDATED'
            result['validation'] = result.get('validation', 'UNVALIDATED') + '; artifact:' + status
    return result, digest
