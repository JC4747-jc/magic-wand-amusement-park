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
        # A scene alias must not turn an explicitly excluded auxiliary class into
        # an interactive target. Legacy artifacts without an allowlist retain
        # their existing profile-based behavior.
        if 'allowed_classes' in metadata:
            declared = metadata['allowed_classes']
            classes = metadata.get('classes')
            names = list(classes.values()) if isinstance(classes, dict) else classes
            if (not isinstance(declared, list) or not declared
                    or not all(isinstance(name, str) for name in declared)
                    or not isinstance(names, list) or not names
                    or not all(isinstance(name, str) for name in names)
                    or not set(declared).issubset(names)):
                raise ValueError(f'MODEL_CLASS_POLICY_INVALID: {metadata_path}')
            aliases = result.get('aliases', {})
            active = set(result.get('allowed', aliases.values()))
            for name in names:
                if name not in declared and aliases.get(name.lower(), name) in active:
                    raise ValueError(f'MODEL_AUXILIARY_CLASS_ROUTED: {name}; {path}')
        # Combine, never replace: metadata cannot promote a pending/rejected
        # profile, and a profile cannot hide a rejected artifact status.
        if 'status' in metadata:
            status = metadata['status']
            status = status.strip() if isinstance(status, str) and status.strip() else 'UNVALIDATED'
            result['validation'] = result.get('validation', 'UNVALIDATED') + '; artifact:' + status
    return result, digest
