import unittest
from cat_gait_trajectory import paw_trajectory


class ContactContinuity(unittest.TestCase):
    def test_contact_velocity_and_acceleration(self):
        h = 1e-5
        for contact in (0.0, .65):
            values = [paw_trajectory(contact + n*h) for n in (-2, -1, 0, 1, 2)]
            for axis in (0, 1):
                before = (values[2][axis] - values[1][axis]) / h
                after = (values[3][axis] - values[2][axis]) / h
                self.assertAlmostEqual(before, after, delta=1e-5)
                expected = .18/.65 if axis == 0 else 0
                self.assertAlmostEqual(after, expected, delta=1e-5)
                for a, b, c in (values[:3], values[2:]):
                    self.assertAlmostEqual((c[axis]-2*b[axis]+a[axis])/h**2, 0, delta=.02)

    def test_clearance_and_periodic_contact(self):
        for n in range(1001):
            phase=n/1000
            point=paw_trajectory(phase)
            self.assertGreaterEqual(point[1], 0)
            self.assertLessEqual(point[1], .055 + 1e-12)
            if phase < .65:
                self.assertEqual(point[1], 0)
            for a,b in zip(point,paw_trajectory(phase+1)):
                self.assertAlmostEqual(a,b,places=12)


if __name__ == '__main__':
    unittest.main()
