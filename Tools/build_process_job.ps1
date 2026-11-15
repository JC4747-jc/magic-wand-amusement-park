if(-not ('MagicMRBuildJob' -as [type])){
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

// An unnamed, non-inherited handle contains only the process we start and its
// future children. Closing it also reaps children whose parent already exited.
public sealed class MagicMRBuildJob : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits {
        public long ProcessTime,JobTime; public uint Flags;
        public UIntPtr MinWorkingSet,MaxWorkingSet; public uint ActiveProcessLimit;
        public UIntPtr Affinity; public uint PriorityClass,SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters {
        public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;
    }
    [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits {
        public BasicLimits Basic; public IoCounters Io;
        public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern IntPtr CreateJobObject(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)]
    static extern bool SetInformationJobObject(IntPtr job,int infoClass,ref ExtendedLimits data,uint length);
    [DllImport("kernel32.dll",SetLastError=true)]
    static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    IntPtr handle;
    MagicMRBuildJob(IntPtr value){handle=value;}
    public static MagicMRBuildJob Attach(Process process) {
        IntPtr value=CreateJobObject(IntPtr.Zero,null);
        if(value==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        var job=new MagicMRBuildJob(value);
        try {
            var limits=new ExtendedLimits();limits.Basic.Flags=0x2000; // KILL_ON_JOB_CLOSE; no breakaway flags.
            if(!SetInformationJobObject(value,9,ref limits,(uint)Marshal.SizeOf<ExtendedLimits>()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if(!AssignProcessToJobObject(value,process.Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return job;
        }catch{job.Dispose();throw;}
    }
    public void Dispose(){IntPtr value=Interlocked.Exchange(ref handle,IntPtr.Zero);if(value!=IntPtr.Zero)CloseHandle(value);GC.SuppressFinalize(this);}
    ~MagicMRBuildJob(){Dispose();}
}
'@
}
