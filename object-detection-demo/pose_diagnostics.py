"""Additive pose diagnostics. Timings are host wall time, not isolated GPU time."""
from contextlib import contextmanager
import time


def rejection_code(reason):
    """Also classify historical replies which predate structured diagnostic fields."""
    for text, code in (
        ('detected 0', 'NO_TARGET'),
        ('Require exactly one lighter', 'MULTIPLE_TARGETS'),
        ('Target clipped', 'TARGET_CLIPPED'),
        ('Low SAM mask score', 'LOW_MASK_SCORE'),
        ('Mask extends beyond', 'MASK_OUTSIDE_BOX'),
        ('Insufficient foreground mask', 'INSUFFICIENT_MASK'),
        ('Insufficient foreground depth', 'INSUFFICIENT_DEPTH'),
        ('POSE_BUSY', 'BUSY'),
        ('RECORD_ONLY', 'RECORD_ONLY'),
    ):
        if text in reason:
            return code
    return 'OK' if reason == 'OK' else 'UNCLASSIFIED'


@contextmanager
def measure_stage(diagnostics, name):
    diagnostics['last_stage'] = name
    started = time.perf_counter()
    try:
        yield
    finally:
        diagnostics.setdefault('timings_ms', {})[name] = (time.perf_counter() - started) * 1000
