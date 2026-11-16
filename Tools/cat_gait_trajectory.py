"""Periodic paw trajectory with continuous velocity and acceleration at contact."""
import math


def paw_trajectory(phase, stance=.65, stride=.18, clearance=.055):
    if not 0 < stance < 1:
        raise ValueError('stance must be inside (0, 1)')
    phase %= 1.0
    if phase < stance:
        return (phase / stance - .5) * stride, 0.0
    swing = (phase - stance) / (1 - stance)
    # Quintic Hermite return: match stance velocity and zero acceleration at
    # both ends. A small overshoot allows departure before reversing direction.
    slope = (1 - stance) / stance
    return stride * (.5 + slope * swing - (1 + slope) *
                     (10 * swing**3 - 15 * swing**4 + 6 * swing**5)), \
        clearance * math.sin(math.pi * swing)**3
