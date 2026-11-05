"""Interpret relative audit paths identically on Windows and Linux."""
from pathlib import PurePosixPath, PureWindowsPath


def manifest_relative_path(value):
    if not isinstance(value, str) or not value.strip():
        raise ValueError('Dataset path must be a non-empty relative string')
    path = PurePosixPath(value.replace('\\', '/'))
    if path.is_absolute() or PureWindowsPath(value).drive or '..' in path.parts:
        raise ValueError('Dataset path must stay relative to the dataset root: ' + value)
    return path
