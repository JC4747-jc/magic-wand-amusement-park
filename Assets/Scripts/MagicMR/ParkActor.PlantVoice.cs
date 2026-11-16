using System;
using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {
        AudioClip plantVoice;
        float[] plantVoiceEnvelope;
        float plantVoiceStarted=-1;
        public bool PlantVoiceActive=>plantVoiceStarted>=0;
        public float PlantVoiceDuration=>plantVoice?plantVoice.length:0;
        public float PlantVoiceMouth {get;private set;}
        void BuildPlantVoice()
        {
            plantVoice=Resources.Load<AudioClip>("MagicMR/PlantDialogue");
            if(!plantVoice||plantVoice.channels!=1||plantVoice.length<1||plantVoice.length>8||plantVoice.samples>200000)
                throw new InvalidOperationException("Bounded mono plant dialogue clip missing or invalid");
            var samples=new float[plantVoice.samples];
            if(!plantVoice.GetData(samples,0))throw new InvalidOperationException("Plant dialogue must be readable PCM");
            int window=Mathf.Max(1,plantVoice.frequency/50);
            plantVoiceEnvelope=new float[(samples.Length+window-1)/window];
            for(int i=0;i<plantVoiceEnvelope.Length;i++)
            {
                float energy=0;int end=Mathf.Min(samples.Length,(i+1)*window);
                for(int n=i*window;n<end;n++)energy+=samples[n]*samples[n];
                plantVoiceEnvelope[i]=Mathf.Clamp01(Mathf.Sqrt(energy/(end-i*window))*5);
            }
        }
        void StartPlantVoice()
        {
            StopPlantVoice();
            if(!plantVoice||!audioSource)return;
            audioSource.Stop();audioSource.clip=plantVoice;audioSource.loop=false;
            audioSource.volume=.5f;audioSource.pitch=1;audioSource.Play();
            plantVoiceStarted=Time.unscaledTime;
        }
        void StopPlantVoice()
        {
            if(plantVoiceStarted>=0&&audioSource){audioSource.Stop();audioSource.clip=null;audioSource.volume=.18f;}
            plantVoiceStarted=-1;PlantVoiceMouth=0;
        }
        void StepPlantVoice()
        {
            if(!PlantVoiceActive)return;
            float elapsed=Time.unscaledTime-plantVoiceStarted;
            if(!state.awake||!tracked||elapsed<0||elapsed>=plantVoice.length){StopPlantVoice();return;}
            int index=Mathf.Min(plantVoiceEnvelope.Length-1,Mathf.FloorToInt(elapsed/plantVoice.length*plantVoiceEnvelope.Length));
            PlantVoiceMouth=Mathf.MoveTowards(PlantVoiceMouth,plantVoiceEnvelope[index],Time.unscaledDeltaTime*12);
        }
        void OnApplicationPause(bool paused){if(paused&&scenario==ParkScenario.Plant)Cancel();}
    }
}
