"""Transport lifecycle regressions; model predictions are explicit test doubles."""
import json
import socket
import struct
import threading
import unittest
from types import SimpleNamespace
from unittest.mock import patch

import cv2
import numpy as np
import tcp_server as server


class ReconnectTests(unittest.TestCase):
  def test_newest_frame_is_processed_without_waiting_for_a_third(self):
    config=server.InferenceConfig('cpu',False,640,.4,.7,1)
    result=SimpleNamespace(names={0:'lighter'},boxes=[],speed={})
    image=np.zeros((48,64,3),np.uint8)
    buffer=server.LatestFrameBuffer()
    buffer.publish(server.DecodedFrame(1,1000,64,48,image))
    calls=[]
    def predict(*args):
      calls.append(1)
      if len(calls)==1:
        buffer.publish(server.DecodedFrame(2,1100,64,48,image))
      return [result]
    client,connection=socket.socketpair()
    client.settimeout(3)
    stop=threading.Event()
    with patch.object(server,'run_yolo_predict',side_effect=predict):
      worker=threading.Thread(target=server.inference_loop,args=(connection,None,buffer,
        server.PerfAggregator(),stop,True,threading.Lock(),'cpu',config),daemon=True)
      worker.start()
      try:
        with client.makefile('rb') as response:
          payload=json.loads(response.readline())
        self.assertEqual(payload['frame_id'],2)
        self.assertEqual(len(calls),2)
      finally:
        stop.set()
        with buffer._cond: buffer._cond.notify_all()
        client.close()
        worker.join(3)
        connection.close()
      self.assertFalse(worker.is_alive())

  def send_frame(self, client, frame_id):
    ok, jpeg = cv2.imencode('.jpg', np.zeros((48,64,3), np.uint8))
    self.assertTrue(ok)
    client.sendall(b'PICF'+bytes([1])+struct.pack('<IqIII',frame_id,1000,64,48,len(jpeg))+jpeg.tobytes())

  def test_consecutive_sessions_have_fresh_buffers(self):
    config=server.InferenceConfig('cpu',False,640,.4,.7,1)
    result=SimpleNamespace(names={0:'lighter'},boxes=[],speed={})
    with patch.object(server,'run_yolo_predict',return_value=[result]):
      for frame_id in (9,1):
        client,connection=socket.socketpair()
        client.settimeout(3)
        worker=threading.Thread(target=server.serve_client,args=(connection,None,'cpu',config,True),daemon=True)
        worker.start()
        try:
          self.send_frame(client,frame_id)
          with client.makefile('rb') as response:
            payload=json.loads(response.readline())
          self.assertEqual(payload['frame_id'],frame_id)
          self.assertEqual(payload['detections'],[])
        finally:
          client.close()
          worker.join(3)
        self.assertFalse(worker.is_alive(),'Disconnected session left worker blocked')

  def test_worker_failure_unblocks_receiver(self):
    config=server.InferenceConfig('cpu',False,640,.4,.7,1)
    client,connection=socket.socketpair()
    client.settimeout(3)
    with patch.object(server,'run_yolo_predict',side_effect=ConnectionError('test worker failure')):
      worker=threading.Thread(target=server.serve_client,args=(connection,None,'cpu',config,True),daemon=True)
      worker.start()
      try:
        self.send_frame(client,1)
        self.assertEqual(client.recv(1),b'')
      finally:
        client.close()
        worker.join(3)
      self.assertFalse(worker.is_alive(),'Receiver blocked after inference worker stopped')


if __name__=='__main__':
  unittest.main(verbosity=2)
