"""No-model checks for bounded writes and narrowly scoped log retention."""
from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace

import yolo_training_logs as logs


class TrainingLogsTests(unittest.TestCase):
    def setUp(self):
        parent = logs.ROOT/'.codex-tmp'
        parent.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix='training-log-test-',dir=parent)
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def receipt(self, name, status, finished):
        p = self.root/(name+'.run.json')
        p.write_text(json.dumps({'owner':'magicmr-bounded-training-log-v1',
                                'status':status,'finished_unix':finished}),encoding='utf8')
        for suffix in ('.prefix.log','.tail.log'):
            (self.root/(name+suffix)).write_text(name,encoding='utf8')

    def test_large_unicode_stream_has_fixed_write_and_tail_budget(self):
        prefix, tail = self.root/'first.log', self.root/'tail.log'
        capture = logs.BoundedLog(prefix,tail,prefix_bytes=4096,tail_bytes=1024)
        with capture:
            for _ in range(100):
                capture.write('训练日志🙂'*2048)
            capture.write('\nFINAL_FAILURE_SENTINEL\n')
        self.assertGreater(capture.received_bytes,1024**2)
        self.assertLessEqual(prefix.stat().st_size,4096)
        self.assertLessEqual(tail.stat().st_size,1024)
        self.assertIn('FINAL_FAILURE_SENTINEL',tail.read_text(encoding='utf8'))
        prefix.read_text(encoding='utf8')
        self.assertLessEqual(capture.prefix_written_bytes+capture.tail_written_bytes,5120)
        capture.close()
        capture.flush()

    def test_existing_log_is_not_overwritten(self):
        prefix = self.root/'protected.log';prefix.write_text('original')
        with self.assertRaises(FileExistsError):
            logs.BoundedLog(prefix,self.root/'tail.log')
        self.assertEqual(prefix.read_text(),'original')

    def test_cleanup_only_removes_old_success_logs(self):
        for i in range(4):
            self.receipt('success-'+str(i),'SUCCESS',i)
        self.receipt('failure-0','FAILED',0)
        self.receipt('running-0','RUNNING',0)
        weight = self.root/'best.pt';weight.write_bytes(b'never-delete')
        metrics = self.root/'metrics.json';metrics.write_text('{}')
        result = logs.clean_logs(self.root)
        self.assertEqual(len(result['removed']),4)
        self.assertFalse((self.root/'success-0.prefix.log').exists())
        for name in ('success-2','success-3','failure-0','running-0'):
            self.assertTrue((self.root/(name+'.prefix.log')).exists())
        self.assertTrue((self.root/'success-0.run.json').exists())
        self.assertEqual(weight.read_bytes(),b'never-delete')
        self.assertEqual(metrics.read_text(),'{}')

    def test_failure_budget_stops_without_deleting_failure(self):
        self.receipt('failure-0','FAILED',0)
        with patch.object(logs,'TOTAL_BYTES',1):
            with self.assertRaisesRegex(RuntimeError,'Log budget full'):
                logs.clean_logs(self.root)
        self.assertTrue((self.root/'failure-0.prefix.log').exists())

    def test_cleanup_refuses_outside_project_before_creating_folder(self):
        with tempfile.TemporaryDirectory() as outside:
            folder=Path(outside)/'must-not-create'
            with self.assertRaises(ValueError):logs.clean_logs(folder)
            self.assertFalse(folder.exists())

    def test_exception_entrypoint_keeps_bounded_failure_evidence(self):
        def fail():
            print('TRAINING_OUTPUT_START')
            raise RuntimeError('SIMULATED_FAILURE_NO_MODEL')
        with patch.object(logs,'LOG_ROOT',self.root),patch('sys.argv',['runner','--run-id','test-failure']),patch.object(logs.shutil,'disk_usage',return_value=SimpleNamespace(free=100*1024**3)),redirect_stdout(io.StringIO()):
            with self.assertRaises(SystemExit) as error:logs.run_with_bounded_logs(fail)
        self.assertEqual(error.exception.code,1)
        record=json.loads((self.root/'test-failure.run.json').read_text(encoding='utf8'))
        self.assertEqual(record['status'],'FAILED')
        tail=(self.root/'test-failure.tail.log').read_text(encoding='utf8')
        self.assertIn('SIMULATED_FAILURE_NO_MODEL',tail)
        self.assertIn('TRAINING_OUTPUT_START',tail)
        self.assertLessEqual(record['written_log_bytes'],logs.PREFIX_BYTES+logs.TAIL_BYTES)

    def test_disk_floor_prevents_log_and_training_creation(self):
        folder=self.root/'not-created'
        from unittest.mock import Mock
        main=Mock()
        with patch.object(logs,'LOG_ROOT',folder),patch('sys.argv',['runner','--run-id','test-disk-floor']),patch.object(logs.shutil,'disk_usage',return_value=SimpleNamespace(free=0)):
            with self.assertRaisesRegex(RuntimeError,'Disk floor crossed'):
                logs.run_with_bounded_logs(main)
        main.assert_not_called()
        self.assertFalse(folder.exists())


if __name__ == '__main__':unittest.main(verbosity=2)
