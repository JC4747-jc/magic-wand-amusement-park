using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MagicMR
{
    // Opt-in, local evidence for the Dynamic XR hand callback in the saved Park scenes.
    public sealed class ParkHandEvidence : IDisposable
    {
        public const string EnableFile = "record-hands.enabled";
        public const int MaxSamples = 6000;
        public const int MaxBytes = 8 * 1024 * 1024;
        public const float MaxSeconds = 120f;
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        [Serializable]
        public struct Row
        {
            public int schema, sequence;
            public float elapsed_seconds, sample_seconds, pinch_m, target_distance_m;
            public Vector3 palm, index_tip, thumb_tip, index_base, swipe_axis;
            public Vector4 extension;
            public string scene, target_before, target_after;
            public bool joints_fresh, sample_valid, target_tracked_before, ui_capture_before,
                fit_pending_before, plane_pending_before, release_pending_before, in_reach_after,
                fist, open, pointing, spell_accepted;
            public EditDimension recognized;
            public long last_recorded_reply_frame_id, last_recorded_reply_capture_ns;
            public float last_recorded_reply_age_seconds;
        }

        readonly float startedAt;
        StreamWriter writer;
        int rows, bytes;
        long replyFrame=-1,replyCaptureNs;
        float replyAt;
        public static ParkHandEvidence Active { get; private set; }
        public bool IsOpen => writer != null;

        ParkHandEvidence(StreamWriter writer, float startedAt)
        {
            this.writer = writer;
            this.startedAt = startedAt;
        }

        public static ParkHandEvidence TryOpen(string directory, string persistentPath, float now)
        {
            if(string.IsNullOrEmpty(directory) || !float.IsFinite(now) ||
                !File.Exists(Path.Combine(persistentPath,EnableFile)))return null;
            try {
                string path=Path.Combine(directory,"hand-samples-"+Guid.NewGuid().ToString("N")+".jsonl");
                var next=new ParkHandEvidence(new StreamWriter(new FileStream(path,FileMode.CreateNew),Utf8),now);
                Active?.Dispose();
                Active=next;
                return next;
            }
            catch(IOException error) { Warn(error); }
            catch(UnauthorizedAccessException error) { Warn(error); }
            return null;
        }

        public void NoteRecordedReply(long frameId,long captureNs,float now)
        {
            replyFrame=frameId;replyCaptureNs=captureNs;replyAt=now;
        }

        public void Append(Row row,float now)
        {
            if(writer==null)return;
            if(!float.IsFinite(now)||now<startedAt||now-startedAt>=MaxSeconds) {Dispose();return;}
            row.schema=1;
            row.sequence=rows+1;
            row.elapsed_seconds=now-startedAt;
            row.last_recorded_reply_frame_id=replyFrame;
            row.last_recorded_reply_capture_ns=replyCaptureNs;
            row.last_recorded_reply_age_seconds=replyFrame>=0?now-replyAt:-1f;
            string line=JsonUtility.ToJson(row);
            int size=Utf8.GetByteCount(line)+1;
            if(bytes+size>MaxBytes) {Dispose();return;}
            try {
                writer.Write(line);
                writer.Write('\n');
                rows++;
                bytes+=size;
                if(rows>=MaxSamples)Dispose();
                else if(rows%30==0)writer.Flush();
            }
            catch(IOException error) { Warn(error);Dispose(); }
            catch(UnauthorizedAccessException error) { Warn(error);Dispose(); }
        }

        public void Dispose()
        {
            var stream=writer;
            writer=null;
            if(Active==this)Active=null;
            if(stream==null)return;
            try {stream.Dispose();}
            catch(IOException error) {Warn(error);}
            catch(UnauthorizedAccessException error) {Warn(error);}
        }

        static void Warn(Exception error) => Debug.LogWarning("[ParkHandEvidence] recording stopped: "+error.GetType().Name);
    }
}
